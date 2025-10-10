using OpenixCard.Logging;
using OpenixCard.Models;

namespace OpenixCard.Services;

internal interface IImageUnpacker
{
    Task<UnpackContext> UnpackAsync(string inputImagePath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);
}
