using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EVBHelper.Models;
using EVBHelper.Services;
using OpenixCard.Logging;
using OpenixCard.Models;

namespace EVBHelper.ViewModels.OpenixCard;

public sealed partial class OpenixCardViewModel : ViewModelBase
{
	private const int MaxLogEntries = 500;

	private readonly IOpenixCardClientService _openixService;
	private readonly IFileDialogService _fileDialogService;
	private readonly ObservableCollection<OpenixLogEntry> _logEntries = new();
	private readonly ReadOnlyObservableCollection<OpenixLogEntry> _readonlyLogs;

	private CancellationTokenSource? _operationCts;

	[ObservableProperty]
	private string _inputImagePath = string.Empty;

	[ObservableProperty]
	private string _packDirectory = string.Empty;

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
	private string? _sizeSummary;

	public OpenixCardViewModel(IOpenixCardClientService openixService, IFileDialogService fileDialogService)
	{
		_openixService = openixService ?? throw new ArgumentNullException(nameof(openixService));
		_fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
		_readonlyLogs = new ReadOnlyObservableCollection<OpenixLogEntry>(_logEntries);
	}

	public ReadOnlyObservableCollection<OpenixLogEntry> Logs => _readonlyLogs;

	private bool CanExecuteOperations() => !IsBusy;

	private bool CanCancel() => IsBusy;

	[RelayCommand(CanExecute = nameof(CanExecuteOperations))]
	private async Task BrowseImageAsync()
	{
		var filters = new List<FilePickerFileType>
		{
			new("Image Files") { Patterns = new[] { "*.img", "*.bin", "*.fw", "*.fex" } },
			FilePickerFileTypes.All
		};

		var request = new FileDialogRequest
		{
			Title = "Select OpenixCard image file",
			Filters = filters,
			AllowMultiple = false
		};

		var result = await _fileDialogService.OpenFileAsync(request).ConfigureAwait(true);
		if (!string.IsNullOrWhiteSpace(result))
		{
			InputImagePath = result;
		}
	}

	[RelayCommand(CanExecute = nameof(CanExecuteOperations))]
	private async Task BrowsePackDirectoryAsync()
	{
		var request = new FolderDialogRequest
		{
			Title = "Choose extraction directory"
		};

		var result = await _fileDialogService.OpenFolderAsync(request).ConfigureAwait(true);
		if (!string.IsNullOrWhiteSpace(result))
		{
			PackDirectory = result;
		}
	}

	[RelayCommand(CanExecute = nameof(CanExecuteOperations))]
	private Task UnpackAsync()
	{
		SizeSummary = null;
		return RunOperationAsync(
		startMessage: "Unpacking image...",
		operation: async (progress, token) =>
		{
			EnsureFileExists(InputImagePath, "Please select a valid image file path");
			return await _openixService.UnpackAsync(InputImagePath, GenerateCfg, progress, token).ConfigureAwait(true);
		},
		onSuccess: result =>
		{
			var successMessage = $"Unpack completed: {result.OutputDirectory}";
			SetStatus(successMessage, OpenixLogLevel.Info);
			if (!string.IsNullOrWhiteSpace(result.GeneratedConfigPath))
			{
				HandleLog(OpenixLogMessage.Info($"Generated config file: {result.GeneratedConfigPath}"));
			}
		});
	}

	[RelayCommand(CanExecute = nameof(CanExecuteOperations))]
	private Task DumpAsync()
	{
		SizeSummary = null;
		return RunOperationAsync(
		startMessage: "Converting image...",
		operation: async (progress, token) =>
		{
			EnsureFileExists(InputImagePath, "Please select a valid image file path");
			return await _openixService.DumpAsync(InputImagePath, progress, token).ConfigureAwait(true);
		},
		onSuccess: result =>
		{
			var successMessage = $"Conversion completed: {result.OutputImagePath}";
			SetStatus(successMessage, OpenixLogLevel.Info);
		});
	}

	[RelayCommand(CanExecute = nameof(CanExecuteOperations))]
	private Task PackAsync()
	{
		SizeSummary = null;
		return RunOperationAsync(
		startMessage: "Packing directory...",
		operation: async (progress, token) =>
		{
			EnsureDirectoryExists(PackDirectory, "Please select a valid directory to pack");
			return await _openixService.PackAsync(PackDirectory, progress, token).ConfigureAwait(true);
		},
		onSuccess: result =>
		{
			var successMessage = $"Pack completed: {result.OutputImagePath}";
			SetStatus(successMessage, OpenixLogLevel.Info);
		});
	}

	[RelayCommand(CanExecute = nameof(CanExecuteOperations))]
	private Task ReportSizeAsync()
	{
		SizeSummary = null;
		return RunOperationAsync(
		startMessage: "Calculating image size...",
		operation: async (progress, token) =>
		{
			EnsureFileExists(InputImagePath, "Please select a valid image file path");
			return await _openixService.ReportSizeAsync(InputImagePath, progress, token).ConfigureAwait(true);
		},
		onSuccess: info =>
		{
			var summary = $"Size: {info.SizeInMegabytes:F2} MB ({info.SizeInKilobytes:F0} KB)";
			SizeSummary = summary;
			SetStatus("Calculation completed", OpenixLogLevel.Info);
		});
	}

	[RelayCommand(CanExecute = nameof(CanCancel))]
	private void Cancel()
	{
		_operationCts?.Cancel();
	}

	[RelayCommand]
	private void ClearLogs()
	{
		_logEntries.Clear();
		HasLogs = false;
	}

	private async Task<TResult?> RunOperationAsync<TResult>(
		string startMessage,
		Func<IProgress<OpenixLogMessage>, CancellationToken, Task<TResult>> operation,
		Action<TResult> onSuccess)
	{
		if (IsBusy)
		{
			return default;
		}

		IsBusy = true;
		_operationCts = new CancellationTokenSource();

		SetStatus(startMessage, OpenixLogLevel.Info);

		var progress = new Progress<OpenixLogMessage>(HandleLog);

		try
		{
			var result = await Task.Run(() => operation(progress, _operationCts.Token), _operationCts.Token).ConfigureAwait(true);
			onSuccess(result);
			return result;
		}
		catch (OperationCanceledException)
		{
			SetStatus("Operation cancelled", OpenixLogLevel.Warning);
			return default;
		}
		catch (Exception ex)
		{
			HandleLog(OpenixLogMessage.Debug(ex.ToString()));
			SetStatus(ex.Message, OpenixLogLevel.Error);
			return default;
		}
		finally
		{
			_operationCts.Dispose();
			_operationCts = null;
			IsBusy = false;
		}
	}

	private static void EnsureFileExists(string path, string errorMessage)
	{
		if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
		{
			throw new InvalidOperationException(errorMessage);
		}
	}

	private static void EnsureDirectoryExists(string path, string errorMessage)
	{
		if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
		{
			throw new InvalidOperationException(errorMessage);
		}
	}

	private void HandleLog(OpenixLogMessage message)
	{
		var entry = new OpenixLogEntry(DateTimeOffset.Now, message.Message, message.Level);
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
		HandleLog(new OpenixLogMessage(level, message));
	}

	partial void OnIsBusyChanged(bool value)
	{
		UnpackCommand.NotifyCanExecuteChanged();
		DumpCommand.NotifyCanExecuteChanged();
		PackCommand.NotifyCanExecuteChanged();
		ReportSizeCommand.NotifyCanExecuteChanged();
		BrowseImageCommand.NotifyCanExecuteChanged();
		BrowsePackDirectoryCommand.NotifyCanExecuteChanged();
		CancelCommand.NotifyCanExecuteChanged();
	}
}
