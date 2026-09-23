// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Reads Microsoft tenant evidence from the current Windows Visual Studio account store.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class WindowsVisualStudioAccountDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "Visual Studio Microsoft tenant";
    public int Stage => 1;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => context.IsWindows;

    public Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        var localAppData = context.GetEnvironmentVariable("LOCALAPPDATA");
        return string.IsNullOrEmpty(localAppData)
            ? Task.FromResult(InternalMicrosoftProbeResult.NotDetected)
            : VisualStudioAccountDetectionParser.ReadAccountStoreAsync(
                Path.Combine(localAppData, ".IdentityService", "V3AccountStore.json"),
                cancellationToken);
    }
}

/// <summary>
/// Reads the Windows Visual Studio account store through WSL.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class WslVisualStudioAccountDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "WSL Visual Studio Microsoft tenant";
    public int Stage => 1;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => context.IsWsl;

    public async Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        var processResult = await context.RunProcessProbeAsync(
            "cmd.exe",
            ["/d", "/s", "/c", "if exist \"%LOCALAPPDATA%\\.IdentityService\\V3AccountStore.json\" type \"%LOCALAPPDATA%\\.IdentityService\\V3AccountStore.json\""],
            cancellationToken).ConfigureAwait(false);
        if (processResult.Failure is not null)
        {
            return InternalMicrosoftProbeResult.Failed(processResult.Failure);
        }

        if (string.IsNullOrWhiteSpace(processResult.StandardOutput))
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        try
        {
            using var document = JsonDocument.Parse(processResult.StandardOutput);
            return VisualStudioAccountDetectionParser.Parse(document.RootElement);
        }
        catch (JsonException exception)
        {
            return InternalMicrosoftProbeResult.Failed(
                InternalMicrosoftDetectionUtilities.CreateExceptionFailure(
                    exception,
                    InternalMicrosoftProbeFailureStage.Parse));
        }
    }
}

/// <summary>
/// Parses Visual Studio account stores and selects one unambiguous Microsoft identity.
/// The parser holds no state.
/// </summary>
internal static class VisualStudioAccountDetectionParser
{
    internal static async Task<InternalMicrosoftProbeResult> ReadAccountStoreAsync(
        string accountStorePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(accountStorePath))
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        try
        {
            await using var stream = new FileStream(
                accountStorePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Parse(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return InternalMicrosoftProbeResult.Failed(
                InternalMicrosoftDetectionUtilities.CreateExceptionFailure(
                    exception,
                    InternalMicrosoftProbeFailureStage.Parse));
        }
    }

    internal static InternalMicrosoftProbeResult Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return InternalMicrosoftProbeResult.Failed(new(
                InternalMicrosoftProbeFailureCode.JsonShape,
                InternalMicrosoftProbeFailureStage.AccountStore));
        }

        var personalization = new List<InternalMicrosoftProbeResult>();
        var fallback = new List<InternalMicrosoftProbeResult>();
        var malformedPersonalization = false;
        var malformedFallback = false;
        InternalMicrosoftProbeResult? firstFailure = null;
        foreach (var account in root.EnumerateArray())
        {
            var result = TryParseAccount(account);
            if (result.Failure is not null)
            {
                firstFailure ??= result;
                if (HasMicrosoftIdentityProvider(account))
                {
                    if (TryGetBooleanValue(account, "IsPersonalizationAccount") == true)
                    {
                        malformedPersonalization = true;
                    }
                    else
                    {
                        malformedFallback = true;
                    }
                }
                continue;
            }

            if (!result.IsInternalMicrosoft)
            {
                continue;
            }

            (TryGetBooleanValue(account, "IsPersonalizationAccount") == true ? personalization : fallback).Add(result);
        }

        if (personalization.Count > 0)
        {
            return malformedPersonalization
                ? new InternalMicrosoftProbeResult(true, null, null)
                : SelectUnambiguousIdentity(personalization);
        }

        if (fallback.Count > 0)
        {
            return malformedPersonalization || malformedFallback
                ? new InternalMicrosoftProbeResult(true, null, null)
                : SelectUnambiguousIdentity(fallback);
        }

