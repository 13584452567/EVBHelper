using Openix.Models;

namespace Openix.Services;

internal interface IFexToCfgConverter
{
    Task<string> SaveAsync(UnpackContext context, CancellationToken cancellationToken);
    Task<PartitionSizeInfo> CalculateSizeAsync(UnpackContext context, CancellationToken cancellationToken);
}
