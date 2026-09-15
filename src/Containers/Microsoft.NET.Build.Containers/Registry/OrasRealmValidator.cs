// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.NET.Build.Containers.Resources;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.NET.Build.Containers;

internal sealed class OrasRealmValidator(string registryName, bool isInsecureRegistry) : IRealmValidator
{
    // A registry controls the bearer-token realm in its authentication challenge. Block token
    // requests to local and private addresses unless an explicitly insecure registry points back
    // to itself, preserving the SDK's existing protection against credential-forwarding SSRF.
    private static readonly IPNetwork[] BlockedV4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("224.0.0.0/24"),
    ];

    public Task<bool> IsRealmAllowedAsync(Uri registryUri, Uri realmUri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            ValidateRealmUri(realmUri.ToString(), registryName, isInsecureRegistry);
            return Task.FromResult(true);
        }
        catch (InvalidAuthResponseException)
        {
            return Task.FromResult(false);
        }
    }

    internal static Uri ValidateRealmUri(string realm, string registryName, bool isInsecureRegistry)
    {
        if (!Uri.TryCreate(realm, UriKind.Absolute, out Uri? realmUri))
        {
            throw new InvalidAuthResponseException(
                registryName,
                Resource.FormatString(nameof(Strings.InvalidAuthResponse_RelativeOrUnparseableRealm), realm));
        }

        bool schemeAllowed = realmUri.Scheme switch
        {
            "https" => true,
            "http" => isInsecureRegistry,
            _ => false,
        };
        if (!schemeAllowed)
        {
            throw new InvalidAuthResponseException(
                registryName,
                Resource.FormatString(nameof(Strings.InvalidAuthResponse_DisallowedScheme), realm, realmUri.Scheme));
        }

        string realmHost = TrimTrailingDot(realmUri.IdnHost);
        if (IPAddress.TryParse(realmHost, out IPAddress? realmIp) && IsBlockedIpLiteral(realmIp))
        {
            if (!(isInsecureRegistry && RegistryHostMatchesIp(registryName, realmIp)))
            {
                throw new InvalidAuthResponseException(
                    registryName,
                    Resource.FormatString(nameof(Strings.InvalidAuthResponse_PrivateIpLiteralRealm), realm, realmHost));
            }
        }
        else if (IsLoopbackDnsName(realmHost)
            && !(isInsecureRegistry && RegistryIsLoopbackEquivalent(registryName)))
        {
            throw new InvalidAuthResponseException(
                registryName,
                Resource.FormatString(nameof(Strings.InvalidAuthResponse_PrivateIpLiteralRealm), realm, realmHost));
        }

        return realmUri;
    }

    private static bool IsLoopbackDnsName(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static string TrimTrailingDot(string host) =>
        host.Length > 1 && host[^1] == '.' ? host[..^1] : host;

    private static bool RegistryIsLoopbackEquivalent(string registryName)
    {
        if (!Uri.TryCreate($"https://{registryName}", UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string host = TrimTrailingDot(uri.IdnHost);
        return IsLoopbackDnsName(host)
            || (IPAddress.TryParse(host, out IPAddress? ip) && IPAddress.IsLoopback(ip));
    }

    private static bool IsBlockedIpLiteral(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }
        if (ip.IsIPv4MappedToIPv6)
        {
            return IsBlockedIpLiteral(ip.MapToIPv4());
        }

        foreach (IPNetwork network in BlockedV4Networks)
        {
            if (network.Contains(ip))
            {
                return true;
            }
        }

        return ip.Equals(IPAddress.IPv6Any)
            || ip.IsIPv6LinkLocal
            || ip.IsIPv6SiteLocal
            || ip.IsIPv6UniqueLocal
            || (ip.IsIPv6Multicast && (ip.GetAddressBytes()[1] & 0x0f) == 0x02);
    }

    private static bool RegistryHostMatchesIp(string registryName, IPAddress ip)
    {
        if (!Uri.TryCreate($"https://{registryName}", UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string host = TrimTrailingDot(uri.IdnHost);
        if (IPAddress.IsLoopback(ip) && IsLoopbackDnsName(host))
        {
            return true;
        }

        return IPAddress.TryParse(host, out IPAddress? registryIp) && registryIp.Equals(ip);
    }
}
