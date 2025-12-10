// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;
using static Microsoft.DotNet.Cli.Commands.Tool.Restore.ToolRestoreCommand;

namespace Microsoft.DotNet.Cli.Commands.Tool.Restore;

internal class ToolPackageRestorer
{
    private readonly IToolPackageDownloader _toolPackageDownloader;
    private readonly string[] _additionalSources;
    private readonly string[] _overrideSources;
    private readonly VerbosityOptions _verbosity;
    private readonly RestoreActionConfig _restoreActionConfig;

    private readonly ILocalToolsResolverCache _localToolsResolverCache;
    private readonly IFileSystem _fileSystem;



    public ToolPackageRestorer(IToolPackageDownloader toolPackageDownloader,
                               string[] additionalSources,
                               string[] overrideSources,
                               VerbosityOptions verbosity,
                                RestoreActionConfig restoreActionConfig,
                               ILocalToolsResolverCache? localToolsResolverCache = null,
                               IFileSystem? fileSystem = null)
    {
        _toolPackageDownloader = toolPackageDownloader;
        _additionalSources = additionalSources;
        _overrideSources = overrideSources;
        _verbosity = verbosity;
        _restoreActionConfig = restoreActionConfig;

        _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
        _fileSystem = fileSystem ?? new FileSystemWrapper();
    }

    public ToolRestoreResult InstallPackage(
        ToolManifestPackage package,
        FilePath? configFile)
    {
        string targetFramework = BundledTargetFramework.GetTargetFrameworkMoniker();

        if (PackageHasBeenRestored(package, targetFramework))
        {
            return ToolRestoreResult.Success(
                saveToCache: null,
                message: string.Format(
                    CliCommandStrings.RestoreSuccessful, package.PackageId,
                    package.Version.ToNormalizedString(), string.Join(", ", package.CommandNames)));
        }

        try
        {
            IToolPackage toolPackage =
                _toolPackageDownloader.InstallPackage(
                    new PackageLocation(
                        nugetConfig: configFile,
                        additionalFeeds: _additionalSources,
                        sourceFeedOverrides: _overrideSources,
                        rootConfigDirectory: package.FirstEffectDirectory),
                    package.PackageId,
                    verbosity: _verbosity,
                    ToVersionRangeWithOnlyOneVersion(package.Version),
                    targetFramework,
                    restoreActionConfig: _restoreActionConfig
                    );

            if (!ManifestCommandMatchesActualInPackage(package.CommandNames, [toolPackage.Command]))
            {
                return ToolRestoreResult.Failure(
                    string.Format(CliCommandStrings.CommandsMismatch,
                        JoinBySpaceWithQuote(package.CommandNames.Select(c => c.Value.ToString())),
                        package.PackageId,
                        toolPackage.Command.Name));
            }

            // Check for newer versions and prepare warning message
            string warning = CheckForNewerVersion(package, configFile);

            return ToolRestoreResult.Success(
                saveToCache:
                    (new RestoredCommandIdentifier(
                        toolPackage.Id,
                        toolPackage.Version,
                        NuGetFramework.Parse(targetFramework),
                        Constants.AnyRid,
                        toolPackage.Command.Name),
                    toolPackage.Command),
                message: string.Format(
                    CliCommandStrings.RestoreSuccessful,
                    package.PackageId,
                    package.Version.ToNormalizedString(),
                    string.Join(" ", package.CommandNames)),
                warning: warning);
        }
        catch (ToolPackageException e)
        {
            return ToolRestoreResult.Failure(package.PackageId, e);
        }
    }

    private static bool ManifestCommandMatchesActualInPackage(
                            ToolCommandName[] commandsFromManifest,
                            IReadOnlyList<ToolCommand> toolPackageCommands)
    {
        ToolCommandName[] commandsFromPackage = [.. toolPackageCommands.Select(t => t.Name)];
return !commandsFromManifest.Any(cmd => !commandsFromPackage.Contains(cmd)) && !commandsFromPackage.Any(cmd => !commandsFromManifest.Contains(cmd));
    }

    public bool PackageHasBeenRestored(
        ToolManifestPackage package,
        string targetFramework)
    {
        var sampleRestoredCommandIdentifierOfThePackage = new RestoredCommandIdentifier(
            package.PackageId,
            package.Version,
            NuGetFramework.Parse(targetFramework),
            Constants.AnyRid,
            package.CommandNames.First());

        return _localToolsResolverCache.TryLoad(
                   sampleRestoredCommandIdentifierOfThePackage,
                   out var toolCommand)
               && _fileSystem.File.Exists(toolCommand.Executable.Value);
    }

    private string CheckForNewerVersion(ToolManifestPackage package, FilePath? configFile)
    {
        try
        {
            // Use wildcard version range to get the latest version
            var latestVersionRange = VersionRange.Parse("*");
            
            var (latestVersion, _) = _toolPackageDownloader.GetNuGetVersion(
                new PackageLocation(
                    nugetConfig: configFile,
                    additionalFeeds: _additionalSources,
                    sourceFeedOverrides: _overrideSources,
                    rootConfigDirectory: package.FirstEffectDirectory),
                package.PackageId,
                _verbosity,
                latestVersionRange,
                _restoreActionConfig);

            // Compare versions - only warn if there's a newer stable version or if the manifest uses prerelease
            if (latestVersion != null && latestVersion > package.Version)
            {
                // If the current version is prerelease, show warning for any newer version
                // If the current version is stable, only show warning for newer stable versions
                if (package.Version.IsPrerelease || !latestVersion.IsPrerelease)
                {
                    return string.Format(CliCommandStrings.RestoreNewVersionAvailable, package.PackageId, latestVersion.ToNormalizedString());
                }
            }
        }
        catch
        {
            // If we can't check for newer versions, don't show a warning
            // This could happen due to network issues, package source problems, etc.
        }

        return string.Empty;
    }

    private static string JoinBySpaceWithQuote(IEnumerable<object> objects)
    {
        return string.Join(" ", objects.Select(o => $"\"{o.ToString()}\""));
    }

    private static VersionRange ToVersionRangeWithOnlyOneVersion(NuGetVersion version)
    {
        return new VersionRange(
            version,
            includeMinVersion: true,
            maxVersion: version,
            includeMaxVersion: true);
    }
}

