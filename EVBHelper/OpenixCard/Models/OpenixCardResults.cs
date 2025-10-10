namespace OpenixCard.Models;

/// <summary>
/// Represents the outcome of an unpack operation.
/// </summary>
/// <param name="InputImagePath">The source image that was unpacked.</param>
/// <param name="OutputDirectory">The directory containing the unpacked contents.</param>
/// <param name="GeneratedConfigPath">Optional path to the generated configuration file.</param>
public sealed record OpenixUnpackResult(string InputImagePath, string OutputDirectory, string? GeneratedConfigPath);

/// <summary>
/// Represents the outcome of a dump operation.
/// </summary>
/// <param name="OutputImagePath">The path to the produced standard image.</param>
/// <param name="ConfigPath">The configuration file used during generation.</param>
/// <param name="WorkingDirectory">The directory used as the working area.</param>
public sealed record OpenixDumpResult(string OutputImagePath, string ConfigPath, string WorkingDirectory);
