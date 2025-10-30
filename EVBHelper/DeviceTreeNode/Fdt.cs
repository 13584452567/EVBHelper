using DeviceTreeNode.Core;
using DeviceTreeNode.Models;
using DeviceTreeNode.Nodes;
using DeviceTreeNode.StandardNodes;
using System.Text;

public class Fdt
{
    private readonly byte[] _data;
    private readonly FdtHeader _header;
    private FdtNode? _rootNode;
    private Root? _rootWrapper;

    // 最大允许的节点嵌套深度，防止恶意构造导致深度递归/解析耗尽资源
    private const int MaxNodeDepth = 256;

    /// <summary>
    /// 从二进制数据创建新的FDT实例
    /// </summary>
    public Fdt(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        _data = data;
        var dataReader = new FdtData(data);

        // 解析头部
        _header = FdtHeader.FromBytes(dataReader) ?? throw new FormatException("Invalid FDT header");

        // 验证魔数和大小
        if (!_header.ValidMagic)
            throw new FormatException("Invalid FDT magic value");

        if (data.Length < _header.TotalSize)
            throw new FormatException("Buffer too small for FDT");

        ValidateLayout();
    }

    /// <summary>
    /// 校验各区块 offset/size 合法性，防止越界与交叉
    /// </summary>
    private void ValidateLayout()
    {
        // 基础范围
        uint total = _header.TotalSize;
        // 检查各 offset < total
        if (_header.StructOffset >= total || _header.StringsOffset >= total || _header.MemoryReservationOffset >= total)
            throw new FormatException("FDT header offsets out of range");

        // 检查结构块范围
        if (CheckedAdd(_header.StructOffset, _header.StructSize) > total)
            throw new FormatException("Struct block exceeds total size");

        // 检查字符串块范围
        if (CheckedAdd(_header.StringsOffset, _header.StringsSize) > total)
            throw new FormatException("Strings block exceeds total size");

        // memory reservation block 至少要包含终止项 (16 字节)
        // 无法确定其确切大小（需扫描），这里只做最小基本检查：offset + 16 <= total
        if (CheckedAdd(_header.MemoryReservationOffset, 16) > total)
            throw new FormatException("Memory reservation block truncated");

        // 块之间允许任意顺序但通常为：header -> mem_rsvmap -> struct -> strings
        // 若违反典型顺序不直接抛错，只要不重叠即可。简单重叠检测：
        // 取每块范围并确保不交叉（宽松处理：struct 与 strings 可以任意只要不越界）
        // 若需要严格顺序，可加判断 _header.MemoryReservationOffset < _header.StructOffset < _header.StringsOffset
        var ranges = new List<(uint start, uint end, string name)>
        {
            (_header.StructOffset, CheckedAdd(_header.StructOffset, _header.StructSize), "struct"),
            (_header.StringsOffset, CheckedAdd(_header.StringsOffset, _header.StringsSize), "strings")
        };
        // 仅当 size>0 时才纳入检测
        foreach (var a in ranges)
        {
            if (a.end < a.start || a.end > total)
                throw new FormatException($"Block {a.name} invalid range");
        }
    }

    private static uint CheckedAdd(uint a, uint b)
    {
        unchecked
        {
            ulong r = (ulong)a + b;
            if (r > uint.MaxValue) throw new FormatException("Integer overflow in size computation");
            return (uint)r;
        }
    }

    /// <summary>
    /// 获取设备树头部信息
    /// </summary>
    public FdtHeader Header => _header;

    /// <summary>
    /// 查找指定路径的节点
    /// </summary>
    public FdtNode? FindNode(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Trim();
        if (path.Length == 0)
            return null;

        if (path == "/")
            return EnsureRootNode();

        if (path.StartsWith("/", StringComparison.Ordinal))
            return ResolveAbsolutePath(path);

        // treat as alias when not an absolute path
        return Aliases?.ResolveAlias(path);
    }

    /// <summary>
    /// 获取所有节点的扁平列表
    /// </summary>
    public IEnumerable<FdtNode> AllNodes()
    {
        var rootNode = EnsureRootNode();
        return EnumerateNodes(rootNode);
    }

