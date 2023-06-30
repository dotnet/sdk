// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Authoring.CLI.Commands;
using Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify;

namespace Microsoft.TemplateEngine.Authoring.CLI
{
    internal sealed class Program
    {
        internal static Task<int> Main(string[] args)
        {
            CliRootCommand rootCommand = new("dotnet-template-authoring");
            rootCommand.Subcommands.Add(new LocalizeCommand());
            rootCommand.Subcommands.Add(new VerifyCommand());
            rootCommand.Subcommands.Add(new ValidateCommand());

            return GetCommandLineConfiguration(rootCommand).InvokeAsync(args);
        }

        internal static CliConfiguration GetCommandLineConfiguration(CliCommand command)
        {
            return new CliConfiguration(command)
            {
                EnablePosixBundling = false
            };
        }
    }
}
