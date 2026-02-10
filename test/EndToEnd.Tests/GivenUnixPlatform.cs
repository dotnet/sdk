// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace EndToEnd.Tests
{
    public class GivenUnixPlatform(ITestOutputHelper log) : SdkTest(log)
    {
        [UnixOnlyTheory]
        [InlineData("wpf")]
        [InlineData("winforms")]
        public void ItDoesNotIncludeWindowsOnlyProjectTemplates(string template)
        {
            var directory = _testAssetsManager.CreateTestDirectory(identifier: template);

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(directory.Path)
                .Execute(template).Should().Fail()
                    .And.HaveStdErrContaining($": '{template}'.");
        }
    }
}
