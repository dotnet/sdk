﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class NewCommandArgs : GlobalArgs
    {
        public NewCommandArgs(NewCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            List<Token> tokensToEvaluate = new List<Token>();
            foreach (var childrenResult in parseResult.CommandResult.Children)
            {
                if (childrenResult is OptionResult o)
                {
                    if (IsHelpOption(o))
                    {
                        continue;
                    }
                    if (!command.LegacyOptions.Contains(o.Option) && !command.PassByOptions.Contains(o.Option))
                    {
                        continue;
                    }

                    if (o.Token is { } token) { tokensToEvaluate.Add(token); }
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

        private bool IsHelpOption(SymbolResult result)
        {
            if (result is not OptionResult optionResult)
            {
                return false;
            }
            if (optionResult.Option.HasAlias(Constants.KnownHelpAliases[0]))
            {
                return true;
            }
            return false;
        }
    }
}
