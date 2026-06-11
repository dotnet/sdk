// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Test-only <see cref="IEnvShellProvider"/> backed by a real on-disk profile path.
/// Used by tests that need to exercise ShellProfileManager / PathPreferenceApplier
/// against a controllable profile file rather than the user's real shell config.
/// </summary>
internal sealed class TestShellProvider : IEnvShellProvider
{
    private readonly string[] _profilePaths;

    public TestShellProvider(string dir, params string[] fileNames)
    {
        _profilePaths = fileNames.Select(f => Path.Combine(dir, f)).ToArray();
    }

    public string ArgumentName => "test";
    public string Extension => "sh";
    public string? HelpDescription => null;
    public string? ProfileEntryOverride { get; init; }
    public Encoding? NewFileEncodingOverride { get; init; }

    Encoding IEnvShellProvider.NewFileEncoding
        => NewFileEncodingOverride ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string GenerateEnvScript(string dotnetInstallPath, string? dotnetupDir = null, bool includeDotnet = true) =>
        includeDotnet
            ? $"export DOTNET_ROOT='{dotnetInstallPath}'"
            : dotnetupDir is not null ? $"export PATH='{dotnetupDir}':$PATH" : "";

    public IReadOnlyList<string> GetProfilePaths() => _profilePaths;

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        if (ProfileEntryOverride is not null)
        {
            return ProfileEntryOverride;
        }

        var flags = dotnetupOnly ? " --dotnetup-only" : "";
        if (!dotnetupOnly && !string.IsNullOrEmpty(dotnetInstallPath))
        {
            flags += $" --dotnet-install-path '{dotnetInstallPath}'";
        }

        return $"eval \"$('{dotnetupPath}' print-env-script --shell test{flags})\"";
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var flags = dotnetupOnly ? " --dotnetup-only" : "";
        if (!dotnetupOnly && !string.IsNullOrEmpty(dotnetInstallPath))
        {
            flags += $" --dotnet-install-path '{dotnetInstallPath}'";
        }

        return $"eval \"$('{dotnetupPath}' print-env-script --shell test{flags})\"";
    }
}
