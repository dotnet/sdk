// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.NET.Build.Containers;

internal sealed class OrasRealmValidator(string registryName, bool isInsecureRegistry) : IRealmValidator
{
    // Validate syntax here. Network-address policy is enforced at connection time by
    // AuthRealmHandlerFactory, including user-configured private-address allowlists.
    public Task<bool> IsRealmAllowedAsync(Uri registryUri, Uri realmUri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidateRealmUri(realmUri.ToString(), registryName, isInsecureRegistry);
        return Task.FromResult(true);
    }

    internal static Uri ValidateRealmUri(string realm, string registryName, bool isInsecureRegistry)
    {
        if (!Uri.TryCreate(realm, UriKind.Absolute, out Uri? realmUri) || !string.IsNullOrEmpty(realmUri.UserInfo))
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

        return realmUri;
    }
}
