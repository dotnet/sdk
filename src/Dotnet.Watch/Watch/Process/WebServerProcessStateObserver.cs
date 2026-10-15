// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Observes the state of the web server by scanning its standard output for known patterns.
/// Notifies when the server starts listening.
/// </summary>
internal static partial class WebServerProcessStateObserver
{
    private static readonly Regex s_nowListeningRegex = GetNowListeningOnRegex();
    private static readonly Regex s_aspireDashboardUrlRegex = GetAspireDashboardUrlRegex();

    [GeneratedRegex(@"Now listening on: (?<url>[^\s]*)\s*$", RegexOptions.Compiled)]
    private static partial Regex GetNowListeningOnRegex();

    [GeneratedRegex(@"Login to the dashboard at (?<url>[^\s]*)\s*$", RegexOptions.Compiled)]
    private static partial Regex GetAspireDashboardUrlRegex();

    public static Action<OutputLine> GetObserver(bool isAspireHost, Action<string> onServerListening)
    {
        var _notified = false;

        return line =>
        {
            if (_notified)
            {
                return;
            }

            var match = (isAspireHost ? s_aspireDashboardUrlRegex : s_nowListeningRegex).Match(line.Content);
            if (!match.Success)
            {
                return;
            }

            var launchUrl = match.Groups["url"].Value;

            // only match valid URLs:
            if (!Uri.TryCreate(launchUrl, UriKind.Absolute, out var validUrl) ||
                (validUrl.Scheme != Uri.UriSchemeHttp && validUrl.Scheme != Uri.UriSchemeHttps))
            {
                return;
            }

            _notified = true;
            onServerListening(launchUrl);
        };
    }
}
