using System;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EVBHelper.Models.Dtb;

public enum DtbPropertyKind
{
    Binary,
    String,
    StringList
}

public partial class EditableDtbProperty : ObservableObject
{
    private byte[] _value;
    private DtbPropertyKind _kind;
    private string? _validationError;

    public event EventHandler? ValueChanged;

    public EditableDtbProperty(string name, byte[]? value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _value = value ?? Array.Empty<byte>();
        _kind = DetermineKind(_value);
    }

    public string Name { get; }

    public DtbPropertyKind Kind
    {
        get => _kind;
        private set
        {
            if (SetProperty(ref _kind, value))
            {
                OnPropertyChanged(nameof(CanEditAsText));
            }
        }
    }

    public string? ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    public bool CanEditAsText => Kind is not DtbPropertyKind.Binary;

    public bool CanEditAsHex => true;

    public string TextValue => Kind switch
    {
        DtbPropertyKind.Binary => string.Empty,
        DtbPropertyKind.String => DecodeSingleString(),
        DtbPropertyKind.StringList => string.Join(Environment.NewLine, DecodeStringList()),
        _ => string.Empty
    };

    public string HexValue => FormatHex(_value);

    public byte[] GetValueCopy()
    {
        byte[] copy = new byte[_value.Length];
        Array.Copy(_value, copy, _value.Length);
        return copy;
    }

    public bool TrySetTextValue(string? text, out string? error)
    {
        if (!CanEditAsText)
        {
            error = "This property can only be edited in hexadecimal.";
            return false;
        }

        string normalized = text ?? string.Empty;
        byte[] encoded = Kind == DtbPropertyKind.StringList
            ? EncodeStringList(normalized)
            : EncodeSingleString(normalized);

        SetValue(encoded);
        error = null;
        return true;
    }

    public bool TrySetHexValue(string? hex, out string? error)
    {
        try
        {
            byte[] data = ParseHex(hex);
            SetValue(data);
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            ValidationError = error;
            return false;
        }
    }

    private void SetValue(byte[] newValue)
    {
        _value = newValue ?? Array.Empty<byte>();
        ValidationError = null;
        Kind = DetermineKind(_value);
        OnPropertyChanged(nameof(TextValue));
        OnPropertyChanged(nameof(HexValue));
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static DtbPropertyKind DetermineKind(byte[] value)
    {
        if (value.Length == 0)
        {
            return DtbPropertyKind.Binary;
        }

        if (value[^1] != 0)
        {
            return DtbPropertyKind.Binary;
        }

        string decoded = Encoding.UTF8.GetString(value, 0, value.Length - 1);
        string[] segments = decoded.Split('\0');

        if (segments.Any(segment => !IsPrintable(segment)))
        {
            return DtbPropertyKind.Binary;
        }

        return segments.Length > 1 ? DtbPropertyKind.StringList : DtbPropertyKind.String;
    }

    private static bool IsPrintable(string text)
    {
        foreach (char c in text)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    private string DecodeSingleString()
    {
        if (_value.Length == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(_value, 0, _value.Length - 1);
    }

    private string[] DecodeStringList()
    {
        if (_value.Length == 0)
        {
            return Array.Empty<string>();
        }

        string decoded = Encoding.UTF8.GetString(_value, 0, _value.Length - 1);
        return decoded.Split('\0');
    }

    private static byte[] EncodeSingleString(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        byte[] withNull = new byte[bytes.Length + 1];
        Array.Copy(bytes, withNull, bytes.Length);
        withNull[^1] = 0;
        return withNull;
    }

    private static byte[] EncodeStringList(string value)
    {
        string normalized = value.Replace("\r\n", "\n");
        string[] parts = normalized.Split('\n');
        using MemoryStream stream = new();

        foreach (string part in parts)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(part);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
        }

        if (stream.Length == 0)
        {
            stream.WriteByte(0);
        }

        return stream.ToArray();
    }

    private static byte[] ParseHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<byte>();
        }

        StringBuilder builder = new();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == '-' || c == ',')
            {
                i++;
                continue;
            }

            if (c == '0' && i + 1 < text.Length && (text[i + 1] == 'x' || text[i + 1] == 'X'))
            {
                i += 2;
                continue;
            }

            if (Uri.IsHexDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
                i++;
                continue;
            }

            throw new FormatException($"Invalid hexadecimal character detected: '{c}'.");
        }

        if (builder.Length % 2 != 0)
        {
            throw new FormatException("Hexadecimal character count must be even.");
        }

        string sanitized = builder.ToString();
        byte[] result = new byte[sanitized.Length / 2];
        for (int j = 0; j < result.Length; j++)
        {
            string byteStr = sanitized.Substring(j * 2, 2);
            result[j] = Convert.ToByte(byteStr, 16);
        }

        return result;
    }

    private static string FormatHex(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        return BitConverter.ToString(data).Replace('-', ' ');
    }
}
