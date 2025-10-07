using Openix.Models;

namespace Openix.Services;

internal interface IGenImageWorkflow
{
    Task<PackContext> PackAsync(string directory, CancellationToken cancellationToken);
    Task<PackContext> DumpAsync(UnpackContext context, string cfgPath, CancellationToken cancellationToken);
}
