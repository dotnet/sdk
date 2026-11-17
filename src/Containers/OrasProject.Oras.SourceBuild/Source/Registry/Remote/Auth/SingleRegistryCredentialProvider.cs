// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/SingleRegistryCredentialProvider.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Provides an implementation of <see cref="ICredentialProvider"/> that returns
/// a static credential for a specific registry hostname.
/// </summary>
public sealed class SingleRegistryCredentialProvider : ICredentialProvider
{
    private readonly string _registry;
    private readonly Credential _credential;

    /// <summary>
    /// Initializes an implementation of <see cref="ICredentialProvider"/> with a static credential
    /// for a specified registry.
    /// </summary>
    /// <param name="registry">The registry hostname for which credentials will be provided.</param>
    /// <param name="credential">The credential to use for the specified registry.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="registry"/> is null or empty, or 
    /// when <paramref name="credential"/> is empty.
    /// </exception>
    /// <remarks>
    /// If the registry is "docker.io", it will be automatically converted to "registry-1.docker.io"
    /// to match expected Docker registry behavior.
    /// </remarks>
    public SingleRegistryCredentialProvider(string registry, Credential credential)
    {
        if (string.IsNullOrWhiteSpace(registry))
        {
            throw new ArgumentException(
                "The registry name cannot be null or empty.", nameof(registry));
        }
        if (credential.IsEmpty())
        {
            throw new ArgumentException("The credential cannot be empty.", nameof(credential));
        }
        if (string.Equals(registry, "docker.io", StringComparison.OrdinalIgnoreCase))
        {
            // It is expected that traffic targeting "docker.io" will be redirected to
            // "registry-1.docker.io"
            // Reference: https://github.com/moby/moby/blob/v24.0.0-beta.2/registry/config.go#L25-L48
            registry = "registry-1.docker.io";
        }

        _registry = registry;
        _credential = credential;
    }

    /// <summary>
    /// Resolves credentials for the specified registry hostname.
    /// </summary>
    /// <param name="registry">The registry hostname to retrieve credentials for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the configured 
    /// credential if the hostname matches the registry, otherwise returns an empty credential.
    /// </returns>
    public Task<Credential> ResolveCredentialAsync(
        string registry,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<Credential>(cancellationToken);
        }

        if (string.Equals(registry, _registry, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(_credential);
        }
        return Task.FromResult(CredentialExtensions.EmptyCredential);
    }
}
