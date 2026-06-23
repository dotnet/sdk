// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Store;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [DoNotParallelize]
    [TestClass]
    public class GivenDotnetStoreInvocation : SdkTest
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context) => TelemetryClient.DisabledForTests = true;

        string[] ExpectedPrefix = ["-maxcpucount", "--verbosity:m", "-tlp:default=auto", "--nologo", "--target:ComposeStore", "<project>"];
        static readonly string[] ArgsPrefix = ["--manifest", "<project>"];
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetStoreInvocation));

        [TestMethod]
        [DataRow("-m")]
        [DataRow("--manifest")]
        public void ItAddsProjectToMsbuildInvocation(string optionName)
        {
            var msbuildPath = "<msbuildpath>";
            string[] args = new string[] { optionName, "<project>" };
            StoreCommand.FromArgs(args, msbuildPath)
                .GetArgumentTokensToMSBuild().Should().Contain(ExpectedPrefix);
        }

        [TestMethod]
        [DataRow(new string[] { "-f", "<tfm>" }, @"--property:TargetFramework=<tfm>")]
        [DataRow(new string[] { "--framework", "<tfm>" }, @"--property:TargetFramework=<tfm>")]
        [DataRow(new string[] { "-r", "<rid>" }, @"--property:RuntimeIdentifier=<rid> --property:_CommandLineDefinedRuntimeIdentifier=true")]
        [DataRow(new string[] { "-r", "linux-amd64" }, @"--property:RuntimeIdentifier=linux-x64 --property:_CommandLineDefinedRuntimeIdentifier=true")]
        [DataRow(new string[] { "--runtime", "<rid>" }, @"--property:RuntimeIdentifier=<rid> --property:_CommandLineDefinedRuntimeIdentifier=true")]
        [DataRow(new string[] { "--use-current-runtime" }, "--property:UseCurrentRuntimeIdentifier=True")]
        [DataRow(new string[] { "--ucr" }, "--property:UseCurrentRuntimeIdentifier=True")]
        [DataRow(new string[] { "--manifest", "one.xml", "--manifest", "two.xml", "--manifest", "three.xml" }, @"--property:AdditionalProjects=<cwd>one.xml%3B<cwd>two.xml%3B<cwd>three.xml")]
        [DataRow(new string[] { "--disable-build-servers" }, "--property:UseRazorBuildServer=false --property:UseSharedCompilation=false /nodeReuse:false")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                args = ArgsPrefix.Concat(args).ToArray();
                string[] expectedarr =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory)
                    .Split(" ", StringSplitOptions.RemoveEmptyEntries);

                var msbuildPath = "<msbuildpath>";
                List<string> expected = [.. ExpectedPrefix, .. expectedarr];
                expected.Should().BeSubsetOf(
                    StoreCommand.FromArgs(args, msbuildPath).GetArgumentTokensToMSBuild()
                );
            });
        }

        [TestMethod]
        [DataRow("-o")]
        [DataRow("--output")]
        public void ItAddsOutputPathToMsBuildInvocation(string optionName)
        {
            string path = Path.Combine("some", "path");
            var args = ArgsPrefix.Concat(new string[] { optionName, path }).ToArray();

            var msbuildPath = "<msbuildpath>";
            StoreCommand.FromArgs(args, msbuildPath)
                .GetArgumentTokensToMSBuild().Should().BeEquivalentTo([..ExpectedPrefix, $"--property:ComposeDir={Path.GetFullPath(path)}{Path.DirectorySeparatorChar}", "--property:_CommandLineDefinedOutputPath=true"]);
        }
    }
}

