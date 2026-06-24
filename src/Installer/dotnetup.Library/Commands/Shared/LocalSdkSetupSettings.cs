// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Describes the sdk properties dotnetup should write for project-local SDK resolution.
/// Keeps roll-forward decisions separate from file mutation so they can be tested independently.
/// </summary>
internal sealed record LocalSdkSetupSettings(
    string SdkVersion,
    string? RollForward,
    bool UpdateRollForward,
    bool? AllowPrerelease,
    bool UpdateAllowPrerelease)
{
    public static LocalSdkSetupSettings Create(
        string? requestedChannel,
        bool globalJsonExisted,
        GlobalJsonInfo globalJsonInfo,
        ReleaseVersion resolvedVersion)
    {
        bool commandSelectedVersion = !string.IsNullOrWhiteSpace(requestedChannel) || !globalJsonExisted;
        string? rollForward = commandSelectedVersion
            ? GetRollForwardForRequestedChannel(requestedChannel ?? ChannelVersionResolver.LatestChannel)
            : globalJsonInfo.RollForward;

        bool isPrerelease = !string.IsNullOrEmpty(resolvedVersion.Prerelease);
        bool? allowPrerelease = commandSelectedVersion
            ? isPrerelease ? true : null
            : isPrerelease ? true : globalJsonInfo.AllowPrerelease;

        return new LocalSdkSetupSettings(
            resolvedVersion.ToString(),
            rollForward,
            UpdateRollForward: commandSelectedVersion || rollForward is not null,
            allowPrerelease,
            UpdateAllowPrerelease: commandSelectedVersion || isPrerelease || globalJsonInfo.AllowPrerelease is not null);
    }

    internal static string GetRollForwardForRequestedChannel(string requestedChannel)
    {
        var channel = new UpdateChannel(requestedChannel);
        if (channel.IsFullySpecifiedVersion())
        {
            return "disable";
        }

        string channelName = UpdateChannel.StripDailySuffix(requestedChannel);
        if (ChannelVersionResolver.KnownChannelKeywords.Any(keyword =>
            string.Equals(keyword, channelName, StringComparison.OrdinalIgnoreCase)))
        {
            return "latestPatch";
        }

        var parts = channelName.Split('.');
        if (parts.Length == 1 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return "latestMinor";
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return "latestFeature";
        }

        return "latestPatch";
    }
}
