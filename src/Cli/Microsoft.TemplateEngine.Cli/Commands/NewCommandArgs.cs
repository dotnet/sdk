// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Commands.New;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class NewCommandArgs : GlobalArgs
    {
        private IEnumerable<string> s_passByOptionNames =
        [
            SharedOptionsFactory.ForceOptionName,
            SharedOptionsFactory.NameOptionName,
            SharedOptionsFactory.DryRunOptionName,
            SharedOptionsFactory.NoUpdateCheckOptionName
        ];

        public NewCommandArgs(NewCommand command, ParseResult parseResult)
            : base(parseResult)
        {
            List<Token> tokensToEvaluate = new();
            foreach (var childrenResult in parseResult.CommandResult.Children)
            {
                if (childrenResult is OptionResult o)
                {
                    if (IsHelpOption(o))
                    {
                        continue;
                    }

                    if (!LegacyOptions.AllNames.Contains(o.Option.Name) && !s_passByOptionNames.Contains(o.Option.Name))
                    {
                        continue;
                    }

                    if (o.IdentifierToken is { } token) { tokensToEvaluate.Add(token); }
                    tokensToEvaluate.AddRange(o.Tokens);
                }
                else
                {
                    tokensToEvaluate.AddRange(childrenResult.Tokens);
                }
            }

            Tokens = tokensToEvaluate
                .Select(t => t.Value).ToArray();
        }

        internal string[] Tokens { get; }

        private static bool IsHelpOption(SymbolResult result)
            => result is OptionResult optionResult && optionResult.Option is HelpOption;
    }
}
