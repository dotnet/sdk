// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class FailingNuGetPackageDownloader : INuGetPackageDownloader
    {
        public readonly string MockPackageDir;

        public FailingNuGetPackageDownloader(string testDir)
        {
            MockPackageDir = Path.Combine(testDir, "MockPackages");
            Directory.CreateDirectory(MockPackageDir);
        }

        public Task<string> DownloadPackageAsync(
            PackageId packageId,
            CancellationToken cancellationToken,
            NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false,
            bool? includeUnlisted = null,
            DirectoryPath? downloadFolder = null,
            PackageSourceMapping packageSourceMapping = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mockPackagePath = Path.Combine(MockPackageDir, $"{packageId}.{packageVersion}.nupkg");
            File.WriteAllText(mockPackagePath, string.Empty);
            return Task.FromResult(mockPackagePath);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(
            string packagePath,
            DirectoryPath targetFolder,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(targetFolder.Value);
            File.WriteAllText(Path.Combine(targetFolder.Value, "testfile.txt"), string.Empty);
            throw new Exception("Test Failure");
        }

        public Task<NuGetVersion> GetLatestPackageVersion(PackageId packageId, CancellationToken cancellationToken, PackageSourceLocation packageSourceLocation = null, bool includePreview = false) => throw new NotImplementedException();
        public Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(PackageId packageId, int numberOfResults, CancellationToken cancellationToken, PackageSourceLocation packageSourceLocation = null, bool includePreview = false) => throw new NotImplementedException();
        public Task<NuGetVersion> GetBestPackageVersionAsync(PackageId packageId, VersionRange versionRange, CancellationToken cancellationToken, PackageSourceLocation packageSourceLocation = null) => throw new NotImplementedException();

        public Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(PackageId packageId,
            VersionRange versionRange, CancellationToken cancellationToken, PackageSourceLocation packageSourceLocation = null) => throw new NotImplementedException();

        public Task<string> GetPackageUrl(PackageId packageId,
            CancellationToken cancellationToken,
            NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("mock-url-" + packageId.ToString());
        }
    }
}
