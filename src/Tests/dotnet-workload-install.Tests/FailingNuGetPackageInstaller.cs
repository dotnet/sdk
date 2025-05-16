// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ToolPackage;
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

        public Task<string> DownloadPackageAsync(PackageId packageId, NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false,
            bool? includeUnlisted = null,
            DirectoryPath? downloadFolder = null,
            PackageSourceMapping packageSourceMapping = null)
        {
            var mockPackagePath = Path.Combine(MockPackageDir, $"{packageId}.{packageVersion}.nupkg");
            File.WriteAllText(mockPackagePath, string.Empty);
            return Task.FromResult(mockPackagePath);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
        {
            Directory.CreateDirectory(targetFolder.Value);
            File.WriteAllText(Path.Combine(targetFolder.Value, "testfile.txt"), string.Empty);
            throw new Exception("Test Failure");
        }

        public Task<NuGetVersion> GetLatestPackageVersion(PackageId packageId, PackageSourceLocation packageSourceLocation = null, bool includePreview = false) => throw new NotImplementedException();
        public Task<NuGetVersion> GetBestPackageVersionAsync(PackageId packageId, VersionRange versionRange, PackageSourceLocation packageSourceLocation = null) => throw new NotImplementedException();
        public Task<string> GetPackageUrl(PackageId packageId,
            NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false)
        {
            return Task.FromResult("mock-url-" + packageId.ToString());
        }
    }
}
