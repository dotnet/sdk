// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.New;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class InstantiateCommandArgs : GlobalArgs
    {
        private readonly IEnumerable<string> s_passByOptionNames =
        [
            SharedOptionsFactory.ForceOptionName,
            SharedOptionsFactory.NameOptionName,
            SharedOptionsFactory.DryRunOptionName,
            SharedOptionsFactory.NoUpdateCheckOptionName,
        ];

        internal string? ShortName { get; }

        internal string[] RemainingArguments { get; }

        internal string[] TokensToInvoke { get; }

        public InstantiateCommandArgs(InstantiateCommand command, ParseResult parseResult)
            : base(parseResult)
        {
            RemainingArguments = parseResult.GetValue(command.Definition.RemainingArguments) ?? [];
            ShortName = parseResult.GetValue(command.Definition.ShortNameArgument);

            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(ShortName))
            {
                tokens.Add(ShortName);
            }
            tokens.AddRange(RemainingArguments);

            foreach (OptionResult optionResult in parseResult.CommandResult.Children.OfType<OptionResult>())
            {
                if (s_passByOptionNames.Contains(optionResult.Option.Name))
                {
                    if (optionResult.IdentifierToken is { } token)
                    {
                        tokens.Add(token.Value);
                    }
                    tokens.AddRange(optionResult.Tokens.Select(t => t.Value));
                }
            }
            TokensToInvoke = tokens.ToArray();
        }

        private InstantiateCommandArgs(string? shortName, string[] remainingArgs, ParseResult parseResult)
            : base(parseResult)
        {
            Debug.Assert(parseResult.CommandResult.Command is NewCommand);

            ShortName = shortName;
            RemainingArguments = remainingArgs;

            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(ShortName))
            {
                tokens.Add(ShortName);
            }
            tokens.AddRange(remainingArgs);
            TokensToInvoke = [.. tokens];
        }

        public Command NewOrInstantiateCommand => ParseResult.CommandResult.Command;

        internal static InstantiateCommandArgs FromNewCommandArgs(NewCommandArgs newCommandArgs)
            => newCommandArgs.Tokens is [var firstToken, .. var rest]
            ? new(firstToken, rest, newCommandArgs.ParseResult)
            : new(shortName: null, remainingArgs: [], newCommandArgs.ParseResult);
    }
}
