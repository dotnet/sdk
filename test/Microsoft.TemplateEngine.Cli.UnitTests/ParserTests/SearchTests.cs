// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    public class SearchTests : BaseTest
    {
        private static Dictionary<string, FilterOptionDefinition> _stringToFilterDefMap = new()
        {
            { "package", FilterOptionDefinition.PackageFilter },
            { "author", FilterOptionDefinition.AuthorFilter },
            { "type", FilterOptionDefinition.TypeFilter },
            { "language", FilterOptionDefinition.LanguageFilter },
            { "tag", FilterOptionDefinition.TagFilter },
            { "baseline", FilterOptionDefinition.BaselineFilter },
        };

        [TestMethod]
        [DataRow("new search source --author filter-value", "author")]
        [DataRow("new --search source --author filter-value", "author")]
        [DataRow("new source --author filter-value --search", "author")]
        [DataRow("new source --search --author filter-value ", "author")]
        [DataRow("new --author filter-value --search source", "author")]
        [DataRow("new search source --package filter-value", "package")]
        [DataRow("new --search source --package filter-value", "package")]
        [DataRow("new source --package filter-value --search", "package")]
        [DataRow("new source --search --package filter-value ", "package")]
        [DataRow("new --package filter-value --search source", "package")]
        [DataRow("new search source --language filter-value", "language")]
        [DataRow("new --search source --language filter-value", "language")]
        [DataRow("new source --language filter-value --search", "language")]
        [DataRow("new source --search --language filter-value ", "language")]
        [DataRow("new --language filter-value --search source", "language")]
        [DataRow("new search source -lang filter-value", "language")]
        [DataRow("new --search source -lang filter-value", "language")]
        [DataRow("new source -lang filter-value --search", "language")]
        [DataRow("new source --search -lang filter-value ", "language")]
        [DataRow("new -lang filter-value --search source", "language")]
        [DataRow("new search source --tag filter-value", "tag")]
        [DataRow("new --search source --tag filter-value", "tag")]
        [DataRow("new source --tag filter-value --search", "tag")]
        [DataRow("new source --search --tag filter-value ", "tag")]
        [DataRow("new --tag filter-value --search source", "tag")]
        [DataRow("new search source --type filter-value", "type")]
        [DataRow("new --search source --type filter-value", "type")]
        [DataRow("new source --type filter-value --search", "type")]
        [DataRow("new source --search --type filter-value ", "type")]
        [DataRow("new --type filter-value --search source", "type")]
        [DataRow("new search source --baseline filter-value", "baseline")]
        [DataRow("new --search source --baseline filter-value", "baseline")]
        [DataRow("new source --baseline filter-value --search", "baseline")]
        [DataRow("new source --search --baseline filter-value ", "baseline")]
        [DataRow("new --baseline filter-value --search source", "baseline")]
        public void Search_CanParseFilterOption(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.HasCount(1, args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.AreEqual("source", args.SearchNameCriteria);
        }

        [TestMethod]
        [DataRow("new search --author filter-value", "author")]
        [DataRow("new --search --author filter-value", "author")]
        [DataRow("new --author filter-value --search", "author")]
        [DataRow("new search  --package filter-value", "package")]
        [DataRow("new --search  --package filter-value", "package")]
        [DataRow("new  --package filter-value --search", "package")]
        [DataRow("new search  --language filter-value", "language")]
        [DataRow("new --search  --language filter-value", "language")]
        [DataRow("new  --language filter-value --search", "language")]
        [DataRow("new search  --tag filter-value", "tag")]
        [DataRow("new --search  --tag filter-value", "tag")]
        [DataRow("new  --tag filter-value --search", "tag")]
        [DataRow("new search  --type filter-value", "type")]
        [DataRow("new --search  --type filter-value", "type")]
        [DataRow("new  --type filter-value --search", "type")]
        [DataRow("new search  --baseline filter-value", "baseline")]
        [DataRow("new --search  --baseline filter-value", "baseline")]
        [DataRow("new  --baseline filter-value --search", "baseline")]
        public void Search_CanParseFilterOptionWithoutMainCriteria(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.HasCount(1, args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.IsNull(args.SearchNameCriteria);
        }

        [TestMethod]
        [DataRow("new --search cr1 cr2")]
        [DataRow("new search cr1 cr2")]
        public void Search_CannotParseMultipleArgs(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual("Unrecognized command or argument 'cr2'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        public void Search_CannotParseArgsAtNewLevel()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse("new smth search");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        [DataRow("new --author filter-value search source", "--author")]
        [DataRow("new --package filter-value search source", "--package")]
        [DataRow("new --type filter-value search source", "--type")]
        [DataRow("new --tag filter-value search source", "--tag")]
        [DataRow("new --language filter-value search source", "--language")]
        [DataRow("new -lang filter-value search source", "-lang")]
        public void Search_CannotParseOptionsAtNewLevel(string command, string expectedFilter)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual($"Unrecognized command or argument(s): '{expectedFilter}','filter-value'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        public void Search_Legacy_CannotParseArgsAtBothLevels()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new smth --search smth-else");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        [DataRow("new --interactive search source", "'--interactive'")]
        [DataRow("new --interactive --search source", "'--interactive'")]
        [DataRow("new foo bar --search source", "'foo'|'bar'")]
        [DataRow("new foo bar search source", "'foo'|'bar'")]
        public void Search_HandleParseErrors(string command, string expectedInvalidTokens)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            IEnumerable<string> errorMessages = parseResult.Errors.Select(error => error.Message);

            string[] expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual(expectedInvalidTokenSets.Length, parseResult.Errors.Count);
            foreach (string tokenSet in expectedInvalidTokenSets)
            {
                Assert.Contains($"Unrecognized command or argument {tokenSet}.", errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}.") || errorMessages);
            }
        }

        [TestMethod]
        [DataRow("new --search --columns-all")]
        [DataRow("new --columns-all --search")]
        [DataRow("new search --columns-all")]
        public void Search_CanParseColumnsAll(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsTrue(args.DisplayAllColumns);
        }

        [TestMethod]
        //https://github.com/dotnet/command-line-api/issues/1503
        [DataRow("new search --columns author type", new[] { "author", "type" })]
        [DataRow("new search --columns author --columns type", new[] { "author", "type" })]
        //[DataRow("new search --columns author,type", new[] { "author", "type" })]
        //[DataRow("new search --columns author, type --columns tag", new[] { "author", "type", "tag" })]
        [DataRow("new --search --columns author --columns type", new[] { "author", "type" })]
        //[DataRow("new --search --columns author,type", new[] { "author", "type" })]
        public void Search_CanParseColumns(string command, string[] expectedColumns)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsNotNull(args.ColumnsToDisplay);
            Assert.IsFalse(args.DisplayAllColumns);
            Assert.IsNotEmpty(args.ColumnsToDisplay);
            Assert.AreEqual(expectedColumns.Length, args.ColumnsToDisplay.Count);
            foreach (string column in expectedColumns)
            {
                Assert.Contains(column, args.ColumnsToDisplay!);
            }
        }

        [TestMethod]
        [DataRow("new --search --columns c1 --columns c2")]
        [DataRow("new search --columns c1 c2")]
        public void Search_CannotParseUnknownColumns(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.Contains("Argument 'c1' not recognized. Must be one of:", parseResult.Errors[0].Message);
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

            ParseResult parseResult = rootCommand.Parse("dotnet new search template");
            Assert.AreEqual("dotnet new search my-template", Example.For<NewCommand>(parseResult).WithSubcommand<SearchCommand>().WithArguments("my-template"));
        }

        [TestMethod]
        public void CommandExampleShowsMandatoryArg()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            Command rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new search template");
            Assert.AreEqual("dotnet new search [<template-name>]", Example.For<NewCommand>(parseResult).WithSubcommand<SearchCommand>().WithArgument(c => c.Definition.NameArgument));
        }

        [TestMethod]
        public void CommandExampleShowsOptionalArgWhenOptionsAreGiven()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            Command rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new search template");
            Assert.AreEqual("dotnet new search [<template-name>] --author Microsoft",
                Example.For<NewCommand>(parseResult)
                    .WithSubcommand<SearchCommand>()
                    .WithArgument(c => c.Definition.NameArgument)
                    .WithOption(c => c.Definition.FilterOptions.AuthorOption, "Microsoft"));
        }
    }
}
