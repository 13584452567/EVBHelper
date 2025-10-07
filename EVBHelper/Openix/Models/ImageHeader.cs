using System.Buffers.Binary;
using System.Text;

namespace Openix.Models;

internal sealed class ImageHeader
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("IMAGEWTY");

    public uint HeaderVersion { get; private init; }
    public uint HeaderSize { get; private init; }
    public uint ImageSize { get; private init; }
    public uint ImageHeaderSize { get; private init; }
    public uint NumFiles { get; private init; }
    public uint HardwareId { get; private init; }
    public uint FirmwareId { get; private init; }
    public uint ProductId { get; private init; }
    public uint VendorId { get; private init; }
    public uint FormatVersion { get; private init; }

    public static bool HasPlainMagic(ReadOnlySpan<byte> buffer)
        => buffer.Length >= Magic.Length && buffer[..Magic.Length].SequenceEqual(Magic);

    public static ImageHeader Parse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 1024)
        {
            throw new ArgumentException("Header buffer must be at least 1024 bytes", nameof(buffer));
        }

    var headerVersion = BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..12]);
    var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer[12..16]);
    var formatVersion = BinaryPrimitives.ReadUInt32LittleEndian(buffer[20..24]);
    var imageSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer[24..28]);
    var imageHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer[28..32]);

        uint numFiles;
        uint hardwareId;
        uint firmwareId;
        uint pid;
        uint vid;

        if (headerVersion == 0x0300)
        {
            var baseOffset = 32;
            pid = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 4, 4));
            vid = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 8, 4));
            hardwareId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 12, 4));
            firmwareId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 16, 4));
            numFiles = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 28, 4));
        }
        else if (headerVersion == 0x0100)
        {
            var baseOffset = 32;
            pid = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 0, 4));
            vid = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 4, 4));
            hardwareId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 8, 4));
            firmwareId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 12, 4));
            numFiles = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(baseOffset + 24, 4));
        }
        else
        {
            throw new InvalidOperationException($"Unsupported IMAGEWTY header version: 0x{headerVersion:X4}");
        }

        return new ImageHeader
        {
            HeaderVersion = headerVersion,
            HeaderSize = headerSize,
            ImageSize = imageSize,
            ImageHeaderSize = imageHeaderSize,
            NumFiles = numFiles,
            HardwareId = hardwareId,
            FirmwareId = firmwareId,
            ProductId = pid,
            VendorId = vid,
            FormatVersion = formatVersion
        };
    }
}