    private IEnumerable<FdtNode> EnumerateNodes(FdtNode node)
    {
        yield return node;

        foreach (var child in node.Children())
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 获取根节点
    /// </summary>
    public Root Root => _rootWrapper ??= new Root(EnsureRootNode());

    /// <summary>
    /// 获取chosen节点
    /// </summary>
    public Chosen? Chosen
    {
        get
        {
            var node = FindNode("/chosen");
            return node != null ? new Chosen(node) : null;
        }
    }

    /// <summary>
    /// 获取memory节点
    /// </summary>
    public Memory? Memory
    {
        get
        {
            var node = FindNode("/memory");
            return node != null ? new Memory(node) : null;
        }
    }

    /// <summary>
    /// 获取所有CPU节点
    /// </summary>
    public IEnumerable<Cpu> Cpus
    {
        get
        {
            var cpusNode = FindNode("/cpus");
            if (cpusNode == null)
                return [];

            return cpusNode.Children()
                .Where(node => node.Name.StartsWith("cpu@") ||
                              node.GetProperty("device_type")?.AsString() == "cpu")
                .Select(node => new Cpu(node));
        }
    }

    /// <summary>
    /// 获取别名节点
    /// </summary>
    public Aliases? Aliases
    {
        get
        {
            var node = FindNode("/aliases");
            return node != null ? new Aliases(node, this) : null;
        }
    }

    /// <summary>
    /// 获取所有内存保留区域
    /// </summary>
    public IEnumerable<MemoryReservation> MemoryReservations
    {
        get
        {
            if (_header.MemoryReservationOffset >= _data.Length)
                yield break;

            int pos = (int)_header.MemoryReservationOffset;
            // 至少终止项 16 字节
            while (pos + 16 <= _data.Length)
            {
                // 读取 address/size (大端)
                if (pos + 16 > _data.Length) yield break;
                ulong address = ReadBeU64(_data.AsSpan(pos, 8));
                ulong size = ReadBeU64(_data.AsSpan(pos + 8, 8));
                pos += 16;
                if (address == 0 && size == 0)
                    yield break; // 终止

                yield return new MemoryReservation(new IntPtr((long)address), size);
            }
        }
    }

    private static ulong ReadBeU64(ReadOnlySpan<byte> s)
    {
        if (BitConverter.IsLittleEndian)
        {
            Span<byte> tmp = stackalloc byte[8];
            s.CopyTo(tmp);
            tmp.Reverse();
            return BitConverter.ToUInt64(tmp);
        }
        return BitConverter.ToUInt64(s);
    }

    /// <summary>
    /// 从字符串块中获取指定偏移量的字符串（带边界校验）
    /// </summary>
    internal string? GetStringAtOffset(int offset)
    {
        // 必须在字符串块内部
        if (offset < 0 || offset >= _header.StringsSize)
            return null;

        int start = (int)_header.StringsOffset + offset;
        int limit = (int)CheckedAdd(_header.StringsOffset, _header.StringsSize); // 终止位置（不含）
        if (start < 0 || start >= _data.Length || start >= limit)
            return null;

        int p = start;
        while (p < _data.Length && p < limit && _data[p] != 0)
            p++;
        if (p >= _data.Length || p >= limit)
            return null; // 未找到终止符或越界
        int len = p - start;
        if (len < 0) return null;
        return Encoding.UTF8.GetString(_data, start, len);
    }

    /// <summary>
    /// 获取所有字符串表中的字符串（调试用）
    /// </summary>
    public IEnumerable<string> Strings
    {
        get
        {
            if (_header.StringsOffset >= _data.Length || _header.StringsSize == 0)
                yield break;

            int start = (int)_header.StringsOffset;
            int size = (int)_header.StringsSize;
            if (start + size > _data.Length) yield break;
            int offset = 0;
            while (offset < size)
            {
                int s = offset;
                while (offset < size && _data[start + offset] != 0) offset++;
                if (offset > s)
                {
                    yield return Encoding.UTF8.GetString(_data, start + s, offset - s);
                }
                offset++; // 跳过0
            }
        }
    }

    /// <summary>
    /// 获取结构块的字节数据
    /// </summary>
    private byte[] GetStructsBlock()
    {
        // 安全裁剪
        if (CheckedAdd(_header.StructOffset, _header.StructSize) > _header.TotalSize ||
            _header.StructOffset + _header.StructSize > _data.Length)
            return [];
        byte[] result = new byte[_header.StructSize];
        Array.Copy(_data, _header.StructOffset, result, 0, _header.StructSize);
        return result;
    }

    /// <summary>
    /// 解析根节点
    /// </summary>
    internal FdtNode? ResolveAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Trim();
        if (path.Length == 0)
            return null;

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path;

        if (path == "/")
            return EnsureRootNode();

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = EnsureRootNode();
        foreach (var segment in segments)
        {
            var next = current.Children().FirstOrDefault(child => child.Name == segment);
            if (next == null)
                return null;
            current = next;
        }

        return current;
    }