        return firstFailure ?? InternalMicrosoftProbeResult.NotDetected;
    }

    private static bool HasMicrosoftIdentityProvider(JsonElement account) =>
        account.ValueKind == JsonValueKind.Object &&
        account.TryGetProperty("Properties", out var properties) &&
        properties.ValueKind == JsonValueKind.Object &&
        InternalMicrosoftDetectionUtilities.TryGetString(properties, "IdentityProvider", out var identityProvider) &&
        identityProvider.Equals(
            InternalMicrosoftDetectionUtilities.MicrosoftTenantId,
            StringComparison.OrdinalIgnoreCase);

    private static InternalMicrosoftProbeResult TryParseAccount(JsonElement account)
    {
        if (account.ValueKind != JsonValueKind.Object)
        {
            return JsonShapeFailure(InternalMicrosoftProbeFailureStage.AccountStoreRecord);
        }

        var stale = TryGetBooleanValue(account, "Stale");
        if (stale is null)
        {
            return JsonShapeFailure(InternalMicrosoftProbeFailureStage.AccountStoreRecordStale);
        }
        if (stale.Value)
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        if (!account.TryGetProperty("Properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return JsonShapeFailure(InternalMicrosoftProbeFailureStage.AccountStoreRecordProperties);
        }

        if (!InternalMicrosoftDetectionUtilities.TryGetString(properties, "IdentityProvider", out var identityProvider))
        {
            return JsonShapeFailure(InternalMicrosoftProbeFailureStage.AccountStoreRecordIdentityProvider);
        }
        if (!identityProvider.Equals(
            InternalMicrosoftDetectionUtilities.MicrosoftTenantId,
            StringComparison.OrdinalIgnoreCase))
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        if (!InternalMicrosoftDetectionUtilities.TryGetString(properties, "HomeTenant", out var homeTenant))
        {
            return JsonShapeFailure(InternalMicrosoftProbeFailureStage.AccountStoreRecordHomeTenant);
        }
        if (!homeTenant.Equals(
            InternalMicrosoftDetectionUtilities.MicrosoftTenantId,
            StringComparison.OrdinalIgnoreCase))
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        if (!InternalMicrosoftDetectionUtilities.TryGetString(properties, "IdTokenPayload", out var payload))
        {
            return JsonShapeFailure(InternalMicrosoftProbeFailureStage.IdTokenPayload);
        }

        try
        {
            using var token = JsonDocument.Parse(payload);
            var tokenRoot = token.RootElement;
            if (tokenRoot.ValueKind != JsonValueKind.Object ||
                !InternalMicrosoftDetectionUtilities.TryGetString(tokenRoot, "tid", out var tenantId) ||
                !tenantId.Equals(
                    InternalMicrosoftDetectionUtilities.MicrosoftTenantId,
                    StringComparison.OrdinalIgnoreCase) ||
                !InternalMicrosoftDetectionUtilities.TryGetString(tokenRoot, "iss", out var issuer) ||
                !issuer.Equals(
                    $"https://login.microsoftonline.com/{InternalMicrosoftDetectionUtilities.MicrosoftTenantId}/v2.0",
                    StringComparison.OrdinalIgnoreCase) ||
                !InternalMicrosoftDetectionUtilities.TryGetString(tokenRoot, "preferred_username", out var username) ||
                !TryParseMicrosoftUsername(username, out var alias, out var domain))
            {
                return JsonShapeFailure(InternalMicrosoftProbeFailureStage.IdTokenPayload);
            }

            return new InternalMicrosoftProbeResult(true, alias, domain);
        }
        catch (JsonException)
        {
            return InternalMicrosoftProbeResult.Failed(new(
                InternalMicrosoftProbeFailureCode.JsonParse,
                InternalMicrosoftProbeFailureStage.IdTokenPayload,
                ExceptionType: nameof(JsonException)));
        }
    }

    private static InternalMicrosoftProbeResult SelectUnambiguousIdentity(
        IReadOnlyList<InternalMicrosoftProbeResult> candidates)
    {
        var first = candidates[0];
        return candidates.Skip(1).All(candidate =>
            string.Equals(candidate.Alias, first.Alias, StringComparison.Ordinal) &&
            string.Equals(candidate.Domain, first.Domain, StringComparison.Ordinal))
                ? first
                : new InternalMicrosoftProbeResult(true, null, null);
    }

    private static InternalMicrosoftProbeResult JsonShapeFailure(string stage) =>
        InternalMicrosoftProbeResult.Failed(new(InternalMicrosoftProbeFailureCode.JsonShape, stage));

    private static bool? TryGetBooleanValue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;

    private static bool TryParseMicrosoftUsername(string username, out string? alias, out string? domain)
    {
        alias = null;
        domain = null;
        var separator = username.IndexOf('@');
        if (separator <= 0 || separator == username.Length - 1)
        {
            return false;
        }

        var suffix = username[(separator + 1)..];
        if (!suffix.EndsWith("microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        alias = InternalMicrosoftDetectionUtilities.NormalizeAlias(username[..separator]);
        domain = InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(suffix, out var corporateDomain)
            ? corporateDomain
            : null;
        return alias is not null;
    }
}
