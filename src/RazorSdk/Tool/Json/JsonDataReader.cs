// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal delegate T ReadValue<T>(JsonDataReader reader);
internal delegate T ReadProperties<T>(JsonDataReader reader);

/// <summary>
///  This is an abstraction used to read JSON data. This wraps a
///  <see cref="JsonElement"/> from System.Text.Json, providing
///  sequential-style property access over the tree model.
/// </summary>
internal ref struct JsonDataReader
{
    private readonly JsonElement _element;
    private JsonElement _currentValue;

    public JsonDataReader(JsonElement element)
    {
        _element = element;
        _currentValue = element;
    }

    public bool IsInteger => _currentValue.ValueKind == JsonValueKind.Number;
    public bool IsObjectStart => _currentValue.ValueKind == JsonValueKind.Object;
    public bool IsString => _currentValue.ValueKind == JsonValueKind.String;

    public bool IsPropertyName(string propertyName)
        => _element.ValueKind == JsonValueKind.Object &&
           _element.TryGetProperty(propertyName, out _);

    public void ReadPropertyName(string propertyName)
    {
        if (!_element.TryGetProperty(propertyName, out _currentValue))
        {
            ThrowUnexpectedPropertyException(propertyName);
        }

        [DoesNotReturn]
        static void ThrowUnexpectedPropertyException(string expectedPropertyName)
        {
            throw new InvalidOperationException(
                Strings.FormatExpected_JSON_property_0_but_it_was_1(expectedPropertyName, null));
        }
    }

    public bool TryReadPropertyName(string propertyName)
        => _element.TryGetProperty(propertyName, out _currentValue);

    public bool TryReadNextPropertyName([NotNullWhen(true)] out string? propertyName)
    {
        // Not directly supported with the JsonElement tree model.
        propertyName = null;
        return false;
    }

    public bool TryReadNull()
        => _currentValue.ValueKind == JsonValueKind.Null;

    public bool ReadBoolean()
        => _currentValue.GetBoolean();

    public bool ReadBoolean(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadBoolean();
    }

    public bool ReadBooleanOrDefault(string propertyName, bool defaultValue = default)
        => TryReadPropertyName(propertyName) ? ReadBoolean() : defaultValue;

    public bool ReadBooleanOrTrue(string propertyName)
        => !TryReadPropertyName(propertyName) || ReadBoolean();

    public bool ReadBooleanOrFalse(string propertyName)
        => TryReadPropertyName(propertyName) && ReadBoolean();

    public bool TryReadBoolean(string propertyName, out bool value)
    {
        if (TryReadPropertyName(propertyName))
        {
            value = ReadBoolean();
            return true;
        }

        value = default;
        return false;
    }

    public byte ReadByte()
        => _currentValue.GetByte();

    public byte ReadByteOrDefault(string propertyName, byte defaultValue = default)
        => TryReadPropertyName(propertyName) ? ReadByte() : defaultValue;

    public byte ReadByteOrZero(string propertyName)
        => TryReadPropertyName(propertyName) ? ReadByte() : (byte)0;

    public bool TryReadByte(string propertyName, out byte value)
    {
        if (TryReadPropertyName(propertyName))
        {
            value = ReadByte();
            return true;
        }

        value = default;
        return false;
    }

    public byte ReadByte(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadByte();
    }

    public int ReadInt32()
        => _currentValue.GetInt32();

    public int ReadInt32OrDefault(string propertyName, int defaultValue = default)
        => TryReadPropertyName(propertyName) ? ReadInt32() : defaultValue;

    public int ReadInt32OrZero(string propertyName)
        => TryReadPropertyName(propertyName) ? ReadInt32() : 0;

    public bool TryReadInt32(string propertyName, out int value)
    {
        if (TryReadPropertyName(propertyName))
        {
            value = ReadInt32();
            return true;
        }

        value = default;
        return false;
    }

    public int ReadInt32(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadInt32();
    }

    public long ReadInt64()
        => _currentValue.GetInt64();

    public long ReadInt64OrDefault(string propertyName, int defaultValue = default)
        => TryReadPropertyName(propertyName) ? ReadInt64() : defaultValue;

    public long ReadInt64OrZero(string propertyName)
        => TryReadPropertyName(propertyName) ? ReadInt64() : 0;

    public bool TryReadInt64(string propertyName, out long value)
    {
        if (TryReadPropertyName(propertyName))
        {
            value = ReadInt64();
            return true;
        }

        value = default;
        return false;
    }

    public long ReadInt64(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadInt64();
    }

    public string? ReadString()
    {
        if (TryReadNull())
        {
            return null;
        }

        return _currentValue.GetString();
    }

    public string? ReadString(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadString();
    }

    public string? ReadStringOrDefault(string propertyName, string? defaultValue = default)
        => TryReadPropertyName(propertyName) ? ReadString() : defaultValue;

    public string? ReadStringOrNull(string propertyName)
        => TryReadPropertyName(propertyName) ? ReadString() : null;

    public bool TryReadString(string propertyName, out string? value)
    {
        if (TryReadPropertyName(propertyName))
        {
            value = ReadString();
            return true;
        }

        value = null;
        return false;
    }

    public string ReadNonNullString()
        => _currentValue.GetString().AssumeNotNull();

    public string ReadNonNullString(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadNonNullString();
    }

    public object? ReadValue()
    {
        return _currentValue.ValueKind switch
        {
            JsonValueKind.String => ReadString(),
            JsonValueKind.Number => (object)ReadInt32(),
            JsonValueKind.True or JsonValueKind.False => (object)ReadBoolean(),
            JsonValueKind.Null => null,

            var kind => ThrowNotSupported(kind)
        };

        [DoesNotReturn]
        static object? ThrowNotSupported(JsonValueKind kind)
        {
            throw new NotSupportedException(
                $"Could not read value - JSON value kind was '{kind}'.");
        }
    }

    public Uri? ReadUri(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadUri();
    }

    public Uri? ReadUri()
    {
        return ReadString() is string uriString
            ? new Uri(uriString)
            : null;
    }

    public Uri ReadNonNullUri(string propertyName)
    {
        ReadPropertyName(propertyName);
        return ReadNonNullUri();
    }

    public Uri ReadNonNullUri()
    {
        var uriString = ReadNonNullString();
        return new Uri(uriString);
    }

    [return: MaybeNull]
    public T ReadObject<T>(ReadProperties<T> readProperties)
    {
        if (TryReadNull())
        {
            return default;
        }

        return ReadNonNullObject(readProperties);
    }

    [return: MaybeNull]
    public T ReadObject<T>(string propertyName, ReadProperties<T> readProperties)
    {
        ReadPropertyName(propertyName);
        return ReadObject(readProperties);
    }

    [return: MaybeNull]
    public T ReadObjectOrDefault<T>(string propertyName, ReadProperties<T> readProperties, T defaultValue)
        => TryReadPropertyName(propertyName) ? ReadObject(readProperties) : defaultValue;

    public T? ReadObjectOrNull<T>(string propertyName, ReadProperties<T> readProperties)
        where T : class
        => ReadObjectOrDefault(propertyName, readProperties!, defaultValue: null);

    public T ReadNonNullObject<T>(ReadProperties<T> readProperties)
        => readProperties(new JsonDataReader(_currentValue));

    public T ReadNonNullObject<T>(string propertyName, ReadProperties<T> readProperties)
    {
        ReadPropertyName(propertyName);
        return ReadNonNullObject(readProperties);
    }

    public T ReadNonNullObjectOrDefault<T>(string propertyName, ReadProperties<T> readProperties, T defaultValue)
        => TryReadPropertyName(propertyName) ? ReadNonNullObject(readProperties) : defaultValue;

    public T[]? ReadArray<T>(ReadValue<T> readElement)
    {
        if (TryReadNull())
        {
            return null;
        }

        var length = _currentValue.GetArrayLength();
        if (length == 0)
        {
            return [];
        }

        var result = new T[length];
        var i = 0;
        foreach (var item in _currentValue.EnumerateArray())
        {
            result[i++] = readElement(new JsonDataReader(item));
        }

        return result;
    }

    public T[]? ReadArray<T>(string propertyName, ReadValue<T> readElement)
    {
        ReadPropertyName(propertyName);
        return ReadArray(readElement);
    }

    public T[] ReadArrayOrEmpty<T>(string propertyName, ReadValue<T> readElement)
        => TryReadPropertyName(propertyName) ? ReadArray(readElement) ?? [] : [];

    public ImmutableArray<T> ReadImmutableArray<T>(ReadValue<T> readElement)
    {
        var length = _currentValue.GetArrayLength();
        if (length == 0)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<T>(length);
        foreach (var item in _currentValue.EnumerateArray())
        {
            builder.Add(readElement(new JsonDataReader(item)));
        }

        return builder.ToImmutable();
    }

    public ImmutableArray<T> ReadImmutableArray<T>(string propertyName, ReadValue<T> readElement)
    {
        ReadPropertyName(propertyName);
        return ReadImmutableArray(readElement);
    }

    public ImmutableArray<T> ReadImmutableArrayOrEmpty<T>(string propertyName, ReadValue<T> readElement)
        => TryReadPropertyName(propertyName) ? ReadImmutableArray(readElement) : [];

    public void ReadToEndOfCurrentObject()
    {
        // No-op: with JsonElement, the entire object is already parsed in memory.
    }
}
