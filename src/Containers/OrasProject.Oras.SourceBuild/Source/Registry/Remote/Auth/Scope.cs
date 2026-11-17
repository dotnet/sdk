// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/Scope.cs
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Scope represents an authorization scope in the form
/// <c>{ResourceType}:{ResourceName}:{Action},{Action},...</c>.
/// Reference: https://distribution.github.io/distribution/spec/auth/scope/
/// </summary>
public class Scope : IComparable<Scope>
{
    /// <summary>
    /// Defines the possible actions that can be performed on a resource.
    /// </summary>
    public enum Action
    {
        /// <summary>
        /// Represents the "pull" action.
        /// </summary>
        Pull,

        /// <summary>
        /// Represents the "push" action.
        /// </summary>
        Push,

        /// <summary>
        /// Represents the "delete" action.
        /// </summary>
        Delete,

        /// <summary>
        /// Represents all actions (wildcard '*').
        /// </summary>
        All
    }

    /// <summary>
    /// ScopeRegistryCatalog is the scope for registry catalog access.
    /// </summary>
    public const string ScopeRegistryCatalog = "registry:catalog:*";

    internal const string ActionWildcard = "*";

    internal const string ActionPull = "pull";

    internal const string ActionPush = "push";

    internal const string ActionDelete = "delete";

    /// <summary>
    /// Gets the resource type of the scope, e.g. <c>repository</c>.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the resource name of the scope, e.g. the repository name.
    /// </summary>
    public required string ResourceName { get; init; }

    /// <summary>
    /// Gets the set of actions granted by the scope, e.g. <c>pull</c> and <c>push</c>.
    /// </summary>
    public required HashSet<string> Actions { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scope"/> class.
    /// </summary>
    /// <param name="resourceType">The resource type of the scope.</param>
    /// <param name="resourceName">The resource name of the scope.</param>
    /// <param name="actions">The set of actions granted by the scope.</param>
    [SetsRequiredMembers]
    public Scope(string resourceType, string resourceName, HashSet<string> actions)
    {
        ResourceType = resourceType;
        ResourceName = resourceName;
        Actions = actions;
    }

    internal Scope Clone()
    {
        return new Scope(
            ResourceType,
            ResourceName,
            new HashSet<string>(Actions, Actions.Comparer));
    }

    /// <summary>
    /// ToString converts the scope to its string representation in the format:
    /// "ResourceType:ResourceName:Action1,Action2,...".
    /// If the wildcard action '*' is present, it will be represented as "ResourceType:ResourceName:*".
    /// </summary>
    /// <returns>A string representation of the scope.</returns>
    public override string ToString()
    {
        return Actions.Contains(ActionWildcard)
            ? $"{ResourceType}:{ResourceName}:{ActionWildcard}"
            : $"{ResourceType}:{ResourceName}:{string.Join(",", Actions.OrderBy(action => action, StringComparer.OrdinalIgnoreCase))}";
    }

    /// <summary>
    /// TryParse attempts to parse a scope string into a <see cref="Scope"/> object.
    /// </summary>
    /// <param name="scopeStr">The scope string to parse.</param>
    /// <param name="scope">
    /// When this method returns, contains the parsed <see cref="Scope"/> object if the parsing succeeded;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string scopeStr, [NotNullWhen(true)] out Scope? scope)
    {
        if (string.IsNullOrWhiteSpace(scopeStr))
        {
            scope = null;
            return false;
        }

        return TryParse(scopeStr.AsSpan(), out scope);
    }

    internal static bool TryParse(ReadOnlySpan<char> scopeSpan, [NotNullWhen(true)] out Scope? scope)
    {
        scope = null;
        if (!TryGetScopeParts(
                scopeSpan,
                out var resourceType,
                out var resourceName,
                out var actionSpan))
        {
            return false;
        }

        if (!TryParseActions(actionSpan, out var actions))
        {
            return false;
        }

        if (actions.Contains(ActionWildcard))
        {
            actions.Clear();
            actions.Add(ActionWildcard);
        }

        scope = new Scope(
            resourceType.ToString(),
            resourceName.ToString(),
            actions);
        return true;
    }

    private static bool TryGetScopeParts(
        ReadOnlySpan<char> scopeSpan,
        out ReadOnlySpan<char> resourceType,
        out ReadOnlySpan<char> resourceName,
        out ReadOnlySpan<char> actions)
    {
        resourceType = [];
        resourceName = [];
        actions = [];

        scopeSpan = scopeSpan.Trim();
        if (scopeSpan.IsEmpty)
        {
            return false;
        }

        var firstSeparator = scopeSpan.IndexOf(':');
        if (firstSeparator < 0)
        {
            return false;
        }

        var remainingScope = scopeSpan[(firstSeparator + 1)..];
        var secondSeparator = remainingScope.IndexOf(':');
        if (secondSeparator < 0)
        {
            return false;
        }

        resourceType = scopeSpan[..firstSeparator].Trim();
        resourceName = remainingScope[..secondSeparator].Trim();
        actions = remainingScope[(secondSeparator + 1)..];
        return !resourceType.IsEmpty
            && !resourceName.IsEmpty
            && !actions.Trim().IsEmpty
            && !actions.Contains(':');
    }

    private static bool TryParseActions(
        ReadOnlySpan<char> actionSpan,
        [NotNullWhen(true)] out HashSet<string>? actions)
    {
        actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var separatorIndex = actionSpan.IndexOf(',');
            var action = separatorIndex < 0
                ? actionSpan
                : actionSpan[..separatorIndex];
            action = action.Trim();
            if (action.IsEmpty)
            {
                actions = null;
                return false;
            }

            actions.Add(action.ToString());

            if (separatorIndex < 0)
            {
                return true;
            }

            actionSpan = actionSpan[(separatorIndex + 1)..];
        }
    }

