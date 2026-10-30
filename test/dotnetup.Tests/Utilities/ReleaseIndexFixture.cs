// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Creates an offline release index and eagerly parses its manifests into memory.
/// Temporary files are removed before the fixture is returned.
/// </summary>
internal sealed class ReleaseIndexFixture
{
    private readonly IReadOnlyDictionary<string, IEnumerable<ProductRelease>> _releases;

    private ReleaseIndexFixture(
        IEnumerable<Product> index, IReadOnlyDictionary<string, IEnumerable<ProductRelease>> releases)
    {
        Index = index;
        _releases = releases;
    }

    public IEnumerable<Product> Index { get; }

    public IEnumerable<ProductRelease> GetReleases(Product product) => _releases[product.ProductVersion];

    public static ReleaseIndexFixture Create(string version11Phase = "preview")
    {
        var directory = Directory.CreateTempSubdirectory("dotnetup-releases-");
        try
        {
            var products = new[]
            {
                (Version: "8.0", Phase: "eol", Type: "lts", Sdks: new[] { "8.0.100" }),
                (Version: "12.0", Phase: "go-live", Type: "lts", Sdks: new[] { "12.0.100-preview.1" }),
                (Version: "10.0", Phase: "active", Type: "lts",
                    Sdks: new[] { "10.0.102", "10.0.105", "10.0.103", "10.0.202", "10.0.106-preview.1" }),
                (Version: "11.0", Phase: version11Phase, Type: "sts",
                    Sdks: version11Phase == "preview" ? new[] { "11.0.100-preview.1" } : new[] { "11.0.100-preview.1", "11.0.100" }),
                (Version: "9.0", Phase: "maintenance", Type: "sts", Sdks: new[] { "9.0.100" })
            };
            var entries = new List<string>();
            var releases = new Dictionary<string, IEnumerable<ProductRelease>>();
            foreach (var product in products)
            {
                entries.Add($$"""
                    {"channel-version":"{{product.Version}}","support-phase":"{{product.Phase}}","release-type":"{{product.Type}}",
                    "latest-release-date":"2026-01-01","latest-release":"{{product.Version}}.5",
                    "latest-runtime":"{{product.Version}}.5","latest-sdk":"{{product.Sdks.Max(v => new ReleaseVersion(v))}}",
                    "product":".NET","eol-date":"2028-11-01","security":false,
                    "releases.json":"https://example.test/{{product.Version}}/releases.json"}
                    """);
                var path = Path.Combine(directory.FullName, product.Version + ".json");
                var sdks = product.Sdks.Select(v => $$"""{"version":"{{v}}","version-display":"{{v}}","files":[]}""");
                File.WriteAllText(path, $$"""
                    {"releases":[{"release-date":"2026-01-01","release-version":"{{product.Version}}.5","security":false,
                    "runtime":{"version":"{{product.Version}}.5","version-display":"{{product.Version}}.5","files":[]},
                    "aspnetcore-runtime":{"version":"{{product.Version}}.4","version-display":"{{product.Version}}.4","files":[]},
                    "windowsdesktop":{"version":"{{product.Version}}.6","version-display":"{{product.Version}}.6","files":[]},
                    "sdks":[{{string.Join(",", sdks)}}]}]}
                    """);
                releases.Add(product.Version, Product.GetReleasesAsync(path).GetAwaiter().GetResult());
            }
            var indexPath = Path.Combine(directory.FullName, "releases-index.json");
            File.WriteAllText(indexPath, $$"""{"releases-index":[{{string.Join(",", entries)}}]}""");
            var index = ProductCollection.GetFromFileAsync(indexPath, downloadLatest: false).GetAwaiter().GetResult();
            return new ReleaseIndexFixture(index, releases);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
