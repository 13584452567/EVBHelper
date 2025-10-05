using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DeviceTreeNode.Nodes;

namespace EVBHelper.Models.Dtb;

public class EditableDtb
{
    private bool _isDirty;

    private EditableDtb(EditableDtbNode root, IReadOnlyList<MemoryReservation> reservations, string? sourcePath)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        MemoryReservations = new ObservableCollection<MemoryReservation>(reservations ?? Array.Empty<MemoryReservation>());
        SourcePath = sourcePath;

        AttachDirtyTracking(root);
    }

    public event EventHandler? DirtyChanged;

    public EditableDtbNode Root { get; }

    public ObservableCollection<MemoryReservation> MemoryReservations { get; }

    public string? SourcePath { get; private set; }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                DirtyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public static EditableDtb Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty", nameof(path));
        }

        byte[] data = File.ReadAllBytes(path);
        return Load(data, path);
    }

    public static EditableDtb Load(byte[] data, string? sourcePath = null)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("DTB data cannot be empty", nameof(data));
        }

        var fdt = new global::Fdt(data);
        EditableDtbNode root = ConvertNode(fdt.Root.Node, null);
        List<MemoryReservation> reservations = fdt.MemoryReservations?.ToList() ?? new List<MemoryReservation>();

        return new EditableDtb(root, reservations, sourcePath);
    }

    public byte[] ToBinary() => EditableDtbSerializer.Serialize(this);

    public void MarkDirty() => IsDirty = true;

    public void ResetDirty() => IsDirty = false;

    public void UpdateSourcePath(string? path) => SourcePath = path;

    public void TrackNode(EditableDtbNode node) => AttachDirtyTracking(node);

    public void TrackProperty(EditableDtbProperty property)
    {
        if (property == null)
        {
            return;
        }

        property.ValueChanged -= PropertyOnValueChanged;
        property.ValueChanged += PropertyOnValueChanged;
    }

    public IEnumerable<EditableDtbNode> EnumerateNodes() => EnumerateNodes(Root);

    private void AttachDirtyTracking(EditableDtbNode node)
    {
        foreach (var n in EnumerateNodes(node))
        {
            foreach (var prop in n.Properties)
            {
                prop.ValueChanged -= PropertyOnValueChanged;
                prop.ValueChanged += PropertyOnValueChanged;
            }
        }
    }

    private void PropertyOnValueChanged(object? sender, EventArgs e) => MarkDirty();

    private static EditableDtbNode ConvertNode(DeviceTreeNode.Nodes.FdtNode source, EditableDtbNode? parent)
    {
        var node = new EditableDtbNode(source?.Name, parent);

        if (source != null)
        {
            foreach (var prop in source.Properties())
            {
                byte[] copy = new byte[prop.Value.Length];
                Array.Copy(prop.Value, copy, copy.Length);
                node.Properties.Add(new EditableDtbProperty(prop.Name, copy));
            }

            foreach (var child in source.Children())
            {
                var childNode = ConvertNode(child, node);
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    private IEnumerable<EditableDtbNode> EnumerateNodes(EditableDtbNode? start)
    {
        if (start == null)
        {
            yield break;
        }

        Queue<EditableDtbNode> queue = new();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
    }
}
