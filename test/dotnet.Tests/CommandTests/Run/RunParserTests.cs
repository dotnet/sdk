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

        [Fact]
        public void RunParserPreservesDuplicateLoggerArgumentsAfterDoubleDash()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);

            var runCommand = RunCommand.FromArgs(["repeated", "-tl:off", "boundary", "--", "-tl:off", "boundary"]);

            runCommand.ApplicationArgs.Should().Equal("repeated", "boundary", "-tl:off", "boundary");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().ContainSingle("-tl:off");
        }

        [Fact]
        public void DoubleDash_Bl()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);

            var runCommand = RunCommand.FromArgs(["b0", "-bl", "b1", "--", "a0", "-bl", "a1"]);

            runCommand.ApplicationArgs.Should().Equal("b0", "b1", "a0", "-bl", "a1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().Contain("-bl");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("b1");
        }

        [Fact]
        public void DoubleDash_Bl_Value()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);

            var runCommand = RunCommand.FromArgs(["b0", "-bl:val1", "b1", "--", "a0", "-bl:val2", "a1"]);

            runCommand.ApplicationArgs.Should().Equal("b0", "b1", "a0", "-bl:val2", "a1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().Contain("-bl:val1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("b1");
        }

        [Fact]
        public void DoubleDash_Tl()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);

            var runCommand = RunCommand.FromArgs(["b0", "-tl", "b1", "--", "a0", "-tl", "a1"]);

            runCommand.ApplicationArgs.Should().Equal("b0", "b1", "a0", "-tl", "a1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().Contain("-tl:auto");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("b1");
        }

        [Theory, CombinatorialData]
        public void DoubleDash_Tl_Value([CombinatorialValues("auto", "on", "off")] string value)
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld", identifier: value).WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);

            var runCommand = RunCommand.FromArgs(["b0", $"-tl:{value}", "b1", "--", "a0", $"-tl:{value}", "a1"]);

            runCommand.ApplicationArgs.Should().Equal("b0", "b1", "a0", $"-tl:{value}", "a1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().Contain($"-tl:{value}");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("b1");
        }

        [Fact]
        public void DoubleDash_Tl_Value_Unknown()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);

            var runCommand = RunCommand.FromArgs(["b0", "-tl:val1", "b1", "--", "a0", "-tl:val2", "a1"]);

            runCommand.ApplicationArgs.Should().Equal("b0", "-tl:val1", "b1", "a0", "-tl:val2", "a1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("-tl:val1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("b1");
        }

        [Theory, CombinatorialData]
        public void DoubleDash_LoggerParameters([CombinatorialValues("tlp", "clp")] string name)
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld", identifier: name).WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);

            var runCommand = RunCommand.FromArgs(["b0", $"-{name}:val1", "b1", "--", "a0", $"-{name}:val2", "a1"]);

            runCommand.ApplicationArgs.Should().Equal("b0", "b1", "a0", $"-{name}:val2", "a1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().Contain($"-{name}:val1");
            runCommand.MSBuildArgs.OtherMSBuildArgs.Should().NotContain("b1");
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
