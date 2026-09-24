// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Reads Microsoft tenant evidence from the current Windows workplace join state.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class WindowsWorkplaceJoinDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "Windows workplace join";
    public int Stage => 2;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => context.IsWindows;

    public async Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        var processResult = await context.RunProcessProbeAsync(
            "dsregcmd",
            ["/status"],
            cancellationToken).ConfigureAwait(false);
        return processResult.Failure is not null
            ? InternalMicrosoftProbeResult.Failed(processResult.Failure)
            : Parse(processResult.StandardOutput, context.GetEnvironmentVariable("USERDNSDOMAIN"));
    }

    internal static InternalMicrosoftProbeResult Parse(string output, string? fallbackDomain = null)
    {
        var tenantMatchesMicrosoft = false;
        var workplaceJoined = false;
        var azureAdJoined = false;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            values[key] = value;

            if (key.Equals("TenantId", StringComparison.OrdinalIgnoreCase))
            {
                tenantMatchesMicrosoft = value.Equals(
                    InternalMicrosoftDetectionUtilities.MicrosoftTenantId,
                    StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("WorkplaceJoined", StringComparison.OrdinalIgnoreCase))
            {
                workplaceJoined = value.Equals("YES", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("AzureAdJoined", StringComparison.OrdinalIgnoreCase))
            {
                azureAdJoined = value.Equals("YES", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!tenantMatchesMicrosoft || (!workplaceJoined && !azureAdJoined))
        {
            return InternalMicrosoftProbeResult.NotDetected;
        }

        var accountIdentifier = GetFirstValue(
            values,
            "UserEmail",
            "User Email",
            "UserPrincipalName",
            "User Principal Name",
            "UPN");
        var alias = accountIdentifier is null
            ? null
            : InternalMicrosoftDetectionUtilities.NormalizeAlias(accountIdentifier.Split('@', 2)[0]);
        var domainValue = GetFirstValue(
            values,
            "DomainName",
            "Domain Name",
            "OnPremisesDomainName",
            "On Premises Domain Name",
            "OnPremDomainName",
            "UserDnsDomain",
            "User DNS Domain");
        var domain = InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(domainValue, out var corporateDomain)
            ? corporateDomain
            : InternalMicrosoftDetectionUtilities.NormalizeDomain(domainValue);
        if (domain is null &&
            InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(fallbackDomain, out var fallbackCorporateDomain))
        {
            domain = fallbackCorporateDomain;
        }

        return new InternalMicrosoftProbeResult(true, alias, domain);
    }

    private static string? GetFirstValue(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>
/// Reads Windows workplace join state through WSL and checks for Microsoft tenant evidence.
/// The detector reuses this stateless provider for process-wide detection.
/// </summary>
internal sealed class WslWindowsWorkplaceJoinDetectionProvider : IInternalMicrosoftDetectionProvider
{
    public string Name => "WSL Windows workplace join";
    public int Stage => 2;

    public bool IsSupported(InternalMicrosoftDetectionContext context) => context.IsWsl;

    public async Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken)
    {
        var processResult = await context.RunProcessProbeAsync(
            "cmd.exe",
            ["/d", "/s", "/c", "dsregcmd /status"],
            cancellationToken).ConfigureAwait(false);
        return processResult.Failure is not null
            ? InternalMicrosoftProbeResult.Failed(processResult.Failure)
            : WindowsWorkplaceJoinDetectionProvider.Parse(processResult.StandardOutput);
    }
}
