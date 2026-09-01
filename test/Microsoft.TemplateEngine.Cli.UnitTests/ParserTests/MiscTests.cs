// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    public class MiscTests : BaseTest
    {
        /// <summary>
        /// This test checks if help aliases are in sync with System.CommandLine.
        /// </summary>
        [TestMethod]
        public void KnownHelpAliasesAreCorrect()
        {
            var result = new RootCommand().Parse(Constants.KnownHelpAliases[0]);

            Option helpOption = result.CommandResult
                .Children
                .OfType<OptionResult>()
                .Select(r => r.Option)
                .Where(o => o is HelpOption)
                .Single();

            var aliases = new[] { helpOption.Name }.Concat(helpOption.Aliases);

            Assert.AreSequenceEqual(aliases.OrderBy(a => a), TemplateCommand.KnownHelpAliases.OrderBy(a => a));
        }

        /// <summary>
        /// This test check if default completion item comparer compares the instances using labels only.
        /// </summary>
        [TestMethod]
        public void CompletionItemCompareIsAsExpected()
        {
            Assert.AreEqual(
                new CompletionItem("my-label", kind: "Value", sortText: "sort-text", insertText: "insert-text", documentation: "doc", detail: "det"),
                new CompletionItem("my-label", kind: "Value", sortText: "sort-text", insertText: "insert-text", documentation: "doc", detail: "det"));

            Assert.AreEqual(
              new CompletionItem("my-label", kind: "Value", sortText: "sort-text1", insertText: "insert-text1", documentation: "doc1", detail: "det1"),
              new CompletionItem("my-label", kind: "Value", sortText: "sort-text2", insertText: "insert-text2", documentation: "doc2", detail: "det2"));

            Assert.AreNotEqual(
                 new CompletionItem("my-label", kind: "Value", sortText: "sort-text1", insertText: "insert-text1", documentation: "doc1", detail: "det1"),
                 new CompletionItem("my-label", kind: "Argument", sortText: "sort-text2", insertText: "insert-text2", documentation: "doc2", detail: "det2"));

        }

        [TestMethod]
        [DataRow("new --debug:attach", "--debug:attach")]
        [DataRow("new --debug:attach console", "--debug:attach")]
        [DataRow("new --debug:reinit", "--debug:reinit")]
        [DataRow("new --debug:ephemeral-hive", "--debug:ephemeral-hive")]
        [DataRow("new --debug:virtual-hive", "--debug:ephemeral-hive")]
        [DataRow("new --debug:rebuildcache", "--debug:rebuild-cache")]
        [DataRow("new --debug:rebuild-cache", "--debug:rebuild-cache")]
        [DataRow("new --debug:show-config", "--debug:show-config")]
        [DataRow("new --debug:showconfig", "--debug:show-config")]

        public void DebugFlagCanBeParsedOnNewLevel(string command, string option)
        {
            Dictionary<string, Func<GlobalArgs, bool>> optionsMap = new()
            {
                { "--debug:attach", args => args.DebugAttach },
                { "--debug:ephemeral-hive", args => args.DebugVirtualizeSettings },
                { "--debug:reinit", args => args.DebugReinit },
                { "--debug:rebuild-cache", args => args.DebugRebuildCache },
                { "--debug:show-config", args => args.DebugShowConfig }
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = ParserFactory.CreateParser(myCommand).Parse(command);
            NewCommandArgs args = new(myCommand, parseResult);

            Assert.IsTrue(optionsMap[option](args));
        }

        [TestMethod]
        [DataRow("new install source --debug:attach", "--debug:attach")]
        [DataRow("new install source --debug:attach --add-source s1", "--debug:attach")]
        [DataRow("new install --debug:reinit source", "--debug:reinit")]
        [DataRow("new install source --debug:ephemeral-hive", "--debug:ephemeral-hive")]
        [DataRow("new install source --debug:virtual-hive", "--debug:ephemeral-hive")]
        [DataRow("new install source --debug:rebuildcache", "--debug:rebuild-cache")]
        [DataRow("new install source --debug:rebuild-cache", "--debug:rebuild-cache")]
        [DataRow("new install source --debug:show-config", "--debug:show-config")]
        [DataRow("new install source --debug:showconfig", "--debug:show-config")]

        public void DebugFlagCanBeParsedOnSubcommandLevel(string command, string option)
        {
            Dictionary<string, Func<GlobalArgs, bool>> optionsMap = new()
            {
                { "--debug:attach", args => args.DebugAttach },
                { "--debug:ephemeral-hive", args => args.DebugVirtualizeSettings },
                { "--debug:reinit", args => args.DebugReinit },
                { "--debug:rebuild-cache", args => args.DebugRebuildCache },
                { "--debug:show-config", args => args.DebugShowConfig }
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = ParserFactory.CreateParser(myCommand).Parse(command);
            InstallCommandArgs args = new((InstallCommand)parseResult.CommandResult.Command, parseResult);

            Assert.IsTrue(optionsMap[option](args));
        }

        [TestMethod]
        [DataRow("new console  --framework net5.0 --flag --debug:attach", "--debug:attach")]
        [DataRow("new console --framework net5.0 --debug:attach --flag", "--debug:attach")]
        [DataRow("new console --debug:reinit --framework net5.0 --flag", "--debug:reinit")]
        [DataRow("new console --framework net5.0 --debug:ephemeral-hive --flag", "--debug:ephemeral-hive")]
        [DataRow("new console --framework net5.0 --debug:virtual-hive --flag", "--debug:ephemeral-hive")]
        [DataRow("new console --framework net5.0 --debug:rebuildcache --flag", "--debug:rebuild-cache")]
        [DataRow("new console --framework net5.0 --debug:rebuild-cache --flag", "--debug:rebuild-cache")]
        [DataRow("new console --framework net5.0 --debug:show-config --flag", "--debug:show-config")]
        [DataRow("new console --framework net5.0 --debug:showconfig --flag", "--debug:show-config")]

        public void DebugFlagCanBeParsedOnTemplateSubcommandLevel(string command, string option)
        {
            Dictionary<string, Func<GlobalArgs, bool>> optionsMap = new()
            {
                { "--debug:attach", args => args.DebugAttach },
                { "--debug:ephemeral-hive", args => args.DebugVirtualizeSettings },
                { "--debug:reinit", args => args.DebugReinit },
                { "--debug:rebuild-cache", args => args.DebugRebuildCache },
                { "--debug:show-config", args => args.DebugShowConfig }
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse(command);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            Assert.IsTrue(optionsMap[option](args));
            Assert.AreEqual("console", args.ShortName);
            Assert.AreSequenceEqual(new[] { "--framework", "net5.0", "--flag" }, args.RemainingArguments);
        }

        [TestMethod]
        public void ManuallyAddedOptionIsPreservedOnTemplateSubcommandLevel()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            var customOption = new Option<string>("--newOption")
            {
                Recursive = true
            };
            myCommand.Options.Add(customOption);

            ParseResult parseResult = myCommand.Parse("new console --newOption val");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            Assert.IsNotNull(args.ParseResult);
            Assert.AreEqual("console", args.ShortName);
            Assert.IsEmpty(args.RemainingArguments);
            Assert.AreEqual("val", args.ParseResult.GetValue(customOption));
        }

        [TestMethod]
        [DataRow("new --output test console", "test")]
        [DataRow("new console --output test", "test")]
        [DataRow("new -o test console", "test")]
        [DataRow("new console -o test", "test")]
        [DataRow("new console --framework net6.0 --output test", "test")]
        [DataRow("--output test new console", null)]
        public void CanParseOutputOption(string command, string? expected)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));

            RootCommand rootCommand = new();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            rootCommand.Add(myCommand);

            ParseResult parseResult = rootCommand.Parse(command);

            Assert.AreEqual(expected, parseResult.GetValue(myCommand.Definition.InstantiateOptions.OutputOption)?.Name);
        }

        [TestMethod]
        [DataRow("new --project $filePath console", "$filePath")]
        [DataRow("new console --project $filePath", "$filePath")]
        [DataRow("new console --framework net6.0 --project $filePath", "$filePath")]
        [DataRow("--project $filePath new console", null)]
        public void CanParseProjectOption(string command, string? expected)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));

            RootCommand rootCommand = new();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            rootCommand.Add(myCommand);

            // ProjectPathOption uses AcceptExistingOnly validator, so the provided file has to exist!
            string existingFilePath = typeof(object).Assembly.Location;
            command = command.Replace("$filePath", $"\"{existingFilePath}\"");
            expected = expected?.Replace("$filePath", Path.GetFileName(existingFilePath));

            ParseResult parseResult = rootCommand.Parse(command);
            Assert.AreEqual(expected, parseResult.GetValue(myCommand.Definition.InstantiateOptions.ProjectOption)?.Name);
        }
    }
}