    private FdtNode EnsureRootNode()
    {
        if (_rootNode != null)
            return _rootNode;

        _rootNode = ParseRoot();
        return _rootNode;
    }

    private FdtNode ParseRoot()
    {
        var stream = new FdtData(GetStructsBlock());

        // 跳过NOP标记
        stream.SkipNops();

        // 检查是否为FDT_BEGIN_NODE
        if (stream.PeekUInt32()?.Value != FdtConstants.FDT_BEGIN_NODE)
            throw new FormatException("Invalid FDT structure: expected FDT_BEGIN_NODE");

        // 跳过标记
        stream.Skip(4);

        // 根节点名称应为空字符串（读取并跳过）
        byte[] nameBytes = stream.Remaining();
        int i = 0;
        while (i < nameBytes.Length && nameBytes[i] != 0) i++;
        // 对齐到4字节边界
        int paddingLen = i + 1; // +1 是null终止符
        int alignedLen = (paddingLen + 3) & ~3;
        stream.Skip(alignedLen);

        // 解析属性和子节点
        byte[] nodeProps = ParseNodeProps(ref stream, 0);
        return new FdtNode("", this, nodeProps);
    }

    /// <summary>
    /// 解析节点的属性部分 (包含子节点) 并返回其原始字节切片
    /// </summary>
    private byte[] ParseNodeProps(ref FdtData stream, int depth)
    {
        if (depth > MaxNodeDepth)
            throw new FormatException("FDT node depth exceeds limit");

        var start = stream.Remaining();
        int propSectionLen = 0;

        while (!stream.IsEmpty())
        {
            stream.SkipNops();
            uint? token = stream.PeekUInt32()?.Value;

            if (!token.HasValue)
                break;

            if (token == FdtConstants.FDT_END_NODE)
            {
                propSectionLen = start.Length - stream.Remaining().Length;
                stream.Skip(4); // 跳过FDT_END_NODE
                break;
            }
            else if (token == FdtConstants.FDT_PROP)
            {
                stream.Skip(4); // 跳过FDT_PROP
                uint? len = stream.ReadUInt32()?.Value;
                uint? nameOffset = stream.ReadUInt32()?.Value;
                if (!len.HasValue || !nameOffset.HasValue)
                    throw new FormatException("Invalid FDT property header");
                // 长度防御：不能超过剩余
                int remaining = stream.Remaining().Length;
                if (len.Value > remaining)
                    throw new FormatException("FDT property length out of range");
                int alignedLen = ((int)len.Value + 3) & ~3;
                stream.Skip(alignedLen);
            }
            else if (token == FdtConstants.FDT_BEGIN_NODE)
            {
                // 跳过子节点完整结构（递归）
                stream.Skip(4); // BEGIN_NODE
                // 读取名称
                byte[] remaining = stream.Remaining();
                int i = 0;
                while (i < remaining.Length && remaining[i] != 0) i++;
                if (i >= remaining.Length)
                    throw new FormatException("Unterminated node name");
                int paddingLen = i + 1;
                int alignedLen = (paddingLen + 3) & ~3;
                stream.Skip(alignedLen);
                // 递归解析其内部（结果忽略，仅用于推进流）
                ParseNodeProps(ref stream, depth + 1);
            }
            else if (token == FdtConstants.FDT_END)
            {
                // 结构块结束
                break;
            }
            else if (token == FdtConstants.FDT_NOP)
            {
                // 已在 SkipNops 中处理，这里忽略
                stream.Skip(4);
            }
            else
            {
                throw new FormatException($"Unknown FDT token: 0x{token:X8}");
            }
        }

        if (propSectionLen > 0)
        {
            byte[] props = new byte[propSectionLen];
            Array.Copy(start, props, propSectionLen);
            return props;
        }

        return [];
    }
}
