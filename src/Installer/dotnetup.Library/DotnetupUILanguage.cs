// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Resolves and applies the user's UI language for dotnetup.
///
/// dotnetup is published with <c>InvariantGlobalization=true</c> on every platform except Windows
/// and macOS, which are the only ones known to ship globalization in-box. Elsewhere (Linux,
/// including musl/Alpine, FreeBSD, etc.) libicu is not guaranteed, and running non-invariant there
/// crashes on startup with "Couldn't find a valid ICU package". On the invariant platforms the
/// runtime no longer initializes <see cref="CultureInfo.CurrentUICulture"/> from the operating
/// system, so it would otherwise always be the invariant (English) culture.
///
/// This type therefore:
/// <list type="bullet">
/// <item>On Windows/macOS (non-invariant), applies only the standard .NET CLI override
/// (<c>DOTNET_CLI_UI_LANGUAGE</c> / <c>VSLANG</c>), since the runtime already initialized the UI
/// culture and <see cref="CultureInfo"/>'s parent fallback works.</item>
/// <item>On the invariant platforms, where invariant mode disables both the runtime's OS-locale
/// detection and <see cref="CultureInfo.Parent"/> fallback, resolves the desired locale (the same
/// CLI override, else the POSIX environment) and maps it onto an exact shipped satellite culture
/// via <see cref="MatchSupportedLanguage"/>.</item>
/// </list>
/// On the invariant platforms the project also sets <c>PredefinedCulturesOnly=false</c> so the
/// matched culture can be created by name (it carries invariant formatting data but retains its
/// name, which is what drives satellite-resource lookup). The Windows/macOS-vs-rest split here
/// mirrors the <c>InvariantGlobalization</c> condition in dotnetup.csproj.
/// </summary>
internal static class DotnetupUILanguage
{
    // POSIX precedence for the message/UI locale: LC_ALL overrides everything, then the
    // message category, then LANG as the catch-all default.
    private static readonly string[] s_posixLocaleVariables = ["LC_ALL", "LC_MESSAGES", "LANG"];

    // The UI languages dotnetup ships satellite resources for. In invariant mode the runtime
    // provides no parent/neutral culture fallback, so the applied culture must exactly match one
    // of these names; MatchSupportedLanguage maps an OS/CLI locale onto the right entry.
    // Keep in sync with the Strings.*.xlf files — DotnetupUILanguageTests enforces this.
    private static readonly string[] s_supportedUILanguages =
    [
        "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant"
    ];

    /// <summary>The UI languages dotnetup ships localized resources for (satellite culture names).</summary>
    internal static IReadOnlyList<string> SupportedUILanguages => s_supportedUILanguages;

    // Windows and macOS run non-invariant (they ship globalization in-box); every other platform
    // runs invariant. Mirrors the InvariantGlobalization condition in dotnetup.csproj.
    internal static bool PlatformRunsInvariant => !OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS();

    /// <summary>
    /// Detects the user's UI language and applies it to the current process so localized
    /// resources resolve.
    /// </summary>
    public static void Setup()
    {
        CultureInfo? uiCulture = ResolveUICulture();
        if (uiCulture is not null)
        {
            CultureInfo.CurrentUICulture = uiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        }
    }

    /// <summary>
    /// Resolves the UI culture to apply, or <c>null</c> to leave the existing default in place
    /// (no shipped language matched, or Windows/macOS where the runtime already initialized it).
    /// </summary>
    internal static CultureInfo? ResolveUICulture()
    {
        // Windows and macOS run non-invariant: the runtime initialized CurrentUICulture from the
        // OS and CultureInfo's parent fallback works, so only the explicit CLI override is applied.
        if (!PlatformRunsInvariant)
        {
            return UILanguageOverride.GetOverriddenUILanguage();
        }

        // Invariant platforms (Linux, etc.): there is no parent/neutral fallback, so the applied
        // culture must exactly match a shipped satellite. Resolve the desired locale with the same
        // override resolver as the rest of the .NET CLI (DOTNET_CLI_UI_LANGUAGE > VSLANG), else the
        // POSIX environment, then map it onto a satellite. (A VSLANG LCID cannot be turned into a
        // named culture in invariant mode, so it resolves to empty and falls through to the
        // environment.)
        string? desired = UILanguageOverride.GetOverriddenUILanguage()?.Name;
        if (string.IsNullOrWhiteSpace(desired))
        {
            desired = GetUnixLocaleName();
        }

        return CreateCulture(MatchSupportedLanguage(desired));
    }

