// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetRunInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private static readonly string[] ConstantRestoreArgs = ["-nologo", "-verbosity:quiet"];
        private static readonly string NuGetDisabledProperty = "--property:NuGetInteractive=false";

        public ITestOutputHelper Log { get; }

        public GivenDotnetRunInvocation(ITestOutputHelper log)
        {
            Log = log;
        }

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
            var tam = new TestAssetsManager(Log);
            var oldWorkingDirectory = Directory.GetCurrentDirectory();
            var newWorkingDir = tam.CopyTestAsset("HelloWorld", identifier: $"{nameof(MsbuildInvocationIsCorrect)}_{args.GetHashCode()}_{expectedArgs.GetHashCode()}").WithSource().Path;

            try
            {
                Directory.SetCurrentDirectory(newWorkingDir);

                CommandDirectoryContext.PerformActionWithBasePath(newWorkingDir, () =>
                {
                    var command = RunCommand.FromArgs(args);
                    command.MSBuildArgs
                        .Should()
                        .BeEquivalentTo(MSBuildArgs.AnalyzeMSBuildArguments([.. ConstantRestoreArgs, .. expectedArgs, NuGetDisabledProperty ], CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, CommonOptions.MSBuildTargetOption(), RunCommandParser.VerbosityOption));
                });
            }
            finally
            {
                Directory.SetCurrentDirectory(oldWorkingDirectory);
            }
        }
    }
}
