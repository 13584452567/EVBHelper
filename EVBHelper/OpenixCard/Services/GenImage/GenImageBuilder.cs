using OpenixCard.Exceptions;
using OpenixCard.Models;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace OpenixCard.Services.GenImage;

internal sealed class GenImageBuilder
{
    private const int SectorSize = 512;
    private const int GptEntryCount = 128;
    private const int GptEntrySize = 128;
    private const int HeadsPerCylinder = 255;
    private const int SectorsPerTrack = 63;
    private static readonly Guid LinuxFilesystemType = Guid.Parse("0fc63daf-8483-4772-8e79-3d69d8477de4");
    private static readonly Guid MicrosoftBasicType = Guid.Parse("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");

    private static int GptSectors => 1 + ((GptEntryCount * GptEntrySize) + SectorSize - 1) / SectorSize;

    public async Task<string> BuildAsync(
        string configPath,
        string inputDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        if (!File.Exists(configPath))
        {
            throw new FileOpenException(configPath);
        }

        var config = GenImageConfigParser.Parse(configPath);
        var partitions = PreparePartitions(config, inputDirectory, cancellationToken);

        var finalSize = CalculateImageSize(partitions, config.HdImage);
        var outputFileName = EnsureImageFileName(config.ImageName);
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, outputFileName);

        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20, FileOptions.SequentialScan);
        output.SetLength(finalSize);

        foreach (var partition in partitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WritePartitionAsync(output, partition, cancellationToken);
        }

        WritePartitionTables(output, partitions, config.HdImage, finalSize);
        await output.FlushAsync(cancellationToken);

        return outputPath;
    }

    private static string EnsureImageFileName(string imageName)
    {
        var trimmed = imageName.Trim();
        if (trimmed.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed + ".img";
    }

    private static List<PartitionLayout> PreparePartitions(GenImageConfig config, string inputDirectory, CancellationToken cancellationToken)
    {
        var layouts = new List<PartitionLayout>(config.Partitions.Count);
        var alignment = Math.Max(512L, config.HdImage.AlignmentBytes);
        var requiresGpt = config.HdImage.TableType is PartitionTableType.Gpt or PartitionTableType.Hybrid;
        var gptRegionEnd = requiresGpt
            ? Math.Max(2 * SectorSize, config.HdImage.GptLocationBytes + GptSectors * SectorSize)
            : alignment;

        var current = Math.Max(alignment, gptRegionEnd);

        foreach (var definition in config.Partitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var imagePath = ResolveImagePath(definition.Image, inputDirectory);
            long size = definition.SizeBytes ?? DetermineImageSize(imagePath, definition.Name);

            if (definition.InPartitionTable)
            {
                size = AlignUp(size, SectorSize);
                if (size == 0)
                {
                    throw new OperatorError($"分区 {definition.Name} 的大小不能为空。");
                }
            }

            long offset;
            if (definition.OffsetBytes.HasValue)
            {
                offset = definition.OffsetBytes.Value;
            }
            else
            {
                var partAlign = definition.InPartitionTable ? alignment : 1L;
                offset = AlignUp(current, Math.Max(1L, partAlign));
            }

            EnsureNoOverlap(layouts, offset, size, definition.Name);
            current = Math.Max(current, offset + size);

            var layout = new PartitionLayout(definition)
            {
                ImagePath = imagePath,
                Offset = offset,
                Size = size,
                PartitionGuid = CreateDeterministicGuid(config.ImageName, definition.Name),
                TypeGuid = DetermineTypeGuid(definition)
            };

            layouts.Add(layout);
        }

        return layouts;
    }

    private static string? ResolveImagePath(string? image, string inputDirectory)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        var trimmed = image.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return trimmed;
        }

        return Path.Combine(inputDirectory, trimmed);
    }

    private static long DetermineImageSize(string? imagePath, string partitionName)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            throw new OperatorError($"分区 {partitionName} 缺少镜像文件路径，且未指定大小。");
        }

        var info = new FileInfo(imagePath);
        if (!info.Exists)
        {
            throw new FileOpenException(imagePath);
        }

        return info.Length;
    }

    private static void EnsureNoOverlap(IEnumerable<PartitionLayout> layouts, long offset, long size, string name)
    {
        foreach (var layout in layouts)
        {
            var start = layout.Offset;
            var end = layout.Offset + layout.Size;
            var newEnd = offset + size;
            if (newEnd <= start || offset >= end)
            {
                continue;
            }

            throw new OperatorError($"分区 {name} 与 {layout.Definition.Name} 存在重叠。");
        }
    }

    private static long CalculateImageSize(IReadOnlyList<PartitionLayout> partitions, HdImageConfig hd)
    {
        if (partitions.Count == 0)
        {
            throw new OperatorError("没有可用于生成镜像的分区。");
        }

        var dataEnd = partitions.Max(p => p.Offset + p.Size);
        var minSize = AlignUp(dataEnd, SectorSize);

        if (hd.TableType is PartitionTableType.Gpt or PartitionTableType.Hybrid)
        {
            var gptRegionEnd = Math.Max(2 * SectorSize, hd.GptLocationBytes + GptSectors * SectorSize);
            var backupSize = (long)GptSectors * SectorSize;
            var baseSize = Math.Max(minSize, gptRegionEnd);
            var finalSize = AlignUp(baseSize + backupSize, 4096);
            var backupStart = finalSize - backupSize;
            if (backupStart < minSize)
            {
                finalSize = AlignUp(minSize + backupSize, 4096);
            }

            return finalSize;
        }

        return AlignUp(minSize, SectorSize);
    }

    private static async Task WritePartitionAsync(FileStream output, PartitionLayout partition, CancellationToken token)
    {
        output.Seek(partition.Offset, SeekOrigin.Begin);

        if (string.IsNullOrEmpty(partition.ImagePath))
        {
            await WriteZerosAsync(output, partition.Size, token).ConfigureAwait(false);
            return;
        }

        var sourceInfo = new FileInfo(partition.ImagePath);
        if (!sourceInfo.Exists)
        {
            throw new FileOpenException(partition.ImagePath);
        }

        if (sourceInfo.Length > partition.Size)
        {
            throw new OperatorError(
                $"分区 {partition.Definition.Name} 的镜像文件大于配置指定大小 ({sourceInfo.Length} > {partition.Size})。");
        }

        using var input = new FileStream(partition.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        var remaining = partition.Size;
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 17);
        try
        {
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = await input.ReadAsync(buffer.AsMemory(0, toRead), token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (remaining > 0)
        {
            await WriteZerosAsync(output, remaining, token).ConfigureAwait(false);
        }
    }

    private static async Task WriteZerosAsync(Stream output, long count, CancellationToken token)
    {
        if (count <= 0)
        {
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        Array.Clear(buffer);
        try
        {
            while (count > 0)
            {
                var toWrite = (int)Math.Min(buffer.Length, count);
                await output.WriteAsync(buffer.AsMemory(0, toWrite), token).ConfigureAwait(false);
                count -= toWrite;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void WritePartitionTables(FileStream output, IReadOnlyList<PartitionLayout> partitions, HdImageConfig hd, long finalSize)
    {
        if (hd.TableType is PartitionTableType.Gpt or PartitionTableType.Hybrid)
        {
            WriteGpt(output, partitions, hd, finalSize);
        }

        if (hd.TableType != PartitionTableType.Gpt)
        {
            WriteMbr(output, partitions, hd.TableType, finalSize);
        }
        else
        {
            // GPT 仍需要保护 MBR
            WriteMbr(output, partitions, PartitionTableType.Gpt, finalSize);
        }
    }

    private static void WriteMbr(FileStream output, IReadOnlyList<PartitionLayout> partitions, PartitionTableType tableType, long finalSize)
    {
        Span<byte> mbr = stackalloc byte[SectorSize];
        mbr.Clear();

        var entryIndex = 0;
        if (tableType is PartitionTableType.Mbr or PartitionTableType.Hybrid)
        {
            foreach (var partition in partitions)
            {
                if (!partition.Definition.InPartitionTable)
                {
                    continue;
                }

                var type = partition.Definition.MbrPartitionType ?? 0x83;
                if (tableType == PartitionTableType.Hybrid && partition.Definition.MbrPartitionType is null)
                {
                    continue;
                }

                if (entryIndex >= 4)
                {
                    break;
                }

                var entry = mbr[(446 + entryIndex * 16)..(446 + (entryIndex + 1) * 16)];
                FillMbrEntry(entry, partition, type);
                entryIndex++;
            }
        }

        if (tableType is PartitionTableType.Gpt or PartitionTableType.Hybrid)
        {
            var protectiveIndex = tableType == PartitionTableType.Hybrid ? Math.Min(entryIndex, 3) : 0;
            var entry = mbr[(446 + protectiveIndex * 16)..(446 + (protectiveIndex + 1) * 16)];
            entry.Clear();
            entry[4] = 0xEE;
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..12], 1);
            var sectors = (uint)Math.Min(uint.MaxValue, (finalSize / SectorSize) - 1);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..16], sectors);
            FillChs(entry[1..4], 1);
            FillChs(entry[5..8], sectors);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(mbr[(SectorSize - 2)..], 0xAA55);
        output.Seek(0, SeekOrigin.Begin);
        output.Write(mbr);
    }

    private static void FillMbrEntry(Span<byte> entry, PartitionLayout partition, byte type)
    {
        entry.Clear();
        entry[0] = partition.Definition.Bootable ? (byte)0x80 : (byte)0x00;
        entry[4] = type;
        var firstLba = (uint)(partition.Offset / SectorSize);
        var sectorCount = (uint)(partition.Size / SectorSize);
        FillChs(entry[1..4], firstLba);
        FillChs(entry[5..8], firstLba + Math.Max(0u, sectorCount - 1));
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..12], firstLba);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[12..16], sectorCount);
    }

    private static void FillChs(Span<byte> buffer, uint lba)
    {
        var cylinder = lba / (SectorsPerTrack * HeadsPerCylinder);
        var temp = lba % (SectorsPerTrack * HeadsPerCylinder);
        var head = temp / SectorsPerTrack;
        var sector = temp % SectorsPerTrack + 1;

        buffer[0] = (byte)head;
        buffer[1] = (byte)(((cylinder >> 2) & 0xC0) | (sector & 0x3F));
        buffer[2] = (byte)(cylinder & 0xFF);
    }

    private static void WriteGpt(FileStream output, IReadOnlyList<PartitionLayout> partitions, HdImageConfig hd, long finalSize)
    {
        var entries = new byte[GptEntryCount * GptEntrySize];
        var index = 0;
        ulong firstUsable = ulong.MaxValue;
        ulong lastUsable = 0;

        foreach (var partition in partitions)
        {
            if (!partition.Definition.InPartitionTable)
            {
                continue;
            }

            if (index >= GptEntryCount)
            {
                throw new OperatorError("分区数量超过 GPT 表所能容纳的上限。");
            }

            var entry = entries.AsSpan(index * GptEntrySize, GptEntrySize);
            partition.TypeGuid.TryWriteBytes(entry[..16]);
            partition.PartitionGuid.TryWriteBytes(entry[16..32]);

            var firstLba = (ulong)(partition.Offset / SectorSize);
            var lastLba = (ulong)((partition.Offset + partition.Size) / SectorSize - 1);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[32..40], firstLba);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[40..48], lastLba);

            ulong attributes = 0;
            if (partition.Definition.Bootable)
            {
                attributes |= 1UL << 2;
            }

            if (partition.Definition.ReadOnly)
            {
                attributes |= 1UL << 60;
            }

            if (partition.Definition.Hidden)
            {
                attributes |= 1UL << 62;
            }

            BinaryPrimitives.WriteUInt64LittleEndian(entry[48..56], attributes);

            var nameBytes = Encoding.Unicode.GetBytes(partition.Definition.Name);
            var nameSpan = entry[56..(56 + 72)];
            nameSpan.Clear();
            var copyLength = Math.Min(nameBytes.Length, nameSpan.Length);
            nameBytes.AsSpan(0, copyLength).CopyTo(nameSpan);

            firstUsable = Math.Min(firstUsable, firstLba);
            lastUsable = Math.Max(lastUsable, lastLba);
            index++;
        }

        if (firstUsable == ulong.MaxValue)
        {
            firstUsable = (ulong)(hd.GptLocationBytes / SectorSize + GptSectors);
        }

        var tableCrc = Crc32.Compute(entries);

        Span<byte> header = stackalloc byte[SectorSize];
        header.Clear();
        Encoding.ASCII.GetBytes("EFI PART").CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..12], 0x00010000);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..16], 92);
        BinaryPrimitives.WriteUInt64LittleEndian(header[24..32], 1);
        BinaryPrimitives.WriteUInt64LittleEndian(header[32..40], (ulong)(finalSize / SectorSize - 1));
        BinaryPrimitives.WriteUInt64LittleEndian(header[40..48], firstUsable);
        var lastUsableLba = (ulong)(finalSize / SectorSize - GptSectors - 1);
        BinaryPrimitives.WriteUInt64LittleEndian(header[48..56], lastUsableLba);

        var diskGuid = CreateDeterministicGuid("OpenixDisk", partitions.Count.ToString(CultureInfo.InvariantCulture));
        diskGuid.TryWriteBytes(header[56..72]);

        BinaryPrimitives.WriteUInt64LittleEndian(header[72..80], (ulong)(hd.GptLocationBytes / SectorSize));
        BinaryPrimitives.WriteUInt32LittleEndian(header[80..84], GptEntryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(header[84..88], GptEntrySize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[88..92], tableCrc);

        BinaryPrimitives.WriteUInt32LittleEndian(header[16..20], 0);
        var headerCrc = Crc32.Compute(header[..92]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..20], headerCrc);

        output.Seek(SectorSize, SeekOrigin.Begin);
        output.Write(header);

        output.Seek(hd.GptLocationBytes, SeekOrigin.Begin);
        output.Write(entries);

        var backupTableOffset = finalSize - (long)GptSectors * SectorSize;
        output.Seek(backupTableOffset, SeekOrigin.Begin);
        output.Write(entries);

        Span<byte> backupHeader = stackalloc byte[SectorSize];
        header.CopyTo(backupHeader);
        BinaryPrimitives.WriteUInt64LittleEndian(backupHeader[24..32], (ulong)(finalSize / SectorSize - 1));
        BinaryPrimitives.WriteUInt64LittleEndian(backupHeader[32..40], 1);
        BinaryPrimitives.WriteUInt64LittleEndian(backupHeader[72..80], (ulong)(backupTableOffset / SectorSize));
        BinaryPrimitives.WriteUInt32LittleEndian(backupHeader[16..20], 0);
        var backupCrc = Crc32.Compute(backupHeader[..92]);
        BinaryPrimitives.WriteUInt32LittleEndian(backupHeader[16..20], backupCrc);

        output.Seek(finalSize - SectorSize, SeekOrigin.Begin);
        output.Write(backupHeader);
    }

    private static long AlignUp(long value, long alignment)
    {
        if (alignment <= 1)
        {
            return value;
        }

        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }

    private static Guid DetermineTypeGuid(PartitionDefinition definition)
    {
        return definition.MbrPartitionType is 0x0B or 0x0C or 0x0E
            ? MicrosoftBasicType
            : LinuxFilesystemType;
    }

    private static Guid CreateDeterministicGuid(string scope, string name)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(scope + "::" + name);
        Span<byte> hash = stackalloc byte[32];
        sha.TryComputeHash(bytes, hash, out _);

        Span<byte> guidBytes = stackalloc byte[16];
        hash[..16].CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | (5 << 4));
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }

    private sealed class PartitionLayout
    {
        public PartitionLayout(PartitionDefinition definition)
        {
            Definition = definition;
        }

        public PartitionDefinition Definition { get; }
        public string? ImagePath { get; set; }
        public long Offset { get; set; }
        public long Size { get; set; }
        public Guid PartitionGuid { get; set; }
        public Guid TypeGuid { get; set; }
    }

    private static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in data)
            {
                crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
            }

            return ~crc;
        }

        private static uint[] CreateTable()
        {
            const uint polynomial = 0xEDB88320u;
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var crc = i;
                for (var j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
                }

                table[i] = crc;
            }

            return table;
        }
    }
}
