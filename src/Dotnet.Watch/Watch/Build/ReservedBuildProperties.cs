// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Properties that <c>dotnet watch</c> reserves for itself. They must win over any value the user
/// specifies, including case variants of the property name, and they must be applied identically to
/// design time evaluations and to real builds so that both observe the same project state.
/// The public key is empty whenever no provider backs the invocation (browser refresh suppressed,
/// or <c>--list</c>), which turns browser tools asset generation off.
/// </summary>
internal static class ReservedBuildProperties
{
    /// <summary>
    /// Only the public half of the browser tools session key is ever passed to the build. The private
    /// half stays in this process, so a build, a build log or an SDK extension cannot impersonate the
    /// provider.
    /// </summary>
    public static ImmutableDictionary<string, string> SetBrowserToolsProperties(
        ImmutableDictionary<string, string> properties,
        EnvironmentOptions environmentOptions,
        string browserToolsPublicKey)
    {
        // The dictionary uses a case insensitive comparer, so this replaces case variants too.
        return properties
            .SetItem(PropertyNames.DotNetWatchBrowserTools, (!environmentOptions.SuppressBrowserRefresh).ToString())
            .SetItem(PropertyNames.DotNetWatchBrowserToolsPublicKey, GetPublicKeyValue(environmentOptions, browserToolsPublicKey));
    }

    /// <summary>
    /// MSBuild applies the last occurrence of a property, case insensitively, so appending
    /// these after the user's build arguments overrides case variant user values.
    /// </summary>
    public static IEnumerable<string> GetBrowserToolsArguments(
        EnvironmentOptions environmentOptions,
        string browserToolsPublicKey,
        string prefix = "-p:")
    {
        yield return $"{prefix}{PropertyNames.DotNetWatchBrowserTools}={!environmentOptions.SuppressBrowserRefresh}";
        yield return $"{prefix}{PropertyNames.DotNetWatchBrowserToolsPublicKey}={GetPublicKeyValue(environmentOptions, browserToolsPublicKey)}";
    }

    private static string GetPublicKeyValue(EnvironmentOptions environmentOptions, string browserToolsPublicKey)
        // Clear the property when browser tools are off so that a user supplied value cannot activate
        // asset generation that the watcher will not serve.
        => environmentOptions.SuppressBrowserRefresh ? "" : browserToolsPublicKey;
}
