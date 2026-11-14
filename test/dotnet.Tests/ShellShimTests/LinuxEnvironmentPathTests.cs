// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    [TestClass]
    public class LinuxEnvironmentPathTests
    {
        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("/bin/bash", "", ".profile")]
        [DataRow("/bin/bash", ".profile", ".profile")]
        [DataRow("/bin/bash", ".bash_login;.profile;.bashrc", ".bash_login")]
        [DataRow("/usr/bin/bash", ".bash_profile;.bash_login;.profile;.bashrc", ".bash_profile")]
        [DataRow("/bin/bash", ".bashrc", ".bashrc")]
        [DataRow("/bin/bash", ".bashrc;.profile", ".profile")]
        [DataRow("/bin/zsh", "", ".zshrc")]
        [DataRow("/bin/zsh", ".zprofile", ".zprofile")]
        [DataRow("/bin/zsh", ".zlogin", ".zlogin")]
        [DataRow("/bin/zsh", ".zlogin;.zshenv", ".zlogin")]
        [DataRow("/bin/zsh", ".zshenv", ".zshenv")]
        [DataRow("/usr/bin/zsh", ".zshrc;.zprofile;.zshenv", ".zshrc")]
        [DataRow("/bin/sh", ".bash_profile", ".profile")]
        [DataRow("/bin/dash", ".bashrc", ".profile")]
        public void GivenPathNotSetItSelectsAnAppropriateStartupFile(string shell, string existingFiles, string expectedFile)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns("/usr/bin");
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns(shell);
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns("/home/user");
            provider.Setup(p => p.GetEnvironmentVariable("ZDOTDIR")).Returns((string?)null);
            var builder = new FileSystemMockBuilder();
            foreach (var file in existingFiles.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                builder.AddFile("/home/user/" + file, "# existing configuration");
            }

            var fileSystem = builder.Build().File;
            var environmentPath = new LinuxEnvironmentPath(toolsPath, reporter, provider.Object, fileSystem);
            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            var output = reporter.Lines.Single();
            output.Should().Contain(">> '/home/user/" + expectedFile + "'");
            output.Should().Contain("printf '\\n%s\\n' 'export PATH=\"$PATH\":'\\''/home/user/.dotnet/tools'\\'''");
            output.Should().Contain("export PATH=\"$PATH\":'/home/user/.dotnet/tools'");
            foreach (var file in existingFiles.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                fileSystem.ReadAllText("/home/user/" + file).Should().Be("# existing configuration");
            }
            if (!existingFiles.Split(';').Contains(expectedFile))
            {
                fileSystem.Exists("/home/user/" + expectedFile).Should().BeFalse();
            }
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("/bin/tcsh")]
        public void GivenUnknownShellItPrintsShellNeutralInstructions(string? shell)
        {
            var reporter = new BufferedReporter();
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns((string?)null);
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns(shell);
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns("/home/user");
            var fileSystem = new FileSystemMockBuilder()
                .AddFile(LinuxEnvironmentPath.DotnetCliToolsProfilePath, "")
                .Build().File;
            var environmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools"), reporter, provider.Object, fileSystem);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            var output = reporter.Lines.Single();
            output.Should().Contain("/home/user/.dotnet/tools");
            output.Should().NotContain("export ").And.NotContain("printf ").And.NotContain(">>");
            output.Should().NotBe(CliStrings.EnvironmentPathLinuxNeedLogout);
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("/home/user/zsh config", ".zprofile")]
        [DataRow("/home/user/zsh config", ".zlogin")]
        [DataRow("/home/user/zsh config", ".zshenv")]
        [DataRow("/home/user/zsh config", ".zshrc")]
        [DataRow("", ".zshrc")]
        [DataRow(null, ".zshrc")]
        public void GivenZshItUsesZdotdirAndDoesNotRelyOnProfileD(string? zdotdir, string existingFile)
        {
            var reporter = new BufferedReporter();
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns("/usr/bin");
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns("/bin/zsh");
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns("/home/user");
            provider.Setup(p => p.GetEnvironmentVariable("ZDOTDIR")).Returns(zdotdir);
            var directory = zdotdir == null ? "/home/user" : zdotdir + "/";
            var startupFile = System.IO.Path.Combine(directory, existingFile);
            var fileSystem = new FileSystemMockBuilder()
                .AddFile(LinuxEnvironmentPath.DotnetCliToolsProfilePath, "")
                .AddFile(startupFile, "# existing configuration")
                .AddFile("/home/user/.zshrc", "# home configuration")
                .Build().File;
            var environmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools"), reporter, provider.Object, fileSystem);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Single().Should().Contain(">> '" + startupFile + "'");
            reporter.Lines.Should().NotContain(CliStrings.EnvironmentPathLinuxNeedLogout);
            fileSystem.ReadAllText(startupFile).Should().Be(
                startupFile == "/home/user/.zshrc" ? "# home configuration" : "# existing configuration");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void GivenMissingHomeItDoesNotSuggestARelativeStartupFile()
        {
            var reporter = new BufferedReporter();
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns("/usr/bin");
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns("/bin/bash");
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns((string?)null);
            var environmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools"),
                reporter, provider.Object, FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Single().Should().NotContain(">>");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void GivenShellMetacharactersItQuotesPathsLiterally()
        {
            var reporter = new BufferedReporter();
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            const string home = "/home/a 'quote' \"$value`command`\\!history";
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns("/usr/bin");
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns("/bin/bash");
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns(home);
            var environmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory(home, ".dotnet/tools"),
                reporter, provider.Object, FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Single().Should().Contain(">> '/home/a '\\''quote'\\'' \"$value`command`\\!history/.profile'");
            reporter.Lines.Single().Should().Contain(
                "export PATH=\"$PATH\":'/home/a '\\''quote'\\'' \"$value`command`\\!history/.dotnet/tools'");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow(null, "/home/user/.config/fish/config.fish")]
        [DataRow("", "/home/user/.config/fish/config.fish")]
        [DataRow("relative/config", "/home/user/.config/fish/config.fish")]
        [DataRow("/custom config", "/custom config/fish/config.fish")]
        public void GivenFishItUsesTheXdgStartupFileWithoutOverwritingIt(string? xdgConfigHome, string startupFile)
        {
            var reporter = new BufferedReporter();
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns("/usr/bin");
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns("/usr/bin/fish");
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns("/home/user");
            provider.Setup(p => p.GetEnvironmentVariable("XDG_CONFIG_HOME")).Returns(xdgConfigHome);
            var fileSystem = new FileSystemMockBuilder()
                .AddFile(startupFile, "# existing configuration")
                .AddFile(LinuxEnvironmentPath.DotnetCliToolsProfilePath, "")
                .Build().File;
            var environmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools"), reporter, provider.Object, fileSystem);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Single().Should().Contain(">> '" + startupFile + "'");
            reporter.Lines.Single().Should().Contain("mkdir -p -- '" + System.IO.Path.GetDirectoryName(startupFile) + "'");
            reporter.Lines.Single().Should().Contain("fish_add_path --append --path '/home/user/.dotnet/tools'");
            reporter.Lines.Single().Should().NotContain("export ").And.NotBe(CliStrings.EnvironmentPathLinuxNeedLogout);
            fileSystem.ReadAllText(startupFile).Should().Be("# existing configuration");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("pwsh")]
        [DataRow("powershell")]
        public void GivenPowerShellItResolvesExistingProfilesInTheCurrentHost(string shell)
        {
            var output = GetAdditionalShellInstructions(shell, "/home/user");

            output.Should().Contain("if ([System.IO.File]::Exists($PROFILE.CurrentUserAllHosts)) { $PROFILE.CurrentUserAllHosts }");
            output.Should().Contain("elseif ([System.IO.File]::Exists($PROFILE)) { $PROFILE }");
            output.Should().Contain("[System.IO.Directory]::CreateDirectory");
            output.Should().Contain("[System.IO.File]::AppendAllText($dotnetProfile,");
            output.Should().Contain("$env:PATH += [System.IO.Path]::PathSeparator + '/home/user/.dotnet/tools'");
            output.Should().NotContain("export ").And.NotContain(".config/powershell");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void GivenNushellItResolvesExistingStartupFilesUsingRuntimeConfigPaths()
        {
            var output = GetAdditionalShellInstructions("nu", "/home/user");

            output.Should().Contain("if ($nu.config-path | path exists) { $nu.config-path }");
            output.Should().Contain("else if ($nu.env-path | path exists) { $nu.env-path }");
            output.Should().Contain("mkdir ($dotnet_profile | path dirname)");
            output.Should().Contain("save --append --raw $dotnet_profile");
            output.Should().Contain("$env.PATH = ($env.PATH | append r#'/home/user/.dotnet/tools'#)");
            output.Should().NotContain("export ").And.NotContain(".config/nushell");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("nu")]
        [DataRow("nushell")]
        public void GivenNushellExecutableAliasesItUsesNushellInstructions(string shell)
        {
            var output = GetAdditionalShellInstructions(shell, "/home/user");

            output.Should().Contain("$nu.config-path / $nu.env-path");
            output.Should().Contain("$env.PATH = ($env.PATH | append r#'/home/user/.dotnet/tools'#)");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("fish", "fish_add_path --append --path '/home/a \\'quote\\' \"$value`command`\\\\!history/.dotnet/tools'")]
        [DataRow("pwsh", "$env:PATH += [System.IO.Path]::PathSeparator + '/home/a ''quote'' \"$value`command`\\!history/.dotnet/tools'")]
        [DataRow("nu", "$env.PATH = ($env.PATH | append r#'/home/a 'quote' \"$value`command`\\!history/.dotnet/tools'#)")]
        [DataRow("nushell", "$env.PATH = ($env.PATH | append r#'/home/a 'quote' \"$value`command`\\!history/.dotnet/tools'#)")]
        public void GivenAdditionalShellsItQuotesMetacharactersLiterally(string shell, string expectedCommand)
        {
            GetAdditionalShellInstructions(shell, "/home/a 'quote' \"$value`command`\\!history")
                .Should().Contain(expectedCommand);
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("\u2018")]
        [DataRow("\u2019")]
        [DataRow("\u201a")]
        [DataRow("\u201b")]
        public void GivenPowerShellTypographicApostrophesItDoublesQuoteDelimiters(string quote)
        {
            var output = GetAdditionalShellInstructions("pwsh", "/home/a" + quote + "b");
            output.Should().Contain(
                "$env:PATH += [System.IO.Path]::PathSeparator + '/home/a" + quote + quote + "b/.dotnet/tools'");
            output.Should().Contain(
                "$env:PATH += [System.IO.Path]::PathSeparator + ''/home/a" + quote + quote + quote + quote + "b/.dotnet/tools''");
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void GivenNushellRawStringDelimitersInThePathItUsesADistinctDelimiter()
        {
            GetAdditionalShellInstructions("nu", "/home/a'#b'##c")
                .Should().Contain("append r###'/home/a'#b'##c/.dotnet/tools'###");
        }

        private static string GetAdditionalShellInstructions(string shell, string home)
        {
            var reporter = new BufferedReporter();
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns("/usr/bin");
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns("/usr/bin/" + shell);
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns(home);
            provider.Setup(p => p.GetEnvironmentVariable("XDG_CONFIG_HOME")).Returns((string?)null);
            var fileSystem = new Mock<IFile>(MockBehavior.Strict);
            fileSystem.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
            var environmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory(home, ".dotnet/tools"), reporter, provider.Object, fileSystem.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            fileSystem.Verify(f => f.WriteAllText(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            reporter.Lines.Single().Should().NotBe(CliStrings.EnvironmentPathLinuxNeedLogout);
            return reporter.Lines.Single();
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("/bin/bash")]
        [DataRow("/bin/sh")]
        [DataRow("/bin/dash")]
        public void GivenPathNotSetAndProfileExistsItPrintsLogoutMessage(string shell)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns(shell);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                new FileSystemMockBuilder()
                    .AddFile(LinuxEnvironmentPath.DotnetCliToolsProfilePath, "")
                    .Build()
                    .File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(CliStrings.EnvironmentPathLinuxNeedLogout);
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("/home/user/.dotnet/tools")]
        [DataRow("~/.dotnet/tools")]
        public void GivenPathSetItPrintsNothing(string toolsDirectoryOnPath)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsDirectoryOnPath);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().BeEmpty();
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        [DataRow("/bin/bash", "/home/user/.dotnet/tools:/usr/bin")]
        [DataRow("/bin/zsh", "/usr/bin:/home/user/.dotnet/tools:/bin")]
        [DataRow("/bin/sh", "/usr/bin:/home/user/.dotnet/tools")]
        [DataRow("/bin/dash", "/usr/bin:/home/user/.dotnet/tools")]
        [DataRow("/bin/bash", "/usr/bin:~/.dotnet/tools:/bin")]
        [DataRow("/usr/bin/fish", "/usr/bin:/home/user/.dotnet/tools")]
        [DataRow("/usr/bin/pwsh", "/usr/bin:/home/user/.dotnet/tools")]
        [DataRow("/usr/bin/powershell", "/usr/bin:/home/user/.dotnet/tools")]
        [DataRow("/usr/bin/nu", "/usr/bin:/home/user/.dotnet/tools")]
        public void GivenToolsDirectoryAddedToPathSubsequentCallsDoNotPrintInstructionsOrWriteFiles(string shell, string updatedPath)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = "/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetEnvironmentVariable("PATH")).Returns(() => pathValue);
            provider.Setup(p => p.GetEnvironmentVariable("SHELL")).Returns(shell);
            provider.Setup(p => p.GetEnvironmentVariable("HOME")).Returns("/home/user");
            provider.Setup(p => p.GetEnvironmentVariable("ZDOTDIR")).Returns((string?)null);
            provider.Setup(p => p.GetEnvironmentVariable("XDG_CONFIG_HOME")).Returns((string?)null);
            var fileSystem = new Mock<IFile>(MockBehavior.Strict);
            fileSystem.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
            var environmentPath = new LinuxEnvironmentPath(
                toolsPath, reporter, provider.Object, fileSystem.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Single().Should().Contain("/home/user/.dotnet/tools");
            reporter.Clear();
            fileSystem.Invocations.Clear();
            pathValue = updatedPath;

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();
            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();
            fileSystem.VerifyNoOtherCalls();

            var subsequentEnvironmentPath = new LinuxEnvironmentPath(
                toolsPath, reporter, provider.Object, fileSystem.Object);

            subsequentEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();
            subsequentEnvironmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();
            fileSystem.VerifyNoOtherCalls();
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void GivenPathSetItDoesNotAddPathToEnvironment()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            var fileSystem = new FileSystemMockBuilder().Build().File;

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsPath.Path);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .Exists(LinuxEnvironmentPath.DotnetCliToolsProfilePath)
                .Should()
                .Be(false);
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void GivenPathNotSetItAddsToEnvironment()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            IFileSystem fileSystem = new FileSystemMockBuilder().Build();
            fileSystem.Directory.CreateDirectory("/etc/profile.d");

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem.File);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .File
                .ReadAllText(LinuxEnvironmentPath.DotnetCliToolsProfilePath)
                .Should()
                .Be($"export PATH=\"$PATH:{toolsPath.PathWithDollar}\"");
        }
    }
}
