using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EVBHelper.Models;
using EVBHelper.Services;
using EVBHelper.Services.OpenixIMG;
using OpenixCard.Logging;
using OpenixIMG;
using Avalonia.Threading;

namespace EVBHelper.ViewModels.OpenixIMG;

public sealed partial class OpenixImgViewModel : ViewModelBase
{
    private const int MaxLogEntries = 500;

    private readonly IOpenixImgService _service;
    private readonly IFileDialogService _fileDialogService;
    private readonly ObservableCollection<OpenixLogEntry> _logEntries = new();
    private readonly ReadOnlyObservableCollection<OpenixLogEntry> _readonlyLogs;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _inputImagePath = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private bool _generateCfg = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private OpenixLogLevel _statusLevel = OpenixLogLevel.Info;

    [ObservableProperty]
    private bool _hasLogs;

    [ObservableProperty]
    private string? _partitionSummary;

    [ObservableProperty]
    private string? _headerSummary;

    [ObservableProperty]
    private bool _isEncrypted;

    public ObservableCollection<FileEntryItem> Files { get; } = new();

    [ObservableProperty]
    private FileEntryItem? _selectedFile;

    public OpenixImgViewModel(IOpenixImgService service, IFileDialogService fileDialogService)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _readonlyLogs = new ReadOnlyObservableCollection<OpenixLogEntry>(_logEntries);
    }

    public ReadOnlyObservableCollection<OpenixLogEntry> Logs => _readonlyLogs;

    private bool CanRun() => !IsBusy;
    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task BrowseImageAsync()
    {
        var filters = new[]
        {
            new FilePickerFileType("OpenixIMG files") { Patterns = new[] { "*.img", "*.bin", "*.fw", "*.fex" } },
            FilePickerFileTypes.All
        };

        var request = new FileDialogRequest
        {
            Title = "Select Openix image",
            Filters = filters,
            AllowMultiple = false
        };

        var result = await _fileDialogService.OpenFileAsync(request).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(result))
        {
            InputImagePath = result;
            OutputDirectory = Path.Combine(Path.GetDirectoryName(result) ?? ".", Path.GetFileNameWithoutExtension(result) + "_unpack");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task UnpackAsync()
    {
        PartitionSummary = null;
        return RunAsync(
            startMsg: "Unpacking...",
            op: async (progress, token) =>
            {
                EnsureFile(InputImagePath, "Please select a valid image file");
                var res = await _service.UnpackAsync(InputImagePath, GenerateCfg, progress, token).ConfigureAwait(true);
                OutputDirectory = res.OutputDirectory;
                if (!string.IsNullOrWhiteSpace(res.GeneratedConfigPath))
                {
                    Log(OpenixLogMessage.Info($"Generated: {res.GeneratedConfigPath}"));
                }
            });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task DecryptAsync()
    {
        PartitionSummary = null;
        return RunAsync(
            startMsg: "Decrypting...",
            op: async (progress, token) =>
            {
                EnsureFile(InputImagePath, "Please select a valid image file");
                var outFile = Path.Combine(Path.GetDirectoryName(InputImagePath) ?? ".", Path.GetFileNameWithoutExtension(InputImagePath) + ".decrypted.img");
                await _service.DecryptAsync(InputImagePath, outFile, progress, token).ConfigureAwait(true);
                Log(OpenixLogMessage.Info($"Decrypted to: {outFile}"));
            });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ShowPartitionAsync()
    {
        return RunAsync(
            startMsg: "Reading partitions...",
            op: async (progress, token) =>
            {
                EnsureFile(InputImagePath, "Please select a valid image file");
                var info = await _service.ReadPartitionInfoAsync(InputImagePath, progress, token).ConfigureAwait(true);
                PartitionSummary = $"Partitions: {info.Partition.Partitions.Count}, MBR size: {info.Partition.MbrSize}";
            });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task InspectAsync()
    {
        return RunAsync(
            startMsg: "Inspecting image...",
            op: async (_, token) =>
            {
                EnsureFile(InputImagePath, "Please select a valid image file");
                var res = await _service.InspectAsync(InputImagePath, token).ConfigureAwait(true);
                var items = new System.Collections.Generic.List<FileEntryItem>();
                if (res.Files != null)
                {
                    uint ver = res.Header.header_version;
                    for (int i = 0; i < res.Files.Length; i++)
                    {
                        var fh = res.Files[i];
                        var maintype = System.Text.Encoding.ASCII.GetString(fh.maintype).TrimEnd('\0', ' ');
                        var subtype = System.Text.Encoding.ASCII.GetString(fh.subtype).TrimEnd('\0', ' ');
                        string? filename = ver == 0x0300 ? fh.v3.filename : fh.v1.filename;
                        uint orig = ver == 0x0300 ? fh.v3.original_length : fh.v1.original_length;
                        uint stored = ver == 0x0300 ? fh.v3.stored_length : fh.v1.stored_length;
                        uint offset = ver == 0x0300 ? fh.v3.offset : fh.v1.offset;
                        items.Add(new FileEntryItem(i, maintype, subtype, filename ?? string.Empty, orig, stored, offset));
                    }
                }
                var headerText = $"ver=0x{res.Header.version:x}, files={(res.Files?.Length ?? 0)}, pid=0x{(res.Header.header_version==0x0300?res.Header.v3.pid:res.Header.v1.pid):x}, vid=0x{(res.Header.header_version==0x0300?res.Header.v3.vid:res.Header.v1.vid):x}";
                Dispatcher.UIThread.Post(() =>
                {
                    IsEncrypted = res.IsEncrypted;
                    HeaderSummary = headerText;
                    Files.Clear();
                    foreach (var it in items)
                    {
                        Files.Add(it);
                    }
                });
            });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task PackAsync()
    {
        return RunAsync(
            startMsg: "Packing...",
            op: async (progress, token) =>
            {
                var filters = new[] { new FilePickerFileType("Image") { Patterns = new[] { "*.img" } }, FilePickerFileTypes.All };
                var saveReq = new FileDialogRequest { Title = "Save image as", Filters = filters, DefaultExtension = "img" };
                var outFile = await _fileDialogService.SaveFileAsync(saveReq, token).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(outFile)) return;

                var folderReq = new FolderDialogRequest { Title = "Select folder containing image.cfg" };
                var folder = await _fileDialogService.OpenFolderAsync(folderReq, token).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(folder)) return;

                await _service.PackAsync(folder, outFile!, progress, token).ConfigureAwait(true);
                Log(OpenixLogMessage.Info($"Packed: {outFile}"));
            });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ExportSelectedAsync()
    {
        return RunAsync(
            startMsg: "Exporting file...",
            op: async (_, token) =>
            {
                EnsureFile(InputImagePath, "Please select a valid image file");
                if (SelectedFile is null || string.IsNullOrWhiteSpace(SelectedFile.FileName)) return;
                var saveReq = new FileDialogRequest
                {
                    Title = $"Save '{SelectedFile.FileName}' as",
                    SuggestedFileName = Path.GetFileName(SelectedFile.FileName)
                };
                var savePath = await _fileDialogService.SaveFileAsync(saveReq, token).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(savePath)) return;
                await _service.ExportFileAsync(InputImagePath, SelectedFile.FileName, savePath!, token).ConfigureAwait(true);
                Log(OpenixLogMessage.Info($"Exported: {savePath}"));
            });
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _logEntries.Clear();
        HasLogs = false;
    }

    private async Task RunAsync(string startMsg, Func<IProgress<OpenixLogMessage>, CancellationToken, Task> op)
    {
        if (IsBusy) return;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        SetStatus(startMsg, OpenixLogLevel.Info);
        var progress = new Progress<OpenixLogMessage>(Log);
        try
        {
            await Task.Run(() => op(progress, _cts.Token), _cts.Token).ConfigureAwait(true);
            SetStatus("Done", OpenixLogLevel.Info);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled", OpenixLogLevel.Warning);
        }
        catch (Exception ex)
        {
            Log(OpenixLogMessage.Debug(ex.ToString()));
            SetStatus(ex.Message, OpenixLogLevel.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    private static void EnsureFile(string path, string message)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Log(OpenixLogMessage msg)
    {
        var entry = new OpenixLogEntry(DateTimeOffset.Now, msg.Message, msg.Level);
        _logEntries.Add(entry);
        while (_logEntries.Count > MaxLogEntries)
        {
            _logEntries.RemoveAt(0);
        }
        HasLogs = _logEntries.Count > 0;
    }

    private void SetStatus(string message, OpenixLogLevel level)
    {
        StatusMessage = message;
        StatusLevel = level;
        Log(new OpenixLogMessage(level, message));
    }

    partial void OnIsBusyChanged(bool value)
    {
        BrowseImageCommand.NotifyCanExecuteChanged();
        UnpackCommand.NotifyCanExecuteChanged();
        DecryptCommand.NotifyCanExecuteChanged();
        ShowPartitionCommand.NotifyCanExecuteChanged();
    InspectCommand.NotifyCanExecuteChanged();
    PackCommand.NotifyCanExecuteChanged();
        ExportSelectedCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}

public sealed record FileEntryItem(int Index, string MainType, string SubType, string FileName, uint OriginalLength, uint StoredLength, uint Offset);
