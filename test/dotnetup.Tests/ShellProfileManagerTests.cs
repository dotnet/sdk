// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ShellProfileManagerTests : IDisposable
{
    private readonly string _tempDir;
    private const string FakeDotnetupPath = "/usr/local/bin/dotnetup";

    public ShellProfileManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { }
        }
    }

    [Fact]
    public void AddProfileEntries_CreatesFileAndAddsEntry()
    {
        var provider = new TestShellProvider(_tempDir, "test.sh");

        var modified = ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);

        modified.Should().HaveCount(1);
        var content = File.ReadAllText(modified[0]);
        content.Should().Contain(ShellProfileManager.MarkerComment);
        content.Should().Contain("print-env-script");
    }

    [Fact]
    public void AddProfileEntries_AppendsToExistingFile()
    {
        var profilePath = Path.Combine(_tempDir, "existing.sh");
        File.WriteAllText(profilePath, "# existing config\nexport FOO=bar\n");
        var provider = new TestShellProvider(_tempDir, "existing.sh");

        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);

        var content = File.ReadAllText(profilePath);
        content.Should().StartWith("# existing config");
        content.Should().Contain(ShellProfileManager.MarkerComment);
    }

    [Fact]
    public void AddProfileEntries_DoesNotDuplicateIfAlreadyPresent()
    {
        var profilePath = Path.Combine(_tempDir, "dup.sh");
        var provider = new TestShellProvider(_tempDir, "dup.sh");

        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);
        var modified = ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);

        modified.Should().BeEmpty();
        var lines = File.ReadAllLines(profilePath);
        lines.Count(l => l.TrimEnd() == ShellProfileManager.MarkerComment).Should().Be(1);
    }

    [Fact]
    public void AddProfileEntries_CreatesBackupOfExistingFile()
    {
        var profilePath = Path.Combine(_tempDir, "backup.sh");
        var originalContent = "# original content\n";
        File.WriteAllText(profilePath, originalContent);
        var provider = new TestShellProvider(_tempDir, "backup.sh");

        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);

        var backupPath = profilePath + ".dotnetup-backup";
        File.Exists(backupPath).Should().BeTrue();
        File.ReadAllText(backupPath).Should().Be(originalContent);
    }

    [Fact]
    public void AddProfileEntries_CreatesParentDirectories()
    {
        var nestedDir = Path.Combine(_tempDir, "config", "powershell");
        var provider = new TestShellProvider(nestedDir, "profile.ps1");

        var modified = ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);

        modified.Should().HaveCount(1);
        File.Exists(Path.Combine(nestedDir, "profile.ps1")).Should().BeTrue();
    }

    [Fact]
    public void RemoveProfileEntries_RemovesMarkerAndEvalLine()
    {
        var profilePath = Path.Combine(_tempDir, "remove.sh");
        var provider = new TestShellProvider(_tempDir, "remove.sh");

        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);
        File.ReadAllText(profilePath).Should().Contain(ShellProfileManager.MarkerComment);

        var modified = ShellProfileManager.RemoveProfileEntries(provider);

        modified.Should().HaveCount(1);
        var content = File.ReadAllText(profilePath);
        content.Should().NotContain(ShellProfileManager.MarkerComment);
        content.Should().NotContain("print-env-script");
    }

    [Fact]
    public void RemoveProfileEntries_LeavesOtherContentIntact()
    {
        var profilePath = Path.Combine(_tempDir, "partial.sh");
        File.WriteAllText(profilePath, "# my config\nexport FOO=bar\n");
        var provider = new TestShellProvider(_tempDir, "partial.sh");

        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);
        ShellProfileManager.RemoveProfileEntries(provider);

        var content = File.ReadAllText(profilePath);
        content.Should().Contain("# my config");
        content.Should().Contain("export FOO=bar");
    }

    [Fact]
    public void RemoveProfileEntries_ReturnsEmptyForMissingFile()
    {
        var provider = new TestShellProvider(_tempDir, "nonexistent.sh");

        var modified = ShellProfileManager.RemoveProfileEntries(provider);

        modified.Should().BeEmpty();
    }

    [Fact]
    public void HasProfileEntry_ReturnsFalseForMissingFile()
    {
        ShellProfileManager.HasProfileEntry(Path.Combine(_tempDir, "missing.sh")).Should().BeFalse();
    }

    [Fact]
    public void HasProfileEntry_ReturnsTrueWhenEntryPresent()
    {
        var profilePath = Path.Combine(_tempDir, "has.sh");
        var provider = new TestShellProvider(_tempDir, "has.sh");
        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);

        ShellProfileManager.HasProfileEntry(profilePath).Should().BeTrue();
    }

    [Fact]
    public void AddProfileEntries_ModifiesMultipleFiles()
    {
        var provider = new TestShellProvider(_tempDir, "file1.sh", "file2.sh");

        var modified = ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);

        modified.Should().HaveCount(2);
        File.ReadAllText(Path.Combine(_tempDir, "file1.sh")).Should().Contain(ShellProfileManager.MarkerComment);
        File.ReadAllText(Path.Combine(_tempDir, "file2.sh")).Should().Contain(ShellProfileManager.MarkerComment);
    }

    [Fact]
    public void AddProfileEntries_DotnetupOnly_IncludesFlag()
    {
        var provider = new TestShellProvider(_tempDir, "admin.sh");

        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath, dotnetupOnly: true);

        var content = File.ReadAllText(Path.Combine(_tempDir, "admin.sh"));
        content.Should().Contain("--dotnetup-only");
    }

    [Fact]
    public void AddProfileEntries_ReplacesExistingEntryInPlace()
    {
        var profilePath = Path.Combine(_tempDir, "replace.sh");
        var provider = new TestShellProvider(_tempDir, "replace.sh");

        // Add user entry
        ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath);
        File.ReadAllText(profilePath).Should().NotContain("--dotnetup-only");

        // Replace with admin entry (AddProfileEntries now replaces in-place)
        var modified = ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath, dotnetupOnly: true);

        modified.Should().HaveCount(1);
        var content = File.ReadAllText(profilePath);
        content.Should().Contain("--dotnetup-only");
        // Should only have one marker
        content.Split('\n').Count(l => l.TrimEnd() == ShellProfileManager.MarkerComment).Should().Be(1);
    }

    [Fact]
    public void AddProfileEntries_WorksWithNoExistingEntry()
    {
        var provider = new TestShellProvider(_tempDir, "fresh.sh");

        var modified = ShellProfileManager.AddProfileEntries(provider, FakeDotnetupPath, dotnetupOnly: true);

        modified.Should().HaveCount(1);
        File.ReadAllText(Path.Combine(_tempDir, "fresh.sh")).Should().Contain("--dotnetup-only");
    }

    [Fact]
    public void BashProvider_GenerateProfileEntry_ContainsEval()
    {
        var provider = new BashEnvShellProvider();
        var entry = provider.GenerateProfileEntry(FakeDotnetupPath);

        entry.Should().Contain(ShellProfileManager.MarkerComment);
        entry.Should().Contain("eval");
        entry.Should().Contain("--shell bash");
        entry.Should().NotContain("--dotnetup-only");
    }

    [Fact]
    public void BashProvider_GenerateProfileEntry_DotnetupOnly()
    {
        var provider = new BashEnvShellProvider();
        var entry = provider.GenerateProfileEntry(FakeDotnetupPath, dotnetupOnly: true);

        entry.Should().Contain("--dotnetup-only");
    }

    [Fact]
    public void ZshProvider_GenerateProfileEntry_ContainsEval()
    {
        var provider = new ZshEnvShellProvider();
        var entry = provider.GenerateProfileEntry(FakeDotnetupPath);

        entry.Should().Contain(ShellProfileManager.MarkerComment);
        entry.Should().Contain("eval");
        entry.Should().Contain("--shell zsh");
        entry.Should().NotContain("--dotnetup-only");
    }

    [Fact]
    public void PowerShellProvider_GenerateProfileEntry_ContainsInvokeExpression()
    {
        var provider = new PowerShellEnvShellProvider();
        var entry = provider.GenerateProfileEntry(FakeDotnetupPath);

        entry.Should().Contain(ShellProfileManager.MarkerComment);
        entry.Should().Contain("Invoke-Expression");
        entry.Should().Contain("--shell pwsh");
        entry.Should().NotContain("--dotnetup-only");
    }

    [Fact]
    public void BashProvider_GenerateActivationCommand_IsCorrect()
    {
        var provider = new BashEnvShellProvider();
        var command = provider.GenerateActivationCommand(FakeDotnetupPath);

        command.Should().Contain("eval");
        command.Should().Contain("--shell bash");
        command.Should().NotContain(ShellProfileManager.MarkerComment);
    }

    [Fact]
    public void ZshProvider_GenerateActivationCommand_IsCorrect()
    {
        var provider = new ZshEnvShellProvider();
        var command = provider.GenerateActivationCommand(FakeDotnetupPath);

        command.Should().Contain("eval");
        command.Should().Contain("--shell zsh");
    }

    [Fact]
    public void PowerShellProvider_GenerateActivationCommand_IsCorrect()
    {
        var provider = new PowerShellEnvShellProvider();
        var command = provider.GenerateActivationCommand(FakeDotnetupPath);

        command.Should().Contain("Invoke-Expression");
        command.Should().Contain("--shell pwsh");
    }

    [Fact]
    public void BashProvider_GetProfilePaths_ReturnsAtLeastBashrc()
    {
        var provider = new BashEnvShellProvider();
        var paths = provider.GetProfilePaths();

        paths.Should().HaveCountGreaterThanOrEqualTo(2);
        paths[0].Should().EndWith(".bashrc");
    }

    [Fact]
    public void ZshProvider_GetProfilePaths_ReturnsZshrc()
    {
        var provider = new ZshEnvShellProvider();
        var paths = provider.GetProfilePaths();

        paths.Should().HaveCount(1);
        paths[0].Should().EndWith(".zshrc");
    }

    [Fact]
    public void PowerShellProvider_GetProfilePaths_ReturnsProfilePs1()
    {
        var provider = new PowerShellEnvShellProvider();
        var paths = provider.GetProfilePaths();

        paths.Should().HaveCount(1);
        paths[0].Should().EndWith("Microsoft.PowerShell_profile.ps1");
    }

    /// <summary>
    /// Test-only shell provider that targets files in the temp directory.
    /// </summary>
    private sealed class TestShellProvider : IEnvShellProvider
    {
        private readonly string[] _profilePaths;

        public TestShellProvider(string dir, params string[] fileNames)
        {
            _profilePaths = fileNames.Select(f => Path.Combine(dir, f)).ToArray();
        }

        public string ArgumentName => "test";
        public string Extension => "sh";
        public string? HelpDescription => null;

        public string GenerateEnvScript(string dotnetInstallPath, string? dotnetupDir = null, bool includeDotnet = true) =>
            includeDotnet
                ? $"export DOTNET_ROOT='{dotnetInstallPath}'"
                : dotnetupDir is not null ? $"export PATH='{dotnetupDir}':$PATH" : "";

        public IReadOnlyList<string> GetProfilePaths() => _profilePaths;

        public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false)
        {
            var flags = dotnetupOnly ? " --dotnetup-only" : "";
            return $"# dotnetup\neval \"$('{dotnetupPath}' print-env-script --shell test{flags})\"";
        }

        public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false)
        {
            var flags = dotnetupOnly ? " --dotnetup-only" : "";
            return $"eval \"$('{dotnetupPath}' print-env-script --shell test{flags})\"";
        }
    }
}
