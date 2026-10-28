// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace EndToEnd.Tests
{
    [TestClass]
    public class GivenDotnetUsesDotnetTools : SdkTest
    {
        [TestMethod]
        public void ThenOneDotnetToolsCanBeCalled()
        {
            new DotnetCommand(Log)
                .Execute("dev-certs", "--help")
                .Should().Pass();
        }
    }
}
