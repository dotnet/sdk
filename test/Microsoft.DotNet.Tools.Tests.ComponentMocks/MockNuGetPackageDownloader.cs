// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class MockNuGetPackageDownloader : INuGetPackageDownloader
    {
        public static readonly string MOCK_FEEDS_TEXT = "{MockFeeds}";

        private readonly string _downloadPath;
        private readonly bool _manifestDownload;
        private NuGetVersion _lastPackageVersion = new("1.0.0");
        private IEnumerable<NuGetVersion> _packageVersions;

        public List<(PackageId id, NuGetVersion version, DirectoryPath? downloadFolder, PackageSourceLocation packageSourceLocation)> DownloadCallParams = new();

        public List<string> DownloadCallResult = new();

        public List<(string, DirectoryPath)> ExtractCallParams = new();

        public HashSet<string> PackageIdsToNotFind { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string MockFeedWithNoPackages { get; set; }

        public MockNuGetPackageDownloader(string dotnetRoot = null, bool manifestDownload = false, IEnumerable<NuGetVersion> packageVersions = null)
        {
            _manifestDownload = manifestDownload;
            _downloadPath = dotnetRoot == null ? string.Empty : Path.Combine(dotnetRoot, "metadata", "temp");
            if (_downloadPath != string.Empty)
            {
                Directory.CreateDirectory(_downloadPath);
            }

            _packageVersions = packageVersions ?? [new NuGetVersion("1.0.42")];

            PackageIdsToNotFind.Add("does.not.exist");
        }

        bool ShouldFindPackage(PackageId packageId, PackageSourceLocation packageSourceLocation)
        {
            if (PackageIdsToNotFind.Contains(packageId.ToString()) ||
                (!string.IsNullOrEmpty(MockFeedWithNoPackages) && packageSourceLocation.SourceFeedOverrides.Length == 1 && packageSourceLocation.SourceFeedOverrides[0] == MockFeedWithNoPackages))
            {
                return false;
            }
            return true;
        }


        public Task<string> DownloadPackageAsync(PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false,
            bool? includeUnlisted = null,
            DirectoryPath? downloadFolder = null,
            PackageSourceMapping packageSourceMapping = null)
        {
            DownloadCallParams.Add((packageId, packageVersion, downloadFolder, packageSourceLocation));

            if (!ShouldFindPackage(packageId, packageSourceLocation))
            {
                return Task.FromException<string>(new NuGetPackageNotFoundException(string.Format(CliStrings.IsNotFoundInNuGetFeeds, packageId, MOCK_FEEDS_TEXT)));
            }

            var path = Path.Combine(_downloadPath, "mock.nupkg");
            DownloadCallResult.Add(path);
            if (_downloadPath != string.Empty)
            {
                try
                {
                    File.WriteAllText(path, string.Empty);
                }
                catch (IOException)
                {
                    // Do not write this file twice in parallel
                }
            }
            _lastPackageVersion = packageVersion ?? _packageVersions.Max();
            return Task.FromResult(path);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
        {
            ExtractCallParams.Add((packagePath, targetFolder));
            if (_manifestDownload)
            {
                var dataFolder = Path.Combine(targetFolder.Value, "data");
                Directory.CreateDirectory(dataFolder);
                string manifestContents = $@"{{
  ""version"": ""{_lastPackageVersion.ToString()}"",
  ""workloads"": {{
    }}
  }},
  ""packs"": {{
  }}
}}";

                File.WriteAllText(Path.Combine(dataFolder, "WorkloadManifest.json"), manifestContents);
            }

            return Task.FromResult(new List<string>() as IEnumerable<string>);
        }

        public Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(PackageId packageId, int numberOfResults, PackageSourceLocation packageSourceLocation = null, bool includePreview = false)
        {

            if (!ShouldFindPackage(packageId, packageSourceLocation))
            {
                return Task.FromResult(Enumerable.Empty<NuGetVersion>());
            }

            return Task.FromResult(_packageVersions ?? Enumerable.Empty<NuGetVersion>());
        }

        public Task<NuGetVersion> GetLatestPackageVersion(PackageId packageId, PackageSourceLocation packageSourceLocation = null, bool includePreview = false)
        {
            if (!ShouldFindPackage(packageId, packageSourceLocation))
            {
                return Task.FromException<NuGetVersion>(new NuGetPackageNotFoundException(string.Format(CliStrings.IsNotFoundInNuGetFeeds, packageId, MOCK_FEEDS_TEXT)));
            }

            return Task.FromResult(_packageVersions.Max());
        }

        public async Task<NuGetVersion> GetBestPackageVersionAsync(PackageId packageId, VersionRange versionRange, PackageSourceLocation packageSourceLocation = null)
        {
            return (await GetBestPackageVersionAndSourceAsync(packageId, versionRange, packageSourceLocation)).version;
        }

        public Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(PackageId packageId,
            VersionRange versionRange,PackageSourceLocation packageSourceLocation = null)
        {
            if (!ShouldFindPackage(packageId, packageSourceLocation))
            {
                return Task.FromException<(NuGetVersion version, PackageSource source)>(new NuGetPackageNotFoundException(string.Format(CliStrings.IsNotFoundInNuGetFeeds, packageId, MOCK_FEEDS_TEXT)));
            }

            var bestVersion = versionRange.FindBestMatch(_packageVersions);
            if (bestVersion == null)
            {
                bestVersion = versionRange.MinVersion;
            }

            var source = new PackageSource("http://mock-url", "MockSource");

            return Task.FromResult((bestVersion, source));
        }



        public Task<string> GetPackageUrl(PackageId packageId,
            NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false)
        {
            return Task.FromResult($"http://mock-url/{packageId}.{packageVersion}.nupkg");
        }
    }
}
