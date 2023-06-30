// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.ShellShim
{
    internal class ShellShimTemplateFinder
    {
        private readonly DirectoryPath _tempDir;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly PackageSourceLocation _packageSourceLocation;

        public ShellShimTemplateFinder(
            INuGetPackageDownloader nugetPackageDownloader,
            DirectoryPath tempDir,
            PackageSourceLocation packageSourceLocation)
        {
            _tempDir = tempDir;
            _nugetPackageDownloader = nugetPackageDownloader;
            _packageSourceLocation = packageSourceLocation;
        }

        public async Task<string> ResolveAppHostSourceDirectoryAsync(string archOption, NuGetFramework targetFramework, Architecture arch)
        {
            string rid;
            var validRids = new string[] { "win-x64", "win-arm64", "osx-x64", "osx-arm64" };
            if (string.IsNullOrEmpty(archOption))
            {
                if (targetFramework != null &&
                    (((targetFramework.Version.Major < 6 && OperatingSystem.IsMacOS()) ||
                    (targetFramework.Version.Major < 5 && OperatingSystem.IsWindows()))
                    && !arch.Equals(Architecture.X64)))
                {
                    rid = OperatingSystem.IsWindows() ? "win-x64" : "osx-x64";
                }
                else
                {
                    // Use the default app host
                    return GetDefaultAppHostSourceDirectory();
                }
            }
            else
            {
                rid = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(null, archOption);
            }

            if (!validRids.Contains(rid))
            {
                throw new GracefulException(string.Format(LocalizableStrings.InvalidRuntimeIdentifier, rid, string.Join(" ", validRids)));
            }

            var packageId = new PackageId($"microsoft.netcore.app.host.{rid}");
            var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(packageId, packageSourceLocation: _packageSourceLocation);
            var content = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, _tempDir);

            return Path.Combine(_tempDir.Value, "runtimes", rid, "native");
        }

        public static string GetDefaultAppHostSourceDirectory()
        {
            return Path.Combine(AppContext.BaseDirectory, "AppHostTemplate");
        }
    }
}
