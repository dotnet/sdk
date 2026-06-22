// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Serializes <see cref="DotnetAccessMode"/> as the lowercase names <c>none</c> / <c>shell</c> /
/// <c>full</c>, and on read also accepts the legacy enum spellings that shipped in internal
/// builds (<c>DotnetupDotnet</c> / <c>ShellProfile</c> / <c>FullPathReplacement</c>) and the
/// numeric form. This is the read-compatibility shim that lets configs written by earlier
/// internal builds keep their chosen mode after the rename; see the design doc
/// (documentation/general/dotnetup/designs/dotnetup-env.md, "Config schema").
/// </summary>
internal sealed class DotnetAccessModeJsonConverter : JsonConverter<DotnetAccessMode>
{
    public override DotnetAccessMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int numeric))
        {
            var numericMode = (DotnetAccessMode)numeric;
            if (!Enum.IsDefined(numericMode))
            {
                throw new JsonException($"Unknown {nameof(DotnetAccessMode)} numeric value '{numeric}'.");
            }

            return numericMode;
        }

        string? value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "none" or "dotnetupdotnet" => DotnetAccessMode.None,
            "shell" or "shellprofile" => DotnetAccessMode.Shell,
            "full" or "fullpathreplacement" => DotnetAccessMode.Full,
            _ => throw new JsonException($"Unknown {nameof(DotnetAccessMode)} value '{value}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, DotnetAccessMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            DotnetAccessMode.None => "none",
            DotnetAccessMode.Shell => "shell",
            DotnetAccessMode.Full => "full",
            _ => throw new JsonException($"Unknown {nameof(DotnetAccessMode)} value '{value}'."),
        });
    }
}
