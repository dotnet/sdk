// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Resolves and applies the user's UI language for dotnetup.
///
/// dotnetup is published with <c>InvariantGlobalization=true</c> only on Linux, where libicu is
/// not guaranteed to be present (running non-invariant there crashes on startup with
/// "Couldn't find a valid ICU package"). Windows and macOS ship globalization in-box, so the
/// binary runs non-invariant there and the runtime initializes
/// <see cref="CultureInfo.CurrentUICulture"/> from the operating system automatically.
///
/// This type therefore:
/// <list type="bullet">
/// <item>Applies the standard .NET CLI overrides (<c>DOTNET_CLI_UI_LANGUAGE</c> / <c>VSLANG</c>)
/// on every platform, since the runtime never honors those automatically.</item>
/// <item>Detects the OS locale from the POSIX environment on Linux only, the single platform
/// where invariant mode suppresses the runtime's own detection.</item>
/// </list>
/// On Linux the project also sets <c>PredefinedCulturesOnly=false</c> so the detected culture can
/// be created by name (it carries invariant formatting data but retains its name, which is what
/// drives satellite-resource lookup).
/// </summary>
internal static class DotnetupUILanguage
{
    // POSIX precedence for the message/UI locale: LC_ALL overrides everything, then the
    // message category, then LANG as the catch-all default.
    private static readonly string[] s_posixLocaleVariables = ["LC_ALL", "LC_MESSAGES", "LANG"];

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
    /// Resolves the UI culture to apply, or <c>null</c> when there is no explicit override and the
    /// running platform's runtime has already initialized the UI culture (Windows/macOS), or none
    /// could be determined.
    /// </summary>
    internal static CultureInfo? ResolveUICulture()
    {
        // Explicit override (DOTNET_CLI_UI_LANGUAGE > VSLANG) applies on every platform.
        CultureInfo? overridden = UILanguageOverride.GetOverriddenUILanguage();
        if (overridden is not null)
        {
            return overridden;
        }

        // Only Linux runs invariant, so it is the only platform where the runtime did not already
        // pick up the OS locale. Windows and macOS leave CurrentUICulture as the runtime set it.
        return OperatingSystem.IsLinux() ? CreateCulture(GetUnixLocaleName()) : null;
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
