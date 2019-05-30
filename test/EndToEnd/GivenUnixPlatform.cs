using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace EndToEnd.Tests
{
    public class GivenUnixPlatform : TestBase
    {
        [UnixOnlyTheory]
        [InlineData("wpf")]
        [InlineData("winforms")]
        public void ItDoesNotIncludeWindowsOnlyProjectTemplates(string template)
        {
            var directory = TestAssets.CreateTestDirectory();

            new NewCommandShim()
                .WithWorkingDirectory(directory.FullName)
                .Execute(template)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining($": {template}.");
        }
    }
}
