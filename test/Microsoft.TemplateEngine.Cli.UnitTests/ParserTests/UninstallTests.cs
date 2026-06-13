// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    public class UninstallTests : BaseTest
    {
        [TestMethod]
        [DataRow("--uninstall")]
        [DataRow("-u")]
        [DataRow("uninstall")]
        public void Uninstall_NoArguments(string commandName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new {commandName}");
            UninstallCommandArgs args = new((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsFalse(parseResult.Errors.Any());
            Assert.IsFalse(args.TemplatePackages.Any());
        }

        [TestMethod]
        [DataRow("--uninstall")]
        [DataRow("-u")]
        [DataRow("uninstall")]
        public void Uninstall_WithArgument(string commandName)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new {commandName} source");
            UninstallCommandArgs args = new((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsFalse(parseResult.Errors.Any());
            Assert.HasCount(1, args.TemplatePackages);
            Assert.Contains("source", args.TemplatePackages);
        }

        [TestMethod]
        [DataRow("new --uninstall source1 --uninstall source2")]
        [DataRow("new --uninstall source1 -u source2")]
        [DataRow("new uninstall source1 source2")]
        public void Uninstall_WithMultipleArgument(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            UninstallCommandArgs args = new((BaseUninstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsFalse(parseResult.Errors.Any());
            Assert.AreEqual(2, args.TemplatePackages.Count);
            Assert.Contains("source1", args.TemplatePackages);
            Assert.Contains("source2", args.TemplatePackages);
        }

        [TestMethod]
        [DataRow("new --add-source my-custom-source uninstall source", "'--add-source','my-custom-source'")]
        [DataRow("new --interactive uninstall source", "'--interactive'")]
        [DataRow("new --language F# --uninstall source", "'--language','F#'")]
        [DataRow("new --language F# uninstall source", "'--language','F#'")]
        [DataRow("new source1 source2 source3 --uninstall source", "'source1'|'source2','source3'")]
        [DataRow("new source1 --uninstall source", "'source1'")]
        public void Uninstall_CanReturnParseError(string command, string expectedInvalidTokens)
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

            ParseResult parseResult = rootCommand.Parse("dotnet new uninstall source");
            Assert.AreEqual("dotnet new uninstall my-source", Example.For<NewCommand>(parseResult).WithSubcommand<UninstallCommand>().WithArguments("my-source"));
        }
    }
}
