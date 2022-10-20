// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.CLI.Commands;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;

namespace Microsoft.TemplateEngine.Authoring.CLI
{
    internal sealed class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            RootCommand rootCommand = new("dotnet-template-authoring");
            rootCommand.AddCommand(new LocalizeCommand(loggerFactory));
            rootCommand.AddCommand(new VerifyCommand(loggerFactory));

            return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }
    }
}
