// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class Example
    {
        internal static Example<T> For<T>(ParseResult parseResult) where T : Command
        {
            var commandResult = parseResult.CommandResult;

            //check for parent commands first
            while (commandResult?.Command != null && commandResult.Command is not T)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }

            if (commandResult?.Command is T typedCommand)
            {
                List<string> parentCommands = new();
                while (commandResult?.Command != null)
                {
                    parentCommands.Add(commandResult.Command.Name);
                    commandResult = (commandResult.Parent as CommandResult);
                }
                parentCommands.Reverse();
                return new Example<T>(typedCommand, [.. parentCommands]);
            }

            // if the command is not found in parents of command result, try to search it in the whole command tree
            T siblingCommand = SearchForSiblingCommand<T>(parseResult.CommandResult.Command);
            List<string> parentCommands2 = new();
            Command? nextCommand = siblingCommand;
            while (nextCommand != null)
            {
                parentCommands2.Add(nextCommand.Name);
                nextCommand = nextCommand.Parents.OfType<Command>().FirstOrDefault();
            }
            parentCommands2.Reverse();
            return new Example<T>(siblingCommand, [.. parentCommands2]);
        }

        private static T SearchForSiblingCommand<T>(Command currentCommand) where T : Command
        {
            Command? next = currentCommand;
            Command root = currentCommand;

            while (next != null)
            {
                root = next;
                next = next?.Parents.OfType<Command>().FirstOrDefault();
            }

            Queue<Command> probes = new();
            probes.Enqueue(root);
            while (probes.Count > 0)
            {
                Command current = probes.Dequeue();
                if (current is T typedCommand)
                {
                    return typedCommand;
                }
                foreach (var child in current.Subcommands)
                {
                    probes.Enqueue(child);
                }
            }
            throw new Exception($"Command structure is not correct: {nameof(T)} is not found.");
        }

        internal static Example<T> FromExistingTokens<T>(ParseResult parseResult) where T : Command
        {
            // root command name is not part of the tokens
            var commandParts = parseResult.Tokens.Select(t => t.Value).Prepend(parseResult.RootCommandResult.Command.Name);
            return new Example<T>((T)parseResult.CommandResult.Command, [.. commandParts]);
        }

        public static implicit operator string(Example e) => e.ToString()!;

        public Example WithHelpOption()
            => WithHelpOptionImpl();

        protected abstract Example WithHelpOptionImpl();
    }

    internal sealed class Example<T>(T currentCommand, ImmutableArray<string> commandParts) : Example
        where T : Command
    {
        public override string ToString()
            => string.Join(" ", commandParts);

        internal Example<T> WithOption(Func<T, Option> optionSelector, params string[] args)
        {
            var option = optionSelector(currentCommand);

            if (args.Any())
            {
                return new(currentCommand, commandParts.Add(option.Name).AddRange(args.Select(a => a.Any(char.IsWhiteSpace) ? $"'{a}'" : a)));
            }

            if (option.Arity.MinimumNumberOfValues == 0)
            {
                return new(currentCommand, commandParts.Add(option.Name));
            }

            return new(currentCommand, commandParts.AddRange(option.Name, CommandLineUtils.FormatArgumentUsage(option)));
        }

        protected override Example WithHelpOptionImpl()
            => WithHelpOption();

        public new Example<T> WithHelpOption()
            => new(currentCommand, commandParts.Add(Constants.KnownHelpAliases.First()));

        public Example<T> WithArguments(params IEnumerable<string> args)
            => new(currentCommand, commandParts.AddRange(args.Select(a => a.Any(char.IsWhiteSpace) ? $"'{a}'" : a)));

        public Example<T> WithArgument(Func<T, Argument> argumentSelector)
            => new(currentCommand, commandParts.Add(CommandLineUtils.FormatArgumentUsage(argumentSelector(currentCommand))));

        public Example<TSubcommand> WithSubcommand<TSubcommand>(Func<T, TSubcommand> subcommandSelector)
            where TSubcommand : Command
        {
            var subcommand = subcommandSelector(currentCommand);
            return new(subcommand, commandParts.Add(subcommand.Name));
        }

        public Example<TSubcommand> WithSubcommand<TSubcommand>() where TSubcommand : Command
        {
            var subcommand = (TSubcommand?)currentCommand.Subcommands.FirstOrDefault(c => c is TSubcommand)
                ?? throw new ArgumentException($"Command {currentCommand.Name} does not have subcommand {typeof(TSubcommand).Name}");

            return new(subcommand, commandParts.Add(subcommand.Name));
        }

        internal Example<Command> WithSubcommand(string token)
        {
            var subcommand = currentCommand.Subcommands.FirstOrDefault(c => c.Name.Equals(token) || c.Aliases.Contains(token))
                ?? throw new ArgumentException($"Command {currentCommand.Name} does not have subcommand '{token}'.");

            return new(subcommand, commandParts.Add(token));
        }
    }

}
