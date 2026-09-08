// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.Tests;
using VerifyMSTest;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.UnitTests
{
    [TestClass]
    [UsesVerify]
    public partial class VerificationEngineTests
    {
        private ILogger Log => new TestContextLogger(TestContext);

        [TestMethod]
        public async Task DefaultExcludePatternsFilterNestedObjAndBinFiles()
        {
            string verifyLocation = "foo/bar/baz";

            Dictionary<string, string> files = new Dictionary<string, string>()
            {
                { "Program.cs", "hello world" },
                { "obj/Debug/net10.0/AssemblyInfo.cs", "generated assembly info" },
                { "obj/project.assets.json", "assets" },
                { "bin/Debug/net10.0/MyApp.dll", "binary" },
                { "bin/Debug/net10.0/MyApp.pdb", "symbols" },
                { "src/obj/nested/deep/file.txt", "deep nested obj" },
            };

            IPhysicalFileSystemEx fileSystem = A.Fake<IPhysicalFileSystemEx>();
            A.CallTo(() => fileSystem.EnumerateFiles(verifyLocation, "*", SearchOption.AllDirectories)).Returns(files.Keys);
            A.CallTo(() => fileSystem.ReadAllTextAsync(A<string>._, A<CancellationToken>._))
                .ReturnsLazily((string fileName, CancellationToken _) => Task.FromResult(files[fileName]));
            A.CallTo(() => fileSystem.PathRelativeTo(A<string>._, A<string>._))
                .ReturnsLazily((string target, string relativeTo) => target);

            Dictionary<string, string> resultContents = new Dictionary<string, string>();

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "console")
            {
                TemplateSpecificArgs = null,
                OutputDirectory = verifyLocation,
                UniqueFor = null,
            }
                .WithCustomDirectoryVerifier(
                    async (content, contentFetcher) =>
                    {
                        await foreach (var (filePath, scrubbedContent) in contentFetcher.Value)
                        {
                            resultContents[filePath] = scrubbedContent;
                        }
                    });

            await VerificationEngine.CreateVerificationTask(
                new VerificationEngine.CallerInfo()
                {
                    ContentDirectory = verifyLocation,
                    CallerSourceFile = "callerLocation",
                    CallerMethod = null
                },
                options,
                fileSystem);

            // Only Program.cs should remain - all obj/bin files (including nested) should be excluded
            resultContents.Keys.Should().BeEquivalentTo(new[] { "Program.cs" });
        }

        [TestMethod]
        public async Task CreateVerificationTaskWithCustomScrubbersAndVerifier()
        {
            string verifyLocation = "foo\\bar\\baz";

            Dictionary<string, string> files = new Dictionary<string, string>()
            {
                { "Program.cs", "aa bb cc" },
                { "Subfolder\\Class.cs", "123 456 789 aa" },
                { "out.dll", "a1 b2" }
            };

            IPhysicalFileSystemEx fileSystem = A.Fake<IPhysicalFileSystemEx>();
            A.CallTo(() => fileSystem.EnumerateFiles(verifyLocation, "*", SearchOption.AllDirectories)).Returns(files.Keys);
            A.CallTo(() => fileSystem.ReadAllTextAsync(A<string>._, A<CancellationToken>._))
                .ReturnsLazily((string fileName, CancellationToken _) => Task.FromResult(files[fileName]));
            A.CallTo(() => fileSystem.PathRelativeTo(A<string>._, A<string>._))
                .ReturnsLazily((string target, string relativeTo) => target);

            Dictionary<string, string> resultContents = new Dictionary<string, string>();

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "console")
            {
                TemplateSpecificArgs = null,
                OutputDirectory = verifyLocation,
                VerificationExcludePatterns = new[] { "*.dll" },
                UniqueFor = null,
            }
                .WithCustomScrubbers(
                    ScrubbersDefinition.Empty
                        .AddScrubber(sb => sb.Replace("bb", "xx"), "cs")
                        .AddScrubber(sb => sb.Replace("cc", "yy"), "dll")
                        .AddScrubber(sb => sb.Replace("123", "yy"), "dll")
                        .AddScrubber(sb => sb.Replace("aa", "zz"))
                        // supports multiple scrubbers per extension
                        .AddScrubber(sb => sb.Replace("cc", "**"), "cs"))
                .WithCustomDirectoryVerifier(
                    async (content, contentFetcher) =>
                    {
                        await foreach (var (filePath, scrubbedContent) in contentFetcher.Value)
                        {
                            resultContents[filePath] = scrubbedContent;
                        }
                    });

            await VerificationEngine.CreateVerificationTask(
                new VerificationEngine.CallerInfo()
                {
                    ContentDirectory = verifyLocation,
                    CallerSourceFile = "callerLocation",
                    CallerMethod = null
                },
                options,
                fileSystem);

            resultContents.Keys.Count.Should().Be(2);
            resultContents["Program.cs"].Should().BeEquivalentTo("zz xx **");
            resultContents["Subfolder\\Class.cs"].Should().BeEquivalentTo("123 456 789 zz");
        }

        [TestMethod]
        public async Task ExecuteFailsOnInstantiationFailure()
        {
            string workingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));
            string snapshotsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));

            ICommandRunner commandRunner = A.Fake<ICommandRunner>();
            A.CallTo(() => commandRunner.RunCommand(A<TestCommand>._))
                .Returns(new CommandResultData(20, "stdout content", "stderr content", workingDir));

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "made-up-template")
            {
                TemplateSpecificArgs = new string[] { "--a", "-b", "c", "--d" },
                DisableDiffTool = true,
                SnapshotsDirectory = snapshotsDir,
                OutputDirectory = workingDir,
                VerifyCommandOutput = true,
                UniqueFor = UniqueForOption.OsPlatform | UniqueForOption.OsPlatform,
            };

            VerificationEngine engine = new VerificationEngine(Log);
            Func<Task> executeTask = () => engine.Execute(options, TestContext.CancellationToken);
            await executeTask
                .Should()
                .ThrowAsync<TemplateVerificationException>()
                .Where(e => e.TemplateVerificationErrorCode == TemplateVerificationErrorCode.InstantiationFailed);
        }

        [TestMethod]
        public async Task ExecuteSucceedsOnExpectedInstantiationFailure()
        {
            string workingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));

            ICommandRunner commandRunner = A.Fake<ICommandRunner>();
            A.CallTo(() => commandRunner.RunCommand(A<TestCommand>._))
                .Returns(new CommandResultData(20, "stdout content", "stderr content", workingDir));

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "made-up-template")
            {
                TemplateSpecificArgs = new string[] { "--a", "-b", "c", "--d" },
                //DisableDiffTool = true,
                SnapshotsDirectory = TestBase.SnapshotsDirectory,
                IsCommandExpectedToFail = true,
                OutputDirectory = workingDir,
                VerifyCommandOutput = true,
            };

            VerificationEngine engine = new VerificationEngine(commandRunner, Log);
            await engine.Execute(options, TestContext.CancellationToken);
        }

        [TestMethod]
        public async Task ExecuteSucceedsOnExpectedInstantiationSuccess()
        {
            string workingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));

            ICommandRunner commandRunner = A.Fake<ICommandRunner>();
            A.CallTo(() => commandRunner.RunCommand(A<TestCommand>._))
                .Returns(new CommandResultData(0, "different stdout content", "another stderr content", workingDir));

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: "made-up-template")
            {
                TemplateSpecificArgs = new string[] { "--x", "y", "-z" },
                //DisableDiffTool = true,
                SnapshotsDirectory = TestBase.SnapshotsDirectory,
                IsCommandExpectedToFail = false,
                OutputDirectory = workingDir,
                VerifyCommandOutput = true,
            };

            VerificationEngine engine = new VerificationEngine(commandRunner, Log);
            await engine.Execute(options, TestContext.CancellationToken);
        }
    }
}
