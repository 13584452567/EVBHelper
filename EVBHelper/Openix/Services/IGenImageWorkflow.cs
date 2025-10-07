using Openix.Logging;
using Openix.Models;

namespace Openix.Services;

internal interface IGenImageWorkflow
{
    Task<PackContext> PackAsync(string directory, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);
    Task<PackContext> DumpAsync(UnpackContext context, string cfgPath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);
}
