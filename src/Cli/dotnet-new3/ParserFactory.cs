﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Dotnet_new3
{
    internal static class ParserFactory
    {
        internal static Parser CreateParser(Command command, bool disableHelp = false)
        {
            var builder = new CommandLineBuilder(command)
            //.UseExceptionHandler(ExceptionHandler)
            //.UseLocalizationResources(new CommandLineValidationMessages())
            //TODO: decide if it's needed to implement it; and implement if needed
            //.UseParseDirective()
            //.UseSuggestDirective()
            .UseParseErrorReporting()//TODO: discuss with SDK if it is possible to use it.
            .EnablePosixBundling(false);

            if (!disableHelp)
            {
                builder = builder.UseHelp(ctx => ctx.HelpBuilder.CustomizeLayout(CustomHelpLayout));
            }
            return builder.Build();
        }

        private static IEnumerable<Action<HelpContext>> CustomHelpLayout(HelpContext context)
        {
            if (context.ParseResult.CommandResult.Command is ICustomHelp custom)
            {
                return custom.CustomHelpLayout();
            }
            else
            {
                return HelpBuilder.Default.GetLayout();
            }
        }
    }
}
