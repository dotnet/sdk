// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Reads Microsoft tenant and corporate identity evidence from macOS Platform SSO.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class MacPlatformSsoDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "Mac Platform SSO";
    public int Stage => 1;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => context.IsMacOS;

    public async Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        if (!context.CommandExists(context.MacPlatformSsoPath))
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        var processResult = await context.RunProcessProbeAsync(
            context.MacPlatformSsoPath,
            ["platform", "-s"],
            cancellationToken).ConfigureAwait(false);
        return processResult.Failure is not null
            ? InternalMicrosoftProbeResult.Failed(processResult.Failure)
            : Parse($"{processResult.StandardOutput}{Environment.NewLine}{processResult.StandardError}");
    }

    internal static InternalMicrosoftProbeResult Parse(string output)
    {
        using var deviceConfiguration = TryParseSection(output, "Device Configuration");
        using var loginConfiguration = TryParseSection(output, "Login Configuration");
        using var userConfiguration = TryParseSection(output, "User Configuration");
        if (deviceConfiguration is null || loginConfiguration is null || userConfiguration is null)
        {
            return Failure(
                InternalMicrosoftProbeFailureCode.JsonParse,
                InternalMicrosoftProbeFailureStage.PlatformSso);
        }

        if (!TryGetBoolean(deviceConfiguration.RootElement, "registrationCompleted", out var registrationCompleted))
        {
            return Failure(
                InternalMicrosoftProbeFailureCode.JsonShape,
                InternalMicrosoftProbeFailureStage.PlatformSsoRegistration);
        }

        if (!registrationCompleted)
        {
            return Failure(
                InternalMicrosoftProbeFailureCode.RegistrationIncomplete,
                InternalMicrosoftProbeFailureStage.PlatformSsoRegistration);
        }

        if (!TryGetAbsoluteUri(loginConfiguration.RootElement, "issuer", out var issuer) ||
            !IsMicrosoftTenantEndpoint(issuer, "/v2.0"))
        {
            return Failure(
                InternalMicrosoftProbeFailureCode.TenantMismatch,
                InternalMicrosoftProbeFailureStage.PlatformSsoIssuer);
        }

        if (!TryGetAbsoluteUri(loginConfiguration.RootElement, "keyEndpointURL", out var keyEndpoint) ||
            !IsMicrosoftTenantEndpoint(keyEndpoint, "/getkeydata"))
        {
            return Failure(
                InternalMicrosoftProbeFailureCode.TenantMismatch,
                InternalMicrosoftProbeFailureStage.PlatformSsoKeyEndpoint);
        }

        if (!TryGetAbsoluteUri(loginConfiguration.RootElement, "tokenEndpointURL", out var tokenEndpoint) ||
            !IsMicrosoftTenantEndpoint(tokenEndpoint, "/oauth2/v2.0/token"))
        {
            return Failure(
                InternalMicrosoftProbeFailureCode.TenantMismatch,
                InternalMicrosoftProbeFailureStage.PlatformSsoTokenEndpoint);
        }

        if (!userConfiguration.RootElement.TryGetProperty("kerberosStatus", out var kerberosStatuses) ||
            kerberosStatuses.ValueKind != JsonValueKind.Array ||
            kerberosStatuses.GetArrayLength() == 0)
        {
            return Failure(
                InternalMicrosoftProbeFailureCode.RegistrationIncomplete,
                InternalMicrosoftProbeFailureStage.PlatformSsoIdentity);
        }

        foreach (var kerberosStatus in kerberosStatuses.EnumerateArray())
        {
            if (kerberosStatus.ValueKind != JsonValueKind.Object ||
                !InternalMicrosoftDetectionUtilities.TryGetString(kerberosStatus, "upn", out var upn) ||
                !InternalMicrosoftDetectionUtilities.TryGetString(kerberosStatus, "realm", out var realm) ||
                !InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(realm, out var realmDomain))
            {
                continue;
            }

            var separator = upn.IndexOf('@');
            if (separator > 0 &&
                upn[(separator + 1)..].StartsWith(realmDomain!, StringComparison.OrdinalIgnoreCase) &&
                InternalMicrosoftDetectionUtilities.NormalizeAlias(upn[..separator]) is { } alias)
            {
                return new InternalMicrosoftProbeResult(true, alias, realmDomain);
            }
        }

        return Failure(
            InternalMicrosoftProbeFailureCode.IdentityMismatch,
            InternalMicrosoftProbeFailureStage.PlatformSsoIdentity);
    }

    private static JsonDocument? TryParseSection(string output, string sectionName)
    {
        var header = sectionName + ":";
        var headerIndex = output.IndexOf(header, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            return null;
        }

        var objectStart = output.IndexOf('{', headerIndex + header.Length);
        if (objectStart < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var index = objectStart; index < output.Length; index++)
        {
            var character = output[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
            }
            else if (character == '{')
            {
                depth++;
            }
            else if (character == '}' && --depth == 0)
            {
                try
                {
                    return JsonDocument.Parse(output[objectStart..(index + 1)]);
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static bool TryGetBoolean(JsonElement root, string propertyName, out bool value)
    {
        value = false;
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetAbsoluteUri(JsonElement root, string propertyName, out Uri uri)
    {
        if (InternalMicrosoftDetectionUtilities.TryGetString(root, propertyName, out var value) &&
            Uri.TryCreate(value, UriKind.Absolute, out var parsedUri))
        {
            uri = parsedUri;
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool IsMicrosoftTenantEndpoint(Uri uri, string expectedSuffix) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "login.microsoftonline.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.Equals(
            $"/{InternalMicrosoftDetectionUtilities.MicrosoftTenantId}{expectedSuffix}",
            StringComparison.OrdinalIgnoreCase);

    private static InternalMicrosoftProbeResult Failure(string code, string stage) =>
        InternalMicrosoftProbeResult.Failed(new(code, stage));
}
