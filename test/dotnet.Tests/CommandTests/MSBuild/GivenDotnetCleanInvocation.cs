// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanCommand = Microsoft.DotNet.Cli.Commands.Clean.CleanCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [DoNotParallelize]
    [TestClass]
    public class GivenDotnetCleanInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        private const string NugetInteractiveProperty = "--property:NuGetInteractive=false";
        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo", "--verbosity:normal", "--target:Clean", NugetInteractiveProperty];


        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetCleanInvocation));

        [TestMethod]
        public void ItAddsProjectToMsbuildInvocation()
        {
            var msbuildPath = "<msbuildpath>";
            ((CleanCommand)CleanCommand.FromArgs(new string[] { "<project>" }, msbuildPath))
                .GetArgumentTokensToMSBuild()
                .Should()
                .BeEquivalentTo([.. ExpectedPrefix, "<project>"]);
        }

        [TestMethod]
        [DataRow(new string[] { }, new string[] { })]
        [DataRow(new string[] { "-o", "<output>" },
            new string[] { "--property:OutputPath=<cwd><output>", "--property:_CommandLineDefinedOutputPath=true" })]
        [DataRow(new string[] { "--output", "<output>" },
            new string[] { "--property:OutputPath=<cwd><output>", "--property:_CommandLineDefinedOutputPath=true" })]
        [DataRow(new string[] { "--artifacts-path", "foo" },
            new string[] { "--property:ArtifactsPath=<cwd>foo" })]
        [DataRow(new string[] { "-f", "<framework>" },
            new string[] { "--property:TargetFramework=<framework>" })]
        [DataRow(new string[] { "--framework", "<framework>" },
            new string[] { "--property:TargetFramework=<framework>" })]
        [DataRow(new string[] { "-c", "<configuration>" },
            new string[] { "--property:Configuration=<configuration>" })]
        [DataRow(new string[] { "--configuration", "<configuration>" },
            new string[] { "--property:Configuration=<configuration>" })]
        [DataRow(new string[] { "-v", "diag" },
            new string[] { "--verbosity:diag" })]
        [DataRow(new string[] { "--verbosity", "diag" },
            new string[] { "--verbosity:diag" })]
        [DataRow(new string[] { "--disable-build-servers" },
            new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory).Replace("<output>", "<output>" + Path.DirectorySeparatorChar))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                ((CleanCommand)CleanCommand.FromArgs(args, msbuildPath))
                    .GetArgumentTokensToMSBuild()
                    .Should()
                    .BeSubsetOf([.. ExpectedPrefix, .. expectedAdditionalArgs]);
            });
        }
    }
}

