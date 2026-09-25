// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Provides shared validation, normalization, and safe failure conversion for detection providers.
/// The utility holds no process state.
/// </summary>
internal static partial class InternalMicrosoftDetectionUtilities
{
    internal const string MicrosoftTenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

    private const string CorpMicrosoftDomainSuffix = ".corp.microsoft.com";

    internal static bool IsWsl(string? distroName, string? interop, string? kernelRelease) =>
        !string.IsNullOrEmpty(distroName) ||
        !string.IsNullOrEmpty(interop) ||
        kernelRelease?.Contains("microsoft", StringComparison.OrdinalIgnoreCase) == true;

    internal static bool TryGetCorporateDomain(string? value, out string? domain)
    {
        domain = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().TrimEnd('.');
        if (!trimmed.EndsWith(CorpMicrosoftDomainSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        domain = NormalizeDomain(trimmed[..^CorpMicrosoftDomainSuffix.Length]);
        return domain is not null;
    }

    internal static string? NormalizeAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return AliasRegex().IsMatch(trimmed) ? trimmed.ToLowerInvariant() : null;
    }

    internal static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return DomainRegex().IsMatch(trimmed) ? trimmed.ToUpperInvariant() : null;
    }

    internal static bool IsGitHubToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        GitHubTokenRegex().IsMatch(value.Trim());

    internal static string? GetEnvironmentVariableFromSetOutput(string output, string variableName)
    {
        var prefix = variableName + "=";
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[prefix.Length..];
            }
        }

        return null;
    }

    internal static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               (value = property.GetString() ?? string.Empty).Length > 0;
    }

    internal static InternalMicrosoftProbeFailure CreateExceptionFailure(Exception exception, string stage) =>
        new(
            InternalMicrosoftProbeFailureCode.Exception,
            stage,
            ExceptionType: GetSafeExceptionType(exception));

    internal static IReadOnlyList<string> ReadGitHubTokensFromJsonFile(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return GitHubTokenValueRegex()
                .Matches(File.ReadAllText(path))
                .Select(match => match.Groups["token"].Value)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? GetSafeExceptionType(Exception exception) =>
        exception switch
        {
            HttpRequestException => nameof(HttpRequestException),
            IOException => nameof(IOException),
            JsonException => nameof(JsonException),
            UnauthorizedAccessException => nameof(UnauthorizedAccessException),
            InvalidOperationException => nameof(InvalidOperationException),
            _ => null
        };

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasRegex();

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainRegex();

    [GeneratedRegex("^(gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})$", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9_])(?<token>gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})(?![A-Za-z0-9_])", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubTokenValueRegex();
}
