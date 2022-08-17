﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public partial class TabCompletionTests
    {
        [Fact]
        public void Instantiate_CanSuggestTemplateOption_StartsWith()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));

            var parseResult = myCommand.Parse("new console --framework net7.0 --l");
            var suggestions = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.Equal(2, suggestions.Length);
            Assert.Contains("--langVersion", suggestions);
            Assert.Contains("--language", suggestions);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact (Skip = "doesn't work at the moment; it matches with legacy --language option which cannot be completed; to discuss how to avoid that")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Instantiate_CanSuggestLanguages()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));

            var parseResult = myCommand.Parse("new console --language ");
            var suggestions = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.Equal(3, suggestions.Length);
            Assert.Contains("C#", suggestions);
            Assert.Contains("F#", suggestions);
            Assert.Contains("VB", suggestions);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "not valid behavior for parser, should suggest --nuget-source")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Install_GetSuggestionsAfterInteractive()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));

            var parseResult = myCommand.Parse("new install --interactive ");
            var result = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.Equal(2, result.Length);
            Assert.Contains("--nuget-source", result);
        }

        [Fact]
        public void Install_GetSuggestionsAfterOptionWithoutArg()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));

            var parseResult = myCommand.Parse("new install --nuget-source ");
            var result = parseResult.GetCompletions().ToArray();

            Assert.Empty(result);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "not valid behavior for parser, should suggest --interactive")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void Install_GetSuggestionsAfterOptionWithArg()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));

            var parseResult = myCommand.Parse("new install --nuget-source me");
            var result = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.Single(result);
            Assert.Contains("--interactive", result);
        }

        [Fact]
        public void Instantiate_CanSuggestTemplate_StartsWith()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));

            var parseResult = myCommand.Parse("new co");
            var suggestions = parseResult.GetCompletions().Select(l => l.Label).ToArray();

            Assert.Single(suggestions);
            Assert.Equal("console", suggestions.Single());
        }

        [Fact]
        public void CanCompleteChoice_FromSingleTemplate()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "val3");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --testChoice ");
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(new[] { "val1", "val2", "val3" }, result);
        }

        [Theory]
        [InlineData(" new foo --testChoice val2 --testChoice va", new[] { "val1", "val2", "val3" })]
        [InlineData(" new foo --testC", new[] { "--testChoice" })]
        // [InlineData(" new foo --testChoice val2 --testC", new[] { "--testChoice" },
        //  Skip = "Multiple arity option completion does not work. https://github.com/dotnet/command-line-api/issues/1727")]
        public void CanCompleteChoice_MultichoiceTabCompletion(string command, string[] suggestions)
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithMultiChoiceParameter("testChoice", "val1", "val2", "val3");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse(command);
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(suggestions, result);
        }

        [Fact]
        public void CanCompleteChoice_FromSingleTemplate_StartsWith()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "boo");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --testChoice v");
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(new[] { "val1", "val2" }, result);
        }

        [Fact]
        public void CanCompleteChoice_FromSingleTemplate_InTheMiddle()
        {
            var template = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1", "val2", "boo");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --testChoice v --name test");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);
            completionContext = completionContext!.AtCursorPosition(23);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(new[] { "val1", "val2" }, result);
        }

        [Fact]
        public void CanCompleteChoice_FromMultipleTemplates()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --testChoice ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(new[] { "val1", "val2", "val3" }, result);
        }

        [Fact]
        public void CanCompleteChoice_FromMultipleTemplates_StartsWith()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "boo");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --testChoice v");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(new[] { "val1", "val2" }, result);
        }

        [Fact]
        public void CanCompleteParameters_FromMultipleTemplates()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1")
                .WithParameters("foo", "bar");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3")
                .WithParameters("param");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

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

        [Fact]
        public void CanCompleteParameters_StartsWith_FromMultipleTemplates()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1")
                .WithParameters("foo", "bar");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3")
                .WithParameters("test");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --t");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

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
        [Fact(Skip = "https://github.com/dotnet/templating/issues/4192")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanCompleteParameters_StartsWith_AfterOption()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val1")
                .WithParameters("foo", "bar");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithChoiceParameter("testChoice", "val2", "val3")
                .WithParameters("test");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --foo val1 --bar val2 --t");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

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

        [Theory]
        [InlineData("-lang")]
        [InlineData("--language")]
        public void CanCompleteLanguages(string optionName)
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithTag("language", "C#");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithTag("language", "F#");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo {optionName} ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(new[] { "C#", "F#" }, result);
        }

        [Fact]
        public void CanCompleteTypes()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "foo.group")
                .WithTag("type", "project");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "foo.group")
                .WithTag("type", "solution");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost();
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo --type ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Equal(new[] { "project", "solution" }, result);
        }

        [Fact]
        public void CanIgnoreTemplateGroupsWithConstraints()
        {
            var template1 = new MockTemplateInfo("foo1", identity: "foo.1")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"));

            var template2 = new MockTemplateInfo("foo2", identity: "foo.2")
                .WithConstraints(new TemplateConstraintInfo("test", "no"));

            var template3 = new MockTemplateInfo("foo3", identity: "foo.3")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"));

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new fo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateNameCompletions(args.ShortName, templateGroups, settings).Select(l => l.Label);

            Assert.Equal(new[] { "foo1" }, result);
        }

        [Fact]
        public void CanIgnoreTemplateGroupsWithConstraints_IgnoresLongEvaluationTemplateGroups()
        {
            var template1 = new MockTemplateInfo("foo1", identity: "foo.1")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"));

            var template2 = new MockTemplateInfo("foo2", identity: "foo.2")
                .WithConstraints(new TemplateConstraintInfo("test", "no"));

            var template3 = new MockTemplateInfo("foo3", identity: "foo.3")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"));

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new LongRunningConstraintFactory("test", 1500)) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new fo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateNameCompletions(args.ShortName, templateGroups, settings).Select(l => l.Label);

            Assert.Empty(result);
        }

        [Fact]
        public void CanIgnoreTemplatesInGroupWithConstraints()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"))
                .WithParameter("a");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "no"))
                .WithParameter("b"); 

            var template3 = new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "group")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"))
             .WithParameter("c");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Contains("--a", result);
            Assert.DoesNotContain("--b", result);
            Assert.DoesNotContain("--c", result);
        }

        [Fact]
        public void IncludesTemplatesInGroupWithLongEvaluatedConstraints()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "yes"))
                .WithParameter("a");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "no"))
                .WithParameter("b");

            var template3 = new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "group")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"))
             .WithParameter("c");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new LongRunningConstraintFactory("test", 1500)) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new foo ");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateCompletions(args, templateGroups, settings, packageManager, completionContext!).Select(l => l.Label);

            Assert.Contains("--a", result);
            Assert.Contains("--b", result);
            Assert.Contains("--c", result);
        }

        [Fact]
        public void WillNotEvaluateConstraints_WhenAtLeastOneTemplateInGroupDoesNotHaveConstraints()
        {
            var template1 = new MockTemplateInfo("foo", identity: "foo.1", groupIdentity: "group")
                .WithParameter("a");

            var template2 = new MockTemplateInfo("foo", identity: "foo.2", groupIdentity: "group")
                .WithConstraints(new TemplateConstraintInfo("test", "no"))
                .WithParameter("b");

            var template3 = new MockTemplateInfo("foo", identity: "foo.3", groupIdentity: "group")
             .WithConstraints(new TemplateConstraintInfo("test", "bad-params"))
             .WithParameter("c");

            var templateGroups = TemplateGroup.FromTemplateList(
                CliTemplateInfo.FromTemplateInfo(new[] { template1, template2, template3 }, A.Fake<IHostSpecificDataLoader>()));

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new LongRunningConstraintFactory("test", 3000)) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);
            TemplatePackageManager packageManager = A.Fake<TemplatePackageManager>();

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($" new fo");
            InstantiateCommandArgs args = InstantiateCommandArgs.FromNewCommandArgs(new NewCommandArgs(myCommand, parseResult));
            var completionContext = parseResult.GetCompletionContext() as TextCompletionContext;
            Assert.NotNull(completionContext);

            var result = InstantiateCommand.GetTemplateNameCompletions(args.ShortName, templateGroups, settings).Select(l => l.Label);

            Assert.Equal(new[] { "foo" }, result);
        }
    }
}
