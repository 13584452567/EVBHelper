using System.Globalization;
using System.Text;
using OpenixCard.Crypto;
using OpenixCard.Exceptions;
using OpenixCard.Logging;
using OpenixCard.Models;

namespace OpenixCard.Services;

internal sealed class ImageUnpacker : IImageUnpacker
{
    public Task<UnpackContext> UnpackAsync(string inputImagePath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputImagePath))
        {
            throw new ArgumentException("输入路径不能为空", nameof(inputImagePath));
        }

        if (!File.Exists(inputImagePath))
        {
            throw new FileOpenException(inputImagePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isAbsolute = Path.IsPathRooted(inputImagePath);
        var outputDirectory = BuildOutputDirectory(inputImagePath);

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, true);
        }
        Directory.CreateDirectory(outputDirectory);

        var imageBytes = File.ReadAllBytes(inputImagePath);
        if (imageBytes.Length <= 0)
        {
            throw new FileSizeException(inputImagePath);
        }

        var span = imageBytes.AsSpan();
        var encryptionEnabled = !ImageHeader.HasPlainMagic(span);

        using var crypto = ImageCrypto.Create(encryptionEnabled);

        var headerSpan = span.Slice(0, 1024);
        crypto.DecryptHeader(headerSpan);
        var header = ImageHeader.Parse(headerSpan);

        if (header.NumFiles == 0)
        {
            throw new OperatorError("镜像中未包含任何文件。");
        }

        var headerTableLength = checked((int)header.NumFiles * 1024);
        var headerTableSpan = span.Slice(1024, headerTableLength);
        crypto.DecryptHeaderTable(headerTableSpan);

        var entries = new List<ImageFileEntry>((int)header.NumFiles);
        for (var i = 0; i < header.NumFiles; i++)
        {
            var entrySpan = headerTableSpan.Slice(i * 1024, 1024);
            var entry = ImageFileEntry.Parse(entrySpan, header.HeaderVersion);
            entries.Add(entry);
        }

        var cursor = 1024 + headerTableLength;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var storedLength = checked((int)entry.StoredLength);
            if (storedLength <= 0)
            {
                continue;
            }

            if (cursor + storedLength > span.Length)
            {
                throw new OperatorError($"文件 {entry.FileName} 的存储长度超过镜像大小。");
            }

            var contentSlice = span.Slice(cursor, storedLength);
            crypto.DecryptContent(contentSlice);
            cursor += storedLength;
        }

        WriteImageCfg(outputDirectory, inputImagePath, header, entries);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = BuildOutputPath(outputDirectory, entry.FileName);
            var directoryName = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            var originalLength = checked((int)entry.OriginalLength);
            var offset = checked((int)entry.Offset);

            if (offset + originalLength > span.Length)
            {
                throw new OperatorError($"文件 {entry.FileName} 超出了镜像数据范围。");
            }

            var data = span.Slice(offset, originalLength).ToArray();
            File.WriteAllBytes(targetPath, data);
        }

        progress?.Report(OpenixLogMessage.Info($"已生成解包目录: {outputDirectory}"));

        return Task.FromResult(new UnpackContext
        {
            InputImagePath = inputImagePath,
            OutputDirectory = outputDirectory,
            InputPathIsAbsolute = isAbsolute
        });
    }

    private static string BuildOutputDirectory(string inputImagePath)
    {
        var directory = Path.GetDirectoryName(inputImagePath) ?? string.Empty;
        var fileName = Path.GetFileName(inputImagePath);
        return Path.Combine(directory, fileName + ".dump");
    }

    private static string BuildOutputPath(string outputDirectory, string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "unnamed";
        }

        fileName = fileName.Replace('\\', Path.DirectorySeparatorChar)
                           .Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(fileName))
        {
            fileName = fileName.TrimStart(Path.DirectorySeparatorChar);
        }

        return Path.Combine(outputDirectory, fileName);
    }

    private static void WriteImageCfg(string outputDirectory, string inputImagePath, ImageHeader header, IReadOnlyList<ImageFileEntry> entries)
    {
        var cfgPath = Path.Combine(outputDirectory, "image.cfg");
        using var writer = new StreamWriter(cfgPath, false, Encoding.UTF8);

        var timestamp = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture);

        writer.WriteLine(";/**************************************************************************/");
        writer.WriteLine($"; {timestamp}");
    writer.WriteLine("; generated by OpenixCard");
        writer.WriteLine($"; {inputImagePath}");
        writer.WriteLine(";/**************************************************************************/");
    writer.WriteLine("[DIR_DEF]");
    var inputDirValue = OperatingSystem.IsWindows() ? "\".\\\\\"" : "\"./\"";
    writer.WriteLine($"INPUT_DIR = {inputDirValue}");
        writer.WriteLine();
        writer.WriteLine("[FILELIST]");

        foreach (var entry in entries)
        {
            var fileName = entry.FileName.StartsWith('/') ? entry.FileName[1..] : entry.FileName;
            writer.WriteLine($"\t{{filename = INPUT_DIR .. \"{fileName}\", maintype = \"{entry.MainType}\", subtype = \"{entry.SubType}\",}},");
        }

        writer.WriteLine();
        writer.WriteLine("[IMAGE_CFG]");
        writer.WriteLine($"version = 0x{header.FormatVersion:X6}");
        writer.WriteLine($"pid = 0x{header.ProductId:X8}");
        writer.WriteLine($"vid = 0x{header.VendorId:X8}");
        writer.WriteLine($"hardwareid = 0x{header.HardwareId:X3}");
        writer.WriteLine($"firmwareid = 0x{header.FirmwareId:X3}");
        writer.WriteLine($"imagename = \"{inputImagePath}\"");
        writer.WriteLine("filelist = FILELIST");
    }
}
