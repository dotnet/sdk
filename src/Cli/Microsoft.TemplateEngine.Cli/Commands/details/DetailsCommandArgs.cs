// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
