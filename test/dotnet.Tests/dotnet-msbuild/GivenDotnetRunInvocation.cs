// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Run;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetRunInvocation : IClassFixture<NullCurrentSessionIdFixture>, IDisposable
    {
        private string WorkingDirectory { get; init; }
        private string OldDir { get; init; }
        public GivenDotnetRunInvocation(ITestOutputHelper log)
        {
            var tam = new TestAssetsManager(log);
            WorkingDirectory = tam.CopyTestAsset("HelloWorld").WithSource().Path;
            OldDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(WorkingDirectory);
        }

        [Theory]
        [InlineData(new string[] { "-p:prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "--property:prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "--property", "prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "-p", "prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "-p", "prop1=true", "-p", "prop2=false" }, new string[] { "--property:prop1=true", "--property:prop2=false" })]
        [InlineData(new string[] { "-p:prop1=true;prop2=false" }, new string[] { "--property:prop1=true", "--property:prop2=false" })]
        [InlineData(new string[] { "-p", "MyProject.csproj", "-p:prop1=true" }, new string[] { "--property:prop1=true" })]
        // The longhand --property option should never be treated as a project
        [InlineData(new string[] { "--property", "MyProject.csproj", "-p:prop1=true" }, new string[] { "--property:MyProject.csproj", "--property:prop1=true" })]
        [InlineData(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedArgs)
        {

            string[] constantRestoreArgs = ["-nologo", "-verbosity:quiet"];
            string[] fullExpectedArgs = constantRestoreArgs.Concat(expectedArgs).ToArray();

            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var command = RunCommand.FromArgs(args);
                command.RestoreArgs
                    .Should()
                    .BeEquivalentTo(fullExpectedArgs);
            });
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(OldDir);
        }
    }
}
