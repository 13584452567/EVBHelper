using Openix.Cli;
using Openix.Exceptions;
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

    public async Task ExecuteAsync(CommandOptions options, CancellationToken cancellationToken)
    {
        if (options.Inputs.Count == 0)
        {
            throw new NoInputProvidedException();
        }

        var input = options.Inputs[0];

        if (options.Pack)
        {
            await PackAsync(input, options, cancellationToken);
            return;
        }

        if (options.Unpack)
        {
            await UnpackAsync(input, options, cancellationToken);
        }

        if (options.Dump)
        {
            await DumpAsync(input, options, cancellationToken);
        }

        if (options.ReportSize)
        {
            await ReportSizeAsync(input, cancellationToken);
        }
    }

    private async Task PackAsync(string directory, CommandOptions options, CancellationToken cancellationToken)
    {
        Logger.Info($"��ʼ���Ŀ¼ {directory}...");
        var context = await _genImageWorkflow.PackAsync(directory, cancellationToken);
        Logger.Info($"�����ɣ�����ļ�: {context.OutputImagePath}");
    }

    private async Task UnpackAsync(string inputPath, CommandOptions options, CancellationToken cancellationToken)
    {
        Logger.Info($"��ʼ������� {inputPath}...");
        var unpackContext = await _imageUnpacker.UnpackAsync(inputPath, cancellationToken);
        Logger.Info($"�����ɣ����Ŀ¼: {unpackContext.OutputDirectory}");

        if (options.GenerateCfg)
        {
            var cfgPath = await _fexToCfgConverter.SaveAsync(unpackContext, cancellationToken);
            Logger.Info($"�����ɷ���������: {cfgPath}");
        }
    }

    private async Task DumpAsync(string inputPath, CommandOptions options, CancellationToken cancellationToken)
    {
        Logger.Info($"��ʼת������ {inputPath}...");
        var unpackContext = await _imageUnpacker.UnpackAsync(inputPath, cancellationToken);
        var cfgPath = await _fexToCfgConverter.SaveAsync(unpackContext, cancellationToken);
        var output = await _genImageWorkflow.DumpAsync(unpackContext, cfgPath, cancellationToken);
        Logger.Info($"ת����ɣ��������: {output.OutputImagePath}");
    }

    private async Task ReportSizeAsync(string inputPath, CancellationToken cancellationToken)
    {
        Logger.Info($"���㾵���С: {inputPath}");
        var unpackContext = await _imageUnpacker.UnpackAsync(inputPath, cancellationToken);
        try
        {
            var info = await _fexToCfgConverter.CalculateSizeAsync(unpackContext, cancellationToken);
            Logger.Data("������:");
            foreach (var partition in info.Partitions)
            {
                var sizeMb = partition.SizeInKilobytes / 1024.0;
                var remaining = string.Equals(partition.Name, "UDISK", StringComparison.OrdinalIgnoreCase)
                    ? " (ʣ��ռ�)"
                    : string.Empty;
                Logger.Data($"  ���� '{partition.Name}' {sizeMb,8:F2} MB - {partition.SizeInKilobytes,7} KB{remaining}");
                Logger.Debug($"    ��Դ: {partition.Source}");
            }

            Logger.Data($"�����С: {info.SizeInMegabytes:F2} MB ({info.SizeInKilobytes:F0} KB)");
        }
        finally
        {
            TryCleanup(unpackContext.OutputDirectory);
        }
    }

    private static void TryCleanup(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"�޷�������ʱĿ¼ {directory}: {ex.Message}");
        }
    }
}
