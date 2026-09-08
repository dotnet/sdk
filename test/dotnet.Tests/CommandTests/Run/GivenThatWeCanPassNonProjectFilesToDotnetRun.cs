// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    [TestClass]
    public class GivenThatWeCanPassNonProjectFilesToDotnetRun : SdkTest
    {
        public GivenThatWeCanPassNonProjectFilesToDotnetRun()
        {
        }

        [TestMethod]
        public void ItFailsWithAnAppropriateErrorMessage()
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferences")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "SlnFileWithNoProjectReferences.sln");

            new DotnetCommand(Log, "run")
                .Execute($"-p", slnFullPath)
                .Should().Fail()
                .And.HaveStdErrContaining(
                    string.Format(
                        CliCommandStrings.RunCommandSpecifiedFileIsNotAValidProject,
                        slnFullPath));
        }
    }
}
