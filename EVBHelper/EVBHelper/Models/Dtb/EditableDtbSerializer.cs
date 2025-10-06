using DeviceTreeNode.Models;
using DeviceTreeNode.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EVBHelper.Models.Dtb;

internal static class EditableDtbSerializer
{
    private const int HeaderSize = 40;
    private const uint FdtMagic = 0xd00dfeed;
    private const uint FdtVersion = 17;
    private const uint FdtLastCompatibleVersion = 16;

    public static byte[] Serialize(EditableDtb dtb)
    {
        if (dtb == null)
        {
            throw new ArgumentNullException(nameof(dtb));
        }

        Dictionary<string, int> stringOffsets = new(StringComparer.Ordinal);
        using MemoryStream structsStream = new();
        using MemoryStream stringsStream = new();

        using (BinaryWriter structWriter = new(structsStream, Encoding.UTF8, leaveOpen: true))
        using (BinaryWriter stringWriter = new(stringsStream, Encoding.UTF8, leaveOpen: true))
        {
            AddString(stringWriter, stringOffsets, stringsStream, string.Empty);
            WriteNode(dtb.Root, structWriter, stringWriter, stringOffsets, stringsStream);
            WriteU32(structWriter, FdtConstants.FDT_END);
        }

        AlignStream(structsStream);
        AlignStream(stringsStream);

        int memReserveSize = CalculateMemoryReservationSize(dtb.MemoryReservations);
        int structsSize = (int)structsStream.Length;
        int stringsSize = (int)stringsStream.Length;

        int memReserveOffset = HeaderSize;
        int structsOffset = memReserveOffset + memReserveSize;
        int stringsOffset = structsOffset + structsSize;
        int totalSize = stringsOffset + stringsSize;

        using MemoryStream output = new(totalSize);
        using BinaryWriter writer = new(output, Encoding.UTF8, leaveOpen: true);

        WriteHeader(writer, totalSize, memReserveOffset, structsOffset, stringsOffset, structsSize, stringsSize);
        WriteMemoryReservations(writer, dtb.MemoryReservations);

        structsStream.Position = 0;
        structsStream.CopyTo(output);
        stringsStream.Position = 0;
        stringsStream.CopyTo(output);
        AlignStream(output);

        return output.ToArray();
    }

    private static void WriteNode(EditableDtbNode node,
                                  BinaryWriter structWriter,
                                  BinaryWriter stringWriter,
                                  Dictionary<string, int> stringOffsets,
                                  MemoryStream stringsStream)
    {
        WriteU32(structWriter, FdtConstants.FDT_BEGIN_NODE);
        WriteNodeName(structWriter, node.Name ?? string.Empty);

        foreach (var prop in node.Properties)
        {
            WriteProperty(prop, structWriter, stringWriter, stringOffsets, stringsStream);
        }

        foreach (var child in node.Children)
        {
            WriteNode(child, structWriter, stringWriter, stringOffsets, stringsStream);
        }

        WriteU32(structWriter, FdtConstants.FDT_END_NODE);
    }

    private static void WriteProperty(EditableDtbProperty property,
                                      BinaryWriter structWriter,
                                      BinaryWriter stringWriter,
                                      Dictionary<string, int> stringOffsets,
                                      MemoryStream stringsStream)
    {
        WriteU32(structWriter, FdtConstants.FDT_PROP);
        byte[] value = property.GetValueCopy();
        WriteU32(structWriter, (uint)value.Length);

        int nameOffset = AddString(stringWriter, stringOffsets, stringsStream, property.Name);
        WriteU32(structWriter, (uint)nameOffset);

        if (value.Length > 0)
        {
            structWriter.Write(value);
        }

        AlignWriter(structWriter);
    }

    private static void WriteNodeName(BinaryWriter writer, string name)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        writer.Write(nameBytes);
        writer.Write((byte)0);

        int padding = (4 - ((nameBytes.Length + 1) % 4)) % 4;
        for (int i = 0; i < padding; i++)
        {
            writer.Write((byte)0);
        }
    }

    private static int AddString(BinaryWriter writer,
                                 Dictionary<string, int> stringOffsets,
                                 MemoryStream stringsStream,
                                 string value)
    {
        if (stringOffsets.TryGetValue(value, out int offset))
        {
            return offset;
        }

        int newOffset = (int)stringsStream.Length;
        stringOffsets[value] = newOffset;

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes);
        writer.Write((byte)0);

        return newOffset;
    }

    private static void WriteHeader(BinaryWriter writer,
                                    int totalSize,
                                    int memReserveOffset,
                                    int structsOffset,
                                    int stringsOffset,
                                    int structsSize,
                                    int stringsSize)
    {
        WriteU32(writer, FdtMagic);
        WriteU32(writer, (uint)totalSize);
        WriteU32(writer, (uint)structsOffset);
        WriteU32(writer, (uint)stringsOffset);
        WriteU32(writer, (uint)memReserveOffset);
        WriteU32(writer, FdtVersion);
        WriteU32(writer, FdtLastCompatibleVersion);
        WriteU32(writer, 0);
        WriteU32(writer, (uint)stringsSize);
        WriteU32(writer, (uint)structsSize);
    }

    private static void WriteMemoryReservations(BinaryWriter writer, IEnumerable<MemoryReservation> reservations)
    {
        if (reservations != null)
        {
            foreach (var reservation in reservations)
            {
                ulong address = unchecked((ulong)reservation.Address.ToInt64());
                WriteU64(writer, address);
                WriteU64(writer, reservation.Size);
            }
        }

        WriteU64(writer, 0);
        WriteU64(writer, 0);
    }

    private static int CalculateMemoryReservationSize(IEnumerable<MemoryReservation> reservations)
    {
        int count = reservations?.Count() ?? 0;
        return (count + 1) * 16;
    }

    private static void WriteU32(BinaryWriter writer, uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        writer.Write(bytes);
    }

    private static void WriteU64(BinaryWriter writer, ulong value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        writer.Write(bytes);
    }

    private static void AlignStream(Stream stream)
    {
        long padding = (4 - (stream.Length % 4)) % 4;
        for (int i = 0; i < padding; i++)
        {
            stream.WriteByte(0);
        }
    }

    private static void AlignWriter(BinaryWriter writer)
    {
        long padding = (4 - (writer.BaseStream.Position % 4)) % 4;
        for (int i = 0; i < padding; i++)
        {
            writer.Write((byte)0);
        }
    }
}
