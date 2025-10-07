using System.Buffers.Binary;
using System.Text;

namespace Openix.Models;

internal sealed class ImageFileEntry
{
    public required string FileName { get; init; }
    public required string MainType { get; init; }
    public required string SubType { get; init; }
    public required uint StoredLength { get; init; }
    public required uint OriginalLength { get; init; }
    public required uint Offset { get; init; }

    public static ImageFileEntry Parse(ReadOnlySpan<byte> buffer, uint headerVersion)
    {
        if (buffer.Length < 1024)
        {
            throw new ArgumentException("File header must be 1024 bytes", nameof(buffer));
        }

        var mainType = ReadAscii(buffer.Slice(8, 8));
        var subType = ReadAscii(buffer.Slice(16, 16));
        string fileName;
        uint storedLength;
        uint originalLength;
        uint offset;

        if (headerVersion == 0x0300)
        {
            fileName = ReadAscii(buffer.Slice(36, 256));
            storedLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(292, 4));
            originalLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(300, 4));
            offset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(308, 4));
        }
        else
        {
            storedLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(36, 4));
            originalLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(40, 4));
            offset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(44, 4));
            fileName = ReadAscii(buffer.Slice(52, 256));
        }

        return new ImageFileEntry
        {
            FileName = fileName,
            MainType = mainType,
            SubType = subType,
            StoredLength = storedLength,
            OriginalLength = originalLength,
            Offset = offset
        };
    }

    private static string ReadAscii(ReadOnlySpan<byte> span)
    {
        var terminator = span.IndexOf((byte)0);
        if (terminator >= 0)
        {
            span = span[..terminator];
        }
        return Encoding.ASCII.GetString(span); // filenames are ASCII in image format
    }
}
