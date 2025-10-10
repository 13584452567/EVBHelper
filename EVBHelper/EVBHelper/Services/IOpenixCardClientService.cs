using System;
using System.Threading;
using System.Threading.Tasks;
using OpenixCard.Logging;
using OpenixCard.Models;

namespace EVBHelper.Services;

public interface IOpenixCardClientService
{
    Task<PackContext> PackAsync(string directory, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);

    Task<OpenixUnpackResult> UnpackAsync(string inputImagePath, bool generateCfg, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);

    Task<OpenixDumpResult> DumpAsync(string inputImagePath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);

    Task<PartitionSizeInfo> ReportSizeAsync(string inputImagePath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken);
}
