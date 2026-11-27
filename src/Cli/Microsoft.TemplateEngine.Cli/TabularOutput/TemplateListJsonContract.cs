// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    internal sealed class TemplateListJsonContract
    {
        [JsonPropertyName("version")]
        public int Version { get; init; } = 1;

        [JsonPropertyName("data")]
        public required TemplateGroupJsonContract[] Data { get; init; }
    }

    internal sealed class TemplateGroupJsonContract
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("shortNames")]
        public required string[] ShortNames { get; init; }

        [JsonPropertyName("languages")]
        public required TemplateLanguageJsonContract[] Languages { get; init; }

        [JsonPropertyName("types")]
        public required string[] Types { get; init; }

        [JsonPropertyName("authors")]
        public required string[] Authors { get; init; }

        [JsonPropertyName("classifications")]
        public required string[] Classifications { get; init; }
    }

    internal sealed class TemplateLanguageJsonContract
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("default")]
        public required bool Default { get; init; }
    }

    [JsonSerializable(typeof(TemplateListJsonContract))]
    internal partial class TemplateListJsonSerializerContext : JsonSerializerContext
    {
        internal static readonly TemplateListJsonSerializerContext DefaultContext = new(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
