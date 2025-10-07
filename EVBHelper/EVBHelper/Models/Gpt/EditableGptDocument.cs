using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EVBHelper.Models.Gpt;

public sealed class EditableGptDocument
{
    private static readonly int[] SectorSizeCandidates = { 512, 4096 };
    private static readonly byte[] SignatureBytes = System.Text.Encoding.ASCII.GetBytes("EFI PART");

    private EditableGptDocument(byte[] rawBytes, string sourcePath)
    {
        RawBytes = rawBytes ?? throw new ArgumentNullException(nameof(rawBytes));
        SourcePath = sourcePath;
    }

    public byte[] RawBytes { get; }

    public string SourcePath { get; private set; }

    public EditableGptTable? Primary { get; private set; }

    public EditableGptTable? Secondary { get; private set; }

    public bool IsDirty => (Primary?.IsDirty ?? false) || (Secondary?.IsDirty ?? false);

    public static EditableGptDocument Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        var bytes = File.ReadAllBytes(path);
        var document = new EditableGptDocument(bytes, path);
        document.ParseTables();

        if (document.Primary is null && document.Secondary is null)
        {
            throw new InvalidDataException("No valid GPT tables were found in the file.");
        }

        return document;
    }

    public void UpdateSourcePath(string newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(newPath));
        }

        SourcePath = newPath;
    }

    public void Save(string path)
    {
        ApplyChanges();
        File.WriteAllBytes(path, RawBytes);
        SourcePath = path;
        ResetDirty();
    }

    public void ResetDirty()
    {
        if (Primary != null)
        {
            Primary.IsDirty = false;
        }

        if (Secondary != null)
        {
            Secondary.IsDirty = false;
        }
    }

    public void ApplyChanges()
    {
        if (Primary != null)
        {
            WriteTableToBuffer(Primary);
        }

        if (Secondary != null)
        {
            WriteTableToBuffer(Secondary);
        }
    }

    private void ParseTables()
    {
        Primary = ParseTable(GptTableKind.Primary);
        Secondary = ParseTable(GptTableKind.Secondary);
    }

    private EditableGptTable? ParseTable(GptTableKind kind)
    {
        foreach (var sector in SectorSizeCandidates)
        {
            if (TryParseTable(kind, sector, out var table))
            {
                return table;
            }
        }

        return null;
    }

    private bool TryParseTable(GptTableKind kind, int sectorSize, out EditableGptTable? table)
    {
        table = null;
        foreach (var headerOffset in EnumerateHeaderOffsets(kind, sectorSize))
        {
            if (headerOffset < 0 || headerOffset + 512 > RawBytes.LongLength || headerOffset > int.MaxValue)
            {
                continue;
            }

            var headerBytes = RawBytes.AsSpan((int)headerOffset, 512).ToArray();
            if (!ValidateSignature(headerBytes))
            {
                continue;
            }

            var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(12, 4));
            if (headerSize == 0 || headerSize > 512)
            {
                continue;
            }

            var headerCrc = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(16, 4));
            var headerValid = ValidateHeaderChecksum(headerBytes.AsSpan(), headerSize, headerCrc);

            var partitionsArrayLba = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.AsSpan(72, 8));
            var partitionsCount = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(80, 4));
            var partitionEntryLength = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(84, 4));
            var partitionsCrc = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(88, 4));
            var primaryHeaderLba = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.AsSpan(24, 8));
            var secondaryHeaderLba = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.AsSpan(32, 8));
            var firstUsableLba = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.AsSpan(40, 8));
            var lastUsableLba = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.AsSpan(48, 8));
            var diskGuid = new Guid(headerBytes.AsSpan(56, 16));

            var totalEntriesLength = (long)partitionEntryLength * partitionsCount;
            if (totalEntriesLength <= 0 || totalEntriesLength > int.MaxValue)
            {
                continue;
            }

            var partitionArrayOffset = CalculatePartitionArrayOffset(kind, headerOffset, partitionsArrayLba, totalEntriesLength, sectorSize);
            if (partitionArrayOffset < 0 || partitionArrayOffset + totalEntriesLength > RawBytes.LongLength || partitionArrayOffset > int.MaxValue)
            {
                continue;
            }

            var partitionsBytes = RawBytes.AsSpan((int)partitionArrayOffset, (int)totalEntriesLength);
            var computedCrc = Crc32Helper.Compute(partitionsBytes);
            var partitionsValid = computedCrc == partitionsCrc;

            var partitions = new List<EditableGptPartitionEntry>((int)partitionsCount);
            for (var index = 0; index < partitionsCount; index++)
            {
                var entryOffset = index * (int)partitionEntryLength;
                var entrySpan = partitionsBytes.Slice(entryOffset, (int)partitionEntryLength);
                partitions.Add(EditableGptPartitionEntry.FromSpan(entrySpan));
            }

            var revision = headerBytes.AsSpan(8, 4).ToArray();

            table = new EditableGptTable(
                kind,
                sectorSize,
                headerOffset,
                partitionArrayOffset,
                headerBytes,
                headerSize,
                revision,
                headerCrc,
                partitionsCrc,
                primaryHeaderLba,
                secondaryHeaderLba,
                firstUsableLba,
                lastUsableLba,
                diskGuid,
                partitionsArrayLba,
                partitionsCount,
                partitionEntryLength,
                partitions,
                headerValid,
                partitionsValid);

            table.EnsureCapacity();
            return true;
        }

        return false;
    }

    private IEnumerable<long> EnumerateHeaderOffsets(GptTableKind kind, int sectorSize)
    {
        if (kind == GptTableKind.Primary)
        {
            yield return sectorSize; // Immediately after the protective MBR
            yield return 0; // Some extracted images omit the protective MBR
        }
        else if (kind == GptTableKind.Secondary)
        {
            yield return RawBytes.LongLength - sectorSize;
            yield return RawBytes.LongLength - 512;
            yield return 0;
        }
    }

    private static bool ValidateSignature(ReadOnlySpan<byte> header)
        => header.Length >= 8 && header[..8].SequenceEqual(SignatureBytes);

    private static bool ValidateHeaderChecksum(ReadOnlySpan<byte> header, uint headerSize, uint expectedCrc)
    {
        var working = header.Slice(0, (int)headerSize).ToArray();
        working.AsSpan(16, 4).Clear();
        var computed = Crc32Helper.Compute(working);
        return computed == expectedCrc;
    }

    private long CalculatePartitionArrayOffset(GptTableKind kind, long headerOffset, ulong partitionsArrayLba, long totalEntriesLength, int sectorSize)
    {
        var expectedOffset = (long)partitionsArrayLba * sectorSize;
        if (expectedOffset >= 0 && expectedOffset + totalEntriesLength <= RawBytes.LongLength)
        {
            return expectedOffset;
        }

        if (kind == GptTableKind.Primary)
        {
            var fallback = headerOffset + sectorSize;
            if (fallback >= 0 && fallback + totalEntriesLength <= RawBytes.LongLength)
            {
                return fallback;
            }
        }
        else if (kind == GptTableKind.Secondary)
        {
            var fallback = headerOffset - totalEntriesLength;
            if (fallback >= 0 && fallback + totalEntriesLength <= RawBytes.LongLength)
            {
                return fallback;
            }
        }

        return -1;
    }

    private void WriteTableToBuffer(EditableGptTable table)
    {
        table.EnsureCapacity();
        int entrySize = checked((int)table.PartitionEntryLength);
        int totalLength = checked(entrySize * (int)table.PartitionsCount);

        var buffer = new byte[totalLength];
        for (int i = 0; i < table.Partitions.Count; i++)
        {
            table.Partitions[i].WriteTo(buffer.AsSpan(i * entrySize, entrySize));
        }

        var crc = Crc32Helper.Compute(buffer);

        var header = new byte[table.HeaderBytes.Length];
        table.WriteHeaderFields(header, crc);

        var headerSpan = header.AsSpan(0, (int)table.HeaderSize);
        var computedHeaderCrc = Crc32Helper.Compute(headerSpan);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), computedHeaderCrc);

        CopyToRaw(RawBytes, table.PartitionArrayOffset, buffer);
        CopyToRaw(RawBytes, table.HeaderOffset, header);
        Array.Copy(header, table.HeaderBytes, Math.Min(header.Length, table.HeaderBytes.Length));
        table.UpdateChecksumStates(true, true);
        table.IsDirty = false;
    }

    private static void CopyToRaw(byte[] destination, long offset, ReadOnlySpan<byte> data)
    {
        if (offset < 0 || offset + data.Length > destination.LongLength)
        {
            throw new InvalidOperationException("Write exceeds the bounds of the original buffer.");
        }

        data.CopyTo(destination.AsSpan((int)offset, data.Length));
    }
}
