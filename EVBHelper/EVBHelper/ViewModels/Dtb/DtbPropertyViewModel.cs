using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using EVBHelper.Models.Dtb;
using System;
using System.ComponentModel;

namespace EVBHelper.ViewModels.Dtb;

public partial class DtbPropertyViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private PropertyEditorMode _editorMode;
    private string? _validationError;
    private static readonly FontFamily HexFontFamily = new("Consolas");
    private readonly string _modeGroupName = $"dtb-prop-mode-{Guid.NewGuid():N}";

    public event EventHandler? ValueChanged;

    public DtbPropertyViewModel(EditableDtbProperty property, Action markDirty)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));

        _editorMode = property.CanEditAsText ? PropertyEditorMode.Text : PropertyEditorMode.Hex;
        property.PropertyChanged += OnEditablePropertyPropertyChanged;
        property.ValueChanged += OnValueChanged;
    }

    public enum PropertyEditorMode
    {
        Text = 0,
        Hex = 1
    }

    public EditableDtbProperty Property { get; }

    public string Name => Property.Name;

    public string ModeGroupName => _modeGroupName;

    public bool CanEditAsText => Property.CanEditAsText;

    public bool UseHexEditing
    {
        get => EditorMode == PropertyEditorMode.Hex;
        set => EditorMode = value ? PropertyEditorMode.Hex : PropertyEditorMode.Text;
    }

    public PropertyEditorMode EditorMode
    {
        get => _editorMode;
        set
        {
            if (value == _editorMode)
            {
                return;
            }

            if (value == PropertyEditorMode.Text && !Property.CanEditAsText)
            {
                ValidationError = "This property can only be edited in hexadecimal.";
                return;
            }

            SetProperty(ref _editorMode, value);
            ValidationError = null;
            OnPropertyChanged(nameof(IsTextMode));
            OnPropertyChanged(nameof(IsHexMode));
            OnPropertyChanged(nameof(EditorModeIndex));
            OnPropertyChanged(nameof(EditorValue));
            OnPropertyChanged(nameof(EditorFontFamily));
            OnPropertyChanged(nameof(EditorAcceptsReturn));
        }
    }

    public bool IsTextMode
    {
        get => EditorMode == PropertyEditorMode.Text;
        set
        {
            if (value)
            {
                EditorMode = PropertyEditorMode.Text;
            }
        }
    }

    public bool IsHexMode
    {
        get => EditorMode == PropertyEditorMode.Hex;
        set
        {
            if (value)
            {
                EditorMode = PropertyEditorMode.Hex;
            }
        }
    }

    public int EditorModeIndex
    {
        get => (int)EditorMode;
        set
        {
            var requested = value == 0 ? PropertyEditorMode.Text : PropertyEditorMode.Hex;
            EditorMode = requested;
        }
    }

    public string EditorValue
    {
        get => EditorMode == PropertyEditorMode.Hex ? Property.HexValue : Property.TextValue;
        set
        {
            if (EditorMode == PropertyEditorMode.Hex)
            {
                if (Property.TrySetHexValue(value, out var hexError))
                {
                    ValidationError = null;
                    NotifyDirty();
                }
                else
                {
                    ValidationError = hexError;
                }
            }
            else
            {
                if (Property.TrySetTextValue(value, out var textError))
                {
                    ValidationError = null;
                    NotifyDirty();
                }
                else
                {
                    ValidationError = textError;
                }
            }

            OnPropertyChanged(nameof(EditorValue));
        }
    }

    public FontFamily EditorFontFamily => EditorMode == PropertyEditorMode.Hex ? HexFontFamily : FontFamily.Default;

    public bool EditorAcceptsReturn => EditorMode != PropertyEditorMode.Hex;

    public string? ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    public DtbPropertyKind Kind => Property.Kind;

    private void NotifyDirty()
    {
        _markDirty();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EditorValue));
        OnPropertyChanged(nameof(EditorFontFamily));
        OnPropertyChanged(nameof(EditorAcceptsReturn));
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(CanEditAsText));
        if (!Property.CanEditAsText)
        {
            EditorMode = PropertyEditorMode.Hex;
        }
        NotifyDirty();
    }

    private void OnEditablePropertyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditableDtbProperty.Kind))
        {
            OnPropertyChanged(nameof(Kind));
            OnPropertyChanged(nameof(CanEditAsText));
            if (!Property.CanEditAsText)
            {
                EditorMode = PropertyEditorMode.Hex;
            }
        }
        else if (e.PropertyName == nameof(EditableDtbProperty.ValidationError))
        {
            ValidationError = Property.ValidationError;
        }
        else if (e.PropertyName == nameof(EditableDtbProperty.TextValue) ||
                 e.PropertyName == nameof(EditableDtbProperty.HexValue))
        {
            OnPropertyChanged(nameof(EditorValue));
        }
    }
}
