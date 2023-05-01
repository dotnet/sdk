// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class NewCommandArgs : GlobalArgs
    {
        public NewCommandArgs(NewCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            List<CliToken> tokensToEvaluate = new();
            foreach (var childrenResult in parseResult.CommandResult.Children)
            {
                if (childrenResult is OptionResult o)
                {
                    if (o.Option is HelpOption)
                    {
                        continue;
                    }
                    if (!command.LegacyOptions.Contains(o.Option) && !command.PassByOptions.Contains(o.Option))
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
    }
}
