// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static partial class ObjectReaders
{
    public static RazorDiagnostic ReadDiagnostic(JsonDataReader reader)
        => reader.ReadNonNullObject(ReadDiagnosticFromProperties);

    public static RazorDiagnostic ReadDiagnosticFromProperties(JsonDataReader reader)
    {
        var id = reader.ReadNonNullString(nameof(RazorDiagnostic.Id));
        var severity = (RazorDiagnosticSeverity)reader.ReadInt32OrZero(nameof(RazorDiagnostic.Severity));
        var message = reader.ReadNonNullString(WellKnownPropertyNames.Message);

        var filePath = reader.ReadStringOrNull(nameof(SourceSpan.FilePath));
        var absoluteIndex = reader.ReadInt32OrZero(nameof(SourceSpan.AbsoluteIndex));
        var lineIndex = reader.ReadInt32OrZero(nameof(SourceSpan.LineIndex));
        var characterIndex = reader.ReadInt32OrZero(nameof(SourceSpan.CharacterIndex));
        var length = reader.ReadInt32OrZero(nameof(SourceSpan.Length));

        var descriptor = new RazorDiagnosticDescriptor(id, message, severity);
        var span = new SourceSpan(filePath, absoluteIndex, lineIndex, characterIndex, length);

        return RazorDiagnostic.Create(descriptor, span);
    }
}
