using CommunityToolkit.Mvvm.ComponentModel;
using EVBHelper.Models.Gpt;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace EVBHelper.ViewModels.Gpt;

public partial class GptTableViewModel : ObservableObject
{
    private readonly EditableGptTable _table;
    private readonly Action _markDirty;

    public GptTableViewModel(EditableGptTable table, Action markDirty)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));

        Partitions = new ObservableCollection<GptPartitionEntryViewModel>(
            table.Partitions.Select((entry, index) => CreatePartition(index, entry)));

        UpdateErrors();
        UpdateUsageCount();
    }

    public string Title => _table.DisplayName;

    public ObservableCollection<GptPartitionEntryViewModel> Partitions { get; }

    public Guid DiskGuid => _table.DiskGuid;

    public ulong FirstUsableLba => _table.FirstUsableLba;

    public ulong LastUsableLba => _table.LastUsableLba;

    public ulong PartitionsArrayLba => _table.PartitionsArrayLba;

    public uint PartitionEntryLength => _table.PartitionEntryLength;

    public uint PartitionCount => _table.PartitionsCount;

    public bool HeaderChecksumValid => _table.HeaderChecksumValid;

    public bool PartitionArrayChecksumValid => _table.PartitionArrayChecksumValid;

    public bool IsDirty => _table.IsDirty;

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private int _usedPartitionCount;

    public GptPartitionEntryViewModel? FindFirstEmptySlot()
        => Partitions.FirstOrDefault(p => !p.IsUsed);

    public void RemovePartition(GptPartitionEntryViewModel entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.Clear();
        _table.IsDirty = true;
        UpdateUsageCount();
        UpdateErrors();
        OnPropertyChanged(nameof(IsDirty));
        _markDirty();
    }

    public void MarkDirty()
    {
        _table.IsDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        _markDirty();
    }

    private GptPartitionEntryViewModel CreatePartition(int index, EditableGptPartitionEntry entry)
    {
        void OnEntryChanged() => OnPartitionChanged();
        var viewModel = new GptPartitionEntryViewModel(index + 1, entry, OnEntryChanged);
        viewModel.PropertyChanged += PartitionOnPropertyChanged;
        return viewModel;
    }

    private void PartitionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GptPartitionEntryViewModel)
        {
            return;
        }

        if (e.PropertyName is nameof(GptPartitionEntryViewModel.HasError)
            or nameof(GptPartitionEntryViewModel.IsUsed))
        {
            UpdateErrors();
            UpdateUsageCount();
        }
    }

    private void OnPartitionChanged()
    {
        _table.IsDirty = true;
        UpdateErrors();
        UpdateUsageCount();
        OnPropertyChanged(nameof(IsDirty));
        _markDirty();
    }

    private void UpdateErrors()
    {
        HasErrors = Partitions.Any(p => p.HasError);
    }

    private void UpdateUsageCount()
    {
        UsedPartitionCount = Partitions.Count(p => p.IsUsed);
    }
}
