// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Publish.Tests
{
    [TestClass]
    public class GivenThatWeWantToBuildANetCoreAppWithWap : SdkTest
    {

        [TestMethod]
        [FullMSBuildOnly]
        public void WhenNetCoreProjectIsReferencedByAWapProject()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset("TestAppWithWapAndWpf")
                .WithSource();

            new BuildCommand(testInstance, "WapProjTemplate1")
                .Execute()
                .Should()
                .Pass();
        }
    }
}
