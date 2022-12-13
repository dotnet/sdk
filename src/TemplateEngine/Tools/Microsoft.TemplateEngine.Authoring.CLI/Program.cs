// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Authoring.CLI.Commands;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;

namespace Microsoft.TemplateEngine.Authoring.CLI
{
    internal sealed class Program
    {
        internal static Task<int> Main(string[] args)
        {
            RootCommand rootCommand = new("dotnet-template-authoring");
            rootCommand.AddCommand(new LocalizeCommand());
            rootCommand.AddCommand(new VerifyCommand());

            return CreateParser(rootCommand).Parse(args).InvokeAsync();
        }

        internal static Parser CreateParser(Command command)
        {
            CommandLineBuilder builder = new CommandLineBuilder(command)
                   .UseDefaults()
                   .EnablePosixBundling(false);
            return builder.Build();
        }
    }
}
