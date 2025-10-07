namespace Openix.Models;

internal sealed class PackContext
{
    public required string OutputImagePath { get; init; }
    public required string SourceDirectory { get; init; }
    public required string ConfigPath { get; init; }
}
