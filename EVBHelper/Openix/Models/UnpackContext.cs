namespace Openix.Models;

/// <summary>
/// Provides contextual data about an unpacked image.
/// </summary>
public sealed class UnpackContext
{
    public required string InputImagePath { get; init; }
    public required string OutputDirectory { get; init; }
    public required bool InputPathIsAbsolute { get; init; }
}
