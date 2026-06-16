// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [TestClass]
    public partial class TabCompletionTests
    {
        [TestMethod]
        public void Instantiate_CanSuggestTemplateOption_StartsWith()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse($"new console --framework {ToolsetInfo.CurrentTargetFramework} --l");
            string[] suggestions = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.AreEqual(2, suggestions.Length);
            Assert.Contains("--langVersion", suggestions);
            Assert.Contains("--language", suggestions);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("doesn't work at the moment; it matches with legacy --language option which cannot be completed; to discuss how to avoid that")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Instantiate_CanSuggestLanguages()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new console --language ");
            string[] suggestions = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.AreEqual(3, suggestions.Length);
            Assert.Contains("C#", suggestions);
            Assert.Contains("F#", suggestions);
            Assert.Contains("VB", suggestions);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("not valid behavior for parser, should suggest --nuget-source")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Install_GetSuggestionsAfterInteractive()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new install --interactive ");
            string[] result = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.AreEqual(2, result.Length);
            Assert.Contains("--nuget-source", result);
        }

        [TestMethod]
        public void Install_GetSuggestionsAfterOptionWithoutArg()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new install --nuget-source ");
            CompletionItem[] result = parseResult.GetCompletions().ToArray();

            Assert.IsFalse(result.Any());
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("not valid behavior for parser, should suggest --interactive")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Install_GetSuggestionsAfterOptionWithArg()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new install --nuget-source me");
            string[] result = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.HasCount(1, result);
            Assert.Contains("--interactive", result);
        }

        [TestMethod]
        public void Instantiate_CanSuggestTemplate_StartsWith()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new co");
            string[] suggestions = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.HasCount(1, suggestions);
            Assert.AreEqual("console", suggestions.Single());
        }

        [TestMethod]
        public void CanCompleteChoice_FromSingleTemplate()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "val3");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --testChoice ");
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "val1", "val2", "val3" }, result);
        }

        [TestMethod]
        [DataRow(" new foo --testChoice val2 --testChoice va", new[] { "val1", "val2", "val3" })]
        [DataRow(" new foo --testC", new[] { "--testChoice" })]
        // [DataRow(" new foo --testChoice val2 --testC", new[] { "--testChoice" },
        //  Skip = "Multiple arity option completion does not work. https://github.com/dotnet/command-line-api/issues/1727")]
        public void CanCompleteChoice_MultichoiceTabCompletion(string command, string[] suggestions)
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithMultiChoiceParameter("testChoice", "val1", "val2", "val3");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse(command);
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(suggestions, result);
        }

        [TestMethod]
        public void CanCompleteChoice_FromSingleTemplate_StartsWith()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "boo");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --testChoice v");
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "val1", "val2" }, result);
        }

        [TestMethod]
        public void CanCompleteChoice_FromSingleTemplate_InTheMiddle()
        {
            MockTemplateInfo template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "boo");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --testChoice v --name test");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);
            completionContext = completionContext!.AtCursorPosition(23);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "val1", "val2" }, result);
        }

        [TestMethod]
        public void CanCompleteChoice_FromMultipleTemplates()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --testChoice ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "val1", "val2", "val3" }, result);
        }

        [TestMethod]
        public void CanCompleteChoice_FromMultipleTemplates_StartsWith()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "boo");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --testChoice v");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "val1", "val2" }, result);
        }

        [TestMethod]
        public void CanCompleteParameters_FromMultipleTemplates()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1")
                .WithParameters("foo", "bar");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3")
                .WithParameters("param");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Contains("--param", result);
            Assert.Contains("--testChoice", result);
            Assert.Contains("--foo", result);
            Assert.Contains("--bar", result);

            Assert.Contains("-p", result);
            Assert.Contains("-t", result);
            Assert.Contains("-f", result);
            Assert.Contains("-b", result);

            Assert.DoesNotContain("--language", result);
            Assert.DoesNotContain("--type", result);
            Assert.DoesNotContain("--baseline", result);
        }

        [TestMethod]
        public void CanCompleteParameters_StartsWith_FromMultipleTemplates()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1")
                .WithParameters("foo", "bar");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3")
                .WithParameters("test");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --t");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Contains("--test", result);
            Assert.Contains("--testChoice", result);
            Assert.DoesNotContain("--foo", result);
            Assert.DoesNotContain("--bar", result);

            Assert.DoesNotContain("-p", result);
            Assert.DoesNotContain("-t", result);
            Assert.DoesNotContain("-f", result);
            Assert.DoesNotContain("-b", result);

            Assert.DoesNotContain("--language", result);
            Assert.DoesNotContain("--type", result);
            Assert.DoesNotContain("--baseline", result);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/templating/issues/4387")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanCompleteParameters_StartsWith_AfterOption()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1")
                .WithParameters("foo", "bar");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3")
                .WithParameters("test");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --foo val1 --bar val2 --t");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.DoesNotContain("--test", result);
            Assert.Contains("--testChoice", result);
            Assert.DoesNotContain("--foo", result);
            Assert.DoesNotContain("--bar", result);

            Assert.DoesNotContain("-p", result);
            Assert.DoesNotContain("-t", result);
            Assert.DoesNotContain("-f", result);
            Assert.DoesNotContain("-b", result);

            Assert.DoesNotContain("--language", result);
            Assert.DoesNotContain("--type", result);
            Assert.DoesNotContain("--baseline", result);
        }

        [TestMethod]
        [DataRow("-lang")]
        [DataRow("--language")]
        public void CanCompleteLanguages(string optionName)
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithTag("language", "C#");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithTag("language", "F#");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo {optionName} ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "C#", "F#" }, result);
        }

        [TestMethod]
        public void CanCompleteTypes()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithTag("type", "project");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithTag("type", "solution");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo --type ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "project", "solution" }, result);
        }

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux, IgnoreMessage = "https://github.com/dotnet/sdk/issues/46212")]
        public void CanIgnoreTemplateGroupsWithConstraints()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo1", identity: "foo.1")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"));

            MockTemplateInfo template2 = new MockTemplateInfo("foo2", identity: "foo.2")
                .WithConstraints(new TemplateConstraintInfo("test", "no"));

            MockTemplateInfo template3 = new MockTemplateInfo("foo3", identity: "foo.3")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"));

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new fo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateNameCompletions(args.ShortName, templateGroups, settings).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "foo1" }, result);
        }

        [TestMethod]
        public void CanIgnoreTemplateGroupsWithConstraints_IgnoresLongEvaluationTemplateGroups()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo1", identity: "foo.1")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"));

            MockTemplateInfo template2 = new MockTemplateInfo("foo2", identity: "foo.2")
                .WithConstraints(new TemplateConstraintInfo("test", "no"));

            MockTemplateInfo template3 = new MockTemplateInfo("foo3", identity: "foo.3")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"));

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new LongRunningConstraintFactory("test", 1500)) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new fo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateNameCompletions(args.ShortName, templateGroups, settings).Select(l => l.Label);

            Assert.IsFalse(result.Any());
        }

        [TestMethod]
        public void CanIgnoreTemplatesInGroupWithConstraints()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"))
                .WithParameter("a");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "no"))
                .WithParameter("b");

            MockTemplateInfo template3 = new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "group")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"))
             .WithParameter("c");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Contains("--a", result);
            Assert.DoesNotContain("--b", result);
            Assert.DoesNotContain("--c", result);
        }

        [TestMethod]
        public void IncludesTemplatesInGroupWithLongEvaluatedConstraints()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"))
                .WithParameter("a");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "no"))
                .WithParameter("b");

            MockTemplateInfo template3 = new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "group")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"))
             .WithParameter("c");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new LongRunningConstraintFactory("test", 1500)) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new foo ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Contains("--a", result);
            Assert.Contains("--b", result);
            Assert.Contains("--c", result);
        }

        [TestMethod]
        public void WillNotEvaluateConstraints_WhenAtLeastOneTemplateInGroupDoesNotHaveConstraints()
        {
            MockTemplateInfo template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "group")
                .WithParameter("a");

            MockTemplateInfo template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "no"))
                .WithParameter("b");

            MockTemplateInfo template3 = new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "group")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"))
             .WithParameter("c");

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new LongRunningConstraintFactory("test", 3000)) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            ParseResult parseResult = myCommand.Parse($" new fo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.IsNotNull(completionContext);

            IEnumerable<string> result = InstantiateCommand.GetTemplateNameCompletions(args.ShortName, templateGroups, settings).Select(l => l.Label);

            Assert.AreSequenceEqual(new[] { "foo" }, result);
        }
    }
}
