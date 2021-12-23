// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.TemplateLocalizer.Commands.Export;

namespace Microsoft.TemplateEngine.TemplateLocalizer
{
    internal sealed class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            ILogger logger = loggerFactory.CreateLogger<Program>();

            RootCommand rootCommand = new("dotnet-template-localizer");
            rootCommand.AddCommand(new ExportCommand(loggerFactory));

            return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }
    }
}
