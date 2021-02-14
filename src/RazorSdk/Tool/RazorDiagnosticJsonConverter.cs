// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.AspNetCore.Razor.Language;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal class RazorDiagnosticJsonConverter : JsonConverter<RazorDiagnostic>
    {
        public static readonly RazorDiagnosticJsonConverter Instance = new RazorDiagnosticJsonConverter();
        private const string RazorDiagnosticMessageKey = "Message";

        public override bool CanConvert(Type objectType)
        {
            return typeof(RazorDiagnostic).IsAssignableFrom(objectType);
        }

        public override RazorDiagnostic Read(ref Utf8JsonReader reader, Type objectType, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            using var doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            var id = root.GetProperty(nameof(RazorDiagnostic.Id)).GetString();
            var severity = root.GetProperty(nameof(RazorDiagnostic.Severity)).GetInt32();
            var message = root.GetProperty(RazorDiagnosticMessageKey).GetString();

            var span = root.GetProperty(nameof(RazorDiagnostic.Span));
            var filePath = span.GetProperty(nameof(SourceSpan.FilePath)).GetString();
            var absoluteIndex = span.GetProperty(nameof(SourceSpan.AbsoluteIndex)).GetInt32();
            var lineIndex = span.GetProperty(nameof(SourceSpan.LineIndex)).GetInt32();
            var characterIndex = span.GetProperty(nameof(SourceSpan.CharacterIndex)).GetInt32();
            var length = span.GetProperty(nameof(SourceSpan.Length)).GetInt32();

            var descriptor = new RazorDiagnosticDescriptor(id, () => message, (RazorDiagnosticSeverity)severity);
            var sourceSpan = new SourceSpan(filePath, absoluteIndex, lineIndex, characterIndex, length);

            return RazorDiagnostic.Create(descriptor, sourceSpan);
        }

        public override void Write(Utf8JsonWriter writer, RazorDiagnostic diagnostic, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(RazorDiagnostic.Id), diagnostic.Id);
            writer.WriteNumber(nameof(RazorDiagnostic.Severity), (int) diagnostic.Severity);
            writer.WriteString(RazorDiagnosticMessageKey, diagnostic.GetMessage(CultureInfo.CurrentCulture));

            writer.WritePropertyName(nameof(RazorDiagnostic.Span));
            writer.WriteStartObject();
            writer.WriteString(nameof(SourceSpan.FilePath), diagnostic.Span.FilePath);
            writer.WriteNumber(nameof(SourceSpan.AbsoluteIndex), diagnostic.Span.AbsoluteIndex);
            writer.WriteNumber(nameof(SourceSpan.LineIndex), diagnostic.Span.LineIndex);
            writer.WriteNumber(nameof(SourceSpan.CharacterIndex), diagnostic.Span.CharacterIndex);
            writer.WriteNumber(nameof(SourceSpan.Length), diagnostic.Span.Length);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
