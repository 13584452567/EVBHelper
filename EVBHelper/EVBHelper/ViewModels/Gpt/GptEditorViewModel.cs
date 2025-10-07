using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EVBHelper.Models.Gpt;
using EVBHelper.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EVBHelper.ViewModels.Gpt;

public partial class GptEditorViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialogService;
    private EditableGptDocument? _document;

    public GptEditorViewModel(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        Tables = new ObservableCollection<GptTableViewModel>();
    StatusMessage = "Select a GPT file.";
    }

    public ObservableCollection<GptTableViewModel> Tables { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _hasDocument;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private GptTableViewModel? _selectedTable;

    [ObservableProperty]
    private GptPartitionEntryViewModel? _selectedPartition;

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var filters = new List<FilePickerFileType>
            {
                new("GPT/Image Files") { Patterns = new[] { "*.gpt", "*.bin", "*.img", "*.raw" } },
                FilePickerFileTypes.All
            };

            var request = new FileDialogRequest
            {
                Title = "Select GPT File",
                Filters = filters
            };

            var path = await _fileDialogService.OpenFileAsync(request).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await LoadDocumentAsync(path).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (_document == null || string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            return;
        }

        await SaveToPathAsync(CurrentFilePath!).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsAsync()
    {
        if (_document == null)
        {
            return;
        }

        var request = new FileDialogRequest
        {
            Title = "Save As",
            SuggestedFileName = Path.GetFileName(CurrentFilePath) ?? "gpt-table.bin",
            Filters = new List<FilePickerFileType>
            {
                new("Binary Files") { Patterns = new[] { "*.bin", "*.gpt" } },
                FilePickerFileTypes.All
            }
        };

        var path = await _fileDialogService.SaveFileAsync(request).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await SaveToPathAsync(path).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanModifyTable))]
    private void AddPartition()
    {
        if (SelectedTable == null)
        {
            return;
        }

        var slot = SelectedTable.FindFirstEmptySlot();
        if (slot == null)
        {
            StatusMessage = "No empty partition slots. Delete a partition first.";
            return;
        }

        slot.PartitionGuidText = Guid.NewGuid().ToString();
        slot.Name = "new-partition";
        slot.FirstLbaText = "0";
        slot.LastLbaText = "0";
        SelectedPartition = slot;
        SelectedTable.MarkDirty();
    StatusMessage = $"Activated partition slot #{slot.Index}.";
        UpdateDirtyState();
    }

    [RelayCommand(CanExecute = nameof(CanRemovePartition))]
    private void RemovePartition()
    {
        if (SelectedTable == null || SelectedPartition == null)
        {
            return;
        }

        SelectedTable.RemovePartition(SelectedPartition);
    StatusMessage = $"Cleared partition slot #{SelectedPartition.Index}.";
        SelectedPartition = null;
        UpdateDirtyState();
    }

    partial void OnSelectedTableChanged(GptTableViewModel? value)
    {
        SelectedPartition = value?.Partitions.FirstOrDefault();
        UpdateErrorState();
        AddPartitionCommand.NotifyCanExecuteChanged();
        RemovePartitionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPartitionChanged(GptPartitionEntryViewModel? value)
    {
        RemovePartitionCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasDocumentChanged(bool value)
    {
        AddPartitionCommand.NotifyCanExecuteChanged();
        RemovePartitionCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    private bool CanSave()
        => HasDocument && IsDirty && !HasErrors && !IsBusy;

    private bool CanModifyTable()
        => HasDocument && SelectedTable != null && !HasErrors;

    private bool CanRemovePartition()
        => SelectedTable != null && SelectedPartition != null;

    private Task LoadDocumentAsync(string path)
    {
        try
        {
            var document = EditableGptDocument.Load(path);
            ResetDocument();
            _document = document;

            if (document.Primary != null)
            {
                Tables.Add(CreateTableViewModel(document.Primary));
            }

            if (document.Secondary != null)
            {
                Tables.Add(CreateTableViewModel(document.Secondary));
            }

            SelectedTable = Tables.FirstOrDefault();
            HasDocument = Tables.Count > 0;
            CurrentFilePath = path;
            _document.ResetDirty();
            UpdateDirtyState();
            UpdateErrorState();
            StatusMessage = $"Loaded {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    private Task SaveToPathAsync(string path)
    {
        if (_document == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _document.Save(path);
            _document.ResetDirty();
            UpdateDirtyState();
            StatusMessage = $"Saved to {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    private void UpdateDirtyState()
    {
        IsDirty = _document?.IsDirty ?? false;
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    private void UpdateErrorState()
    {
        HasErrors = Tables.Any(t => t.HasErrors);
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        AddPartitionCommand.NotifyCanExecuteChanged();
    }

    private void OnTableDirty()
    {
        UpdateDirtyState();
        UpdateErrorState();
    }

    private GptTableViewModel CreateTableViewModel(EditableGptTable table)
    {
        var vm = new GptTableViewModel(table, OnTableDirty);
        vm.PropertyChanged += TableOnPropertyChanged;
        return vm;
    }

    private void TableOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GptTableViewModel.HasErrors) or nameof(GptTableViewModel.IsDirty))
        {
            UpdateDirtyState();
            UpdateErrorState();
        }
    }

    private void ResetDocument()
    {
        foreach (var table in Tables.ToList())
        {
            table.PropertyChanged -= TableOnPropertyChanged;
        }

        Tables.Clear();
        _document = null;
        HasDocument = false;
        CurrentFilePath = null;
        SelectedTable = null;
        SelectedPartition = null;
    StatusMessage = "Select a GPT file.";
        UpdateDirtyState();
        UpdateErrorState();
    }
}
