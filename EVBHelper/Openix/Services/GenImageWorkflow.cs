using System.Linq;
using Openix.Exceptions;
using Openix.Logging;
using Openix.Models;
using Openix.Services.GenImage;

namespace Openix.Services;

internal sealed class GenImageWorkflow : IGenImageWorkflow
{
    private readonly GenImageBuilder _builder = new();

    public async Task<PackContext> PackAsync(string directory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        var cfgPath = FindTargetCfgFile(directory);
        EnsureBlankFex(directory);

        var outputImagePath = await _builder.BuildAsync(cfgPath, directory, directory, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(outputImagePath))
        {
            var expected = DeterminePrimaryImageName(cfgPath) ?? outputImagePath;
            Logger.Warning($"未在预期路径找到生成的镜像文件: {expected}。");
        }

        return new PackContext
        {
            ConfigPath = cfgPath,
            OutputImagePath = outputImagePath,
            SourceDirectory = directory
        };
    }

    public async Task<PackContext> DumpAsync(UnpackContext context, string cfgPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!File.Exists(cfgPath))
        {
            throw new FileOpenException(cfgPath);
        }

        EnsureBlankFex(context.OutputDirectory);
        var outputDirectory = context.OutputDirectory + ".out";

        var outputImagePath = await _builder.BuildAsync(cfgPath, context.OutputDirectory, outputDirectory, cancellationToken).ConfigureAwait(false);

        return new PackContext
        {
            ConfigPath = cfgPath,
            OutputImagePath = outputImagePath,
            SourceDirectory = context.OutputDirectory
        };
    }

    private static string FindTargetCfgFile(string directory)
    {
        var candidates = Directory.GetFiles(directory, "*.cfg");
        var target = candidates.FirstOrDefault(file => !string.Equals(Path.GetFileName(file), "image.cfg", StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            throw new OperatorError("未找到目标分区表 cfg 文件。");
        }
        return target;
    }

    private static void EnsureBlankFex(string directory)
    {
        var blankPath = Path.Combine(directory, "blank.fex");
        if (!File.Exists(blankPath))
        {
            File.WriteAllText(blankPath, "blank.fex");
        }
    }

    private static string? DeterminePrimaryImageName(string cfgPath)
    {
        foreach (var line in File.ReadLines(cfgPath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("image ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                var name = tokens[1];
                if (name.EndsWith("{", StringComparison.Ordinal))
                {
                    name = name[..^1];
                }
                return name;
            }
        }

        return null;
    }
}
