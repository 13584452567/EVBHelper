namespace Openix.Models;

/// <summary>
/// Represents a summarized size analysis for an image.
/// </summary>
public sealed class PartitionSizeInfo
{
    public double SizeInMegabytes { get; init; }
    public double SizeInKilobytes { get; init; }
    public IReadOnlyList<PartitionEntry> Partitions { get; init; } = Array.Empty<PartitionEntry>();
}

/// <summary>
/// Provides metadata about an individual partition entry.
/// </summary>
public sealed class PartitionEntry
{
    public required string Name { get; init; }
    public required long SizeInKilobytes { get; init; }
    public required string Source { get; init; }
}
