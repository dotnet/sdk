// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.DefaultInstall;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public sealed class DefaultInstallCommandTests : IDisposable
{
    private readonly string _tempHome;
    private readonly string? _originalHome;

    public DefaultInstallCommandTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), "dotnetup-defaultinstall-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempHome);
        _originalHome = Environment.GetEnvironmentVariable("HOME");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HOME", _originalHome);

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

        Environment.SetEnvironmentVariable("HOME", _tempHome);

        string defaultInstallPath = Path.Combine(_tempHome, "dotnet-managed");
        var parseResult = Parser.Parse(["defaultinstall", "user", "--shell", "pwsh"]);
        var environmentManager = new MockDotnetInstallManager(defaultInstallPath: defaultInstallPath);

        var exitCode = new DefaultInstallCommand(parseResult, environmentManager).Execute();

        exitCode.Should().Be(0);

        string profilePath = Path.Combine(_tempHome, ".config", "powershell", "Microsoft.PowerShell_profile.ps1");
        File.Exists(profilePath).Should().BeTrue();
        var profileContents = File.ReadAllText(profilePath);
        profileContents.Should().Contain("print-env-script --shell pwsh");
        profileContents.Should().NotContain("--dotnet-install-path");
    }
}
