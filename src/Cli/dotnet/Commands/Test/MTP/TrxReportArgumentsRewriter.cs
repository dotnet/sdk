// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Disambiguates the file name passed to Microsoft.Testing.Platform's TRX report extension when the SDK is
/// orchestrating multiple test modules in parallel, so each module writes to its own file.
/// Single-module runs are left untouched.
/// </summary>
/// <remarks>
/// The TRX report extension introduced filename placeholders (e.g. <c>{asm}</c>, <c>{tfm}</c>, <c>{pname}</c>,
/// <c>{pid}</c>, <c>{time}</c>) only in Microsoft.Testing.Platform 2.3.0-preview.26274.1
/// (microsoft/testfx#8223). To stay compatible with older versions of the platform that may be referenced by
/// user test projects, the SDK substitutes the values itself rather than forwarding placeholder tokens.
/// </remarks>
internal static class TrxReportArgumentsRewriter
{
    private const string ReportTrxOption = "--report-trx";
    private const string ReportTrxFilenameOption = "--report-trx-filename";
    private const string TrxExtension = ".trx";

    // Placeholders that are guaranteed unique per test module process. If a user already includes one of
    // these in their --report-trx-filename value we don't rewrite the filename ourselves.
    // Notably {tfm} is NOT in this list because two modules can share the same TFM (the original bug).
    private static readonly string[] UniquePerModulePlaceholders = ["{asm}", "{pname}", "{pid}"];

    /// <summary>
    /// Returns a possibly-rewritten copy of <paramref name="arguments"/> with a unique
    /// <c>--report-trx-filename</c> per module when multiple modules are being run.
    /// </summary>
    /// <param name="utcNow">
    /// Override for the timestamp embedded in the injected file name when the user passed
    /// <c>--report-trx</c> without a file name. Defaults to <see cref="DateTimeOffset.UtcNow"/>.
    /// </param>
    public static List<string> RewriteIfNeeded(IReadOnlyList<string> arguments, TestModule module, bool isMultiTestModule, DateTimeOffset? utcNow = null)
    {
        if (!isMultiTestModule)
        {
            return [.. arguments];
        }

        int trxFlagIndex = -1;
        int trxFilenameIndex = -1;
        string? trxFilenameValue = null;
        bool trxFilenameUsesEqualsForm = false;

        for (int i = 0; i < arguments.Count; i++)
        {
            string arg = arguments[i];

            if (string.Equals(arg, ReportTrxOption, StringComparison.Ordinal))
            {
                trxFlagIndex = i;
            }
            else if (string.Equals(arg, ReportTrxFilenameOption, StringComparison.Ordinal))
            {
                trxFilenameIndex = i;
                trxFilenameUsesEqualsForm = false;
                trxFilenameValue = i + 1 < arguments.Count ? arguments[i + 1] : null;
            }
            else if (arg.StartsWith(ReportTrxFilenameOption + "=", StringComparison.Ordinal))
            {
                trxFilenameIndex = i;
                trxFilenameUsesEqualsForm = true;
                trxFilenameValue = arg.Substring(ReportTrxFilenameOption.Length + 1);
            }
        }

        // TRX reporting was not requested at all - nothing to do.
        if (trxFlagIndex < 0 && trxFilenameIndex < 0)
        {
            return [.. arguments];
        }

        // User opted into per-process placeholders that already disambiguate the file name.
        if (trxFilenameValue is not null && ContainsUniquePlaceholder(trxFilenameValue))
        {
            return [.. arguments];
        }

        string assemblyName = SanitizeForFileName(Path.GetFileNameWithoutExtension(module.TargetPath));
        string? targetFramework = SanitizeForFileNameOrNull(GetTargetFrameworkShortName(module));

        var rewritten = new List<string>(arguments.Count + 2);
        rewritten.AddRange(arguments);

        if (trxFilenameValue is not null)
        {
            string newValue = AppendUniquenessSuffix(trxFilenameValue, assemblyName, targetFramework);
            if (trxFilenameUsesEqualsForm)
            {
                rewritten[trxFilenameIndex] = ReportTrxFilenameOption + "=" + newValue;
            }
            else
            {
                rewritten[trxFilenameIndex + 1] = newValue;
            }
        }
        else
        {
            // Only --report-trx was provided (no filename). Inject one so each module gets a unique file
            // instead of relying on MTP's default <user>_<machine>_<time>.trx which collides under parallel runs.
            // Include a timestamp so re-running `dotnet test` doesn't trip MTP's
            // "Trx file '...' already exists and will be overwritten" warning on every module.
            string injectedName = BuildInjectedFileName(assemblyName, targetFramework, utcNow ?? DateTimeOffset.UtcNow);
            rewritten.Add(ReportTrxFilenameOption);
            rewritten.Add(injectedName);
        }

        return rewritten;
    }

