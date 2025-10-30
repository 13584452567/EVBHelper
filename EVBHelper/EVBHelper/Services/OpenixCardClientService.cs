using OpenixCard;
using OpenixCard.Logging;
using OpenixCard.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EVBHelper.Services;

public sealed class OpenixCardClientService : IOpenixCardClientService
{
    private readonly OpenixCardClient _client = new();

    public Task<PackContext> PackAsync(string directory, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
        => _client.PackAsync(directory, progress, cancellationToken);

    public Task<OpenixUnpackResult> UnpackAsync(string inputImagePath, bool generateCfg, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
        => _client.UnpackAsync(inputImagePath, generateCfg, progress, cancellationToken);

    public Task<OpenixDumpResult> DumpAsync(string inputImagePath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
        => _client.DumpAsync(inputImagePath, progress, cancellationToken);

    public Task<PartitionSizeInfo> ReportSizeAsync(string inputImagePath, IProgress<OpenixLogMessage>? progress, CancellationToken cancellationToken)
        => _client.ReportSizeAsync(inputImagePath, progress, cancellationToken);
}
