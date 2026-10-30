// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;
using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Shared command-line parsing for <see cref="DotnetAccessMode"/> values. CLI tokens are derived
/// from the enum names (lowercased), so the enum stays the single source of truth for the modes.
/// </summary>
internal static class AccessModeCliParsing
{
    private static readonly DotnetAccessMode[] s_modes = Enum.GetValues<DotnetAccessMode>();

    /// <summary>The tokens for the modes supported on the current platform.</summary>
    public static IReadOnlyList<string> SupportedTokens { get; } = s_modes
        .Where(DotnetAccessModePolicy.IsSupportedOnCurrentPlatform)
        .Select(ToToken)
        .ToArray();

    /// <summary>The supported tokens formatted for help text, e.g. <c>'none', 'shell'</c>.</summary>
    public static string FormatSupportedTokens() => string.Join(", ", SupportedTokens.Select(m => $"'{m}'"));

    public static string ToToken(DotnetAccessMode mode) => mode.ToString().ToLowerInvariant();

    /// <summary>
    /// Custom parser for an access-mode argument or option. Returns null when no token was given,
    /// and records a parse error for unknown tokens or modes unsupported on this platform.
    /// </summary>
    public static DotnetAccessMode? Parse(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var token = result.Tokens[0].Value.ToLowerInvariant();
        foreach (var mode in s_modes)
        {
            if (!string.Equals(ToToken(mode), token, StringComparison.Ordinal))
            {
                continue;
            }

            if (!DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(mode))
            {
                return AddError(result, string.Format(CultureInfo.InvariantCulture, Strings.EnvModeWindowsOnly, token));
            }

            return mode;
        }

        return AddError(result, string.Format(
            CultureInfo.InvariantCulture,
            Strings.EnvSetUnknownMode,
            token,
            string.Join(", ", SupportedTokens)));
    }

    private static DotnetAccessMode? AddError(ArgumentResult result, string message)
    {
        result.AddError(message);
        return null;
    }
}
