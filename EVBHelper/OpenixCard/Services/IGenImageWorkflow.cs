using OpenixCard.Logging;
using OpenixCard.Models;

namespace OpenixCard.Services;

internal interface IGenImageWorkflow
{
    Task<PackContext> PackAsync(string directory, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);
    Task<PackContext> DumpAsync(UnpackContext context, string cfgPath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);
}
