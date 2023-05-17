// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class DetailsCommandArgs : GlobalArgs
    {
        internal DetailsCommandArgs(BaseDetailsCommand detailsCommand, ParseResult parseResult) : base(detailsCommand, parseResult)
        {
            string? nameCriteria = parseResult.GetValue(BaseDetailsCommand.NameArgument)
                ?? throw new ArgumentException($"{nameof(parseResult)} should contain one argument for {nameof(BaseDetailsCommand.NameArgument)}", nameof(parseResult));

            NameCriteria = nameCriteria;
            VersionCriteria = parseResult.GetValueForOptionOrNull(BaseDetailsCommand.VersionOption);
        }

        internal string? NameCriteria { get; }

        internal string? VersionCriteria { get; }
    }
}
