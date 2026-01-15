// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class GlobalArgs<TDefinition> : ICommandArgs
        where TDefinition : Command
    {
        public GlobalArgs(BaseCommand<TDefinition> command, ParseResult parseResult)
        {
            RootCommand = GetNewCommandFromParseResult(parseResult);

            var definition = RootCommand.Definition;

            DebugCustomSettingsLocation = parseResult.GetValue(definition.DebugCustomSettingsLocationOption);
            DebugVirtualizeSettings = parseResult.GetValue(definition.DebugVirtualizeSettingsOption);
            DebugAttach = parseResult.GetValue(definition.DebugAttachOption);
            DebugReinit = parseResult.GetValue(definition.DebugReinitOption);
            DebugRebuildCache = parseResult.GetValue(definition.DebugRebuildCacheOption);
            DebugShowConfig = parseResult.GetValue(definition.DebugShowConfigOption);
            ParseResult = parseResult;
            Command = command;
            HasHelpOption = parseResult.CommandResult.Children.Any(child => child is OptionResult optionResult && optionResult.Option is HelpOption);
        }

        protected GlobalArgs(GlobalArgs<NewCommandDefinition> args)
            : this(args.Command, args.ParseResult)
        {
        }

        public NewCommand RootCommand { get; }

        public BaseCommand<TDefinition> Command { get; }

        public ParseResult ParseResult { get; }

        Command ICommandArgs.Command => Command;

        internal bool DebugAttach { get; private set; }

        internal bool DebugRebuildCache { get; private set; }

        internal bool DebugVirtualizeSettings { get; private set; }

        internal bool DebugReinit { get; private set; }

        internal bool DebugShowConfig { get; private set; }

        internal string? DebugCustomSettingsLocation { get; private set; }

        internal bool HasHelpOption { get; private set; }

        protected static (bool, IReadOnlyList<string>?) ParseTabularOutputSettings(ITabularOutputCommand command, ParseResult parseResult)
        {
            return (parseResult.GetValue(command.ColumnsAllOption), parseResult.GetValue(command.ColumnsOption));
        }

        /// <summary>
        /// Gets root <see cref="NewCommand"/> from <paramref name="parseResult"/>.
        /// </summary>
        private static NewCommand GetNewCommandFromParseResult(ParseResult parseResult)
        {
            var commandResult = parseResult.CommandResult;

            while (commandResult?.Command != null && commandResult.Command is not NewCommand)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }
            if (commandResult == null || commandResult.Command is not NewCommand newCommand)
            {
                throw new Exception($"Command structure is not correct: {nameof(NewCommand)} is not found as part of parse result.");
            }
            return newCommand;
        }
    }
}
