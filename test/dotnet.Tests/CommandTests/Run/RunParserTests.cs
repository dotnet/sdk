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
