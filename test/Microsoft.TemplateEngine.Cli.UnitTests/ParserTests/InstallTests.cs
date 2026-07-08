// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    public class InstallTests : BaseTest
    {
        [TestMethod]
        [DataRow("--add-source")]
        [DataRow("--nuget-source")]
        public void Install_CanParseAddSourceOption(string optionName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new install source {optionName} my-custom-source");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.HasCount(1, args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        public void Install_Error_NoArguments()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new install");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.Contains(error => error.Message.Contains("Required argument") && error.Message.Contains("missing"), parseResult.Errors);

            Assert.Throws<ArgumentException>(() => new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult));
        }

        [TestMethod]
        public void Install_Legacy_Error_NoArguments()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new --install --interactive");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.Contains(error => error.Message.Contains("Required argument") && error.Message.Contains("missing"), parseResult.Errors);

            Assert.Throws<ArgumentException>(() => new InstallCommandArgs((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult));
        }

        [TestMethod]
        [DataRow("new install source --add-source my-custom-source1 my-custom-source2")]
        [DataRow("new install source --add-source my-custom-source1 --add-source my-custom-source2")]
        public void Install_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.HasCount(2, args.AdditionalSources);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        public void Install_CanParseInteractiveOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new install source --interactive");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsTrue(args.Interactive);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);

            parseResult = myCommand.Parse($"new install source");
            args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsFalse(args.Interactive);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        public void Install_CanParseForceOption()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new install source --force");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsTrue(args.Force);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);

            parseResult = myCommand.Parse($"new install source");
            args = new InstallCommandArgs((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsFalse(args.Force);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        public void Install_CanParseMultipleArgs()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new install source1 source2");
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.HasCount(2, args.TemplatePackages);
            Assert.Contains("source1", args.TemplatePackages);
            Assert.Contains("source2", args.TemplatePackages);
        }

        [TestMethod]
        [DataRow("new --install source --add-source my-custom-source")]
        [DataRow("new --install source --nuget-source my-custom-source")]
        [DataRow("new --nuget-source my-custom-source --install source")]
        public void Install_Legacy_CanParseAddSourceOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.HasCount(1, args.AdditionalSources);
            Assert.Contains("my-custom-source", args.AdditionalSources);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        [DataRow("new --install source --interactive")]
        [DataRow("new --interactive --install source")]
        public void Install_Legacy_CanParseInteractiveOption(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsTrue(args.Interactive);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        [DataRow("new --install source1 --install source2")]
        [DataRow("new --install source1 source2")]
        public void Install_Legacy_CanParseMultipleArgs(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.HasCount(2, args.TemplatePackages);
            Assert.Contains("source1", args.TemplatePackages);
            Assert.Contains("source2", args.TemplatePackages);
        }

        [TestMethod]
        [DataRow("new --install source --add-source my-custom-source1 --add-source my-custom-source2")]
        [DataRow("new --add-source my-custom-source1 --add-source my-custom-source2 --install source")]
        [DataRow("new --add-source my-custom-source1 --install source --add-source my-custom-source2")]
        public void Install_Legacy_CanParseAddSourceOption_MultipleEntries(string testCase)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse(testCase);
            InstallCommandArgs args = new((LegacyInstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsNotNull(args.AdditionalSources);
            Assert.HasCount(2, args.AdditionalSources);
            Assert.Contains("my-custom-source1", args.AdditionalSources);
            Assert.Contains("my-custom-source2", args.AdditionalSources);
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        [DataRow("new --add-source my-custom-source install source", "'--add-source','my-custom-source'")]
        [DataRow("new --interactive install source", "'--interactive'")]
        [DataRow("new --language F# --install source", "'--language','F#'")]
        [DataRow("new --language F# install source", "'--language','F#'")]
        [DataRow("new source1 source2 source3 --install source", "'source1'|'source2','source3'")]
        [DataRow("new source1 --install source", "'source1'")]
        public void Install_CanReturnParseError(string command, string expectedInvalidTokens)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            IEnumerable<string> errorMessages = parseResult.Errors.Select(error => error.Message);

            string[] expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.HasCount(expectedInvalidTokenSets.Length, parseResult.Errors);
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

            ParseResult parseResult = rootCommand.Parse("dotnet new install source");
            Assert.AreEqual("dotnet new install my-source", Example.For<NewCommand>(parseResult).WithSubcommand<InstallCommand>().WithArguments("my-source"));
        }
    }
}
