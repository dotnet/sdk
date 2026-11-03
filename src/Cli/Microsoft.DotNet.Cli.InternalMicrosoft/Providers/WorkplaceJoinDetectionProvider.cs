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
        var deviceState = CreateValueSet();
        var tenantDetails = CreateValueSet();
        var userState = CreateValueSet();
        var unsectioned = CreateValueSet();
        var workAccounts = new List<IReadOnlyDictionary<string, string>>();
        Dictionary<string, string> currentValues = unsectioned;

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryGetSection(line, out var section))
            {
                switch (section)
                {
                    case DsregSection.DeviceState:
                        currentValues = deviceState;
                        break;
                    case DsregSection.TenantDetails:
                        currentValues = tenantDetails;
                        break;
                    case DsregSection.UserState:
                        currentValues = userState;
                        break;
                    case DsregSection.WorkAccount:
                        currentValues = CreateValueSet();
                        workAccounts.Add(currentValues);
                        break;
                    default:
                        currentValues = CreateValueSet();
                        break;
                }
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator > 0)
            {
                currentValues[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        if (deviceState.Count == 0 && tenantDetails.Count == 0 && userState.Count == 0 && workAccounts.Count == 0)
        {
            deviceState = unsectioned;
            tenantDetails = unsectioned;
            userState = unsectioned;
            workAccounts.Add(unsectioned);
        }

        var candidates = new List<InternalMicrosoftProbeResult>();
        if (IsYes(deviceState, "AzureAdJoined") && HasMicrosoftTenant(tenantDetails))
        {
            var domain = GetCorporateDomain(deviceState, fallbackDomain);
            candidates.Add(new InternalMicrosoftProbeResult(true, null, domain));
        }

        if (IsYes(userState, "WorkplaceJoined"))
        {
            var workplaceEvidence = workAccounts.Count > 0
                ? workAccounts
                : [userState];
            foreach (var workAccount in workplaceEvidence)
            {
                if (HasMicrosoftTenant(workAccount))
                {
                    candidates.Add(GetWorkAccountResult(workAccount));
                }
            }
        }

        return candidates.Count switch
        {
            0 => InternalMicrosoftProbeResult.NotDetected,
            1 => candidates[0],
            _ => SelectUnambiguousIdentity(candidates)
        };
    }

    private static Dictionary<string, string> CreateValueSet() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static bool TryGetSection(string line, out DsregSection section)
    {
        var trimmed = line.Trim();
        var isFramedHeading = trimmed.Length > 2 && trimmed[0] == '|' && trimmed[^1] == '|';
        var heading = trimmed.Trim('|').Trim();
        if (heading.Equals("Device State", StringComparison.OrdinalIgnoreCase))
        {
            section = DsregSection.DeviceState;
            return true;
        }
        if (heading.Equals("Tenant Details", StringComparison.OrdinalIgnoreCase))
        {
            section = DsregSection.TenantDetails;
            return true;
        }
        if (heading.Equals("User State", StringComparison.OrdinalIgnoreCase))
        {
            section = DsregSection.UserState;
            return true;
        }
        if (heading.StartsWith("Work Account ", StringComparison.OrdinalIgnoreCase))
        {
            section = DsregSection.WorkAccount;
            return true;
        }

        section = DsregSection.None;
        return isFramedHeading;
    }

    private static bool IsYes(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) &&
        value.Equals("YES", StringComparison.OrdinalIgnoreCase);

    private static bool HasMicrosoftTenant(IReadOnlyDictionary<string, string> values) =>
        GetFirstValue(values, "TenantId", "Tenant Id", "WorkplaceTenantId", "Workplace Tenant Id")
            ?.Equals(
                InternalMicrosoftDetectionUtilities.MicrosoftTenantId,
                StringComparison.OrdinalIgnoreCase) == true;

    private static string? GetCorporateDomain(
        IReadOnlyDictionary<string, string> values,
        string? fallbackDomain)
    {
        var domainValue = GetFirstValue(
            values,
            "DomainName",
            "Domain Name",
            "OnPremisesDomainName",
            "On Premises Domain Name",
            "OnPremDomainName",
            "UserDnsDomain",
            "User DNS Domain");
        if (InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(domainValue, out var corporateDomain))
        {
            return corporateDomain;
        }

        return InternalMicrosoftDetectionUtilities.TryGetCorporateDomain(
            fallbackDomain,
            out var fallbackCorporateDomain)
                ? fallbackCorporateDomain
                : null;
    }

    private static InternalMicrosoftProbeResult GetWorkAccountResult(
        IReadOnlyDictionary<string, string> values)
    {
        var accountIdentifier = GetFirstValue(
            values,
            "UserEmail",
            "User Email",
            "UserPrincipalName",
            "User Principal Name",
            "UPN");
        return InternalMicrosoftDetectionUtilities.TryGetMicrosoftAccountIdentity(
            accountIdentifier,
            out var alias,
            out var domain)
                ? new InternalMicrosoftProbeResult(true, alias, domain)
                : new InternalMicrosoftProbeResult(true, null, null);
    }

    private static InternalMicrosoftProbeResult SelectUnambiguousIdentity(
        IReadOnlyList<InternalMicrosoftProbeResult> candidates)
    {
        var aliases = candidates
            .Select(candidate => candidate.Alias)
            .Where(alias => alias is not null)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var domains = candidates
            .Select(candidate => candidate.Domain)
            .Where(domain => domain is not null)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        return new InternalMicrosoftProbeResult(
            true,
            aliases.Length == 1 ? aliases[0] : null,
            domains.Length == 1 ? domains[0] : null);
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

    private enum DsregSection
    {
        None,
        DeviceState,
        TenantDetails,
        UserState,
        WorkAccount
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
