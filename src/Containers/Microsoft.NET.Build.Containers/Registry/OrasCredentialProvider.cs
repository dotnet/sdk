// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.NET.Build.Containers.Credentials;
using OrasProject.Oras.Registry.Remote.Auth;
using Valleysoft.DockerCredsProvider;

namespace Microsoft.NET.Build.Containers;

internal sealed class OrasCredentialProvider(RegistryMode mode) : ICredentialProvider
{
    public async Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DockerCredentials credentials;
        if (GetDockerCredentialsFromEnvironment(mode) is (string username, string password))
        {
            credentials = new DockerCredentials(username, password);
        }
        else
        {
            credentials = await GetLoginCredentialsAsync(hostname).ConfigureAwait(false);
        }

        return new Credential(
            credentials.Username,
            credentials.Password,
            RefreshToken: credentials.IdentityToken);
    }

    internal static (string username, string password)? GetDockerCredentialsFromEnvironment(RegistryMode mode)
    {
        if (mode == RegistryMode.Push)
        {
            return TryGetCredentialsFromEnvironment(ContainerHelpers.PushHostObjectUser, ContainerHelpers.PushHostObjectPass)
                ?? TryGetCredentialsFromEnvironment(ContainerHelpers.HostObjectUser, ContainerHelpers.HostObjectPass)
                ?? TryGetCredentialsFromEnvironment(ContainerHelpers.HostObjectUserLegacy, ContainerHelpers.HostObjectPassLegacy);
        }

        if (mode == RegistryMode.Pull)
        {
            return TryGetCredentialsFromEnvironment(ContainerHelpers.PullHostObjectUser, ContainerHelpers.PullHostObjectPass);
        }

        if (mode == RegistryMode.PullFromOutput)
        {
            return TryGetCredentialsFromEnvironment(ContainerHelpers.PullHostObjectUser, ContainerHelpers.PullHostObjectPass)
                ?? TryGetCredentialsFromEnvironment(ContainerHelpers.HostObjectUser, ContainerHelpers.HostObjectPass)
                ?? TryGetCredentialsFromEnvironment(ContainerHelpers.HostObjectUserLegacy, ContainerHelpers.HostObjectPassLegacy);
        }

        throw new InvalidEnumArgumentException(nameof(mode), (int)mode, typeof(RegistryMode));
    }

    private static (string username, string password)? TryGetCredentialsFromEnvironment(string usernameVariable, string passwordVariable)
    {
        string? username = Environment.GetEnvironmentVariable(usernameVariable);
        string? password = Environment.GetEnvironmentVariable(passwordVariable);
        return !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
            ? (username, password)
            : null;
    }

    private static async Task<DockerCredentials> GetLoginCredentialsAsync(string registry)
    {
        if (registry is "docker.io" or "registry-1.docker.io")
        {
            try
            {
                return await CredsProvider.GetCredentialsAsync("https://index.docker.io/v1/").ConfigureAwait(false);
            }
            catch
            {
            }
        }

        try
        {
            return await CredsProvider.GetCredentialsAsync(registry).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new CredentialRetrievalException(registry, e);
        }
    }
}
