using CommunityToolkit.Mvvm.ComponentModel;
using EVBHelper.Models.Gpt;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EVBHelper.ViewModels.Gpt;

public partial class GptPartitionEntryViewModel : ObservableObject
{
    private readonly EditableGptPartitionEntry _entry;
    private readonly Action _markDirty;
    private readonly Dictionary<string, string> _errors = new();

    public GptPartitionEntryViewModel(int index, EditableGptPartitionEntry entry, Action markDirty)
    {
        Index = index;
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));

        _partitionTypeText = FormatGuid(_entry.PartitionType);
        _partitionGuidText = FormatGuid(_entry.PartitionGuid);
        _firstLbaText = _entry.FirstLba.ToString(CultureInfo.InvariantCulture);
        _lastLbaText = _entry.LastLba.ToString(CultureInfo.InvariantCulture);
        _name = _entry.Name;
        _isRequired = _entry.IsRequired;
        _noDriveLetter = _entry.NoDriveLetter;
        _isHidden = _entry.IsHidden;
        _isShadowCopy = _entry.IsShadowCopy;
        _isReadOnly = _entry.IsReadOnly;
        UpdateUsage();
        UpdateErrorState();
    }

    public int Index { get; }

    public EditableGptPartitionEntry Entry => _entry;

    [ObservableProperty]
    private string _partitionTypeText = string.Empty;

    [ObservableProperty]
    private string _partitionGuidText = string.Empty;

    [ObservableProperty]
    private string _firstLbaText = string.Empty;

    [ObservableProperty]
    private string _lastLbaText = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isRequired;

    [ObservableProperty]
    private bool _noDriveLetter;

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _isShadowCopy;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isUsed;

    partial void OnPartitionTypeTextChanged(string value)
        => TrySetGuid(nameof(PartitionTypeText), value, guid => _entry.PartitionType = guid, "Partition type GUID format is invalid.");

    partial void OnPartitionGuidTextChanged(string value)
        => TrySetGuid(nameof(PartitionGuidText), value, guid => _entry.PartitionGuid = guid, "Partition GUID format is invalid.");

    partial void OnFirstLbaTextChanged(string value)
        => TrySetULong(nameof(FirstLbaText), value, v => _entry.FirstLba = v, "First LBA must be a non-negative integer.");

    partial void OnLastLbaTextChanged(string value)
        => TrySetULong(nameof(LastLbaText), value, v => _entry.LastLba = v, "Last LBA must be a non-negative integer.");

    partial void OnNameChanged(string value)
    {
        if (!_entry.TrySetName(value, out var error))
        {
            SetError(nameof(Name), error ?? "Invalid partition name.");
            return;
        }

        ClearError(nameof(Name));
        _name = _entry.Name;
        OnPropertyChanged(nameof(Name));
        UpdateUsage();
        _markDirty();
    }

    partial void OnIsRequiredChanged(bool value)
    {
        _entry.IsRequired = value;
        _markDirty();
    }

    partial void OnNoDriveLetterChanged(bool value)
    {
        _entry.NoDriveLetter = value;
        _markDirty();
    }

    partial void OnIsHiddenChanged(bool value)
    {
        _entry.IsHidden = value;
        _markDirty();
    }

    partial void OnIsShadowCopyChanged(bool value)
    {
        _entry.IsShadowCopy = value;
        _markDirty();
    }

    partial void OnIsReadOnlyChanged(bool value)
    {
        _entry.IsReadOnly = value;
        _markDirty();
    }

    public void RefreshUsage()
        => UpdateUsage();

    public void Clear()
    {
        _entry.Clear();
        PartitionTypeText = string.Empty;
        PartitionGuidText = string.Empty;
        FirstLbaText = "0";
        LastLbaText = "0";
        Name = string.Empty;
        IsRequired = false;
        NoDriveLetter = false;
        IsHidden = false;
        IsShadowCopy = false;
        IsReadOnly = false;
        UpdateUsage();
        ClearAllErrors();
        _markDirty();
    }

    private void TrySetGuid(string key, string value, Action<Guid> apply, string errorMessage)
    {
        value ??= string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            apply(Guid.Empty);
            ClearError(key);
            UpdateUsage();
            _markDirty();
            return;
        }

        if (!Guid.TryParse(value, out var guid))
        {
            SetError(key, errorMessage);
            return;
        }

        apply(guid);
        ClearError(key);
        UpdateUsage();
        _markDirty();
    }

    private void TrySetULong(string key, string value, Action<ulong> apply, string errorMessage)
    {
        value ??= string.Empty;
        if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            SetError(key, errorMessage);
            return;
        }

        apply(parsed);
        ClearError(key);
        UpdateUsage();
        ValidateLbaRange();
        _markDirty();
    }

    private void UpdateUsage()
    {
        IsUsed = !_entry.IsEmpty;
    }

    private void SetError(string key, string message)
    {
        _errors[key] = message;
        UpdateErrorState();
    }

    private void ClearError(string key)
    {
        if (_errors.Remove(key))
        {
            UpdateErrorState();
        }
    }

    private void ClearAllErrors()
    {
        _errors.Clear();
        UpdateErrorState();
    }

    private void UpdateErrorState()
    {
        HasError = _errors.Count > 0;
        ErrorMessage = _errors.Count > 0
            ? string.Join(Environment.NewLine, _errors.Values)
            : null;
    }

    private void ValidateLbaRange()
    {
        if (!ulong.TryParse(FirstLbaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var first))
        {
            return;
        }

        if (!ulong.TryParse(LastLbaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var last))
        {
            return;
        }

        if (last < first)
        {
            SetError("Range", "Last LBA must be greater than or equal to first LBA.");
        }
        else
        {
            ClearError("Range");
        }
    }

    private static string FormatGuid(Guid guid)
        => guid == Guid.Empty ? string.Empty : guid.ToString();
}
