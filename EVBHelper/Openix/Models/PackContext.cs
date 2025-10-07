namespace Openix.Models;

/// <summary>
/// Describes the artifacts generated during a pack or dump operation.
/// </summary>
public sealed class PackContext
{
    public required string OutputImagePath { get; init; }
    public required string SourceDirectory { get; init; }
    public required string ConfigPath { get; init; }
}
