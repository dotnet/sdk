// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Authoring.CLI.IntegrationTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public class ValidateCommandTests : TestBase
    {
        private readonly ITestOutputHelper _log;

        public ValidateCommandTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public Task ValidateCommand_BasicTest()
        {
            CommandResult commandResult = new BasicCommand(
                          _log,
                          "dotnet",
                          Path.GetFullPath("Microsoft.TemplateEngine.Authoring.CLI.dll"),
                          "validate",
                          Path.Combine(TestTemplatesLocation, "Invalid"))
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(FormatOutputStreams(commandResult))
               .UniqueForOSPlatform()
               .AddScrubber(text => text.Replace(Path.Combine(TestTemplatesLocation, "Invalid"), "%TEMPLATE_LOCATION%"));
        }

        private static string FormatOutputStreams(CommandResult commandResult)
        {
            return "StdErr:" + Environment.NewLine + commandResult.StdErr + Environment.NewLine + "StdOut:" + Environment.NewLine + commandResult.StdOut;
        }
    }
}
