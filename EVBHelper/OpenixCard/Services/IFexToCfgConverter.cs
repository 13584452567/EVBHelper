using OpenixCard.Models;

namespace OpenixCard.Services;

internal interface IFexToCfgConverter
{
    Task<string> SaveAsync(UnpackContext context, CancellationToken cancellationToken);
    Task<PartitionSizeInfo> CalculateSizeAsync(UnpackContext context, CancellationToken cancellationToken);
}
