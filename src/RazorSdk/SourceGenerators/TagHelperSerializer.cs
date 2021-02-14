// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Serialization;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal class TagHelperSerializer
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters =
            {
                new TagHelperDescriptorJsonConverter(),
                new RazorDiagnosticJsonConverter(),
            }
        };

        public static void Serialize(string manifestFilePath, IReadOnlyList<TagHelperDescriptor> tagHelpers)
        {
            JsonSerializer.Serialize<IReadOnlyList<TagHelperDescriptor>>(
                new Utf8JsonWriter(File.Create(manifestFilePath), new JsonWriterOptions()), tagHelpers, _options);
        }

        public static IReadOnlyList<TagHelperDescriptor> Deserialize(string manifestFilePath)
        {
            return JsonSerializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(File.ReadAllText(manifestFilePath), _options);
        }
    }
}
