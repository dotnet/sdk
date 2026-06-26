// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using PackCommand = Microsoft.DotNet.Cli.Commands.Pack.PackCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenDotnetPackInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        private static readonly string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo", "-restore", "--target:Pack"];
        private static readonly string[] ExpectedNoBuildPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo", "--target:Pack"];
        private readonly string[] ExpectedProperties = ["--property:_IsPacking=true", "--property:NuGetInteractive=false"];

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetPackInvocation));

        [TestMethod]
        [DataRow(new string[] { }, new string[] { })]
        [DataRow(new string[] { "-o", "<packageoutputpath>" }, new string[] { "--property:PackageOutputPath=<cwd><packageoutputpath>" })]
        [DataRow(new string[] { "--output", "<packageoutputpath>" }, new string[] { "--property:PackageOutputPath=<cwd><packageoutputpath>" })]
        [DataRow(new string[] { "--artifacts-path", "foo" }, new string[] { "--property:ArtifactsPath=<cwd>foo" })]
        [DataRow(new string[] { "--no-build" }, new string[] { "--property:NoBuild=true" })]
        [DataRow(new string[] { "--include-symbols" }, new string[] { "--property:IncludeSymbols=true" })]
        [DataRow(new string[] { "--include-source" }, new string[] { "--property:IncludeSource=true" })]
        [DataRow(new string[] { "-c", "<config>" }, new string[] { "--property:Configuration=<config>", "--property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [DataRow(new string[] { "--configuration", "<config>" }, new string[] { "--property:Configuration=<config>", "--property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true" })]
        [DataRow(new string[] { "--version-suffix", "<versionsuffix>" }, new string[] { "--property:VersionSuffix=<versionsuffix>" })]
        [DataRow(new string[] { "-s" }, new string[] { "--property:Serviceable=true" })]
        [DataRow(new string[] { "--serviceable" }, new string[] { "--property:Serviceable=true" })]
        [DataRow(new string[] { "-v", "diag" }, new string[] { "--verbosity:diag" })]
        [DataRow(new string[] { "--verbosity", "diag" }, new string[] { "--verbosity:diag" })]
        [DataRow(new string[] { "<project>" }, new string[] { "<project>" })]
        [DataRow(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = (PackCommand)PackCommand.FromArgs(args, msbuildPath);
                var expectedPrefix = args.FirstOrDefault() == "--no-build" ? ExpectedNoBuildPrefix : [.. ExpectedPrefix, .. GivenDotnetBuildInvocation.RestoreExpectedPrefixForImplicitRestore];

                command.SeparateRestoreCommand.Should().BeNull();
                List<string> expectedArgs = [.. expectedPrefix, .. ExpectedProperties, .. expectedAdditionalArgs,];
                expectedArgs.Should().BeSubsetOf(command.GetArgumentTokensToMSBuild());

            });
        }
    }
}

