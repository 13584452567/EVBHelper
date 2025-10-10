using OpenixCard.Logging;
using OpenixCard.Models;
using OpenixCard.Services;

namespace OpenixCard;

/// <summary>
/// Provides a high-level facade for performing OpenixCard operations programmatically.
/// </summary>
public sealed class OpenixCardClient
{
    private readonly OpenixCardService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenixCardClient"/> class with default dependencies.
    /// </summary>
    public OpenixCardClient()
        : this(new OpenixCardService(new FexToCfgConverter(), new ImageUnpacker(), new GenImageWorkflow()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenixCardClient"/> class for testing or advanced scenarios.
    /// </summary>
    internal OpenixCardClient(OpenixCardService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Packs the specified directory into an Allwinner-compatible image.
    /// </summary>
    public Task<PackContext> PackAsync(
        string directory,
        IProgress<OpenixLogMessage>? progress = null,
        CancellationToken cancellationToken = default)
        => _service.PackAsync(directory, progress, cancellationToken);

    /// <summary>
    /// Unpacks the provided image. Optionally emits a cfg representation of the partition table.
    /// </summary>
    public Task<OpenixUnpackResult> UnpackAsync(
        string inputImagePath,
        bool generateCfg = false,
        IProgress<OpenixLogMessage>? progress = null,
        CancellationToken cancellationToken = default)
        => _service.UnpackAsync(inputImagePath, generateCfg, progress, cancellationToken);

    /// <summary>
    /// Converts an Allwinner image into a standard image by unpacking and repacking it.
    /// </summary>
    public Task<OpenixDumpResult> DumpAsync(
        string inputImagePath,
        IProgress<OpenixLogMessage>? progress = null,
        CancellationToken cancellationToken = default)
        => _service.DumpAsync(inputImagePath, progress, cancellationToken);

    /// <summary>
    /// Analyses the partitions of an image and reports their sizes.
    /// </summary>
    public Task<PartitionSizeInfo> ReportSizeAsync(
        string inputImagePath,
        IProgress<OpenixLogMessage>? progress = null,
        CancellationToken cancellationToken = default)
        => _service.ReportSizeAsync(inputImagePath, progress, cancellationToken);
}
