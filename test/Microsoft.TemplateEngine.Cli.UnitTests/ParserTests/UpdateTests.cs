// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    public class UpdateTests : BaseTest
    {
        [TestMethod]
        [DataRow("--add-source")]
        [DataRow("--nuget-source")]
        public void Update_CanParseAddSourceOption(string optionName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new update {optionName} my-custom-source");
            UpdateCommandArgs args = new(parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.HasCount(1, args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
        }

        [TestMethod]
        [DataRow("--update-apply")]
        [DataRow("--update-check")]
        [DataRow("update")]
        public void Update_Error_WhenArguments(string commandName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new {commandName} source");

            Assert.IsTrue(parseResult.Errors.Any());
            Assert.IsTrue(parseResult.Errors.Any(error => error.Message.Contains("Unrecognized command or argument 'source'")));
        }

        [TestMethod]
        [DataRow("new update --add-source my-custom-source1 my-custom-source2")]
        [DataRow("new update --check-only --add-source my-custom-source1 --add-source my-custom-source2")]
        public void Update_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new(parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.AreEqual(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
        }

        [TestMethod]
        public void Update_CanParseInteractiveOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new update --interactive");
            UpdateCommandArgs args = new(parseResult);

            Assert.IsTrue(args.Interactive);

            parseResult = myCommand.Parse($"new update");
            args = new UpdateCommandArgs(parseResult);

            Assert.IsFalse(args.Interactive);
        }

        [TestMethod]
        [DataRow("--check-only")]
        [DataRow("--dry-run")]
        public void Update_CanParseCheckOnlyOption(string optionAlias)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new update {optionAlias}");
            UpdateCommandArgs args = new(parseResult);

            Assert.IsTrue(args.CheckOnly);

            parseResult = myCommand.Parse($"new update");
            args = new UpdateCommandArgs(parseResult);

            Assert.IsFalse(args.CheckOnly);
        }

        [TestMethod]
        public void Update_Legacy_CanParseCheckOnlyOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new --update-check");
            UpdateCommandArgs args = new(parseResult);

            Assert.IsTrue(args.CheckOnly);

            parseResult = myCommand.Parse($"new --update-apply");
            args = new UpdateCommandArgs(parseResult);

            Assert.IsFalse(args.CheckOnly);
        }

        [TestMethod]
        [DataRow("new --update-check --add-source my-custom-source")]
        [DataRow("new --update-apply --nuget-source my-custom-source")]
        [DataRow("new --nuget-source my-custom-source --update-apply")]
        public void Update_Legacy_CanParseAddSourceOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new(parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.HasCount(1, args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
        }

        [TestMethod]
        [DataRow("new --update-check source --interactive")]
        [DataRow("new --interactive --update-apply source")]
        public void Update_Legacy_CanParseInteractiveOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new(parseResult);

            Assert.IsTrue(args.Interactive);
        }

        [TestMethod]
        [DataRow("new --update-check --add-source my-custom-source1 --add-source my-custom-source2")]
        [DataRow("new --add-source my-custom-source1 --add-source my-custom-source2 --update-apply source")]
        [DataRow("new --add-source my-custom-source1 --update-apply --add-source my-custom-source2")]
        public void Update_Legacy_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse(testCase);
            UpdateCommandArgs args = new(parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.AreEqual(2, args.AdditionalSources.Count);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
        }

        [TestMethod]
        [DataRow("new --add-source my-custom-source update source", "'--add-source','my-custom-source'|'source'")]
        [DataRow("new --interactive update source", "'--interactive'|'source'")]
        [DataRow("new --language F# --update-check", "'--language','F#'")]
        [DataRow("new --language F# --update-apply", "'--language','F#'")]
        [DataRow("new --language F# update", "'--language','F#'")]
        [DataRow("new source1 source2 source3 --update-apply source", "'source1'|'source'|'source2','source3'")]
        [DataRow("new source1 --update-apply source", "'source1'|'source'")]
        public void Update_CanReturnParseError(string command, string expectedInvalidTokens)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            IEnumerable<string> errorMessages = parseResult.Errors.Select(error => error.Message);

            string[] expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.IsTrue(parseResult.Errors.Any());
            Assert.AreEqual(expectedInvalidTokenSets.Length, parseResult.Errors.Count);
            foreach (string tokenSet in expectedInvalidTokenSets)
            {
                Assert.IsTrue(errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}.") || errorMessages.Contains($"Unrecognized command or argument {tokenSet}."));
            }
        }

        [TestMethod]
        public void CommandExampleCanShowParentCommandsBeyondNew()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            Command rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new update");
            Assert.AreEqual("dotnet new update", Example.For<NewCommand>(parseResult).WithSubcommand<UpdateCommand>());
        }
    }
}
