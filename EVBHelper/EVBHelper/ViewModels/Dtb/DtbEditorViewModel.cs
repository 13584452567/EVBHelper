using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EVBHelper.Models.Dtb;
using EVBHelper.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EVBHelper.ViewModels.Dtb;

public partial class DtbEditorViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialogService;
    private EditableDtb? _document;

    public DtbEditorViewModel(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        Nodes = new ObservableCollection<DtbNodeViewModel>();
        StatusMessage = "Select a DTB file";
    }

    public ObservableCollection<DtbNodeViewModel> Nodes { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private DtbNodeViewModel? _selectedNode;

    [ObservableProperty]
    private DtbPropertyViewModel? _selectedProperty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _hasDocument;

    [ObservableProperty]
    private string _newPropertyName = string.Empty;

    [ObservableProperty]
    private string _newPropertyValue = string.Empty;

    [ObservableProperty]
    private bool _newPropertyUseHex;

    [ObservableProperty]
    private string _newNodeName = string.Empty;

    public int NewPropertyModeIndex
    {
        get => NewPropertyUseHex ? 1 : 0;
        set => NewPropertyUseHex = value == 1;
    }

    private static readonly FontFamily HexEditorFont = new("Consolas");

    public FontFamily NewPropertyFontFamily => NewPropertyUseHex ? HexEditorFont : FontFamily.Default;

    public bool NewPropertyAcceptsReturn => !NewPropertyUseHex;

    public string NewPropertyWatermark => NewPropertyUseHex ? "Hex bytes (e.g. 00 FF AA)" : "Property value";

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
            var request = new FileDialogRequest
            {
                Title = "Select DTB file",
                Filters = new List<FilePickerFileType>
                {
                    new("Device tree binaries") { Patterns = new[] { "*.dtb", "*.bin" } },
                    FilePickerFileTypes.All
                }
            };

            string? path = await _fileDialogService.OpenFileAsync(request).ConfigureAwait(true);
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

    [RelayCommand(CanExecute = nameof(CanSaveCommand))]
    private async Task SaveAsync()
    {
        if (_document == null)
        {
            return;
        }

        string? targetPath = CurrentFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            await SaveAsAsync().ConfigureAwait(true);
            return;
        }

        await SaveToFileAsync(targetPath).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanSaveCommand))]
    private async Task SaveAsAsync()
    {
        if (_document == null)
        {
            return;
        }

        var request = new FileDialogRequest
        {
            Title = "Save DTB As",
            SuggestedFileName = Path.GetFileName(CurrentFilePath) ?? "output.dtb",
            DefaultExtension = ".dtb",
            Filters = new List<FilePickerFileType>
            {
                new("DTB files") { Patterns = new[] { "*.dtb" } },
                FilePickerFileTypes.All
            }
        };

        string? path = await _fileDialogService.SaveFileAsync(request).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await SaveToFileAsync(path).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanModifySelection))]
    private void AddProperty()
    {
        if (_document == null || SelectedNode == null)
        {
            return;
        }

        string name = (NewPropertyName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusMessage = "Property name cannot be empty";
            return;
        }

        if (SelectedNode.Node.Properties.Any(p => p.Name == name))
        {
            StatusMessage = "A property with the same name already exists in this node";
            return;
        }

    byte[] initialValue = NewPropertyUseHex ? Array.Empty<byte>() : new byte[] { 0 };
    var property = new EditableDtbProperty(name, initialValue);
        _document.TrackProperty(property);

        if (NewPropertyUseHex)
        {
            if (!property.TrySetHexValue(NewPropertyValue, out var error))
            {
                StatusMessage = error ?? "Unable to parse hexadecimal value";
                return;
            }
        }
        else
        {
            property.TrySetTextValue(NewPropertyValue, out _);
        }

        var vm = SelectedNode.AddProperty(name, property);
        vm.UseHexEditing = NewPropertyUseHex;
        SelectedProperty = vm;
        _document.MarkDirty();
        UpdateDirtyState();
        StatusMessage = $"Added property {name}";
        NewPropertyName = string.Empty;
        NewPropertyValue = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveProperty))]
    private void RemoveProperty()
    {
        if (_document == null || SelectedNode == null || SelectedProperty == null)
        {
            return;
        }

        string name = SelectedProperty.Name;
        SelectedNode.RemoveProperty(SelectedProperty);
        _document.MarkDirty();
        UpdateDirtyState();
        StatusMessage = $"Removed property {name}";
    }

    [RelayCommand(CanExecute = nameof(CanModifySelection))]
    private void AddNode()
    {
        if (_document == null || SelectedNode == null)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(NewNodeName) ? "new-node" : NewNodeName.Trim();
        var child = SelectedNode.AddChild(name);
        _document.TrackNode(child.Node);
        SelectedNode = child;
        _document.MarkDirty();
        UpdateDirtyState();
        StatusMessage = $"Added child node {name}";
        NewNodeName = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveNode))]
    private void RemoveNode()
    {
        if (_document == null || SelectedNode == null)
        {
            return;
        }

        var parent = FindParent(SelectedNode);
        if (parent == null)
        {
            StatusMessage = "Cannot delete the root node";
            return;
        }

        parent.RemoveChild(SelectedNode);
        SelectedNode = parent;
        _document.MarkDirty();
        UpdateDirtyState();
        StatusMessage = "Node deleted";
    }

    private bool CanSaveCommand() => HasDocument && !IsBusy;

    private bool CanModifySelection() => HasDocument && SelectedNode != null;

    private bool CanRemoveProperty() => CanModifySelection() && SelectedProperty != null;

    private bool CanRemoveNode() => CanModifySelection() && SelectedNode?.Node.Parent != null;

    private async Task LoadDocumentAsync(string path)
    {
        EditableDtb? newDocument = null;

        try
        {
            byte[] data = await File.ReadAllBytesAsync(path).ConfigureAwait(true);
            newDocument = EditableDtb.Load(data, path);

            foreach (var node in newDocument.EnumerateNodes())
            {
                newDocument.TrackNode(node);
            }

            newDocument.DirtyChanged += DocumentOnDirtyChanged;

            DisposeDocument();

            _document = newDocument;
            newDocument = null;

            Nodes.Clear();
            var rootVm = CreateNodeViewModel(_document.Root);
            Nodes.Add(rootVm);
            SelectedNode = rootVm;

            CurrentFilePath = path;
            HasDocument = true;
            _document.ResetDirty();
            UpdateDirtyState();
            StatusMessage = $"Loaded {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            var message = BuildLoadErrorMessage(path, ex);
            StatusMessage = message;
            Debug.WriteLine($"[DTB] {message}{Environment.NewLine}{ex}");
            UpdateDirtyState();
        }
        finally
        {
            if (newDocument != null)
            {
                newDocument.DirtyChanged -= DocumentOnDirtyChanged;
            }
        }
    }

    private async Task SaveToFileAsync(string path)
    {
        if (_document == null)
        {
            return;
        }

        byte[] data = _document.ToBinary();
        await File.WriteAllBytesAsync(path, data).ConfigureAwait(true);
        _document.ResetDirty();
        _document.UpdateSourcePath(path);
        UpdateDirtyState();
        CurrentFilePath = path;
        StatusMessage = $"Saved to {Path.GetFileName(path)}";
    }

    private void UpdateDirtyState()
    {
        IsDirty = _document?.IsDirty ?? false;
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    private DtbNodeViewModel? FindParent(DtbNodeViewModel node)
    {
        return Nodes.SelectMany(Flatten)
            .FirstOrDefault(n => n.Children.Contains(node));
    }

    private static IEnumerable<DtbNodeViewModel> Flatten(DtbNodeViewModel node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private DtbNodeViewModel CreateNodeViewModel(EditableDtbNode node)
    {
        var vm = new DtbNodeViewModel(node, MarkDirty);
        vm.ValueChanged += (_, _) => MarkDirty();

        foreach (var propertyVm in vm.Properties)
        {
            propertyVm.ValueChanged += (_, _) => MarkDirty();
            _document?.TrackProperty(propertyVm.Property);
        }

        foreach (var child in vm.Children)
        {
            child.ValueChanged += (_, _) => MarkDirty();
        }

        return vm;
    }

    private void MarkDirty()
    {
        _document?.MarkDirty();
        UpdateDirtyState();
    }

    private void DisposeDocument()
    {
        if (_document == null)
        {
            return;
        }

        _document.DirtyChanged -= DocumentOnDirtyChanged;
        Nodes.Clear();
        _document = null;
        HasDocument = false;
        SelectedNode = null;
        SelectedProperty = null;
        CurrentFilePath = null;
        StatusMessage = "Select a DTB file";
        UpdateDirtyState();
    }

    private void DocumentOnDirtyChanged(object? sender, EventArgs e) => UpdateDirtyState();

    partial void OnHasDocumentChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        AddPropertyCommand.NotifyCanExecuteChanged();
        AddNodeCommand.NotifyCanExecuteChanged();
        RemoveNodeCommand.NotifyCanExecuteChanged();
        RemovePropertyCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPropertyChanged(DtbPropertyViewModel? oldValue, DtbPropertyViewModel? newValue)
    {
        RemovePropertyCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedNodeChanged(DtbNodeViewModel? oldValue, DtbNodeViewModel? newValue)
    {
        SelectedProperty = newValue?.Properties.FirstOrDefault();
        AddPropertyCommand.NotifyCanExecuteChanged();
        AddNodeCommand.NotifyCanExecuteChanged();
        RemoveNodeCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewPropertyUseHexChanged(bool value)
    {
        OnPropertyChanged(nameof(NewPropertyModeIndex));
        OnPropertyChanged(nameof(NewPropertyFontFamily));
        OnPropertyChanged(nameof(NewPropertyAcceptsReturn));
        OnPropertyChanged(nameof(NewPropertyWatermark));
    }

    private static string BuildLoadErrorMessage(string path, Exception ex)
    {
        var fileName = Path.GetFileName(path ?? string.Empty);
        var displayName = string.IsNullOrWhiteSpace(fileName) ? "DTB file" : fileName;

        return ex switch
        {
            FileNotFoundException => $"File '{displayName}' was not found.",
            UnauthorizedAccessException => $"Access to '{displayName}' was denied.",
            InvalidDataException => string.IsNullOrWhiteSpace(ex.Message) ? $"The selected file is not a valid DTB binary." : ex.Message,
            IOException ioEx => $"I/O error while loading '{displayName}': {ioEx.Message}",
            _ => $"Failed to load {displayName}: {ex.Message}"
        };
    }
}
