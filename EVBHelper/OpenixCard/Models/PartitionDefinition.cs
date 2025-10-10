namespace OpenixCard.Models;

internal sealed class PartitionDefinition
{
    public required string Name { get; init; }
    public long SizeSectors { get; init; }
    public string? DownloadFile { get; init; }
    public long? UserType { get; init; }
}