    private static bool ContainsUniquePlaceholder(string value)
    {
        foreach (string placeholder in UniquePerModulePlaceholders)
        {
            if (value.Contains(placeholder, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string AppendUniquenessSuffix(string originalFileName, string assemblyName, string? targetFramework)
    {
        string? directory = Path.GetDirectoryName(originalFileName);
        string baseName = Path.GetFileNameWithoutExtension(originalFileName);
        string extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = TrxExtension;
        }

        string suffix = BuildUniquenessSuffix(assemblyName, targetFramework);
        string newBaseName = string.IsNullOrEmpty(baseName) ? suffix.TrimStart('_') : baseName + suffix;
        string newFileName = newBaseName + extension;

        return string.IsNullOrEmpty(directory) ? newFileName : Path.Combine(directory, newFileName);
    }

    private static string BuildInjectedFileName(string assemblyName, string? targetFramework, DateTimeOffset utcNow)
    {
        // Filename-safe, sortable, second precision. Sufficient to distinguish back-to-back
        // `dotnet test` invocations and avoid MTP's "file already exists" warning on re-runs.
        string timestamp = utcNow.ToString("yyyy-MM-dd_HH_mm_ss", DateTimeFormatInfo.InvariantInfo);
        return string.IsNullOrEmpty(targetFramework)
            ? assemblyName + "_" + timestamp + TrxExtension
            : assemblyName + "_" + targetFramework + "_" + timestamp + TrxExtension;
    }

    private static string BuildUniquenessSuffix(string assemblyName, string? targetFramework)
        => string.IsNullOrEmpty(targetFramework)
            ? "_" + assemblyName
            : "_" + assemblyName + "_" + targetFramework;

    private static string? GetTargetFrameworkShortName(TestModule module)
    {
        if (!string.IsNullOrEmpty(module.TargetFramework))
        {
            return module.TargetFramework;
        }

        // --test-modules path: TargetFramework isn't known, try to infer from the target path
        // (e.g. bin/Debug/net9.0/Foo.dll → "net9.0", bin/Debug/net8.0/win-x64/Foo.dll → "net8.0").
        if (string.IsNullOrEmpty(module.TargetPath))
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(module.TargetPath);
        while (!string.IsNullOrEmpty(directory))
        {
            string segment = Path.GetFileName(directory);
            if (!string.IsNullOrEmpty(segment) && TryParseFrameworkFolder(segment, out string? shortName))
            {
                return shortName;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static bool TryParseFrameworkFolder(string segment, out string? shortName)
    {
        shortName = null;

        // Pre-filter to avoid NuGetFramework.ParseFolder treating RID-like segments (e.g. "win-x64",
        // "linux-x64") as valid frameworks. All modern .NET TFM folder names start with "net".
        if (!segment.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            NuGetFramework framework = NuGetFramework.ParseFolder(segment);
            if (framework is not null && !framework.IsUnsupported)
            {
                shortName = framework.GetShortFolderName();
                return true;
            }
        }
        catch
        {
            // Fall through to no match.
        }

        return false;
    }

    private static string? SanitizeForFileNameOrNull(string? value)
        => string.IsNullOrEmpty(value) ? null : SanitizeForFileName(value);

    private static string SanitizeForFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        if (value.IndexOfAny(invalid) < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return builder.ToString();
    }
}
