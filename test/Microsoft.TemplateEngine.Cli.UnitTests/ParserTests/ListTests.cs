// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    public class ListTests : BaseTest
    {
        private static readonly Dictionary<string, FilterOptionDefinition> _stringToFilterDefMap = new()
        {
            { "author", FilterOptionDefinition.AuthorFilter },
            { "type", FilterOptionDefinition.TypeFilter },
            { "language", FilterOptionDefinition.LanguageFilter },
            { "tag", FilterOptionDefinition.TagFilter },
            { "baseline", FilterOptionDefinition.BaselineFilter },
        };

        [TestMethod]
        [DataRow("new list source --author filter-value", "author")]
        [DataRow("new --list source --author filter-value", "author")]
        [DataRow("new source --author filter-value --list", "author")]
        [DataRow("new source --list --author filter-value ", "author")]
        [DataRow("new --author filter-value --list source", "author")]
        [DataRow("new list source --language filter-value", "language")]
        [DataRow("new --list source --language filter-value", "language")]
        [DataRow("new source --language filter-value --list", "language")]
        [DataRow("new source --list --language filter-value ", "language")]
        [DataRow("new --language filter-value --list source", "language")]
        [DataRow("new list source -lang filter-value", "language")]
        [DataRow("new --list source -lang filter-value", "language")]
        [DataRow("new source -lang filter-value --list", "language")]
        [DataRow("new source --list -lang filter-value ", "language")]
        [DataRow("new -lang filter-value --list source", "language")]
        [DataRow("new list source --tag filter-value", "tag")]
        [DataRow("new --list source --tag filter-value", "tag")]
        [DataRow("new source --tag filter-value --list", "tag")]
        [DataRow("new source --list --tag filter-value ", "tag")]
        [DataRow("new --tag filter-value --list source", "tag")]
        [DataRow("new list source --type filter-value", "type")]
        [DataRow("new --list source --type filter-value", "type")]
        [DataRow("new source --type filter-value --list", "type")]
        [DataRow("new source --list --type filter-value ", "type")]
        [DataRow("new --type filter-value --list source", "type")]
        [DataRow("new list source --baseline filter-value", "baseline")]
        [DataRow("new --list source --baseline filter-value", "baseline")]
        [DataRow("new source --baseline filter-value --list", "baseline")]
        [DataRow("new source --list --baseline filter-value ", "baseline")]
        [DataRow("new --baseline filter-value --list source", "baseline")]
        [DataRow("new -l source --author filter-value", "author")]
        [DataRow("new source --author filter-value -l", "author")]
        [DataRow("new source -l --author filter-value ", "author")]
        [DataRow("new --author filter-value -l source", "author")]
        [DataRow("new -l source --language filter-value", "language")]
        [DataRow("new source --language filter-value -l", "language")]
        [DataRow("new source -l --language filter-value ", "language")]
        [DataRow("new --language filter-value -l source", "language")]
        [DataRow("new -l source -lang filter-value", "language")]
        [DataRow("new source -lang filter-value -l", "language")]
        [DataRow("new source -l -lang filter-value ", "language")]
        [DataRow("new -lang filter-value -l source", "language")]
        [DataRow("new -l source --tag filter-value", "tag")]
        [DataRow("new source --tag filter-value -l", "tag")]
        [DataRow("new source -l --tag filter-value ", "tag")]
        [DataRow("new --tag filter-value -l source", "tag")]
        [DataRow("new -l source --type filter-value", "type")]
        [DataRow("new source --type filter-value -l", "type")]
        [DataRow("new source -l --type filter-value ", "type")]
        [DataRow("new --type filter-value -l source", "type")]
        [DataRow("new -l source --baseline filter-value", "baseline")]
        [DataRow("new source --baseline filter-value -l", "baseline")]
        [DataRow("new source -l --baseline filter-value ", "baseline")]
        [DataRow("new --baseline filter-value -l source", "baseline")]
        public void List_CanParseFilterOption(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.HasCount(1, args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.AreEqual("source", args.ListNameCriteria);
        }

        [TestMethod]
        [DataRow("new list --author filter-value", "author")]
        [DataRow("new --list --author filter-value", "author")]
        [DataRow("new --author filter-value --list", "author")]
        [DataRow("new list  --language filter-value", "language")]
        [DataRow("new --list  --language filter-value", "language")]
        [DataRow("new  --language filter-value --list", "language")]
        [DataRow("new list  --tag filter-value", "tag")]
        [DataRow("new --list  --tag filter-value", "tag")]
        [DataRow("new  --tag filter-value --list", "tag")]
        [DataRow("new list  --type filter-value", "type")]
        [DataRow("new --list  --type filter-value", "type")]
        [DataRow("new  --type filter-value --list", "type")]
        [DataRow("new list  --baseline filter-value", "baseline")]
        [DataRow("new --list  --baseline filter-value", "baseline")]
        [DataRow("new  --baseline filter-value --list", "baseline")]
        [DataRow("new -l --author filter-value", "author")]
        [DataRow("new --author filter-value -l", "author")]
        [DataRow("new -l  --language filter-value", "language")]
        [DataRow("new  --language filter-value -l", "language")]
        [DataRow("new -l  --tag filter-value", "tag")]
        [DataRow("new  --tag filter-value -l", "tag")]
        [DataRow("new -l  --type filter-value", "type")]
        [DataRow("new  --type filter-value -l", "type")]
        [DataRow("new -l  --baseline filter-value", "baseline")]
        [DataRow("new  --baseline filter-value -l", "baseline")]
        public void List_CanParseFilterOptionWithoutMainCriteria(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);
            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.HasCount(1, args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.IsNull(args.ListNameCriteria);
        }

        [TestMethod]
        [DataRow("new --list cr1 cr2")]
        [DataRow("new list cr1 cr2")]
        public void List_CannotParseMultipleArgs(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual("Unrecognized command or argument 'cr2'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        public void List_CannotParseArgsAtNewLevel()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new smth list");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        [DataRow("new --author filter-value list source", "--author")]
        [DataRow("new --type filter-value list source", "--type")]
        [DataRow("new --tag filter-value list source", "--tag")]
        [DataRow("new --language filter-value list source", "--language")]
        [DataRow("new -lang filter-value list source", "-lang")]
        public void List_CannotParseOptionsAtNewLevel(string command, string expectedFilter)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual($"Unrecognized command or argument(s): '{expectedFilter}','filter-value'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        public void List_Legacy_CannotParseArgsAtBothLevels()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new smth --list smth-else");

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.AreEqual("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [TestMethod]
        [DataRow("new --list --columns-all")]
        [DataRow("new --columns-all --list")]
        [DataRow("new list --columns-all")]
        public void List_CanParseColumnsAll(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsTrue(args.DisplayAllColumns);
        }

        [TestMethod]
        [DataRow("new list --columns author type", new[] { "author", "type" })]
        [DataRow("new list --columns author --columns type", new[] { "author", "type" })]
        //https://github.com/dotnet/command-line-api/issues/1503
        //[DataRow("new list --columns author,type", new[] { "author", "type" })]
        //[DataRow("new list --columns author, type --columns tag", new[] { "author", "type", "tag" })]
        [DataRow("new --list --columns author --columns type", new[] { "author", "type" })]
        //[DataRow("new --list --columns author,type", new[] { "author", "type" })]
        public void List_CanParseColumns(string command, string[] expectedColumns)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsFalse(args.DisplayAllColumns);
            Assert.IsNotNull(args.ColumnsToDisplay);
            Assert.IsNotEmpty(args.ColumnsToDisplay);
            Assert.AreEqual(expectedColumns.Length, args.ColumnsToDisplay.Count);
            foreach (string column in expectedColumns)
            {
                Assert.Contains(column, args.ColumnsToDisplay!);
            }
        }

        [TestMethod]
        [DataRow("new --list --columns c1 --columns c2")]
        [DataRow("new list --columns c1 c2")]
        public void List_CannotParseUnknownColumns(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.IsNotEmpty(parseResult.Errors);
            Assert.Contains("Argument 'c1' not recognized. Must be one of:", parseResult.Errors[0].Message);
        }

        [TestMethod]
        [DataRow("new --interactive list source", "'--interactive'")]
        [DataRow("new --interactive --list source", "'--interactive'")]
        [DataRow("new foo bar --list source", "'foo'|'bar'")]
        [DataRow("new foo bar list source", "'foo'|'bar'")]
        public void List_HandleParseErrors(string command, string expectedInvalidTokens)
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
        public void CommandExampleCanShowParentCommandsBeyondNew()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            Command rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new list");
            Assert.AreEqual("dotnet new list", Example.For<NewCommand>(parseResult).WithSubcommand<ListCommand>());
        }
    }
}
