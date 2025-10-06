using CommunityToolkit.Mvvm.ComponentModel;
using EVBHelper.Models.Dtb;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace EVBHelper.ViewModels.Dtb;

public partial class DtbNodeViewModel : ObservableObject
{
    private readonly Action _markDirty;

    public DtbNodeViewModel(EditableDtbNode node, Action markDirty)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));

        Properties = new ObservableCollection<DtbPropertyViewModel>(
            node.Properties.Select(CreatePropertyViewModel));

        Children = new ObservableCollection<DtbNodeViewModel>(
            node.Children.Select(child => new DtbNodeViewModel(child, markDirty)));

        foreach (var child in Children)
        {
            child.ValueChanged += ChildOnValueChanged;
        }
    }

    public EditableDtbNode Node { get; }

    public ObservableCollection<DtbPropertyViewModel> Properties { get; }

    public ObservableCollection<DtbNodeViewModel> Children { get; }

    public event EventHandler? ValueChanged;

    public string Name
    {
        get => Node.Name;
        set
        {
            string normalized = value ?? string.Empty;
            if (normalized == Node.Name)
            {
                return;
            }

            Node.Name = normalized;
            OnPropertyChanged();
            RaiseValueChanged();
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "/" : Name;

    public bool HasChildren => Children.Count > 0;

    public bool HasProperties => Properties.Count > 0;

    public DtbPropertyViewModel AddProperty(string name, EditableDtbProperty property)
    {
        Node.Properties.Add(property);
        var vm = CreatePropertyViewModel(property);
        Properties.Add(vm);
        RaiseValueChanged();
        return vm;
    }

    public void RemoveProperty(DtbPropertyViewModel property)
    {
        if (property == null)
        {
            return;
        }

        if (Node.Properties.Remove(property.Property))
        {
            property.ValueChanged -= PropertyOnValueChanged;
            Properties.Remove(property);
            RaiseValueChanged();
        }
    }

    public DtbNodeViewModel AddChild(string name)
    {
        var childNode = Node.AddChild(name);
        var vm = new DtbNodeViewModel(childNode, _markDirty);
        vm.ValueChanged += ChildOnValueChanged;
        Children.Add(vm);
        RaiseValueChanged();
        return vm;
    }

    public void RemoveChild(DtbNodeViewModel child)
    {
        if (child == null)
        {
            return;
        }

        Node.RemoveChild(child.Node);
        child.ValueChanged -= ChildOnValueChanged;
        Children.Remove(child);
        RaiseValueChanged();
    }

    private DtbPropertyViewModel CreatePropertyViewModel(EditableDtbProperty property)
    {
        var vm = new DtbPropertyViewModel(property, RaiseValueChanged);
        vm.ValueChanged += PropertyOnValueChanged;
        return vm;
    }

    private void ChildOnValueChanged(object? sender, EventArgs e) => RaiseValueChanged();

    private void PropertyOnValueChanged(object? sender, EventArgs e) => RaiseValueChanged();

    private void RaiseValueChanged()
    {
        _markDirty();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(HasProperties));
        OnPropertyChanged(nameof(HasChildren));
    }
}
