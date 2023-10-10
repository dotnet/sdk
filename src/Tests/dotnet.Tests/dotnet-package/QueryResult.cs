// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;

namespace Microsoft.DotNet.Cli.Package.Tests
{
    public class QueryResult
    {
        [JsonProperty("@context")]
        public Context Context { get; set; }

        [JsonProperty("totalhits")]
        public int TotalHits { get; set; }

        [JsonProperty("data")]
        public List<DataItem> Data { get; set; }
    }

    public class MockIndex
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("resources")]
        public Resource[] Resources { get; set; }

        [JsonProperty("@context")]
        public Context Context { get; set; }
    }

    public class Resource
    {
        [JsonProperty("@id")]
        public string Id { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }
    }

    public class Context
    {
        [JsonProperty("@vocab")]
        public string Vocab { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("@base")]
        public string Base { get; set; }
    }

    public class DataItem
    {
        [JsonProperty("@id")]
        public string Id { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("registration")]
        public string Registration { get; set; }

        [JsonProperty("id")]
        public string PackageId { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("iconurl")]
        public string IconUrl { get; set; }

        [JsonProperty("licenseurl")]
        public string LicenseUrl { get; set; }

        [JsonProperty("projecturl")]
        public string ProjectUrl { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("authors")]
        public List<string> Authors { get; set; }

        [JsonProperty("totaldownloads")]
        public long TotalDownloads { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("packagetypes")]
        public List<PackageType> PackageTypes { get; set; }

        [JsonProperty("versions")]
        public List<VersionItem> Versions { get; set; }
    }

    public class PackageType
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class VersionItem
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("downloads")]
        public int Downloads { get; set; }

        [JsonProperty("@id")]
        public string Id { get; set; }
    }
}
