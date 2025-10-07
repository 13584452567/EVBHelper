using Openix.Logging;
using Openix.Models;

namespace Openix.Services;

internal interface IImageUnpacker
{
    Task<UnpackContext> UnpackAsync(string inputImagePath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);
}
