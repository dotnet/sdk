// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.TemplateLocalizer.Commands;
using Microsoft.TemplateEngine.TemplateLocalizer.Commands.Export;

namespace Microsoft.TemplateEngine.TemplateLocalizer
{
    internal sealed class Program
    {
        private static readonly Func<ILoggerFactory, ExecutableCommand>[] CommandCreators = new Func<ILoggerFactory, ExecutableCommand>[]
        {
            (factory) => new ExportCommand(factory),
        };

        internal static async Task<int> Main(string[] args)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            ILogger logger = loggerFactory.CreateLogger<Program>();

            RootCommand rootCommand = new ();
            rootCommand.Name = "dotnet-template-localizer";

            foreach (Func<ILoggerFactory, ExecutableCommand> commandCreator in CommandCreators)
            {
                ExecutableCommand command = commandCreator(loggerFactory);
                rootCommand.AddCommand(command.CreateCommand());
            }

            return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }
    }
}
