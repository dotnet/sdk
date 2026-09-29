// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.BuildServer;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands
{
    [TestClass]
    public class BuildServerShutdownCommandTests : SdkTest
    {
        public BuildServerShutdownCommandTests()
        {
        }

        private readonly BufferedReporter _reporter = new();

        [TestMethod]
        public void GivenNoOptionsItEnumeratesAllServers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(serverProvider: provider.Object);

            command.Execute(TestContext.CancellationToken).Should().Be(0);

            _reporter.Lines.Should().Equal(CliCommandStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.All), Times.Once);
        }

        [TestMethod]
        public void GivenMSBuildOptionOnlyItEnumeratesOnlyMSBuildServers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.MSBuild))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(options: ["--msbuild"], serverProvider: provider.Object);

            command.Execute(TestContext.CancellationToken).Should().Be(0);

            _reporter.Lines.Should().Equal(CliCommandStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.MSBuild), Times.Once);
        }

        [TestMethod]
        public void GivenVBCSCompilerOptionOnlyItEnumeratesOnlyVBCSCompilers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.VBCSCompiler))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(options: ["--vbcscompiler"], serverProvider: provider.Object);

            command.Execute(TestContext.CancellationToken).Should().Be(0);

            _reporter.Lines.Should().Equal(CliCommandStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.VBCSCompiler), Times.Once);
        }

        [TestMethod]
        public void GivenRazorOptionOnlyItEnumeratesOnlyRazorServers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.Razor))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(options: ["--razor"], serverProvider: provider.Object);

            command.Execute(TestContext.CancellationToken).Should().Be(0);

            _reporter.Lines.Should().Equal(CliCommandStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.Razor), Times.Once);
        }

        [TestMethod]
        public void GivenSuccessfulShutdownsItPrintsSuccess()
        {
            var mocks = new[] {
                CreateServerMock("first"),
                CreateServerMock("second"),
                CreateServerMock("third")
            };

            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns(mocks.Select(m => m.Object));

            var command = CreateCommand(serverProvider: provider.Object);

            command.Execute(TestContext.CancellationToken).Should().Be(0);

            _reporter.Lines.Should().Equal(
                FormatShuttingDownMessage(mocks[0].Object),
                FormatShuttingDownMessage(mocks[1].Object),
                FormatShuttingDownMessage(mocks[2].Object),
                FormatSuccessMessage(mocks[0].Object),
                FormatSuccessMessage(mocks[1].Object),
                FormatSuccessMessage(mocks[2].Object));

            VerifyShutdownCalls(mocks);
        }

        [TestMethod]
        public void GivenAFailingShutdownItPrintsFailureMessage()
        {
            const string FirstFailureMessage = "first failed!";
            const string ThirdFailureMessage = "third failed!";

            var mocks = new[] {
                CreateServerMock("first", exceptionMessage: FirstFailureMessage),
                CreateServerMock("second"),
                CreateServerMock("third", exceptionMessage: ThirdFailureMessage)
            };

            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns(mocks.Select(m => m.Object));

            var command = CreateCommand(serverProvider: provider.Object);

            command.Execute(TestContext.CancellationToken).Should().Be(1);

            _reporter.Lines.Should().Equal(
                FormatShuttingDownMessage(mocks[0].Object),
                FormatShuttingDownMessage(mocks[1].Object),
                FormatShuttingDownMessage(mocks[2].Object),
                FormatFailureMessage(mocks[0].Object, FirstFailureMessage),
                FormatSuccessMessage(mocks[1].Object),
                FormatFailureMessage(mocks[2].Object, ThirdFailureMessage));

            VerifyShutdownCalls(mocks);
        }

        [TestMethod]
        public async Task GivenCancellationItCancelsTheServerShutdown()
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            using var shutdownStarted = new ManualResetEventSlim();
            using var shutdownFinished = new ManualResetEventSlim();
            var server = new Mock<IBuildServer>(MockBehavior.Strict);
            server.SetupGet(s => s.ProcessId).Returns(0);
            server.SetupGet(s => s.Name).Returns("server");
            server
                .Setup(s => s.Shutdown(cancellationSource.Token))
                .Callback<CancellationToken>(cancellationToken =>
                {
                    shutdownStarted.Set();
                    try
                    {
                        cancellationToken.WaitHandle.WaitOne();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    finally
                    {
                        shutdownFinished.Set();
                    }
                });

            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns([server.Object]);

            var command = CreateCommand(serverProvider: provider.Object);
            Task<int> execution = Task.Run(
                () => command.Execute(cancellationSource.Token),
                TestContext.CancellationToken);

            shutdownStarted.Wait(TestContext.CancellationToken);
            cancellationSource.Cancel();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => execution);
            shutdownFinished.Wait(TestContext.CancellationToken);
            server.Verify(s => s.Shutdown(cancellationSource.Token), Times.Once);
        }

        [TestMethod]
        public void GivenServerShutdownThrowsMatchingCancellationItPropagates()
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            var server = new Mock<IBuildServer>(MockBehavior.Strict);
            server.SetupGet(s => s.ProcessId).Returns(0);
            server.SetupGet(s => s.Name).Returns("server");
            server
                .Setup(s => s.Shutdown(cancellationSource.Token))
                .Throws(new OperationCanceledException(cancellationSource.Token));

            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns([server.Object]);

            var command = CreateCommand(serverProvider: provider.Object);

            var exception = Assert.ThrowsExactly<OperationCanceledException>(
                () => command.Execute(cancellationSource.Token));

            exception.CancellationToken.Should().Be(cancellationSource.Token);
            server.Verify(s => s.Shutdown(cancellationSource.Token), Times.Once);
        }

        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/3684")]
        public void GivenARunningRazorServerItShutsDownSuccessfully()
        {
            var pipeName = Path.GetRandomFileName();

            var pidDirectory = TestAssetsManager.CreateTestDirectory(identifier: "pidDirectory").Path;

            var testInstance = TestAssetsManager
                .CopyTestAsset("TestRazorApp")
                .WithSource();

            new BuildCommand(testInstance)
                .WithEnvironmentVariable(BuildServerProvider.PidFileDirectoryVariableName, pidDirectory)
                .Execute($"/p:_RazorBuildServerPipeName={pipeName}")
                .Should()
                .Pass();

            var files = Directory.GetFiles(pidDirectory, RazorPidFile.FilePrefix + "*");
            files.Length.Should().Be(1);

            var pidFile = RazorPidFile.Read(new FilePath(files.First()));
            pidFile.PipeName.Should().Be(pipeName);

            new BuildServerCommand(Log)
                .WithWorkingDirectory(testInstance.TestRoot)
                .WithEnvironmentVariable(BuildServerProvider.PidFileDirectoryVariableName, pidDirectory)
                .Execute("shutdown", "--razor")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(
                    string.Format(
                        CliCommandStrings.ShutDownSucceededWithPid,
                        CliStrings.RazorServer,
                        pidFile.ProcessId));
        }

        private BuildServerShutdownCommand CreateCommand(
            ReadOnlySpan<string> options = default,
            IBuildServerProvider serverProvider = null,
            IEnumerable<IBuildServer> buildServers = null,
            ServerEnumerationFlags expectedFlags = ServerEnumerationFlags.None)
        {
            ParseResult result = Parser.Parse(["dotnet", "build-server", "shutdown", .. options]);
            return new BuildServerShutdownCommand(
                result: result,
                serverProvider: serverProvider,
                useOrderedWait: true,
                reporter: _reporter);
        }

        private Mock<IBuildServer> CreateServerMock(string name, int pid = 0, string exceptionMessage = null)
        {
            var mock = new Mock<IBuildServer>(MockBehavior.Strict);

            mock.SetupGet(s => s.ProcessId).Returns(pid);
            mock.SetupGet(s => s.Name).Returns(name);

            if (exceptionMessage == null)
            {
                mock.Setup(s => s.Shutdown(It.IsAny<CancellationToken>()));
            }
            else
            {
                mock.Setup(s => s.Shutdown(It.IsAny<CancellationToken>())).Throws(new Exception(exceptionMessage));
            }

            return mock;
        }

        private void VerifyShutdownCalls(IEnumerable<Mock<IBuildServer>> mocks)
        {
            foreach (var mock in mocks)
            {
                mock.Verify(s => s.Shutdown(TestContext.CancellationToken), Times.Once);
            }
        }

        private static string FormatShuttingDownMessage(IBuildServer server)
        {
            if (server.ProcessId != 0)
            {
                return string.Format(CliCommandStrings.ShuttingDownServerWithPid, server.Name, server.ProcessId);
            }
            return string.Format(CliCommandStrings.ShuttingDownServer, server.Name);
        }

        private static string FormatSuccessMessage(IBuildServer server)
        {
            if (server.ProcessId != 0)
            {
                return string.Format(CliCommandStrings.ShutDownSucceededWithPid, server.Name, server.ProcessId).Green();
            }
            return string.Format(CliCommandStrings.ShutDownSucceeded, server.Name).Green();
        }

        private static string FormatFailureMessage(IBuildServer server, string message)
        {
            if (server.ProcessId != 0)
            {
                return string.Format(CliCommandStrings.ShutDownFailedWithPid, server.Name, server.ProcessId, message).Red();
            }
            return string.Format(CliCommandStrings.ShutDownFailed, server.Name, message).Red();
        }
    }
}
