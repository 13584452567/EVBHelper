using System;
using System.Buffers.Binary;
using System.Text;

namespace EVBHelper.Models.Gpt;

public sealed class EditableGptPartitionEntry
{
    private const int AttributeLength = 8;
    private const int NameCharCapacity = 36;

    private readonly byte[] _attributeFlags;

    public EditableGptPartitionEntry()
        : this(Guid.Empty, Guid.Empty, 0, 0, new byte[AttributeLength], string.Empty)
    {
    }

    public EditableGptPartitionEntry(
        Guid partitionType,
        Guid partitionGuid,
        ulong firstLba,
        ulong lastLba,
        byte[] attributeFlags,
        string name)
    {
        PartitionType = partitionType;
        PartitionGuid = partitionGuid;
        FirstLba = firstLba;
        LastLba = lastLba;
        _attributeFlags = new byte[AttributeLength];
        if (attributeFlags.Length > AttributeLength)
        {
            Buffer.BlockCopy(attributeFlags, 0, _attributeFlags, 0, AttributeLength);
        }
        else
        {
            Buffer.BlockCopy(attributeFlags, 0, _attributeFlags, 0, attributeFlags.Length);
        }

        Name = SanitizeName(name);
    }

    public Guid PartitionType { get; set; }

    public Guid PartitionGuid { get; set; }

    public ulong FirstLba { get; set; }

    public ulong LastLba { get; set; }

    public string Name { get; private set; }

    public ReadOnlySpan<byte> AttributeFlags => _attributeFlags;

    public bool IsRequired
    {
        get => (_attributeFlags[0] & 0x01) != 0;
        set => SetFlag(0, 0x01, value);
    }

    public bool NoDriveLetter
    {
        get => (_attributeFlags[^1] & 0x80) != 0;
        set => SetFlag(^1, 0x80, value);
    }

    public bool IsHidden
    {
        get => (_attributeFlags[^1] & 0x40) != 0;
        set => SetFlag(^1, 0x40, value);
    }

    public bool IsShadowCopy
    {
        get => (_attributeFlags[^1] & 0x20) != 0;
        set => SetFlag(^1, 0x20, value);
    }

    public bool IsReadOnly
    {
        get => (_attributeFlags[^1] & 0x10) != 0;
        set => SetFlag(^1, 0x10, value);
    }

    public bool IsEmpty
        => PartitionType == Guid.Empty
           && PartitionGuid == Guid.Empty
           && FirstLba == 0
           && LastLba == 0
           && string.IsNullOrWhiteSpace(Name);

    public void Clear()
    {
        PartitionType = Guid.Empty;
        PartitionGuid = Guid.Empty;
        FirstLba = 0;
        LastLba = 0;
        Array.Fill(_attributeFlags, (byte)0);
        Name = string.Empty;
    }

    public bool TrySetName(string? name, out string? error)
    {
        name ??= string.Empty;
        if (name.Length > NameCharCapacity)
        {
            error = $"Name cannot exceed {NameCharCapacity} characters.";
            return false;
        }

        Name = SanitizeName(name);
        error = null;
        return true;
    }

    public void CopyTo(EditableGptPartitionEntry target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        target.PartitionType = PartitionType;
        target.PartitionGuid = PartitionGuid;
        target.FirstLba = FirstLba;
        target.LastLba = LastLba;
        target.Name = Name;
        _attributeFlags.CopyTo(target._attributeFlags, 0);
    }

    public byte[] ToByteArray()
    {
        var buffer = new byte[128];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> buffer)
    {
        if (buffer.Length < 128)
        {
            throw new ArgumentException("Buffer too small", nameof(buffer));
        }

        buffer.Clear();
        PartitionType.TryWriteBytes(buffer[..16]);
        PartitionGuid.TryWriteBytes(buffer.Slice(16, 16));
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(32, 8), FirstLba);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(40, 8), LastLba);
        _attributeFlags.CopyTo(buffer.Slice(48, AttributeLength));

        var nameSpan = buffer.Slice(56, NameCharCapacity * 2);
        var source = Name.AsSpan();
        Encoding.Unicode.GetBytes(source, nameSpan);
    }

    public static EditableGptPartitionEntry FromSpan(ReadOnlySpan<byte> span)
    {
        if (span.Length < 128)
        {
            throw new ArgumentException("GPT partition entry data must be at least 128 bytes.", nameof(span));
        }

        var type = new Guid(span[..16]);
        var guid = new Guid(span.Slice(16, 16));
        var first = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(32, 8));
        var last = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(40, 8));
        var attributes = span.Slice(48, AttributeLength).ToArray();
        var name = Encoding.Unicode.GetString(span.Slice(56, NameCharCapacity * 2)).TrimEnd('\0');

        return new EditableGptPartitionEntry(type, guid, first, last, attributes, name);
    }

    private void SetFlag(Index index, byte mask, bool value)
    {
        var offset = index.GetOffset(_attributeFlags.Length);
        if (value)
        {
            _attributeFlags[offset] |= mask;
        }
        else
        {
            _attributeFlags[offset] &= unchecked((byte)~mask);
        }
    }

    private static string SanitizeName(string name)
    {
        name ??= string.Empty;
        if (name.Length > NameCharCapacity)
        {
            name = name[..NameCharCapacity];
        }

        return name.TrimEnd('\0');
    }
}
