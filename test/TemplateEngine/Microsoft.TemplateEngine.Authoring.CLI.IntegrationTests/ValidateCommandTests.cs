// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Authoring.CLI.IntegrationTests
{
    [TestClass]
    public class ValidateCommandTests : TestBase
    {
        private TestContext _testContext = null!;

        public TestContext TestContext
        {
            get => _testContext;
            set
            {
                _testContext = value;
                VerifyMSTest.Verifier.CurrentTestContext.Value = new VerifyMSTest.TestExecutionContext(value, GetType());
            }
        }

        private ILogger Log => new TestContextLogger(TestContext);

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            new VerifySettingsFixture();
        }

        [TestMethod]
        public Task ValidateCommand_BasicTest()
        {
            CommandResult commandResult = new BasicCommand(
                          Log,
                          "dotnet",
                          Path.GetFullPath("Microsoft.TemplateEngine.Authoring.CLI.dll"),
                          "validate",
                          Path.Combine(TestTemplatesLocation, "Invalid"))
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verifier.Verify(FormatOutputStreams(commandResult))
               .UniqueForOSPlatform()
               .AddScrubber(text => text.Replace(Path.Combine(TestTemplatesLocation, "Invalid"), "%TEMPLATE_LOCATION%"))
               //warning can appear in a different order, therefore scrubbing them
               .AddScrubber(text => ScrubByRegex(text, "(warn: Template Engine\\[0\\]\\r?\\n)([^\\r\\n]*)", $"warn: Template Engine: %scrubbed%", RegexOptions.Multiline));
        }

        private static string FormatOutputStreams(CommandResult commandResult)
        {
            return "StdErr:" + Environment.NewLine + commandResult.StdErr + Environment.NewLine + "StdOut:" + Environment.NewLine + commandResult.StdOut;
        }

        private static void ScrubByRegex(StringBuilder output, string pattern, string replacement, RegexOptions regexOptions = RegexOptions.None)
        {
            string finalOutput = Regex.Replace(output.ToString(), pattern, replacement, regexOptions);
            output.Clear();
            output.Append(finalOutput);
        }
    }
}
