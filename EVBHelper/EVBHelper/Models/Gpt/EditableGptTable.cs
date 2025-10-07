using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EVBHelper.Models.Gpt;

public sealed class EditableGptTable
{
    private const string EfiSignature = "EFI PART";

    public EditableGptTable(
        GptTableKind kind,
        int sectorSize,
        long headerOffset,
        long partitionArrayOffset,
        byte[] headerBytes,
        uint headerSize,
        byte[] revision,
        uint originalHeaderCrc,
        uint originalPartitionArrayCrc,
        ulong primaryHeaderLba,
        ulong secondaryHeaderLba,
        ulong firstUsableLba,
        ulong lastUsableLba,
        Guid diskGuid,
        ulong partitionsArrayLba,
        uint partitionsCount,
        uint partitionEntryLength,
        IReadOnlyList<EditableGptPartitionEntry> partitions,
        bool headerChecksumValid,
        bool partitionArrayChecksumValid)
    {
        Kind = kind;
        SectorSize = sectorSize;
        HeaderOffset = headerOffset;
        PartitionArrayOffset = partitionArrayOffset;
        HeaderBytes = headerBytes ?? throw new ArgumentNullException(nameof(headerBytes));
        HeaderSize = headerSize;
        Revision = revision ?? throw new ArgumentNullException(nameof(revision));
        OriginalHeaderCrc = originalHeaderCrc;
        OriginalPartitionArrayCrc = originalPartitionArrayCrc;
        PrimaryHeaderLba = primaryHeaderLba;
        SecondaryHeaderLba = secondaryHeaderLba;
        FirstUsableLba = firstUsableLba;
        LastUsableLba = lastUsableLba;
        DiskGuid = diskGuid;
        PartitionsArrayLba = partitionsArrayLba;
        PartitionsCount = partitionsCount;
        PartitionEntryLength = partitionEntryLength;
        Partitions = partitions.ToList();
        HeaderChecksumValid = headerChecksumValid;
        PartitionArrayChecksumValid = partitionArrayChecksumValid;
    }

    public GptTableKind Kind { get; }

    public int SectorSize { get; }

    public long HeaderOffset { get; }

    public long PartitionArrayOffset { get; }

    public byte[] HeaderBytes { get; }

    public uint HeaderSize { get; }

    public byte[] Revision { get; }

    public uint OriginalHeaderCrc { get; }

    public uint OriginalPartitionArrayCrc { get; }

    public ulong PrimaryHeaderLba { get; set; }

    public ulong SecondaryHeaderLba { get; set; }

    public ulong FirstUsableLba { get; set; }

    public ulong LastUsableLba { get; set; }

    public Guid DiskGuid { get; set; }

    public ulong PartitionsArrayLba { get; set; }

    public uint PartitionsCount { get; set; }

    public uint PartitionEntryLength { get; set; }

    public List<EditableGptPartitionEntry> Partitions { get; }

    public bool HeaderChecksumValid { get; private set; }

    public bool PartitionArrayChecksumValid { get; private set; }

    public bool IsDirty { get; set; }

    public string DisplayName => Kind switch
    {
        GptTableKind.Primary => "Primary GPT",
        GptTableKind.Secondary => "Secondary GPT",
        _ => "Unknown GPT"
    };

    public void EnsureCapacity()
    {
        var requiredEntries = (int)PartitionsCount;
        while (Partitions.Count < requiredEntries)
        {
            Partitions.Add(new EditableGptPartitionEntry());
        }
        if (Partitions.Count > requiredEntries)
        {
            Partitions.RemoveRange(requiredEntries, Partitions.Count - requiredEntries);
        }
    }

    public byte[] BuildPartitionArray()
    {
    EnsureCapacity();
    int entrySize = checked((int)PartitionEntryLength);
    int totalLength = checked(entrySize * (int)PartitionsCount);
    var buffer = new byte[totalLength];

        for (int i = 0; i < Partitions.Count; i++)
        {
            Partitions[i].WriteTo(buffer.AsSpan(i * entrySize, entrySize));
        }

        return buffer;
    }

    public void UpdateChecksumStates(bool headerValid, bool partitionValid)
    {
        HeaderChecksumValid = headerValid;
        PartitionArrayChecksumValid = partitionValid;
    }

    public void WriteHeaderFields(Span<byte> headerSpan, uint partitionsCrc)
    {
        if (headerSpan.Length < HeaderBytes.Length)
        {
            throw new ArgumentException("Header buffer size mismatch", nameof(headerSpan));
        }

        HeaderBytes.CopyTo(headerSpan);

    var signatureSpan = headerSpan.Slice(0, 8);
    signatureSpan.Clear();
    Encoding.ASCII.GetBytes(EfiSignature.AsSpan(), signatureSpan);

        if (Revision.Length >= 4)
        {
            Revision.AsSpan(0, 4).CopyTo(headerSpan.Slice(8, 4));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(headerSpan.Slice(12, 4), HeaderSize);
        headerSpan.Slice(16, 4).Clear(); // header CRC placeholder
        headerSpan.Slice(20, 4).Clear();

        BinaryPrimitives.WriteUInt64LittleEndian(headerSpan.Slice(24, 8), PrimaryHeaderLba);
        BinaryPrimitives.WriteUInt64LittleEndian(headerSpan.Slice(32, 8), SecondaryHeaderLba);
        BinaryPrimitives.WriteUInt64LittleEndian(headerSpan.Slice(40, 8), FirstUsableLba);
        BinaryPrimitives.WriteUInt64LittleEndian(headerSpan.Slice(48, 8), LastUsableLba);
        DiskGuid.TryWriteBytes(headerSpan.Slice(56, 16));
        headerSpan.Slice(72, 8).Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(headerSpan.Slice(72, 8), PartitionsArrayLba);
        BinaryPrimitives.WriteUInt32LittleEndian(headerSpan.Slice(80, 4), PartitionsCount);
        BinaryPrimitives.WriteUInt32LittleEndian(headerSpan.Slice(84, 4), PartitionEntryLength);
        BinaryPrimitives.WriteUInt32LittleEndian(headerSpan.Slice(88, 4), partitionsCrc);
    }
}

public enum GptTableKind
{
    Unknown = 0,
    Primary,
    Secondary
}
