// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using CliEnvironmentProvider = Microsoft.DotNet.Cli.Utils.EnvironmentProvider;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Compares the configured env state against what the <em>current process</em> actually resolves
/// on its PATH, so the commands can tell the user whether a change is already live in this
/// terminal, needs an activation command, or needs a brand-new terminal. Resolution is done with
/// <see cref="CliEnvironmentProvider.GetCommandPath"/> (the same helper the rest of dotnetup uses),
/// which honors PATH ordering and Windows executable extensions — i.e. it answers "does the managed
/// dotnet/dotnetup actually win?" rather than merely "is the directory somewhere on PATH".
/// </summary>
internal static class EnvActivationStatus
{
    /// <summary>
    /// Pure comparison: given the managed directories and the directories that <c>dotnet</c> /
    /// <c>dotnetup</c> currently resolve to (or <c>null</c> when not found), reports whether each
    /// axis needs to be added or removed to match <paramref name="config"/>.
    /// </summary>
    public static EnvTerminalState Evaluate(
        DotnetupConfigData config,
        string managedDotnetDir,
        string managedDotnetupDir,
        string? resolvedDotnetDir,
        string? resolvedDotnetupDir)
    {
        bool wantDotnet = config.AccessMode is DotnetAccessMode.Shell or DotnetAccessMode.Everywhere;
        bool haveDotnet = resolvedDotnetDir is not null && DotnetupUtilities.PathsEqual(resolvedDotnetDir, managedDotnetDir);

        bool wantDotnetup = config.DotnetupOnPath;
        bool haveDotnetup = resolvedDotnetupDir is not null && DotnetupUtilities.PathsEqual(resolvedDotnetupDir, managedDotnetupDir);

        // An axis needs adding when it's wanted but not currently resolving to the managed dir, and
        // needs removing when it resolves to the managed dir but isn't wanted.
        bool needsAdditions = (wantDotnet && !haveDotnet) || (wantDotnetup && !haveDotnetup);
        bool needsRemovals = (!wantDotnet && haveDotnet) || (!wantDotnetup && haveDotnetup);

        return new EnvTerminalState(needsAdditions, needsRemovals);
    }

    /// <summary>
    /// Resolves the live <c>dotnet</c> / <c>dotnetup</c> locations from the current process PATH and
    /// delegates to <see cref="Evaluate"/>.
    /// </summary>
    public static EnvTerminalState EvaluateCurrentProcess(DotnetupConfigData config, IDotnetEnvironmentManager environment)
    {
        var provider = new CliEnvironmentProvider();
        string managedDotnetDir = environment.GetDefaultDotnetInstallPath();
        string managedDotnetupDir = ShellProviderHelpers.GetDotnetupDirectoryOrThrow();

        return Evaluate(
            config,
            managedDotnetDir,
            managedDotnetupDir,
            ExecutablePathResolver.ResolveRealDirectory(provider.GetCommandPath("dotnet")),
            ExecutablePathResolver.ResolveRealDirectory(provider.GetCommandPath("dotnetup")));
    }
}
