using System;
using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using EVBHelper.Models.Dtb;

namespace EVBHelper.ViewModels.Dtb;

public partial class DtbPropertyViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private bool _useHexEditing;
    private string? _validationError;
    private static readonly FontFamily HexFontFamily = new("Consolas");

    public event EventHandler? ValueChanged;

    public DtbPropertyViewModel(EditableDtbProperty property, Action markDirty)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));

        _useHexEditing = !property.CanEditAsText;
        property.PropertyChanged += OnEditablePropertyPropertyChanged;
        property.ValueChanged += OnValueChanged;
    }

    public EditableDtbProperty Property { get; }

    public string Name => Property.Name;

    public bool CanEditAsText => Property.CanEditAsText;

    public bool UseHexEditing
    {
        get => _useHexEditing;
        set
        {
            if (value == _useHexEditing)
            {
                return;
            }

            if (!value && !Property.CanEditAsText)
            {
                ValidationError = "This property can only be edited in hexadecimal.";
                return;
            }

            SetProperty(ref _useHexEditing, value);
            ValidationError = null;
            OnPropertyChanged(nameof(EditorValue));
            OnPropertyChanged(nameof(EditorFontFamily));
            OnPropertyChanged(nameof(EditorAcceptsReturn));
        }
    }

    public string EditorValue
    {
        get => UseHexEditing ? Property.HexValue : Property.TextValue;
        set
        {
            if (UseHexEditing)
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

    public FontFamily EditorFontFamily => UseHexEditing ? HexFontFamily : FontFamily.Default;

    public bool EditorAcceptsReturn => !UseHexEditing;

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
            UseHexEditing = true;
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
                UseHexEditing = true;
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
