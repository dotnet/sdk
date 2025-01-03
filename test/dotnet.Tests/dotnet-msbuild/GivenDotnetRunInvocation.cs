// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Run;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetRunInvocation(ITestOutputHelper log) : SdkTest(log), IClassFixture<NullCurrentSessionIdFixture>
    {
        [Theory]
        [InlineData(new string[] { "-p:prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "--property:prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "--property", "prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "-p", "prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "-p", "prop1=true", "-p", "prop2=false" }, new string[] { "--property:prop1=true", "--property:prop2=false" })]
        [InlineData(new string[] { "-p:prop1=true;prop2=false" }, new string[] { "--property:prop1=true", "--property:prop2=false" })]
        [InlineData(new string[] { "-p", "MyProject.csproj", "-p:prop1=true" }, new string[] { "--property:prop1=true" })]
        [InlineData(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedArgs)
        {

            string[] constantRestoreArgs = ["-nologo", "-verbosity:quiet"];
            string[] fullExpectedArgs = constantRestoreArgs.Concat(expectedArgs).ToArray();
            var oldWorkingDirectory = Directory.GetCurrentDirectory();
            var newWorkingDir = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: $"{nameof(MsbuildInvocationIsCorrect)}_{args.GetHashCode()}_{expectedArgs.GetHashCode()}").WithSource().Path;
            try
            {
                Directory.SetCurrentDirectory(newWorkingDir);

                CommandDirectoryContext.PerformActionWithBasePath(newWorkingDir, () =>
                {
                    var command = RunCommand.FromArgs(args);
                    command.RestoreArgs
                        .Should()
                        .BeEquivalentTo(fullExpectedArgs);
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(oldWorkingDirectory);
            }
        }
    }
}
