// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewSearchTests
    {
        [Theory]
        [InlineData("--search")]
        [InlineData("search")]
        public Task CannotExecuteEmptyCriteria(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase)
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should().Fail();

            return Verify(commandResult.StdErr)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/sdk/issues/51147")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "search", "do-not-exist")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdOut);
        }
    }
}
