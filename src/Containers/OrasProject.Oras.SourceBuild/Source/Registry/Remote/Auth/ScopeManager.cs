// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/ScopeManager.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// ScopeManager tracks authorization scopes required for registry requests.
/// </summary>
public class ScopeManager
{
    /// <summary>
    /// A thread-safe dictionary that maps a string key to a sorted set of <see cref="Scope"/> objects.
    /// This is used to manage and organize scopes in a concurrent environment.
    /// </summary>
    private ConcurrentDictionary<string, SortedSet<Scope>> Scopes { get; } = new();

    /// <summary>
    /// GetScopeKey generates the storage key for scope state from the registry host and an
    /// optional <paramref name="partitionId"/>, mirroring <see cref="Cache"/>'s partitioning
    /// scheme. Pipe (|) is used as the delimiter since it cannot appear in registry hostnames.
    /// When <paramref name="partitionId"/> is null or empty, the key is the registry alone,
    /// preserving the default (unpartitioned) behavior.
    /// </summary>
    /// <param name="registry">The registry host.</param>
    /// <param name="partitionId">Optional scope partition identifier.</param>
    /// <returns>A composite key isolating scope state per (partitionId, registry).</returns>
    private static string GetScopeKey(string registry, string? partitionId) =>
        string.IsNullOrEmpty(partitionId) ? registry : $"{partitionId}|{registry}";

    /// <summary>
    /// GetScopesForHost returns a sorted set of scopes for the given registry within the scope
    /// partition identified by <paramref name="partitionId"/> if found, otherwise, returns an
    /// empty sorted set. Scope state is isolated per (partitionId, registry).
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="partitionId">
    /// Scope partition identifier isolating scopes per ID, mirroring the token
    /// <see cref="Cache"/> partitioning. A null or empty value selects the default partition.
    /// Required (pass null explicitly for the default) so a forgotten argument cannot silently
    /// bleed scope state across partitions.
    /// </param>
    /// <returns></returns>
    public SortedSet<Scope> GetScopesForHost(string registry, string? partitionId)
    {
        return Scopes.TryGetValue(GetScopeKey(registry, partitionId), out var scopes) ? scopes : new();
    }

    /// <summary>
    /// GetScopesStringForHost returns a list of scope strings for the given registry within the
    /// scope partition identified by <paramref name="partitionId"/> if found, otherwise, returns
    /// empty list. Scope state is isolated per (partitionId, registry).
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="partitionId">
    /// Scope partition identifier. A null or empty value selects the default partition. Required
    /// (pass null explicitly for the default) so a forgotten argument cannot silently bleed scope
    /// state across partitions.
    /// </param>
    /// <returns></returns>
    public List<string> GetScopesStringForHost(string registry, string? partitionId)
    {
        return Scopes.TryGetValue(GetScopeKey(registry, partitionId), out var scopes)
            ? scopes.Select(scope => scope.ToString()).ToList()
            : new();
    }

    /// <summary>
    /// SetActionsForRepository sets actions for the given repository within the scope partition
    /// identified by <paramref name="partitionId"/> if the client is auth.Client.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="reference"></param>
    /// <param name="partitionId">
    /// Scope partition identifier. A null or empty value selects the default partition.
    /// </param>
    /// <param name="actions"></param>
    public static void SetActionsForRepository(
        IClient client,
        Reference reference,
        string? partitionId,
        params Scope.Action[] actions)
    {
        if (client is Client authClient)
        {
            authClient.ScopeManager.SetActionsForRepository(reference, partitionId, actions);
        }
    }

    /// <summary>
    /// SetActionsForRepository sets the actions for a repository by creating a scope and
    /// associating it with the specified registry within the scope partition identified by
    /// <paramref name="partitionId"/>.
    /// </summary>
    /// <param name="reference">
    /// The reference object containing the registry and repository information.
    /// </param>
    /// <param name="partitionId">
    /// Scope partition identifier. A null or empty value selects the default partition.
    /// </param>
    /// <param name="actions">
    /// The actions to be associated with the repository scope.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the registry or repository in the <paramref name="reference"/> is null.
    /// </exception>
    public void SetActionsForRepository(Reference reference, string? partitionId, params Scope.Action[] actions)
    {
        var registry = reference.Registry
            ?? throw new ArgumentNullException(nameof(reference), "Reference.Registry is null.");
        var repository = reference.Repository
            ?? throw new ArgumentNullException(nameof(reference), "Reference.Repository is null.");

        var scope = new Scope(
            resourceType: "repository",
            resourceName: repository,
            actions: actions.Select(Scope.ActionToString).ToHashSet(StringComparer.OrdinalIgnoreCase));

        SetScopeForRegistry(registry, scope, partitionId);
    }

    /// <summary>
    /// SetScopeForRegistry sets scope for the given registry within the scope partition
    /// identified by <paramref name="partitionId"/> when the client is auth.Client.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="registry"></param>
    /// <param name="scope"></param>
    /// <param name="partitionId">
    /// Scope partition identifier. A null or empty value selects the default partition. Required
    /// (pass null explicitly for the default) so a forgotten argument cannot silently bleed scope
    /// state across partitions.
    /// </param>
    public static void SetScopeForRegistry(IClient client, string registry, Scope scope, string? partitionId)
    {
        if (client is Client authClient)
        {
            authClient.ScopeManager.SetScopeForRegistry(registry, scope, partitionId);
        }
    }

    /// <summary>
    /// SetScopeForRegistry sets the scope for a specific registry within the scope partition
    /// identified by <paramref name="partitionId"/>. If the scope contains the "All" action, it
    /// ensures that only the "All" action is retained. Otherwise, it merges the actions of the
    /// provided scope with any existing scope for the (partitionId, registry).
    /// </summary>
    /// <param name="registry">The registry for which the scope is being set.</param>
    /// <param name="scope">The scope to be set for the registry, including its actions.</param>
    /// <param name="partitionId">
    /// Scope partition identifier. A null or empty value selects the default partition, preserving
    /// the original unpartitioned behavior. Required (pass null explicitly for the default) so a
    /// forgotten argument cannot silently bleed scope state across partitions.
    /// </param>
    public void SetScopeForRegistry(string registry, Scope scope, string? partitionId)
    {
        Scopes.AddOrUpdate(GetScopeKey(registry, partitionId),
            _ => new SortedSet<Scope> { CloneAndNormalizeScope(scope) },
            (_, existingScopes) =>
            {
                var updatedScopes = CloneScopes(existingScopes);
                Scope.AddOrMergeScope(updatedScopes, CloneAndNormalizeScope(scope));
                return updatedScopes;
            });
    }

    private static SortedSet<Scope> CloneScopes(SortedSet<Scope> scopes)
    {
        var clonedScopes = new SortedSet<Scope>();
        foreach (var scope in scopes)
        {
            clonedScopes.Add(scope.Clone());
        }
        return clonedScopes;
    }

    private static Scope CloneAndNormalizeScope(Scope scope)
    {
        var clonedScope = scope.Clone();
        if (clonedScope.Actions.Contains(Scope.ActionWildcard))
        {
            clonedScope.Actions.Clear();
            clonedScope.Actions.Add(Scope.ActionWildcard);
        }
        return clonedScope;
    }
}