    /// <summary>
    /// ActionToString converts an <see cref="Action"/> enumeration value to its corresponding string representation.
    /// </summary>
    /// <param name="action">The <see cref="Action"/> value to be converted.</param>
    /// <returns>
    /// A string representation of the specified <paramref name="action"/>:
    /// <list type="bullet">
    /// <item><description>"pull" for <see cref="Action.Pull"/></description></item>
    /// <item><description>"push" for <see cref="Action.Push"/></description></item>
    /// <item><description>"delete" for <see cref="Action.Delete"/></description></item>
    /// <item><description>"*" for <see cref="Action.All"/></description></item>
    /// <item><description>An empty string for any other value.</description></item>
    /// </list>
    /// </returns>
    internal static string ActionToString(Action action)
    {
        return action switch
        {
            Action.Pull => ActionPull,
            Action.Push => ActionPush,
            Action.Delete => ActionDelete,
            Action.All => ActionWildcard,
            _ => ""
        };
    }

    /// <summary>
    /// AddOrMergeScope adds a new scope to the collection or merges it with an existing scope if a matching scope is found.
    /// </summary>
    /// <param name="scopes">A sorted set of existing scopes.</param>
    /// <param name="newScope">The new scope to add or merge.</param>
    /// <returns>
    /// The updated sorted set of scopes, with the new scope added or merged.
    /// </returns>
    /// <remarks>
    /// If a matching scope already exists in the set:
    /// - If either the existing scope or the new scope contains the wildcard action '*',
    ///   the existing scope's actions are cleared and replaced with the wildcard '*'.
    /// - Otherwise, the actions of the new scope are unioned with the existing scope's actions.
    /// If no matching scope exists, the new scope is added to the set.
    /// </remarks>
    public static SortedSet<Scope> AddOrMergeScope(SortedSet<Scope> scopes, Scope newScope)
    {
        if (scopes.TryGetValue(newScope, out var existingScope))
        {
            // Mutates the matched scope in place.
            MergeActions(existingScope, newScope);
        }
        else
        {
            // Add the new scope if no matching scope exists
            scopes.Add(newScope);
        }
        return scopes;
    }

    /// <summary>
    /// AddOrMergeScopeCopyOnWrite merges <paramref name="newScope"/> into <paramref name="scopes"/> without
    /// mutating any existing <see cref="Scope"/> instance: a matched scope is replaced with a merged clone. This
    /// lets callers merge into a shallow set copy whose <see cref="Scope"/> instances are shared with another set.
    /// </summary>
    /// <param name="scopes">A sorted set of existing scopes.</param>
    /// <param name="newScope">The new scope to add or merge.</param>
    internal static void AddOrMergeScopeCopyOnWrite(SortedSet<Scope> scopes, Scope newScope)
    {
        if (scopes.TryGetValue(newScope, out var existingScope))
        {
            // Copy-on-write: replace the shared existing scope with a merged clone
            // rather than mutating it in place.
            scopes.Remove(existingScope);
            var mergedScope = existingScope.Clone();
            MergeActions(mergedScope, newScope);
            scopes.Add(mergedScope);
        }
        else
        {
            scopes.Add(newScope);
        }
    }

    private static void MergeActions(Scope target, Scope source)
    {
        if (target.Actions.Contains(ActionWildcard) || source.Actions.Contains(ActionWildcard))
        {
            // If either scope has the wildcard '*', clear and add '*'
            target.Actions.Clear();
            target.Actions.Add(ActionWildcard);
        }
        else
        {
            // Otherwise, union the actions
            target.Actions.UnionWith(source.Actions);
        }
    }

    /// <summary>
    /// CompareTo is to implement the method defined in the interface in IComparable.
    ///
    /// Note: This comparer is intended for use with SortedSet&lt;Scope&gt; in the ScopeManager.
    /// If two Scope instances have the same ResourceType and ResourceName,
    /// they are considered equivalent and will be merged into a single entry by uniting their Actions.
    /// 
    /// It compares the current <see cref="Scope"/> object with another <see cref="Scope"/> object.
    /// The comparison is based on the <see cref="ResourceType"/> and <see cref="ResourceName"/> properties.
    /// </summary>
    /// <param name="other">The other <see cref="Scope"/> object to compare to.</param>
    /// <returns>
    /// A value less than zero if this instance is less than <paramref name="other"/>;
    /// zero if this instance is equal to <paramref name="other"/>;
    /// a value greater than zero if this instance is greater than <paramref name="other"/>.
    /// </returns>
    public int CompareTo(Scope? other)
    {
        if (other == null) return 1; // Current instance is greater than null.

        // Compare ResourceType first
        int resourceTypeComparison = string.Compare(ResourceType, other.ResourceType, StringComparison.Ordinal);
        if (resourceTypeComparison != 0)
        {
            return resourceTypeComparison;
        }

        // If ResourceType is equal, compare ResourceName
        int resourceNameComparison = string.Compare(ResourceName, other.ResourceName, StringComparison.Ordinal);
        if (resourceNameComparison != 0)
        {
            return resourceNameComparison;
        }

        return 0;
    }
}
