// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal delegate void WriteProperties<T>(JsonDataWriter writer, T value);
internal delegate void WriteValue<T>(JsonDataWriter writer, T value);

/// <summary>
///  This is an abstraction used to write JSON data. Currently, this
///  wraps a <see cref="JsonWriter"/> from JSON.NET.
/// </summary>
internal readonly ref struct JsonDataWriter(JsonWriter writer)
{
    private readonly JsonWriter _writer = writer;

    public void Write(bool value)
    {
        _writer.WriteValue(value);
    }

    public void Write(string propertyName, bool value)
    {
        _writer.WritePropertyName(propertyName);
        _writer.WriteValue(value);
    }

    public void WriteIfNotTrue(string propertyName, bool value)
    {
        if (!value)
        {
            Write(propertyName, value);
        }
    }

    public void WriteIfNotFalse(string propertyName, bool value)
    {
        if (value)
        {
            Write(propertyName, value);
        }
    }

    public void Write(byte value)
    {
        _writer.WriteValue(value);
    }

    public void Write(string propertyName, byte value)
    {
        _writer.WritePropertyName(propertyName);
        _writer.WriteValue(value);
    }

    public void WriteIfNotZero(string propertyName, byte value)
    {
        WriteIfNotDefault(propertyName, value, defaultValue: (byte)0);
    }

    public void WriteIfNotDefault(string propertyName, byte value, byte defaultValue)
    {
        if (value != defaultValue)
        {
            Write(propertyName, value);
        }
    }

    public void Write(int value)
    {
        _writer.WriteValue(value);
    }

    public void Write(string propertyName, int value)
    {
        _writer.WritePropertyName(propertyName);
        _writer.WriteValue(value);
    }

    public void WriteIfNotZero(string propertyName, int value)
    {
        WriteIfNotDefault(propertyName, value, defaultValue: 0);
    }

    public void WriteIfNotDefault(string propertyName, int value, int defaultValue)
    {
        if (value != defaultValue)
        {
            Write(propertyName, value);
        }
    }

    public void Write(long value)
    {
        _writer.WriteValue(value);
    }

    public void Write(string propertyName, long value)
    {
        _writer.WritePropertyName(propertyName);
        _writer.WriteValue(value);
    }

    public void WriteIfNotZero(string propertyName, long value)
    {
        WriteIfNotDefault(propertyName, value, defaultValue: 0);
    }

    public void WriteIfNotDefault(string propertyName, long value, long defaultValue)
    {
        if (value != defaultValue)
        {
            Write(propertyName, value);
        }
    }

    public void Write(string? value)
    {
        _writer.WriteValue(value);
    }

    public void Write(string propertyName, string? value)
    {
        _writer.WritePropertyName(propertyName);
        _writer.WriteValue(value);
    }

    public void WriteIfNotDefault(string propertyName, string? value, string? defaultValue)
    {
        if (value != defaultValue)
        {
            Write(propertyName, value);
        }
    }

    public void WriteIfNotNull(string propertyName, string? value)
    {
        if (value is not null)
        {
            Write(propertyName, value);
        }
    }

    public void WriteValue(object? value)
    {
        switch (value)
        {
            case string s:
                Write(s);
                break;

            case int i:
                Write(i);
                break;

            case bool b:
                Write(b);
                break;

            case null:
                Write((string?)null);
                break;

            default:
                throw new NotSupportedException();
        }
    }

    public void Write(string propertyName, Uri? value)
    {
        _writer.WritePropertyName(propertyName);
        Write(value);
    }

    public void Write(Uri? value)
    {
        if (value is null)
        {
            _writer.WriteNull();
        }
        else
        {
            _writer.WriteValue(value.AbsoluteUri);
        }
    }

    public void WriteObject<T>(string propertyName, T? value, WriteProperties<T> writeProperties)
    {
        _writer.WritePropertyName(propertyName);
        WriteObject(value, writeProperties);
    }

    public void WriteObject<T>(T? value, WriteProperties<T> writeProperties)
    {
        if (value is null)
        {
            _writer.WriteNull();
            return;
        }

        _writer.WriteStartObject();
        writeProperties(this, value);
        _writer.WriteEndObject();
    }

    public void WriteObjectIfNotDefault<T>(string propertyName, T? value, T? defaultValue, WriteProperties<T> writeProperties)
    {
        if (!EqualityComparer<T?>.Default.Equals(value, defaultValue))
        {
            WriteObject(propertyName, value, writeProperties);
        }
    }

    public void WriteObjectIfNotNull<T>(string propertyName, T? value, WriteProperties<T> writeProperties)
    {
        if (value is not null)
        {
            WriteObject(propertyName, value, writeProperties);
        }
    }

    public void WriteArray<T>(IEnumerable<T>? elements, WriteValue<T> writeElement)
    {
        ArgumentNullException.ThrowIfNull(writeElement);

        if (elements is null)
        {
            _writer.WriteNull();
            return;
        }

        _writer.WriteStartArray();

        foreach (var element in elements)
        {
            writeElement(this, element);
        }

        _writer.WriteEndArray();
    }

    public void WriteArray<T>(string propertyName, IEnumerable<T>? elements, WriteValue<T> writeElement)
    {
        _writer.WritePropertyName(propertyName);
        WriteArray(elements, writeElement);
    }

    public void WriteArray<T>(IReadOnlyList<T>? elements, WriteValue<T> writeElement)
    {
        ArgumentNullException.ThrowIfNull(writeElement);

        if (elements is null)
        {
            _writer.WriteNull();
            return;
        }

        _writer.WriteStartArray();

        var count = elements.Count;

        for (var i = 0; i < count; i++)
        {
            writeElement(this, elements[i]);
        }

        _writer.WriteEndArray();
    }

    public void WriteArray<T>(string propertyName, IReadOnlyList<T>? elements, WriteValue<T> writeElement)
    {
        _writer.WritePropertyName(propertyName);
        WriteArray(elements, writeElement);
    }

    public void WriteArray<T>(ImmutableArray<T> elements, WriteValue<T> writeElement)
    {
        ArgumentNullException.ThrowIfNull(writeElement);

        _writer.WriteStartArray();

        foreach (var element in elements)
        {
            writeElement(this, element);
        }

        _writer.WriteEndArray();
    }

    public void WriteArray<T>(string propertyName, ImmutableArray<T> elements, WriteValue<T> writeElement)
    {
        _writer.WritePropertyName(propertyName);
        WriteArray(elements, writeElement);
    }

    public void WriteArrayIfNotNullOrEmpty<T>(string propertyName, IEnumerable<T>? elements, WriteValue<T> writeElement)
    {
        if (elements?.Any() == true)
        {
            WriteArray(propertyName, elements, writeElement);
        }
    }

    public void WriteArrayIfNotDefaultOrEmpty<T>(string propertyName, ImmutableArray<T> elements, WriteValue<T> writeElement)
    {
        if (!elements.IsDefaultOrEmpty)
        {
            WriteArray(propertyName, elements, writeElement);
        }
    }
}
