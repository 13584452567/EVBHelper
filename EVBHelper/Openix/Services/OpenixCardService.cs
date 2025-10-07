using Openix.Logging;
using Openix.Models;

namespace Openix.Services;

internal sealed class OpenixCardService
{
    private readonly IFexToCfgConverter _fexToCfgConverter;
    private readonly IImageUnpacker _imageUnpacker;
    private readonly IGenImageWorkflow _genImageWorkflow;

    public OpenixCardService(
        IFexToCfgConverter fexToCfgConverter,
        IImageUnpacker imageUnpacker,
        IGenImageWorkflow genImageWorkflow)
    {
        _fexToCfgConverter = fexToCfgConverter;
        _imageUnpacker = imageUnpacker;
        _genImageWorkflow = genImageWorkflow;
    }

    public async Task<PackContext> PackAsync(string directory, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
    {
        Report(progress, OpenixLogLevel.Info, $"开始打包目录 {directory}...");
        var context = await _genImageWorkflow.PackAsync(directory, progress, cancellationToken).ConfigureAwait(false);
        Report(progress, OpenixLogLevel.Info, $"打包完成，输出文件: {context.OutputImagePath}");
        return context;
    }

    public async Task<OpenixUnpackResult> UnpackAsync(string inputPath, bool generateCfg, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("输入路径不能为空", nameof(inputPath));
        }

        Report(progress, OpenixLogLevel.Info, $"开始解包镜像 {inputPath}...");
        var unpackContext = await _imageUnpacker.UnpackAsync(inputPath, progress, cancellationToken).ConfigureAwait(false);
        Report(progress, OpenixLogLevel.Info, $"解包完成，输出目录: {unpackContext.OutputDirectory}");

        string? cfgPath = null;
        if (generateCfg)
        {
            cfgPath = await _fexToCfgConverter.SaveAsync(unpackContext, cancellationToken).ConfigureAwait(false);
            Report(progress, OpenixLogLevel.Info, $"已生成分区表配置: {cfgPath}");
        }

        return new OpenixUnpackResult(unpackContext.InputImagePath, unpackContext.OutputDirectory, cfgPath);
    }

    public async Task<OpenixDumpResult> DumpAsync(string inputPath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("输入路径不能为空", nameof(inputPath));
        }

        Report(progress, OpenixLogLevel.Info, $"开始转换镜像 {inputPath}...");
        var unpackContext = await _imageUnpacker.UnpackAsync(inputPath, progress, cancellationToken).ConfigureAwait(false);
        var cfgPath = await _fexToCfgConverter.SaveAsync(unpackContext, cancellationToken).ConfigureAwait(false);
        var output = await _genImageWorkflow.DumpAsync(unpackContext, cfgPath, progress, cancellationToken).ConfigureAwait(false);
        Report(progress, OpenixLogLevel.Info, $"转换完成，输出镜像: {output.OutputImagePath}");

        return new OpenixDumpResult(output.OutputImagePath, cfgPath, unpackContext.OutputDirectory);
    }

    public async Task<PartitionSizeInfo> ReportSizeAsync(string inputPath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("输入路径不能为空", nameof(inputPath));
        }

        Report(progress, OpenixLogLevel.Info, $"计算镜像大小: {inputPath}");
        var unpackContext = await _imageUnpacker.UnpackAsync(inputPath, progress, cancellationToken).ConfigureAwait(false);
        try
        {
            var info = await _fexToCfgConverter.CalculateSizeAsync(unpackContext, cancellationToken).ConfigureAwait(false);
            Report(progress, OpenixLogLevel.Data, "分区表:");
            foreach (var partition in info.Partitions)
            {
                var sizeMb = partition.SizeInKilobytes / 1024.0;
                var remaining = string.Equals(partition.Name, "UDISK", StringComparison.OrdinalIgnoreCase)
                    ? " (剩余空间)"
                    : string.Empty;
                Report(progress, OpenixLogLevel.Data, $"  分区 '{partition.Name}' {sizeMb,8:F2} MB - {partition.SizeInKilobytes,7} KB{remaining}");
                Report(progress, OpenixLogLevel.Debug, $"    来源: {partition.Source}");
            }

            Report(progress, OpenixLogLevel.Data, $"镜像大小: {info.SizeInMegabytes:F2} MB ({info.SizeInKilobytes:F0} KB)");
            return info;
        }
        finally
        {
            TryCleanup(unpackContext.OutputDirectory, progress);
        }
    }

    private static void TryCleanup(string directory, IProgress<OpenixLogMessage>? progress)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            Directory.Delete(directory, true);
        }
        catch (Exception ex)
        {
            Report(progress, OpenixLogLevel.Warning, $"无法清理临时目录 {directory}: {ex.Message}");
        }
    }

    private static void Report(IProgress<OpenixLogMessage>? progress, OpenixLogLevel level, string message)
        => progress?.Report(new OpenixLogMessage(level, message));
}
