// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.NugetSearch.NugetSearchApiSerializable;
using Microsoft.DotNet.Cli.ToolPackage;

namespace Microsoft.DotNet.Cli.NugetSearch;

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(AuthorsConverter)])]
[JsonSerializable(typeof(NugetSearchApiContainerSerializable))]
internal partial class NugetSearchApiJsonSerializerContext : JsonSerializerContext;

internal static class NugetSearchApiResultDeserializer
{
    public static IReadOnlyCollection<SearchResultPackage> Deserialize(string json)
    {
        var deserialized = JsonSerializer.Deserialize(json, NugetSearchApiJsonSerializerContext.Default.NugetSearchApiContainerSerializable);
        var resultPackages = new List<SearchResultPackage>();
        foreach (var deserializedPackage in deserialized.Data)
        {
            var versions =
                deserializedPackage.Versions.Select(v => new SearchResultPackageVersion(v.Version, v.Downloads))
                    .ToArray();

            string[] authors = deserializedPackage?.Authors?.Authors ?? [];

            var searchResultPackage = new SearchResultPackage(new PackageId(deserializedPackage.Id),
                deserializedPackage.Version, deserializedPackage.Description, deserializedPackage.Summary,
                deserializedPackage.Tags, authors, deserializedPackage.TotalDownloads, deserializedPackage.Verified,
                versions);

            resultPackages.Add(searchResultPackage);
        }

        return resultPackages;
    }
}