    /// <summary>
    /// Maps a BCP-47 culture name (e.g. <c>fr-FR</c>, <c>zh-CN</c>) onto the closest shipped UI
    /// language, emulating the specific→neutral resource fallback that invariant mode disables.
    /// Returns the matched satellite culture name, or <c>null</c> when none is shipped.
    /// </summary>
    internal static string? MatchSupportedLanguage(string? cultureName)
    {
        if (string.IsNullOrEmpty(cultureName))
        {
            return null;
        }

        foreach (string candidate in GetFallbackCandidates(cultureName))
        {
            foreach (string supported in s_supportedUILanguages)
            {
                if (string.Equals(candidate, supported, StringComparison.OrdinalIgnoreCase))
                {
                    return supported;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Yields candidate culture names from most to least specific: the name as given, the
    /// script-qualified form for Chinese (whose satellites are script- rather than region-based),
    /// then the bare language. ResourceManager would normally walk this chain via
    /// <see cref="CultureInfo.Parent"/>, but that chain collapses straight to invariant in
    /// invariant mode, so it is reconstructed here.
    /// </summary>
    private static IEnumerable<string> GetFallbackCandidates(string cultureName)
    {
        yield return cultureName;

        string[] parts = cultureName.Split('-');
        string language = parts[0];

        if (string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"zh-{GetChineseScript(parts)}";
        }

        if (parts.Length > 1)
        {
            yield return language;
        }
    }

    /// <summary>
    /// Determines the Chinese script (<c>Hans</c>/<c>Hant</c>) for a <c>zh*</c> culture: an
    /// explicit script subtag wins, otherwise it is inferred from the region, defaulting to
    /// Simplified.
    /// </summary>
    private static string GetChineseScript(string[] parts)
    {
        // The script subtag, when present, is the second BCP-47 subtag (e.g. zh-Hant, zh-Hant-TW).
        if (parts.Length > 1)
        {
            if (string.Equals(parts[1], "Hans", StringComparison.OrdinalIgnoreCase))
            {
                return "Hans";
            }

            if (string.Equals(parts[1], "Hant", StringComparison.OrdinalIgnoreCase))
            {
                return "Hant";
            }
        }

        string region = parts.Length > 1 ? parts[^1].ToUpperInvariant() : string.Empty;
        return region switch
        {
            "TW" or "HK" or "MO" => "Hant",
            _ => "Hans",
        };
    }

    private static string? GetUnixLocaleName()
    {
        foreach (string variable in s_posixLocaleVariables)
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(value))
            {
                return NormalizePosixLocale(value);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a POSIX locale string (e.g. <c>fr_FR.UTF-8@euro</c>) into a .NET culture name
    /// (<c>fr-FR</c>). Returns <c>null</c> for the C/POSIX locales, which map to invariant.
    /// </summary>
    internal static string? NormalizePosixLocale(string value)
    {
        // Strip the encoding (".UTF-8") and modifier ("@euro") suffixes.
        int encodingIndex = value.IndexOf('.', StringComparison.Ordinal);
        if (encodingIndex >= 0)
        {
            value = value[..encodingIndex];
        }

        int modifierIndex = value.IndexOf('@', StringComparison.Ordinal);
        if (modifierIndex >= 0)
        {
            value = value[..modifierIndex];
        }

        if (value.Length == 0
            || value.Equals("C", StringComparison.OrdinalIgnoreCase)
            || value.Equals("POSIX", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Replace('_', '-');
    }

    private static CultureInfo? CreateCulture(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        try
        {
            return new CultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
