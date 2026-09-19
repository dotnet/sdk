// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToBuildASolutionWithProjRefDiffCase : SdkTest
    {

        [TestMethod]
        [OSCondition(OperatingSystems.Windows | OperatingSystems.OSX)]
        public void ItBuildsTheSolutionSuccessfully()
        {
            const string solutionFile = "AppWithProjRefCaseDiff.sln";

            var asset = TestAssetsManager
                .CopyTestAsset("AppWithProjRefCaseDiff")
                .WithSource();

            var command = new BuildCommand(asset, solutionFile);
            command.Execute().Should().Pass();
        }
    }
}
