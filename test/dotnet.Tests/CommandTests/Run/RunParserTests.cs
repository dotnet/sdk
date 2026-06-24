// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class RunParserTests : IDisposable
    {
        private readonly string _previousWorkingDirectory;

        public RunParserTests()
        {
            // Reset current working directory after tests run to avoid breaking other tests
            _previousWorkingDirectory = Directory.GetCurrentDirectory();
        }

        public TestContext TestContext { get; set; } = null!;

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_previousWorkingDirectory);
        }

        [TestMethod]
        public void RunParserCanGetArgumentFromDoubleDash()
        {
            var tam = new TestAssetsManager(new TestContextOutputHelper(TestContext));
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            var projectPath = Path.Combine(newWorkingDir, "HelloWorld.csproj");

            var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath, "--", "foo" });
            runCommand.ApplicationArgs.Single().Should().Be("foo");
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void RunParserAcceptsWindowsPathSeparatorsOnWindows()
        {
            var tam = new TestAssetsManager(new TestContextOutputHelper(TestContext));
            var testAsset = tam.CopyTestAsset("HelloWorld").WithSource();
            var newWorkingDir = testAsset.Path;

            Directory.SetCurrentDirectory(newWorkingDir);
            var projectPath = @".\HelloWorld.csproj";
            // Should not throw on Windows
            var runCommand = RunCommand.FromArgs(new[] { "--project", projectPath });
            runCommand.ProjectFileFullPath.Should().NotBeNull();
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
        public void RunParserAcceptsWindowsPathSeparatorsOnLinux()
        {
            var tam = new TestAssetsManager(new TestContextOutputHelper(TestContext));
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
