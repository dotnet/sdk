﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanCommand = Microsoft.DotNet.Tools.Clean.CleanCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetCleanInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        const string ExpectedPrefix = "-maxcpucount -verbosity:m -tlp:default=auto -nologo -verbosity:normal -target:Clean";

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetCleanInvocation));

        [Fact]
        public void ItAddsProjectToMsbuildInvocation()
        {
            var msbuildPath = "<msbuildpath>";
            CleanCommand.FromArgs(new string[] { "<project>" }, msbuildPath)
                .GetArgumentsToMSBuild().Should().Be("-maxcpucount -verbosity:m -tlp:default=auto -nologo -verbosity:normal <project> -target:Clean");
        }

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-o", "<output>" }, "-property:OutputPath=<cwd><output> -property:_CommandLineDefinedOutputPath=true")]
        [InlineData(new string[] { "--output", "<output>" }, "-property:OutputPath=<cwd><output> -property:_CommandLineDefinedOutputPath=true")]
        [InlineData(new string[] { "--artifacts-path", "foo" }, "-property:ArtifactsPath=<cwd>foo")]
        [InlineData(new string[] { "-f", "<framework>" }, "-property:TargetFramework=<framework>")]
        [InlineData(new string[] { "--framework", "<framework>" }, "-property:TargetFramework=<framework>")]
        [InlineData(new string[] { "-c", "<configuration>" }, "-property:Configuration=<configuration>")]
        [InlineData(new string[] { "--configuration", "<configuration>" }, "-property:Configuration=<configuration>")]
        [InlineData(new string[] { "-r", "<rid>" }, "-property:RuntimeIdentifier=<rid> -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "-r", "linux-amd64" }, "-property:RuntimeIdentifier=linux-x64 -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "-r", "linux-arm64" }, "-property:RuntimeIdentifier=linux-arm64 -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--runtime", "<rid>" }, "-property:RuntimeIdentifier=<rid> -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--runtime", "linux-amd64" }, "-property:RuntimeIdentifier=linux-x64 -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--runtime", "linux-arm64" }, "-property:RuntimeIdentifier=linux-arm64 -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--os", "linux" }, "-property:RuntimeIdentifier=linux-x64")]
        [InlineData(new string[] { "--os", "linux", "--arch", "amd64" }, "-property:RuntimeIdentifier=linux-x64")]
        [InlineData(new string[] { "--os", "linux", "--arch", "arm64" }, "-property:RuntimeIdentifier=linux-arm64")]
        [InlineData(new string[] { "-v", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "--verbosity", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "--disable-build-servers" }, "-p:UseRazorBuildServer=false -p:UseSharedCompilation=false /nodeReuse:false")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                CleanCommand.FromArgs(args, msbuildPath)
                    .GetArgumentsToMSBuild().Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
            });
        }
    }
}
