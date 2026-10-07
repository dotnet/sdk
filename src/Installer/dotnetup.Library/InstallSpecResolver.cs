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
            result.Installation = installations
                .Where(i => i.Component == spec.Component
                    && ReleaseVersion.TryParse(i.Version, out var version) && requirement.Matches(version))
                .OrderByDescending(i => new ReleaseVersion(i.Version)).FirstOrDefault();
        }
        return result;
    }

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
                Error = string.Format(CultureInfo.InvariantCulture, Strings.InstallSpecRequirementError,
                    spec.GlobalJsonPath, ex.Message)
            };
        }
    }
}
