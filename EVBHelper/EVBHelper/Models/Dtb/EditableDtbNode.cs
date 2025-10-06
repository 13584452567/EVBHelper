using System.Collections.ObjectModel;

namespace EVBHelper.Models.Dtb;

public class EditableDtbNode
{
    public EditableDtbNode(string? name, EditableDtbNode? parent = null)
    {
        Name = string.IsNullOrEmpty(name) ? string.Empty : name;
        Parent = parent;
        Properties = new ObservableCollection<EditableDtbProperty>();
        Children = new ObservableCollection<EditableDtbNode>();
    }

    public string Name { get; set; }

    public EditableDtbNode? Parent { get; private set; }

    public ObservableCollection<EditableDtbProperty> Properties { get; }

    public ObservableCollection<EditableDtbNode> Children { get; }

    public string DisplayName => string.IsNullOrEmpty(Name) ? "/" : Name;

    public EditableDtbNode AddChild(string name)
    {
        var child = new EditableDtbNode(name, this);
        Children.Add(child);
        return child;
    }

    public void AttachChild(EditableDtbNode child)
    {
        if (child == null)
        {
            return;
        }

        child.Parent = this;
        Children.Add(child);
    }

    public void RemoveChild(EditableDtbNode child)
    {
        if (child == null)
        {
            return;
        }

        if (Children.Remove(child))
        {
            child.Parent = null;
        }
    }
}
