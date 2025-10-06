using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EVBHelper.Models;
using EVBHelper.Services;
using EVBHelper.ViewModels.Dtb;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EVBHelper.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private const int MaxLogEntries = 500;

        private readonly IRfelCliService _rfelCliService;
        private readonly IFileDialogService _fileDialogService;
        private readonly ObservableCollection<LogMessage> _logMessages = new();
        private readonly ReadOnlyObservableCollection<LogMessage> _readonlyLogs;

        private CancellationTokenSource? _operationCts;

        [ObservableProperty]
        private string _rfelPath = "rfel";

        [ObservableProperty]
        private string _firmwarePath = string.Empty;

        [ObservableProperty]
        private string _loadAddress = "0x40008000";

        [ObservableProperty]
        private bool _initializeDdr = true;

        [ObservableProperty]
        private string? _ddrProfile;

        [ObservableProperty]
        private bool _resetAfterFlash = true;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isProgressIndeterminate = true;

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private string _memoryAddress = "0x40000000";

        [ObservableProperty]
        private string _memoryLength = "0x100";

        [ObservableProperty]
        private string _memoryValue = "0x00000000";

        [ObservableProperty]
        private string _memoryReadFilePath = string.Empty;

        [ObservableProperty]
        private string _memoryWriteFilePath = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private RfelLogLevel _statusLevel = RfelLogLevel.Info;

        [ObservableProperty]
        private bool _hasLogs;

        [ObservableProperty]
        private VerbosityOption _selectedVerbosity;

        public DtbEditorViewModel DtbEditor { get; }

        public MainWindowViewModel(IRfelCliService rfelCliService, IFileDialogService fileDialogService)
        {
            _rfelCliService = rfelCliService ?? throw new ArgumentNullException(nameof(rfelCliService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _readonlyLogs = new ReadOnlyObservableCollection<LogMessage>(_logMessages);
            DtbEditor = new DtbEditorViewModel(fileDialogService);

            VerbosityOptions = new List<VerbosityOption>
            {
                new(0, "Errors Only"),
                new(1, "Information"),
                new(2, "Debug"),
                new(3, "Trace")
            };

            _selectedVerbosity = VerbosityOptions[1];
        }

        public IReadOnlyList<VerbosityOption> VerbosityOptions { get; }

        public ReadOnlyObservableCollection<LogMessage> LogMessages => _readonlyLogs;

        private bool CanExecuteCommands() => !IsBusy;

        private bool CanCancel() => IsBusy;

        [RelayCommand]
        private async Task BrowseRfelAsync()
        {
            var filters = new List<FilePickerFileType>
            {
                new("rfel Executable") { Patterns = new[] { "rfel", "rfel.exe", "*.exe" } },
                FilePickerFileTypes.All
            };

            var request = new FileDialogRequest
            {
                Title = "Select rfel Executable",
                Filters = filters,
                AllowMultiple = false
            };

            var result = await _fileDialogService.OpenFileAsync(request).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                RfelPath = result;
            }
        }

        [RelayCommand]
        private async Task BrowseFirmwareAsync()
        {
            var filters = new List<FilePickerFileType>
            {
                new("Firmware Files") { Patterns = new[] { "*.bin", "*.img", "*.dtb", "*.elf" } },
                FilePickerFileTypes.All
            };

            var request = new FileDialogRequest
            {
                Title = "Select Firmware File",
                Filters = filters,
                AllowMultiple = false
            };

            var result = await _fileDialogService.OpenFileAsync(request).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                FirmwarePath = result;
            }
        }

        [RelayCommand]
        private async Task BrowseMemoryWriteFileAsync()
        {
            var filters = new List<FilePickerFileType>
            {
                new("Binary Files") { Patterns = new[] { "*.bin", "*.img", "*.dtb", "*.elf" } },
                FilePickerFileTypes.All
            };

            var request = new FileDialogRequest
            {
                Title = "Select Source File",
                Filters = filters,
                AllowMultiple = false
            };

            var result = await _fileDialogService.OpenFileAsync(request).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                MemoryWriteFilePath = result;
            }
        }

        [RelayCommand]
        private async Task BrowseMemoryReadFileAsync()
        {
            var filters = new List<FilePickerFileType>
            {
                new("Binary Files") { Patterns = new[] { "*.bin", "*.img", "*.dtb", "*.elf" } },
                FilePickerFileTypes.All
            };

            var request = new FileDialogRequest
            {
                Title = "Save Output File",
                Filters = filters,
                SuggestedFileName = $"memory_{DateTime.Now:yyyyMMdd_HHmmss}.bin",
                DefaultExtension = ".bin"
            };

            var result = await _fileDialogService.SaveFileAsync(request).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(result))
            {
                MemoryReadFilePath = result;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task CheckDeviceAsync() => RunOperationAsync(
            "Checking device...",
            async (progress, token) =>
            {
                if (!EnsureExecutableExists())
                {
                    return;
                }

                var result = await _rfelCliService.CheckVersionAsync(RfelPath, SelectedVerbosity.Level, progress, token).ConfigureAwait(true);
                UpdateStatusFromResult(result, "Device online", "No Allwinner FEL device detected");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task FlashAsync() => RunOperationAsync(
            "Flashing firmware...",
            async (progress, token) =>
            {
                if (!EnsureExecutableExists() || !EnsureFirmwareExists() || !EnsureAddressValid())
                {
                    return;
                }

                var request = new RfelFlashRequest
                {
                    ExecutablePath = RfelPath,
                    FirmwarePath = FirmwarePath,
                    Address = LoadAddress.Trim(),
                    InitializeDdr = InitializeDdr,
                    DdrProfile = string.IsNullOrWhiteSpace(DdrProfile) ? null : DdrProfile.Trim(),
                    ResetAfterFlash = ResetAfterFlash,
                    VerbosityLevel = SelectedVerbosity.Level,
                    WorkingDirectory = Path.GetDirectoryName(FirmwarePath)
                };

                var pipelineResult = await _rfelCliService.FlashAsync(request, progress, token).ConfigureAwait(true);

                if (pipelineResult.Success)
                {
                    SetStatus("Flash completed", RfelLogLevel.Info);
                }
                else if (pipelineResult.WasCancelled)
                {
                    SetStatus("Flash cancelled", RfelLogLevel.Warning);
                }
                else
                {
                    var message = pipelineResult.FirstFailure?.DescribeOutcome() ?? "Flash failed";
                    SetStatus(message, RfelLogLevel.Error);
                }
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task ResetDeviceAsync() => RunOperationAsync(
            "Sending reset...",
            async (progress, token) =>
            {
                if (!EnsureExecutableExists())
                {
                    return;
                }

                var result = await _rfelCliService.ResetAsync(RfelPath, SelectedVerbosity.Level, progress, token).ConfigureAwait(true);
                UpdateStatusFromResult(result, "Reset command sent", "Reset failed, check logs");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task Read32Async() => ExecuteRfelCommandAsync(
            "Reading 32-bit value...",
            () =>
            {
                var address = NormalizeInput(MemoryAddress);
                if (!EnsureField(address, "Please enter the memory address"))
                {
                    return null;
                }

                return new CommandInvocation(
                    new List<string> { "read32", address },
                    $"Read 32-bit value from {address}",
                    "read32 command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task Write32Async() => ExecuteRfelCommandAsync(
            "Writing 32-bit value...",
            () =>
            {
                var address = NormalizeInput(MemoryAddress);
                var value = NormalizeInput(MemoryValue);

                if (!EnsureField(address, "Please enter the memory address") ||
                    !EnsureField(value, "Please enter the value to write"))
                {
                    return null;
                }

                return new CommandInvocation(
                    new List<string> { "write32", address, value },
                    $"Wrote value {value} to {address}",
                    "write32 command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task HexdumpAsync() => ExecuteRfelCommandAsync(
            "Hex dumping memory...",
            () =>
            {
                var address = NormalizeInput(MemoryAddress);
                var length = NormalizeInput(MemoryLength);

                if (!EnsureField(address, "Please enter the memory address") ||
                    !EnsureField(length, "Please enter the length"))
                {
                    return null;
                }

                return new CommandInvocation(
                    new List<string> { "hexdump", address, length },
                    $"Hexdump completed for {length} bytes at {address}",
                    "hexdump command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task DumpAsync() => ExecuteRfelCommandAsync(
            "Dumping memory...",
            () =>
            {
                var address = NormalizeInput(MemoryAddress);
                var length = NormalizeInput(MemoryLength);

                if (!EnsureField(address, "Please enter the memory address") ||
                    !EnsureField(length, "Please enter the length"))
                {
                    return null;
                }

                return new CommandInvocation(
                    new List<string> { "dump", address, length },
                    $"Dump completed for {length} bytes at {address}",
                    "dump command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task ReadMemoryAsync() => ExecuteRfelCommandAsync(
            "Reading memory to file...",
            () =>
            {
                var address = NormalizeInput(MemoryAddress);
                var length = NormalizeInput(MemoryLength);
                var destination = NormalizeInput(MemoryReadFilePath);

                if (!EnsureField(address, "Please enter the memory address") ||
                    !EnsureField(length, "Please enter the length") ||
                    !EnsureField(destination, "Please choose an output file") ||
                    !EnsureWritableFilePath(destination))
                {
                    return null;
                }

                return new CommandInvocation(
                    new List<string> { "read", address, length, destination },
                    $"Saved {length} bytes to {destination}",
                    "read command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task WriteMemoryAsync() => ExecuteRfelCommandAsync(
            "Writing memory from file...",
            () =>
            {
                var address = NormalizeInput(MemoryAddress);
                var source = NormalizeInput(MemoryWriteFilePath);

                if (!EnsureField(address, "Please enter the memory address") ||
                    !EnsureField(source, "Please select a source file") ||
                    !EnsureSourceFileExists(source))
                {
                    return null;
                }

                return new CommandInvocation(
                    new List<string> { "write", address, source },
                    $"Wrote {Path.GetFileName(source)} to {address}",
                    "write command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task ExecuteCodeAsync() => ExecuteRfelCommandAsync(
            "Executing code...",
            () =>
            {
                var address = NormalizeInput(MemoryAddress);

                if (!EnsureField(address, "Please enter the execution address"))
                {
                    return null;
                }

                return new CommandInvocation(
                    new List<string> { "exec", address },
                    $"Execution command sent to {address}",
                    "exec command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task ShowSidAsync() => ExecuteRfelCommandAsync(
            "Querying SID...",
            () => new CommandInvocation(
                new List<string> { "sid" },
                "SID query completed",
                "sid command failed"));

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task EnableJtagAsync() => ExecuteRfelCommandAsync(
            "Enabling JTAG...",
            () => new CommandInvocation(
                new List<string> { "jtag" },
                "JTAG enable command sent",
                "jtag command failed"));

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task RunDdrInitAsync() => ExecuteRfelCommandAsync(
            "Initializing DDR...",
            () =>
            {
                var arguments = new List<string> { "ddr" };
                var profile = NormalizeInput(DdrProfile);
                if (!string.IsNullOrWhiteSpace(profile))
                {
                    arguments.Add("--profile");
                    arguments.Add(profile);
                }

                return new CommandInvocation(
                    arguments,
                    "DDR initialization command sent",
                    "ddr command failed");
            });

        [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
        private Task ShowHelpAsync() => ExecuteRfelCommandAsync(
            "Displaying help...",
            () => new CommandInvocation(
                new List<string> { "help" },
                "Help displayed in log",
                "help command failed"));

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel()
        {
            _operationCts?.Cancel();
        }

        [RelayCommand]
        private void ClearLogs()
        {
            _logMessages.Clear();
            HasLogs = false;
        }

        partial void OnIsBusyChanged(bool value)
        {
            FlashCommand.NotifyCanExecuteChanged();
            CheckDeviceCommand.NotifyCanExecuteChanged();
            ResetDeviceCommand.NotifyCanExecuteChanged();
            Read32Command.NotifyCanExecuteChanged();
            Write32Command.NotifyCanExecuteChanged();
            HexdumpCommand.NotifyCanExecuteChanged();
            DumpCommand.NotifyCanExecuteChanged();
            ReadMemoryCommand.NotifyCanExecuteChanged();
            WriteMemoryCommand.NotifyCanExecuteChanged();
            ExecuteCodeCommand.NotifyCanExecuteChanged();
            ShowSidCommand.NotifyCanExecuteChanged();
            EnableJtagCommand.NotifyCanExecuteChanged();
            RunDdrInitCommand.NotifyCanExecuteChanged();
            ShowHelpCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }

        private bool EnsureExecutableExists()
        {
            if (string.IsNullOrWhiteSpace(RfelPath))
            {
                SetStatus("Please select the rfel executable", RfelLogLevel.Warning);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Warning, StatusMessage));
                return false;
            }

            if (!File.Exists(RfelPath) && !File.Exists(RfelPath + (OperatingSystem.IsWindows() ? ".exe" : string.Empty)))
            {
                SetStatus($"rfel executable not found: {RfelPath}", RfelLogLevel.Error);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Error, StatusMessage));
                return false;
            }

            return true;
        }

        private bool EnsureFirmwareExists()
        {
            if (string.IsNullOrWhiteSpace(FirmwarePath))
            {
                SetStatus("Please select a firmware file", RfelLogLevel.Warning);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Warning, StatusMessage));
                return false;
            }

            if (!File.Exists(FirmwarePath))
            {
                SetStatus($"Firmware file not found: {FirmwarePath}", RfelLogLevel.Error);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Error, StatusMessage));
                return false;
            }

            return true;
        }

        private bool EnsureAddressValid()
        {
            if (string.IsNullOrWhiteSpace(LoadAddress))
            {
                SetStatus("Please enter the load address", RfelLogLevel.Warning);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Warning, StatusMessage));
                return false;
            }

            return true;
        }

        private Task RunOperationAsync(string startMessage, Func<IProgress<RfelLogEvent>, CancellationToken, Task> operation)
        {
            var progress = new Progress<RfelLogEvent>(HandleLogEvent);

            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();

            IsProgressIndeterminate = true;
            ProgressValue = 0;
            SetStatus(startMessage, RfelLogLevel.Info);
            IsBusy = true;

            return ExecuteAsync(operation, progress, _operationCts.Token);
        }

        private Task ExecuteRfelCommandAsync(string startMessage, Func<CommandInvocation?> commandFactory)
        {
            return RunOperationAsync(startMessage, async (progress, token) =>
            {
                if (!EnsureExecutableExists())
                {
                    return;
                }

                var invocation = commandFactory();
                if (invocation is null)
                {
                    return;
                }

                var arguments = RfelCommandPlanner.CreateVerbosityArguments(SelectedVerbosity.Level);
                arguments.AddRange(invocation.Arguments);

                var result = await _rfelCliService.ExecuteAsync(
                    new RfelExecutionRequest(RfelPath, arguments),
                    progress,
                    token).ConfigureAwait(true);

                UpdateStatusFromResult(result, invocation.SuccessMessage, invocation.FailureMessage);
            });
        }

        private string NormalizeInput(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private bool EnsureField(string value, string message)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            SetStatus(message, RfelLogLevel.Warning);
            HandleLogEvent(new RfelLogEvent(RfelLogLevel.Warning, message));
            return false;
        }

        private bool EnsureWritableFilePath(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                return true;
            }
            catch (Exception ex)
            {
                var message = $"Unable to prepare file path '{path}': {ex.Message}";
                SetStatus(message, RfelLogLevel.Error);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Error, message));
                return false;
            }
        }

        private bool EnsureSourceFileExists(string path)
        {
            if (File.Exists(path))
            {
                return true;
            }

            var message = $"Source file not found: {path}";
            SetStatus(message, RfelLogLevel.Error);
            HandleLogEvent(new RfelLogEvent(RfelLogLevel.Error, message));
            return false;
        }

        private sealed record CommandInvocation(List<string> Arguments, string SuccessMessage, string FailureMessage);

        private async Task ExecuteAsync(Func<IProgress<RfelLogEvent>, CancellationToken, Task> operation, IProgress<RfelLogEvent> progress, CancellationToken token)
        {
            try
            {
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Info, StatusMessage));
                await operation(progress, token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Operation cancelled", RfelLogLevel.Warning);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Warning, "Operation cancelled"));
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, RfelLogLevel.Error);
                HandleLogEvent(new RfelLogEvent(RfelLogLevel.Error, ex.Message));
            }
            finally
            {
                _operationCts?.Dispose();
                _operationCts = null;
                IsBusy = false;
            }
        }

        private void UpdateStatusFromResult(RfelExecutionResult result, string successMessage, string failureMessage)
        {
            if (result.Success)
            {
                SetStatus(successMessage, RfelLogLevel.Info);
            }
            else if (result.WasCancelled)
            {
                SetStatus("Operation cancelled", RfelLogLevel.Warning);
            }
            else
            {
                var message = result.Exception?.Message ?? (result.ExitCode.HasValue ? $"{failureMessage} (exit code {result.ExitCode})" : failureMessage);
                SetStatus(message, RfelLogLevel.Error);
            }
        }

        private void HandleLogEvent(RfelLogEvent logEvent)
        {
            var entry = new LogMessage(logEvent.Timestamp, logEvent.Message, logEvent.Level);
            _logMessages.Add(entry);

            if (_logMessages.Count > MaxLogEntries)
            {
                _logMessages.RemoveAt(0);
            }

            HasLogs = _logMessages.Count > 0;
        }

        private void SetStatus(string message, RfelLogLevel level)
        {
            StatusMessage = message;
            StatusLevel = level;
        }

        public sealed record VerbosityOption(int Level, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }
    }
}
