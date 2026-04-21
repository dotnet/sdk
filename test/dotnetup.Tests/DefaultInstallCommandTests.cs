// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public sealed class DefaultInstallCommandTests : IDisposable
{
    private readonly string _tempHome;
    private readonly string _tempXdgDataHome;

    public DefaultInstallCommandTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), "dotnetup-defaultinstall-tests", Guid.NewGuid().ToString("N"));
        _tempXdgDataHome = Path.Combine(_tempHome, ".local", "share");
        Directory.CreateDirectory(_tempHome);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempHome, recursive: true);
        }
        catch
        {
            // Cleanup is best-effort in tests.
        }
    }

    [Fact]
    public void DefaultInstallUser_DoesNotPassDefaultInstallPathToPwshProfileOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            ["defaultinstall", "user", "--shell", "pwsh"],
            captureOutput: true,
            workingDirectory: AppContext.BaseDirectory,
            environmentVariables: new Dictionary<string, string>
            {
                ["HOME"] = _tempHome,
                ["XDG_DATA_HOME"] = _tempXdgDataHome,
            });

        exitCode.Should().Be(0, output);

        string profilePath = Path.Combine(_tempHome, ".config", "powershell", "Microsoft.PowerShell_profile.ps1");
        File.Exists(profilePath).Should().BeTrue();
        var profileContents = File.ReadAllText(profilePath);
        profileContents.Should().Contain("print-env-script --shell pwsh");
        profileContents.Should().NotContain("--dotnet-install-path");
    }
}
