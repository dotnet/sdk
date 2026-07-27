// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tests.TelemetryTests;
using BuildCommand = Microsoft.DotNet.Cli.Commands.Build.BuildCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [TestClass]
    public class GivenDotnetBuildInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo"];
        public static string[] RestoreExpectedPrefixForImplicitRestore = [.. RestoringCommand.RestoreOptimizationProperties.Select(kvp => $"--restoreProperty:{kvp.Key}={kvp.Value}")];
        public static string[] RestoreExpectedPrefixForSeparateRestore = [.. RestoringCommand.RestoreOptimizationProperties.Select(kvp => $"--property:{kvp.Key}={kvp.Value}")];

        const string NugetInteractiveProperty = "--property:NuGetInteractive=false";

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetBuildInvocation));

        [TestMethod]
        [DataRow(new string[] { }, new string[] { })]
        [DataRow(new string[] { "-o", "myoutput" }, new string[] { "--property:OutputPath=<cwd>myoutput", "--property:_CommandLineDefinedOutputPath=true" })]
        [DataRow(new string[] { "-property:Verbosity=diag" }, new string[] { "--property:Verbosity=diag" })]
        [DataRow(new string[] { "--output", "myoutput" }, new string[] { "--property:OutputPath=<cwd>myoutput", "--property:_CommandLineDefinedOutputPath=true" })]
        [DataRow(new string[] { "--artifacts-path", "foo" }, new string[] { "--property:ArtifactsPath=<cwd>foo" })]
        [DataRow(new string[] { "-o", "foo1 myoutput" }, new string[] { "--property:OutputPath=<cwd>foo1 myoutput", "--property:_CommandLineDefinedOutputPath=true" })]
        [DataRow(new string[] { "--no-incremental" }, new string[] { "--target:Rebuild" })]
        [DataRow(new string[] { "-r", "rid" }, new string[] { "--property:RuntimeIdentifier=rid", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [DataRow(new string[] { "-r", "linux-amd64" }, new string[] { "--property:RuntimeIdentifier=linux-x64", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [DataRow(new string[] { "--runtime", "rid" }, new string[] { "--property:RuntimeIdentifier=rid", "--property:_CommandLineDefinedRuntimeIdentifier=true" })]
        [DataRow(new string[] { "--use-current-runtime" }, new string[] { "--property:UseCurrentRuntimeIdentifier=True" })]
        [DataRow(new string[] { "--ucr" }, new string[] { "--property:UseCurrentRuntimeIdentifier=True" })]
        [DataRow(new string[] { "-c", "config" }, new string[] { "--property:Configuration=config" })]
        [DataRow(new string[] { "--configuration", "config" }, new string[] { "--property:Configuration=config" })]
        [DataRow(new string[] { "--version-suffix", "mysuffix" }, new string[] { "--property:VersionSuffix=mysuffix" })]
        [DataRow(new string[] { "--no-dependencies" }, new string[] { "--property:BuildProjectReferences=false" })]
        [DataRow(new string[] { "-v", "diag" }, new string[] { "--verbosity:diag" })]
        [DataRow(new string[] { "--verbosity", "diag" }, new string[] { "--verbosity:diag" })]
        [DataRow(new string[] { "--no-incremental", "-o", "myoutput", "-r", "myruntime", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
                   new string[] { "--target:Rebuild", "--property:RuntimeIdentifier=myruntime", "--property:_CommandLineDefinedRuntimeIdentifier=true", "--verbosity:diag", "--property:OutputPath=<cwd>myoutput", "--property:_CommandLineDefinedOutputPath=true", "/ArbitrarySwitchForMSBuild" })]
        [DataRow(new string[] { "/t:CustomTarget" }, new string[] { "--target:CustomTarget" })]
        [DataRow(new string[] { "--disable-build-servers" }, new string[] { "--property:UseRazorBuildServer=false", "--property:UseSharedCompilation=false", "/nodeReuse:false" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs = expectedAdditionalArgs.Select(arg => arg.Replace("<cwd>", WorkingDirectory).Replace("myoutput", "myoutput" + Path.DirectorySeparatorChar)).ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = (RestoringCommand)BuildCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand.Should().BeNull();
                var commandArgs = command.GetArgumentTokensToMSBuild();
                List<string> expectedArgs = [.. ExpectedPrefix, "-restore", "-consoleloggerparameters:Summary", NugetInteractiveProperty, .. expectedAdditionalArgs, .. RestoreExpectedPrefixForImplicitRestore];
                expectedArgs.Should().BeSubsetOf(commandArgs);
            });
        }

        [TestMethod]
        public void NoRestoreMeansNoSeparateRestoreCommand()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = (RestoringCommand)BuildCommand.FromArgs(new[] { "--no-restore" }, msbuildPath);

                command.SeparateRestoreCommand.Should().BeNull();
                command.GetArgumentTokensToMSBuild().Should().NotContain("-restore");
            });
        }

        [TestMethod]
        [DataRow(new string[] { "-f", "tfm" },
            new string[] { "--target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "--property:TargetFramework=tfm" })]
        [DataRow(new string[] { "-p:TargetFramework=tfm" },
            new string[] { "--target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "--property:TargetFramework=tfm" })]
        [DataRow(new string[] { "/p:TargetFramework=tfm" },
            new string[] { "--target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "--property:TargetFramework=tfm" })]
        [DataRow(new string[] { "-t:Run", "-f", "tfm" },
            new string[] { "--target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "--property:TargetFramework=tfm", "--target:Run" })]
        [DataRow(new string[] { "/t:Run", "-f", "tfm" },
            new string[] { "--target:Restore", "-tlp:verbosity=quiet" },
            new string[] { "--property:TargetFramework=tfm", "--target:Run" })]
        [DataRow(new string[] { "-o", "myoutput", "-f", "tfm", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
            new string[] { "--target:Restore", "-tlp:verbosity=quiet", "--verbosity:diag", "--property:OutputPath=<cwd>myoutput", "--property:_CommandLineDefinedOutputPath=true", "/ArbitrarySwitchForMSBuild" },
            new string[] { "--property:TargetFramework=tfm", "--verbosity:diag", "--property:OutputPath=<cwd>myoutput", "--property:_CommandLineDefinedOutputPath=true", "/ArbitrarySwitchForMSBuild" })]
        [DataRow(new string[] { "-f", "tfm", "-getItem:Compile", "-getProperty:TargetFramework", "-getTargetResult:Build" },
            new string[] { "--target:Restore", "-tlp:verbosity=quiet", "--nologo", "--verbosity:quiet" },
            new string[] { "--property:TargetFramework=tfm", "--getItem:Compile", "--getProperty:TargetFramework", "--getTargetResult:Build" })]
        public void MsbuildInvocationIsCorrectForSeparateRestore(
            string[] args,
            string[] expectedAdditionalArgsForRestore,
            string[] expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgsForRestore = expectedAdditionalArgsForRestore
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory).Replace("myoutput", "myoutput" + Path.DirectorySeparatorChar))
                    .ToArray();

                expectedAdditionalArgs = expectedAdditionalArgs
                    .Select(arg => arg.Replace("<cwd>", WorkingDirectory).Replace("myoutput", "myoutput" + Path.DirectorySeparatorChar))
                    .ToArray();

                var msbuildPath = "<msbuildpath>";
                var command = (RestoringCommand)BuildCommand.FromArgs(args, msbuildPath);

                List<string> expectedItems = [.. ExpectedPrefix, NugetInteractiveProperty, .. expectedAdditionalArgsForRestore, .. RestoreExpectedPrefixForSeparateRestore];
                expectedItems.Should().BeSubsetOf(command.SeparateRestoreCommand!.GetArgumentTokensToMSBuild());

                command.GetArgumentTokensToMSBuild()
                    .Should()
                    .BeEquivalentTo([.. ExpectedPrefix, "-consoleloggerparameters:Summary", NugetInteractiveProperty, .. expectedAdditionalArgs]);
            });
        }

        [TestMethod]
        [DynamicData(nameof(TelemetryCommonPropertiesTests.LLMTelemetryTestCases), typeof(TelemetryCommonPropertiesTests))]
        public void WhenLLMIsDetectedTLLiveUpdateIsDisabled(Dictionary<string, string>? llmEnvVarsToSet, string? expectedLLMName)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                try
                {
                    // Set environment variables to simulate LLM environment
                    if (llmEnvVarsToSet is not null)
                    {
                        foreach (var (key, value) in llmEnvVarsToSet)
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }

                    var command = (RestoringCommand)BuildCommand.FromArgs([]);

                    if (expectedLLMName is not null)
                    {
                        command.GetArgumentTokensToMSBuild().Should().Contain(Constants.TerminalLogger_DisableNodeDisplay);
                    }
                    else
                    {
                        command.GetArgumentTokensToMSBuild().Should().NotContain(Constants.TerminalLogger_DisableNodeDisplay);
                    }
                }
                finally
                {
                    // Clear the environment variables after the test
                    if (llmEnvVarsToSet is not null)
                    {
                        foreach (var (key, value) in llmEnvVarsToSet)
                        {
                            Environment.SetEnvironmentVariable(key, null);
                        }
                    }
                }
            });
        }
    }
}

