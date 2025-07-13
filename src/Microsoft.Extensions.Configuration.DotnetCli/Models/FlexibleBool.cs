using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.Configuration.DotnetCli;

public struct FlexibleBool
{
    private string _originalValue;
    private readonly bool _value;

    public FlexibleBool(bool value)
    {
        _originalValue = value.ToString();
        _value = value;
    }
    public FlexibleBool(int value)
    {
        _originalValue = value.ToString();
        _value = value != 0;
    }

    public FlexibleBool(string value)
    {
        _originalValue = value;
        if (bool.TryParse(value, out var result))
        {
            _value = result;
        }
        else if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            _value = true;
        }
        else if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            _value = false;
        }
        else if (int.TryParse(value, out var intValue))
        {
            _value = intValue != 0; // Treat non-zero as true, zero as false
        }
        else
        {
            throw new ArgumentException($"Invalid boolean value: {value}");
        }
    }

    public override string ToString()
    {
        return _originalValue.ToString();
    }

    public static implicit operator bool(FlexibleBool flexibleBool) => flexibleBool._value;
    public static implicit operator FlexibleBool(bool value) => new(value);
    public static implicit operator FlexibleBool(int value) => new(value);
    public static implicit operator FlexibleBool(string value) => new(value);
    public static bool operator true(FlexibleBool myBoolString) => (bool)myBoolString;
    public static bool operator false(FlexibleBool myBoolString) => !(bool)myBoolString;

    public override bool Equals([NotNullWhen(true)] object? obj) => base.Equals(obj) && obj is FlexibleBool other && _value == other._value;
    public override int GetHashCode() => _originalValue.GetHashCode();

}
