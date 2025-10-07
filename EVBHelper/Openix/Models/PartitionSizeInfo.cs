namespace Openix.Models;

internal sealed class PartitionSizeInfo
{
    public double SizeInMegabytes { get; init; }
    public double SizeInKilobytes { get; init; }
    public IReadOnlyList<PartitionEntry> Partitions { get; init; } = Array.Empty<PartitionEntry>();
}

internal sealed class PartitionEntry
{
    public required string Name { get; init; }
    public required long SizeInKilobytes { get; init; }
    public required string Source { get; init; }
}
