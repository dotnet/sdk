// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.TestFramework;
using UnixOnlyTheoryAttribute = Microsoft.DotNet.Tools.Test.Utilities.UnixOnlyTheoryAttribute;
using NewCommandShim = Microsoft.DotNet.Tools.Test.Utilities.NewCommandShim;
using TestBase = Microsoft.DotNet.Tools.Test.Utilities.TestBase;
using static Microsoft.DotNet.Tools.Test.Utilities.TestCommandExtensions;

namespace EndToEnd.Tests
{
    public class GivenUnixPlatform : TestBase
    {
        [UnixOnlyTheory(Skip="https://github.com/dotnet/templating/issues/1979")]
        [InlineData("wpf")]
        [InlineData("winforms")]
        public void ItDoesNotIncludeWindowsOnlyProjectTemplates(string template)
        {
            var directory = TestAssets.CreateTestDirectory();

            new NewCommandShim()
                .WithWorkingDirectory(directory.FullName)
                .Execute(template).Should().Fail()
                    .And.HaveStdErrContaining($": {template}.");
        }
    }
}
