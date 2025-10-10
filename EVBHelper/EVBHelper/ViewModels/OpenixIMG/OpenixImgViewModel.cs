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
        CancelCommand.NotifyCanExecuteChanged();
    }
}
