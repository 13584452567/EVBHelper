using OpenixCard.Models;
using System.Globalization;
namespace OpenixCard.Services.GenImage;

internal static class GenImageConfigParser
{
    public static GenImageConfig Parse(string configPath)
    {
        var lines = File.ReadAllLines(configPath);
        var parser = new ParserState();

        foreach (var rawLine in lines)
        {
            parser.ProcessLine(rawLine);
        }

        parser.Complete();
        return parser.Build();
    }

    private sealed class ParserState
    {
        private readonly Stack<BlockType> _stack = new();
        private readonly List<PartitionDefinition> _partitions = new();
        private PartitionDefinition? _currentPartition;
        private HdImageConfig _hdImage = new();
        private string? _imageName;

        public ParserState()
        {
        }

        public void ProcessLine(string rawLine)
        {
            var line = StripComment(rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var trimmed = line.Trim();

            if (trimmed.StartsWith("image", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('{'))
            {
                var name = ExtractTokenBetween(trimmed, "image", "{");
                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidDataException($"无法从配置行解析镜像名称: {rawLine}");
                }

                _imageName = name.Trim().Trim('"');
                Push(BlockType.Image);
                return;
            }

            if (trimmed.StartsWith("hdimage", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('{'))
            {
                Push(BlockType.HdImage);
                return;
            }

            if (trimmed.StartsWith("partition", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('{'))
            {
                var name = ExtractTokenBetween(trimmed, "partition", "{");
                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidDataException($"无法从配置行解析分区名称: {rawLine}");
                }

                _currentPartition = new PartitionDefinition
                {
                    Name = name.Trim().Trim('"')
                };

                Push(BlockType.Partition);
                return;
            }

            if (trimmed.StartsWith("}", StringComparison.Ordinal))
            {
                Pop();
                return;
            }

            if (_stack.Count == 0)
            {
                return;
            }

            switch (_stack.Peek())
            {
                case BlockType.HdImage:
                    ParseHdImageProperty(trimmed);
                    break;
                case BlockType.Partition:
                    ParsePartitionProperty(trimmed);
                    break;
            }
        }

        public void Complete()
        {
            if (_stack.Count != 0)
            {
                throw new InvalidDataException("cfg 文件的括号不匹配，无法完成解析。");
            }

            if (string.IsNullOrWhiteSpace(_imageName))
            {
                throw new InvalidDataException("cfg 文件中缺少镜像定义。");
            }

            if (_partitions.Count == 0)
            {
                throw new InvalidDataException("cfg 文件中缺少分区定义。");
            }
        }

        public GenImageConfig Build()
        {
            return new GenImageConfig
            {
                ImageName = _imageName!,
                HdImage = _hdImage,
                Partitions = _partitions
            };
        }

        private void ParseHdImageProperty(string trimmed)
        {
            var (key, value) = ParseKeyValue(trimmed);
            if (key is null)
            {
                return;
            }

            switch (key)
            {
                case "partition-table-type":
                    _hdImage.TableType = value.Equals("hybrid", StringComparison.OrdinalIgnoreCase)
                        ? PartitionTableType.Hybrid
                        : value.Equals("gpt", StringComparison.OrdinalIgnoreCase)
                            ? PartitionTableType.Gpt
                            : PartitionTableType.Mbr;
                    break;
                case "gpt-location":
                    _hdImage.GptLocationBytes = ParseSize(value);
                    break;
                case "align":
                    _hdImage.AlignmentBytes = ParseSize(value);
                    break;
            }
        }

        private void ParsePartitionProperty(string trimmed)
        {
            if (_currentPartition is null)
            {
                throw new InvalidOperationException("当前分区上下文为空，无法解析属性。");
            }

            var (key, value) = ParseKeyValue(trimmed);
            if (key is null)
            {
                return;
            }

            switch (key)
            {
                case "image":
                    _currentPartition.Image = value.Trim();
                    break;
                case "size":
                    _currentPartition.SizeBytes = ParseSize(value);
                    break;
                case "offset":
                    _currentPartition.OffsetBytes = ParseSize(value);
                    break;
                case "in-partition-table":
                    _currentPartition.InPartitionTable = !value.Equals("no", StringComparison.OrdinalIgnoreCase);
                    break;
                case "partition-type":
                    _currentPartition.MbrPartitionType = ParseByte(value);
                    break;
                case "bootable":
                    _currentPartition.Bootable = ParseBoolean(value);
                    break;
                case "read-only":
                    _currentPartition.ReadOnly = ParseBoolean(value);
                    break;
                case "hidden":
                    _currentPartition.Hidden = ParseBoolean(value);
                    break;
            }
        }

        private static bool ParseBoolean(string value)
        {
            return value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        private static (string? Key, string Value) ParseKeyValue(string line)
        {
            var idx = line.IndexOf('=');
            if (idx < 0)
            {
                return (null, string.Empty);
            }

            var key = line[..idx].Trim().Trim('"');
            var value = line[(idx + 1)..].Trim().Trim('"');
            return (key, value);
        }

        private byte ParseByte(string value)
        {
            var cleaned = value.Trim();
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return byte.Parse(cleaned[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return byte.Parse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static long ParseSize(string raw)
        {
            var value = raw.Trim();
            if (value.Length == 0)
            {
                throw new InvalidDataException("无法解析空的容量字段。");
            }

            var multiplier = 1L;
            var suffix = char.ToUpperInvariant(value[^1]);
            if (suffix is 'K' or 'M' or 'G')
            {
                value = value[..^1];
                multiplier = suffix switch
                {
                    'K' => 1024L,
                    'M' => 1024L * 1024L,
                    'G' => 1024L * 1024L * 1024L,
                    _ => 1L
                };
            }

            value = value.Trim();
            long number;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                number = long.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
                number = long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            return checked(number * multiplier);
        }

        private void Push(BlockType type) => _stack.Push(type);

        private void Pop()
        {
            if (_stack.Count == 0)
            {
                throw new InvalidDataException("cfg 文件括号不匹配，检测到多余的闭合符号。");
            }

            var block = _stack.Pop();
            if (block == BlockType.Partition)
            {
                if (_currentPartition is null)
                {
                    throw new InvalidDataException("分区定义未正确初始化。");
                }

                _partitions.Add(_currentPartition);
                _currentPartition = null;
            }
        }

        private static string StripComment(string line)
        {
            var index = line.IndexOf(';');
            if (index < 0)
            {
                return line;
            }

            return line[..index];
        }

        private static string ExtractTokenBetween(string input, string prefix, string suffixToken)
        {
            var startIndex = input.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
            {
                return string.Empty;
            }

            var afterPrefix = input[(startIndex + prefix.Length)..].TrimStart();
            var braceIndex = afterPrefix.IndexOf(suffixToken, StringComparison.Ordinal);
            if (braceIndex < 0)
            {
                return afterPrefix;
            }

            return afterPrefix[..braceIndex].Trim();
        }

        private enum BlockType
        {
            Image,
            HdImage,
            Partition
        }
    }
}

internal sealed class GenImageConfig
{
    public required string ImageName { get; init; }
    public required HdImageConfig HdImage { get; init; }
    public required IReadOnlyList<PartitionDefinition> Partitions { get; init; }
}

internal sealed class HdImageConfig
{
    public PartitionTableType TableType { get; set; } = PartitionTableType.Gpt;
    public long GptLocationBytes { get; set; } = 0x100000;
    public long AlignmentBytes { get; set; } = 512;
}

internal sealed class PartitionDefinition
{
    public required string Name { get; init; }
    public string? Image { get; set; }
    public long? SizeBytes { get; set; }
    public long? OffsetBytes { get; set; }
    public bool InPartitionTable { get; set; } = true;
    public byte? MbrPartitionType { get; set; }
    public bool Bootable { get; set; }
    public bool ReadOnly { get; set; }
    public bool Hidden { get; set; }
}
