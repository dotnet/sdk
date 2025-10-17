// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal sealed class ReleaseVersionJsonConverter : JsonConverter<ReleaseVersion>
{
    public override ReleaseVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("ReleaseVersion value cannot be null or empty.");
        }

        return new ReleaseVersion(value);
    }

    public override void Write(Utf8JsonWriter writer, ReleaseVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
