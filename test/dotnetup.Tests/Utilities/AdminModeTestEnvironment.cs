// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// A temporary dotnetup data directory plus a machine policy backed by an in-memory store. Pass
/// <see cref="Policy"/> to the code under test; nothing here replaces process-wide policy state.
/// </summary>
internal sealed class AdminModeTestEnvironment : IDisposable
{
    private readonly TestEnvironment _files = new();

    public AdminModeTestEnvironment()
    {
        DotnetupPaths.SetTestDataDirectoryOverride(TempRoot);
        Policy = new DotnetupPolicy(Store);
    }

    public string TempRoot => _files.TempRoot;

    /// <summary>The in-memory machine policy values.</summary>
    public FakeDotnetupPolicyStore Store { get; } = new();

    /// <summary>The policy over <see cref="Store"/>.</summary>
    public DotnetupPolicy Policy { get; }

    /// <summary>Creates a non-ANSI console that writes to <paramref name="output"/>.</summary>
    public static IAnsiConsole CreateConsole(StringWriter output) => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(output),
        Ansi = AnsiSupport.No,
    });

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();
        _files.Dispose();
    }
}
