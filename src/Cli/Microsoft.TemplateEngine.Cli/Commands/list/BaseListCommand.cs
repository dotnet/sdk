﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseListCommand : BaseCommand<ListCommandArgs>, IFilterableCommand, ITabularOutputCommand
    {
        internal static readonly IReadOnlyList<FilterOptionDefinition> SupportedFilters = new List<FilterOptionDefinition>()
        {
            FilterOptionDefinition.AuthorFilter,
            FilterOptionDefinition.BaselineFilter,
            FilterOptionDefinition.LanguageFilter,
            FilterOptionDefinition.TypeFilter,
            FilterOptionDefinition.TagFilter
        };

        internal BaseListCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_List_Description)
        {
            ParentCommand = parentCommand;
            Filters = SetupFilterOptions(SupportedFilters);

            this.AddArgument(NameArgument);
            this.AddOption(IgnoreConstraintsOption);
            this.AddOption(SharedOptions.OutputOption);
            this.AddOption(SharedOptions.ProjectPathOption);
            SetupTabularOutputOptions(this);
        }

        public virtual Option<bool> ColumnsAllOption { get; } = SharedOptionsFactory.CreateColumnsAllOption();

        public virtual Option<string[]> ColumnsOption { get; } = SharedOptionsFactory.CreateColumnsOption();

        public IReadOnlyDictionary<FilterOptionDefinition, Option> Filters { get; protected set; }

        internal static Option<bool> IgnoreConstraintsOption { get; } = new("--ignore-constraints")
        {
            Description = SymbolStrings.ListCommand_Option_IgnoreConstraints,
            Arity = new ArgumentArity(0, 1)
        };

        internal static Argument<string> NameArgument { get; } = new("template-name")
        {
            Description = SymbolStrings.Command_List_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        internal NewCommand ParentCommand { get; }

        protected override Task<NewCommandStatus> ExecuteAsync(
            ListCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            TemplateListCoordinator templateListCoordinator = new TemplateListCoordinator(
                environmentSettings,
                templatePackageManager,
                new HostSpecificDataLoader(environmentSettings));

            return templateListCoordinator.DisplayTemplateGroupListAsync(args, default);
        }

        protected override ListCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);

    }
}
