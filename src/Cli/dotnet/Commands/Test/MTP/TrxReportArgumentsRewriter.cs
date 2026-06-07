// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Security.Cryptography;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// When the SDK is orchestrating multiple test modules and the user enabled the Microsoft.Testing.Platform
/// TRX report extension with only <c>--report-trx</c> (no explicit file name), each module would otherwise
/// fall back to the platform's default file name and the modules would race to the same file. This helper
/// injects a unique-per-module <c>--report-trx-filename</c> for that single case only.
/// </summary>
/// <remarks>
/// When the user already supplied <c>--report-trx-filename</c> (any form), we leave the arguments alone and
/// let Microsoft.Testing.Platform handle the file name — including its own overwrite warning. The SDK does
/// not interpret MTP filename placeholders or rewrite user-provided values.
/// </remarks>
internal static class TrxReportArgumentsRewriter
{
    private const string ReportTrxOption = "--report-trx";
    private const string ReportTrxFilenameOption = "--report-trx-filename";
    private const string ReportTrxFilenameOptionWithEquals = ReportTrxFilenameOption + "=";
    private const string TrxExtension = ".trx";

    /// <summary>
    /// Returns a copy of <paramref name="arguments"/> with <c>--report-trx-filename &lt;unique&gt;</c>
    /// appended when (and only when) more than one module is being run, <c>--report-trx</c> is present,
    /// and the user did not specify <c>--report-trx-filename</c>.
    /// </summary>
    /// <param name="utcNow">
    /// Override for the timestamp embedded in the injected file name. Defaults to <see cref="DateTimeOffset.UtcNow"/>.
    /// </param>
    public static List<string> RewriteIfNeeded(IReadOnlyList<string> arguments, TestModule module, bool isMultiTestModule, DateTimeOffset? utcNow = null)
    {
        if (!isMultiTestModule)
        {
            return [.. arguments];
        }

        bool hasReportTrx = false;
        bool hasReportTrxFilename = false;

        foreach (string arg in arguments)
        {
            if (string.Equals(arg, ReportTrxOption, StringComparison.Ordinal))
            {
                hasReportTrx = true;
            }
            else if (string.Equals(arg, ReportTrxFilenameOption, StringComparison.Ordinal)
                || arg.StartsWith(ReportTrxFilenameOptionWithEquals, StringComparison.Ordinal))
            {
                hasReportTrxFilename = true;
            }
        }

        // Only inject when the user enabled TRX reporting via --report-trx but did not name the file
        // themselves. Any user-supplied --report-trx-filename is respected verbatim; MTP is responsible
        // for the resulting behavior (including its own overwrite warning).
        if (!hasReportTrx || hasReportTrxFilename)
        {
            return [.. arguments];
        }

        var rewritten = new List<string>(arguments.Count + 2);
        rewritten.AddRange(arguments);
        rewritten.Add(ReportTrxFilenameOption);
        rewritten.Add(BuildInjectedFileName(module, utcNow ?? DateTimeOffset.UtcNow));
        return rewritten;
    }

    private static string BuildInjectedFileName(TestModule module, DateTimeOffset utcNow)
    {
        string assemblyName = SanitizeForFileName(Path.GetFileNameWithoutExtension(module.TargetPath));
        string disambiguator = SanitizeForFileNameOrNull(module.TargetFramework)
            ?? ComputeShortHash(module.TargetPath);

        // Filename-safe, sortable, high-precision timestamp so back-to-back `dotnet test` invocations
        // don't trip MTP's "Trx file '...' already exists and will be overwritten." warning on re-runs.
        string timestamp = utcNow.ToString("yyyy-MM-dd_HH-mm-ss.fffffff", DateTimeFormatInfo.InvariantInfo);
        return assemblyName + "_" + disambiguator + "_" + timestamp + TrxExtension;
    }

    private static string ComputeShortHash(string value)
    {
        // 8 hex chars of SHA-256 is plenty to disambiguate parallel modules within one `dotnet test`
        // invocation. Used only when TargetFramework is unavailable (e.g. --test-modules glob).
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        var builder = new StringBuilder(8);
        for (int i = 0; i < 4; i++)
        {
            builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
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
