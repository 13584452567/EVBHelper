using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenixIMG;
using OpenixCard.Logging;

namespace EVBHelper.Services.OpenixIMG;

public interface IOpenixImgService
{
    Task<UnpackResult> UnpackAsync(string imagePath, bool generateCfg, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default);
    Task<DecryptResult> DecryptAsync(string imagePath, string outputFile, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default);
    Task<PartitionInfoResult> ReadPartitionInfoAsync(string imagePath, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default);
    Task<InspectResult> InspectAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<PackResult> PackAsync(string folder, string outputFile, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default);
    Task ExportFileAsync(string imagePath, string filename, string savePath, CancellationToken cancellationToken = default);
}

public sealed record UnpackResult(string OutputDirectory, string? GeneratedConfigPath);
public sealed record DecryptResult(string OutputFile);
public sealed record PartitionInfoResult(OpenixPartition Partition, string? TempCfgPath);
public sealed record InspectResult(ImageHeader Header, FileHeader[] Files, bool IsEncrypted);
public sealed record PackResult(string OutputFile);

public sealed class OpenixImgService : IOpenixImgService
{
    public Task<UnpackResult> UnpackAsync(string imagePath, bool generateCfg, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(OpenixLogMessage.Info($"Loading image: {imagePath}"));

            var packer = new OpenixPacker(verbose: true);
            if (!packer.LoadImage(imagePath))
            {
                throw new InvalidOperationException("Failed to load image");
            }

            var outputDir = Path.Combine(Path.GetDirectoryName(imagePath) ?? ".", Path.GetFileNameWithoutExtension(imagePath) + "_unpack");
            Directory.CreateDirectory(outputDir);

            cancellationToken.ThrowIfCancellationRequested();
            packer.SetOutputFormat(OutputFormat.UNIMG);
            progress?.Report(OpenixLogMessage.Info("Unpacking files..."));
            if (!packer.UnpackImage(outputDir))
            {
                throw new InvalidOperationException("Unpack failed");
            }

            string? cfgPath = null;
            if (generateCfg)
            {
                // OpenixPacker.UnpackImage already generates image.cfg; validate presence
                var candidate = Path.Combine(outputDir, "image.cfg");
                if (File.Exists(candidate))
                {
                    cfgPath = candidate;
                }
            }

            progress?.Report(OpenixLogMessage.Info("Done"));
            return new UnpackResult(outputDir, cfgPath);
        }, cancellationToken);
    }

    public Task<DecryptResult> DecryptAsync(string imagePath, string outputFile, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(OpenixLogMessage.Info($"Decrypting image to: {outputFile}"));

            var packer = new OpenixPacker(verbose: true);
            if (!packer.LoadImage(imagePath))
            {
                throw new InvalidOperationException("Failed to load image");
            }

            if (!packer.DecryptImage(outputFile))
            {
                throw new InvalidOperationException("Decrypt failed");
            }

            progress?.Report(OpenixLogMessage.Info("Decrypt completed"));
            return new DecryptResult(outputFile);
        }, cancellationToken);
    }

    public Task<PartitionInfoResult> ReadPartitionInfoAsync(string imagePath, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(OpenixLogMessage.Info("Parsing sys_partition.fex ..."));

            var packer = new OpenixPacker(verbose: false);
            if (!packer.LoadImage(imagePath))
            {
                throw new InvalidOperationException("Failed to load image");
            }

            var data = packer.GetFileDataByFilename("sys_partition.fex");
            if (data is null)
            {
                throw new InvalidOperationException("sys_partition.fex not found in image");
            }

            var partition = new OpenixPartition(data, verbose: false);
            return new PartitionInfoResult(partition, null);
        }, cancellationToken);
    }

    public Task<InspectResult> InspectAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var packer = new OpenixPacker();
            if (!packer.LoadImage(imagePath))
            {
                throw new InvalidOperationException("Failed to load image");
            }
            return new InspectResult(packer.ImageHeader, packer.FileHeaders, packer.IsEncrypted);
        }, cancellationToken);
    }

    public Task<PackResult> PackAsync(string folder, string outputFile, IProgress<OpenixLogMessage>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            progress?.Report(OpenixLogMessage.Info($"Packing folder: {folder}"));
            var builder = new OpenixImageBuilder();
            builder.BuildFromFolder(folder, outputFile);
            progress?.Report(OpenixLogMessage.Info("Pack completed"));
            return new PackResult(outputFile);
        }, cancellationToken);
    }

    public Task ExportFileAsync(string imagePath, string filename, string savePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var packer = new OpenixPacker();
            if (!packer.LoadImage(imagePath))
            {
                throw new InvalidOperationException("Failed to load image");
            }
            var data = packer.GetFileDataByFilename(filename);
            if (data == null)
            {
                throw new FileNotFoundException($"File not found in image: {filename}");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? ".");
            File.WriteAllBytes(savePath, data);
        }, cancellationToken);
    }
}
