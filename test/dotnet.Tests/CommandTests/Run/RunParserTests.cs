// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class RunParserTests
    {
        public RunParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private readonly ITestOutputHelper output;

        [Fact]
        public void RunParserCanGetArgumentFromDoubleDash()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);
            var projectPath = Path.Combine(newWorkingDir, "HelloWorld.csproj");
                
            var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath, "--", "foo" });
            runCommand.ApplicationArgs.Single().Should().Be("foo");
        }

        [Fact]
        public void RunParserCanSeparateInterleavedLoggerArguments()
        {
            var tam = new TestAssetsManager(output);
            var testDirectory = tam.CreateTestDirectory();
            File.WriteAllText(Path.Join(testDirectory.Path, "Program.cs"), "Console.WriteLine();");

            Directory.SetCurrentDirectory(testDirectory.Path);

            var runCommand = RunCommand.FromArgs(["Program.cs", "argX", "--no-build", "argY", "-tl:off"]);

            runCommand.NoBuild.Should().BeTrue();
            runCommand.ApplicationArgs.Should().Equal("argX", "argY");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().Contain("-tl:off");
        }

        [Fact]
        public void RunParserPreservesInterleavedLoggerArgumentsAfterDoubleDash()
        {
            var tam = new TestAssetsManager(output);
            var testDirectory = tam.CreateTestDirectory();
            File.WriteAllText(Path.Join(testDirectory.Path, "Program.cs"), "Console.WriteLine();");

            Directory.SetCurrentDirectory(testDirectory.Path);

            var runCommand = RunCommand.FromArgs(["Program.cs", "argX", "--no-build", "argY", "--", "-tl:off"]);

            runCommand.NoBuild.Should().BeTrue();
            runCommand.ApplicationArgs.Should().Equal("argX", "argY", "-tl:off");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("-tl:off");
        }

        [WindowsOnlyFact]
        public void RunParserAcceptsWindowsPathSeparatorsOnWindows()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);
            var projectPath = @".\HelloWorld.csproj";
                
            // Should not throw on Windows
            var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath });
            runCommand.ProjectFileFullPath.Should().NotBeNull();
        }

        [UnixOnlyFact]
        public void RunParserAcceptsWindowsPathSeparatorsOnLinux()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);
            var projectPath = @".\HelloWorld.csproj";
                
            // Should not throw on Linux with backslash separators
            var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath });
            runCommand.ProjectFileFullPath.Should().NotBeNull();
        }
    }
}
