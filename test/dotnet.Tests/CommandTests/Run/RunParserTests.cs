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
            var projectPath = Path.Combine(newWorkingDir, "HelloWorld.csproj");

            // An absolute --project path is passed, so resolution does not depend on the process-wide
            // current directory. Avoid Directory.SetCurrentDirectory so this test does not leak CWD
            // state onto other tests running in parallel.
            var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath, "--", "foo" });
            runCommand.ApplicationArgs.Single().Should().Be("foo");
        }

        [WindowsOnlyFact]
        public void RunParserAcceptsWindowsPathSeparatorsOnWindows()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;
            var projectPath = @".\HelloWorld.csproj";

            // This scenario resolves a relative --project path against the process-wide current
            // directory, so it must be changed. Restore it in a finally so the change does not leak
            // onto other parallel tests (see AddReferenceHasDefaultArgumentSetToCurrentDirectory).
            var originalWorkingDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(newWorkingDir);
                // Should not throw on Windows
                var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath });
                runCommand.ProjectFileFullPath.Should().NotBeNull();
            }
            finally
            {
                Directory.SetCurrentDirectory(originalWorkingDir);
            }
        }

        [UnixOnlyFact]
        public void RunParserAcceptsWindowsPathSeparatorsOnLinux()
        {
            var tam = new TestAssetsManager(output);
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;
            var projectPath = @".\HelloWorld.csproj";

            // This scenario resolves a relative --project path against the process-wide current
            // directory, so it must be changed. Restore it in a finally so the change does not leak
            // onto other parallel tests (see AddReferenceHasDefaultArgumentSetToCurrentDirectory).
            var originalWorkingDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(newWorkingDir);
                // Should not throw on Linux with backslash separators
                var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath });
                runCommand.ProjectFileFullPath.Should().NotBeNull();
            }
            finally
            {
                Directory.SetCurrentDirectory(originalWorkingDir);
            }
        }
    }
}
