// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Test-only <see cref="IEnvShellProvider"/> backed by a real on-disk profile path.
/// Used by tests that need to exercise ShellProfileManager / EnvSettingsApplier
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

    public string GenerateEnvScript(string dotnetInstallPath, string dotnetupDir = "", bool includeDotnet = true)
    {
        var lines = new List<string> { "# test env script" };
        if (includeDotnet)
        {
            lines.Add($"export DOTNET_ROOT='{dotnetInstallPath}'");
        }

        if (!string.IsNullOrEmpty(dotnetupDir))
        {
            lines.Add($"export PATH='{dotnetupDir}':$PATH");
        }

        return string.Join("\n", lines);
    }

    public IReadOnlyList<string> GetProfilePaths() => _profilePaths;

    public string GenerateProfileEntry(string dotnetupPath, bool includeDotnet = true, bool includeDotnetup = true, string? dotnetInstallPath = null)
    {
        if (ProfileEntryOverride is not null)
        {
            return ProfileEntryOverride;
        }

        // Reuse the production line builder and guard so this double can't drift from the real
        // profile composition. Only the env script it wraps is test-specific.
        var envScript = ShellProviderHelpers.GetProfileEnvScript(this, dotnetupPath, includeDotnet, includeDotnetup, dotnetInstallPath);
        return ShellProviderHelpers.BuildPosixProfileEntry(dotnetupPath, envScript);
    }

    public string GenerateActivationCommand(string dotnetupPath)
        => $"eval \"$('{dotnetupPath}' env script)\"";
}
