using Openix.Models;

namespace Openix.Services;

internal interface IImageUnpacker
{
    Task<UnpackContext> UnpackAsync(string inputImagePath, CancellationToken cancellationToken);
}
