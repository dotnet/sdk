// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Completions;
using System.CommandLine.StaticCompletions.Shells;
using System.Diagnostics;

namespace System.CommandLine.StaticCompletions;

public sealed class CompletionsCommandParser
{
    public static readonly IReadOnlyDictionary<string, IShellProvider> ShellProviders;

    static CompletionsCommandParser()
    {
        var providers = new IShellProvider[]
        {
            new BashShellProvider(),
            new PowerShellShellProvider(),
            new FishShellProvider(),
            new ZshShellProvider(),
            new NushellShellProvider()
        };

        Debug.Assert(providers.Select(provider => provider.ArgumentName).SequenceEqual(ShellNames.All));

        ShellProviders = providers.ToDictionary(s => s.ArgumentName, StringComparer.OrdinalIgnoreCase);
    }

    public static void ConfigureCommand(CompletionsCommandDefinition command)
    {
        command.ShellArgument.CompletionSources.Add(context =>
            ShellNames.All.Select(shellName => new CompletionItem(shellName, documentation: ShellProviders[shellName].HelpDescription)));

        command.GenerateScriptCommand.SetAction(args =>
        {
            var shellName = args.GetValue(command.GenerateScriptCommand.ShellArgument) ?? throw new InvalidOperationException();
            var shell = ShellProviders[shellName];

            var script = shell.GenerateCompletions(args.RootCommandResult.Command);
            args.InvocationConfiguration.Output.Write(script);
        });
    }
}
