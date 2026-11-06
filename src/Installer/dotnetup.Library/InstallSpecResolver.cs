// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Resolves saved specs against installations in the same root and architecture.
/// Reads current repository requirements without modifying specs or installations.
/// Callers handle inactive registrations and report errors.
/// </summary>
internal static class InstallSpecResolver
{
    public static InstallSpecResolution Resolve(InstallSpec spec, IEnumerable<Installation> installations)
    {
        var result = Evaluate(spec);
        if (result.Spec is { } requirement)
        {
            result.Installation = SelectInstallation(spec.Component, requirement, installations,
                i => i.Component, i => ReleaseVersion.TryParse(i.Version, out var version) ? version : null);
        }
        return result;
    }

    public static T? SelectInstallation<T>(InstallComponent component, UpdateChannel requirement,
        IEnumerable<T> installations, Func<T, InstallComponent> getComponent, Func<T, ReleaseVersion?> getVersion)
        where T : class =>
        installations.Where(i => getComponent(i) == component)
            .Select(i => (Installation: i, Version: getVersion(i)))
            .Where(i => i.Version is { } version && requirement.Matches(version))
            .OrderByDescending(i => i.Version)
            .Select(i => i.Installation)
            .FirstOrDefault();

    public static InstallSpecResolution Evaluate(InstallSpec spec)
    {
        if (spec.InstallSource != InstallSource.GlobalJson)
        {
            return new InstallSpecResolution { Spec = new UpdateChannel(spec.VersionOrChannel) };
        }

        try
        {
            if (string.IsNullOrEmpty(spec.GlobalJsonPath))
            {
                throw new JsonException(Strings.GlobalJsonMissingPath);
            }
            var sdk = GlobalJsonChannelResolver.ReadSdkSection(spec.GlobalJsonPath);
            return new InstallSpecResolution
            {
                IsActive = sdk is not null,
                GlobalJsonSdk = sdk,
                Spec = sdk is not null ? GlobalJsonChannelResolver.CreateChannel(sdk) : null
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new InstallSpecResolution { IsActive = false };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new InstallSpecResolution
            {
                Spec = spec.MinimumVersion is { } minimum && spec.AllowPrerelease is { } previews
                    ? new UpdateChannel(spec.VersionOrChannel, new ReleaseVersion(minimum), previews) : null,
                Error = string.Format(CultureInfo.InvariantCulture, Strings.InstallSpecRequirementError,
                    spec.GlobalJsonPath, ex.Message)
            };
        }
    }
}
