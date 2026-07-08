// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal sealed class TagHelperDescriptorJsonConverter : ObjectJsonConverter<TagHelperDescriptor>
{
    public static readonly TagHelperDescriptorJsonConverter Instance = new();

    internal static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private TagHelperDescriptorJsonConverter()
    {
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(Instance);
        return options;
    }

    protected override TagHelperDescriptor ReadFromProperties(JsonDataReader reader)
        => ObjectReaders.ReadTagHelperFromProperties(reader);

    protected override void WriteProperties(JsonDataWriter writer, TagHelperDescriptor value)
        => ObjectWriters.WriteProperties(writer, value);
}
