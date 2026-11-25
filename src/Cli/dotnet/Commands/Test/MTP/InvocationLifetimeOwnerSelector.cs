// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal enum InvocationLifetimeOwnerSelectionStatus
{
    /// <summary>No module declares an invocation lifetime handler; every module self-hosts (Phase 1).</summary>
    NotRequested,

    /// <summary>An owner was selected and coordination may be attempted.</summary>
    Selected,

    /// <summary>Handlers are declared, but no single module can own all of them.</summary>
    Ambiguous,

    /// <summary>The selected owner cannot use the named-pipe transport and its control channel.</summary>
    UnsupportedTransport,
}

internal sealed record InvocationLifetimeOwnerSelection(
    InvocationLifetimeOwnerSelectionStatus Status,
    TestModule? Owner = null);

/// <summary>
/// Selects the module that owns invocation-lifetime handlers before any module is launched
/// (microsoft/testfx RFC 024, section 8.1).
/// </summary>
internal static class InvocationLifetimeOwnerSelector
{
    /// <summary>
    /// Handler identities are UIDs and compare ordinally. Returns <see langword="null"/> when no handler is declared.
    /// </summary>
    internal static IReadOnlyList<string>? NormalizeHandlers(IEnumerable<string> handlerUids)
    {
        string[] normalized = handlerUids
            .Select(static uid => uid.Trim())
            .Where(static uid => uid.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return normalized.Length == 0 ? null : normalized;
    }

    /// <param name="modules">The modules of the command, in any order. Selection orders them by project path and target framework.</param>
    internal static InvocationLifetimeOwnerSelection Select(IEnumerable<TestModule> modules)
    {
        List<TestModule> candidates = modules
            .Where(static module => module.IsTestingPlatformApplication)
            .OrderBy(static module => module.ProjectFullPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static module => module.TargetFramework, StringComparer.Ordinal)
            .ToList();
        if (!candidates.Any(static module => module.InvocationLifetimeHandlers is { Count: > 0 }))
        {
            return new(InvocationLifetimeOwnerSelectionStatus.NotRequested);
        }

        TestModule? owner = null;
        List<TestModule> explicitOwners = candidates.Where(static module => module.IsInvocationLifetimeOwner).ToList();
        if (explicitOwners.Count == 1 && explicitOwners[0].InvocationLifetimeHandlers is { Count: > 0 })
        {
            owner = explicitOwners[0];
        }
        else
        {
            foreach (TestModule candidate in candidates)
            {
                if (candidate.InvocationLifetimeHandlers is not { Count: > 0 } candidateHandlers)
                {
                    continue;
                }

                var candidateSet = new HashSet<string>(candidateHandlers, StringComparer.Ordinal);
                if (candidates.All(other => other.InvocationLifetimeHandlers is null || candidateSet.IsSupersetOf(other.InvocationLifetimeHandlers)))
                {
                    owner = candidate;
                    break;
                }
            }
        }

        if (owner is null)
        {
            return new(InvocationLifetimeOwnerSelectionStatus.Ambiguous);
        }

        return TestApplication.RequiresHttpTransport(owner)
            ? new(InvocationLifetimeOwnerSelectionStatus.UnsupportedTransport, owner)
            : new(InvocationLifetimeOwnerSelectionStatus.Selected, owner);
    }
}
