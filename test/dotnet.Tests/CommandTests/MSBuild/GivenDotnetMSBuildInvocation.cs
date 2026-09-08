// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MSBuildCommand = Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenDotnetMSBuildInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        private static readonly string[] ExpectedPrefix = [ "-maxcpucount", "--verbosity:m", "-tlp:default=auto" ];
        private static readonly string WorkingDirectory = TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetPackInvocation));

        [TestMethod]
        [DataRow(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = MSBuildCommand.FromArgs(args, msbuildPath);

                command.GetArgumentTokensToMSBuild().Should().BeEquivalentTo([..ExpectedPrefix, ..expectedAdditionalArgs]);
            });
        }
    }
}

