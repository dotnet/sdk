// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class OsxEnvironmentPathTests
    {
        [UnixOnlyTheory]
        [InlineData("/bin/bash")]
        [InlineData("/bin/zsh")]
        public void GivenPathNotSetItPrintsManualInstructions(string shell)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(shell);

            var environmentPath = new MacOSEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            if (shell == "/bin/zsh")
            {
                reporter.Lines.Should().Equal(
                    string.Format(
                        CommonLocalizableStrings.EnvironmentPathOSXZshManualInstructions,
                        toolsPath.Path));
            }
            else
            {
                reporter.Lines.Should().Equal(
                    string.Format(
                        CommonLocalizableStrings.EnvironmentPathOSXBashManualInstructions,
                        toolsPath.Path));
            }

        }

        [UnixOnlyTheory]
        [InlineData("/bin/bash")]
        [InlineData("/bin/zsh")]
        public void GivenPathNotSetAndProfileExistsItPrintsReopenMessage(string shell)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(shell);

            var environmentPath = new MacOSEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                new FileSystemMockBuilder()
                    .AddFile(MacOSEnvironmentPath.DotnetCliToolsPathsDPath, "")
                    .Build()
                    .File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(CommonLocalizableStrings.EnvironmentPathOSXNeedReopen);
        }

        [UnixOnlyTheory]
        [InlineData("/home/user/.dotnet/tools", "/bin/bash")]
        [InlineData("~/.dotnet/tools", "/bin/bash")]
        [InlineData("/home/user/.dotnet/tools", "/bin/zsh")]
        [InlineData("~/.dotnet/tools", "/bin/zsh")]
        public void GivenPathSetItPrintsNothing(string toolsDirectoryOnPath, string shell)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsDirectoryOnPath);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(shell);

            var environmentPath = new MacOSEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().BeEmpty();
        }

        [UnixOnlyTheory]
        [InlineData("/bin/bash")]
        [InlineData("/bin/zsh")]
        public void GivenPathSetItDoesNotAddPathToEnvironment(string shell)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            var fileSystem = new FileSystemMockBuilder().Build().File;

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsPath.Path);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(shell);

            var environmentPath = new MacOSEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .Exists(MacOSEnvironmentPath.DotnetCliToolsPathsDPath)
                .Should()
                .Be(false);
        }

        [UnixOnlyTheory]
        [InlineData("/bin/bash")]
        [InlineData("/bin/zsh")]
        public void GivenPathNotSetItAddsToEnvironment(string shell)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            IFileSystem fileSystem = new FileSystemMockBuilder().Build();
            fileSystem.Directory.CreateDirectory("/etc/paths.d");

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(shell);

            var environmentPath = new MacOSEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem.File);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .File
                .ReadAllText(MacOSEnvironmentPath.DotnetCliToolsPathsDPath)
                .Should()
                .Be(toolsPath.Path);
        }
    }
}
