// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using FluentAssertions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    [UsesVerify]
    public class InteractiveModeTests : BaseTest
    {
        [Fact]
        public async Task CanRetrieveDataForInteractiveMode()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            InvocationContext context = new(myCommand.Parse("new console"));

            Questionnaire questionsToAsk = await myCommand.GetQuestionsAsync(context, default).ConfigureAwait(false);

            Assert.Equal("Creating a new \u001b[32mC#\u001b[39m \u001b[1m\u001b[34mConsole App\u001b[39m\u001b[22m project. Press 'skip button' to skip defaults", questionsToAsk.OpeningMessage);

            await Verify(questionsToAsk.Questions.ToList());
        }

        [Fact]
        public async Task CanRetrieveDataForInteractiveMode_Instantiate()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            InvocationContext context = new(myCommand.Parse("new create console"));

            IInteractiveMode? command = context.ParseResult.CommandResult.Command as IInteractiveMode;

            Assert.NotNull(command);
            Questionnaire questionsToAsk = await command.GetQuestionsAsync(context, default).ConfigureAwait(false);

            Assert.Equal("Creating a new \u001b[32mC#\u001b[39m \u001b[1m\u001b[34mConsole App\u001b[39m\u001b[22m project. Press 'skip button' to skip defaults", questionsToAsk.OpeningMessage);

            await Verify(questionsToAsk.Questions.ToList());
        }

        [Fact]
        public async Task InteractiveModeWorksSameForNewAndInstantiate()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            InvocationContext context = new(myCommand.Parse("new console"));

            Questionnaire questionsToAskForNewCommand = await myCommand.GetQuestionsAsync(context, default).ConfigureAwait(false);

            //reading host data doesn't work well with virtual host, so recreating the host
            //to be fixed with https://github.com/dotnet/templating/pull/6613
            host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            context = new(myCommand.Parse("new create console"));

            IInteractiveMode? command = context.ParseResult.CommandResult.Command as IInteractiveMode;

            Assert.NotNull(command);
            Questionnaire questionsToAskForInstantiateCommand = await command.GetQuestionsAsync(context, default).ConfigureAwait(false);

            questionsToAskForInstantiateCommand.Questions.Should().BeEquivalentTo(questionsToAskForNewCommand.Questions);
        }
    }
}
