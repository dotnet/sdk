// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static partial class ObjectWriters
{
    public static void Write(JsonDataWriter writer, RazorDiagnostic? value)
        => writer.WriteObject(value, WriteProperties);

    public static void WriteProperties(JsonDataWriter writer, RazorDiagnostic value)
    {
        writer.Write(nameof(value.Id), value.Id);
        writer.Write(nameof(value.Severity), (int)value.Severity);
        writer.Write(WellKnownPropertyNames.Message, value.GetMessage(CultureInfo.CurrentCulture));

        var span = value.Span;
        writer.WriteIfNotNull(nameof(span.FilePath), span.FilePath);
        writer.WriteIfNotZero(nameof(span.AbsoluteIndex), span.AbsoluteIndex);
        writer.WriteIfNotZero(nameof(span.LineIndex), span.LineIndex);
        writer.WriteIfNotZero(nameof(span.CharacterIndex), span.CharacterIndex);
        writer.WriteIfNotZero(nameof(span.Length), span.Length);
    }

    public static void Write(JsonDataWriter writer, RazorExtension? value)
        => writer.WriteObject(value, WriteProperties);

    public static void WriteProperties(JsonDataWriter writer, RazorExtension value)
    {
        writer.Write(nameof(value.ExtensionName), value.ExtensionName);
    }
}
