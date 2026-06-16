// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Serializes <see cref="PathPreference"/> as the lowercase names <c>none</c> / <c>shell</c> /
/// <c>all</c>, and on read also accepts the legacy enum spellings that shipped in internal
/// builds (<c>DotnetupDotnet</c> / <c>ShellProfile</c> / <c>FullPathReplacement</c>) and the
/// numeric form. This is the read-compatibility shim that lets configs written by earlier
/// internal builds keep their chosen mode after the rename; see the design doc
/// (documentation/general/dotnetup/designs/dotnetup-env.md, "Config schema").
/// </summary>
internal sealed class PathPreferenceJsonConverter : JsonConverter<PathPreference>
{
    public override PathPreference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int numeric))
        {
            return (PathPreference)numeric;
        }

        string? value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "none" or "dotnetupdotnet" => PathPreference.None,
            "shell" or "shellprofile" => PathPreference.Shell,
            "all" or "fullpathreplacement" => PathPreference.All,
            _ => throw new JsonException($"Unknown {nameof(PathPreference)} value '{value}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, PathPreference value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            PathPreference.None => "none",
            PathPreference.Shell => "shell",
            PathPreference.All => "all",
            _ => throw new JsonException($"Unknown {nameof(PathPreference)} value '{value}'."),
        });
    }
}
