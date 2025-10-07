namespace Openix.Models;

internal sealed class UnpackContext
{
    public required string InputImagePath { get; init; }
    public required string OutputDirectory { get; init; }
    public required bool InputPathIsAbsolute { get; init; }
}
