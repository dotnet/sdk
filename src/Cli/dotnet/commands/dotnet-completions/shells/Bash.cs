// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions.Shells;

using System.CodeDom.Compiler;
using System.CommandLine;
using System.CommandLine.Completions;
using System.IO;

public class BashShellProvider : IShellProvider
{
    public string ArgumentName => "bash";

    public string GenerateCompletions(System.CommandLine.CliCommand command)
    {
        var initialFunctionName = new[] { command }.FunctionName().MakeSafeFunctionName();
        return
            $"""
            #! /bin/bash
            {GenerateCommandsCompletions([command])}

            complete -F {initialFunctionName} {command.Name}
            """;
    }

    private string GenerateCommandsCompletions(CliCommand[] commands)
    {
        var command = commands.Last();
        var functionName = commands.FunctionName().MakeSafeFunctionName();

        var isRootCommand = commands.Length == 1;
        var dollarOne = isRootCommand ? "1" : "$1";
        var subcommandArgument = isRootCommand ? "2" : "$(($1+1))";

        // generate the words for options and subcommands
        var visibleSubcommands = command.Subcommands.Where(c => !c.Hidden).ToArray();
        var completionOptions = command.Options.Where(o => !o.Hidden).SelectMany(o => o.Names()).ToArray();
        var completionSubcommands = visibleSubcommands.Select(x => x.Name).ToArray();
        string[] completionWords = [.. completionSubcommands, .. completionOptions];

        // for positional arguments this can be pretty dynamic
        var positionalArgumentCompletions = PositionalArgumentTerms(command.Arguments.Where(a => !a.Hidden).ToArray());

        using var textWriter = new StringWriter { NewLine = "\n" };
        using var writer = new IndentedTextWriter(textWriter);

        // write the overall completion function shell
        writer.WriteLine($"{functionName}() {{");
        writer.WriteLine();
        writer.Indent++;

        // set up state
        writer.WriteLine("""cur="${COMP_WORDS[COMP_CWORD]}" """);
        writer.WriteLine("""prev="${COMP_WORDS[COMP_CWORD-1]}" """);
        writer.WriteLine("COMPREPLY=()");
        writer.WriteLine();

        // fill in a set of completions for all of the subcommands and flag options for the top-level command
        writer.WriteLine($"""opts="{String.Join(' ', completionWords)}" """);
        foreach (var positionalArgumentCompletion in positionalArgumentCompletions)
        {
            writer.WriteLine($"""opts="$opts {positionalArgumentCompletion}" """);
        }
        writer.WriteLine();

        // emit a short-circuit for when the first argument index (COMP_CWORD) is 1 (aka "dotnet")
        // in this short-circuit we'll just use the choices we set up above in $opts
        writer.WriteLine($"""if [[ $COMP_CWORD == {dollarOne} ]]; then""");
        writer.Indent++;
        writer.WriteLine("""COMPREPLY=( $(compgen -W "$opts" -- "$cur") )""");
        writer.WriteLine("return");
        writer.Indent--;
        writer.WriteLine("fi");
        writer.WriteLine();

        // generate how to handle completions for options or flags
        var optionHandlers = GenerateOptionHandlers(command);
        if (optionHandlers is not null)
        {
            writer.WriteLine("case $prev in");
            writer.Indent++;
            foreach (var line in optionHandlers.Split('\n'))
            {
                writer.WriteLine(line);
            }
            writer.Indent--;
            writer.WriteLine("esac");
            writer.WriteLine();
        }

        // finally subcommand completions - these are going to emit calls to subcommand completion functions that we'll emit at the end of this method
        if (visibleSubcommands.Length > 0)
        {
            writer.WriteLine($"case ${{COMP_WORDS[{dollarOne}]}} in");
            writer.Indent++;
            foreach (var subcommand in visibleSubcommands)
            {
                writer.WriteLine($"({subcommand.Name})");
                writer.Indent++;
                writer.WriteLine($"{functionName}_{subcommand.Name} {subcommandArgument}");
                writer.WriteLine("return");
                writer.WriteLine(";;");
                writer.WriteLine();
                writer.Indent--;
            }
            writer.Indent--;
            writer.WriteLine("esac");
            writer.WriteLine();
        }

        // write the final trailer for the overall completion script
        writer.WriteLine("""COMPREPLY=( $(compgen -W "$opts" -- "$cur") )""");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();

        // annnnd flush!
        writer.Flush();
        return textWriter.ToString() + String.Join('\n', visibleSubcommands.Select(c => GenerateCommandsCompletions([.. commands, c])));
    }

    private static string[] PositionalArgumentTerms(CliArgument[] arguments)
    {
        var completions = new List<string>();
        foreach (var argument in arguments)
        {
            if (argument.GetType().GetGenericTypeDefinition() == typeof(DynamicArgument<int>).GetGenericTypeDefinition())
            {
                // if the argument is a not-static-friendly argument, we need to call into the app for completions
                completions.Add($"$({GenerateDynamicCall()})");
                continue;
            }
            var argCompletions = argument.GetCompletions(CompletionContext.Empty).Select(c => c.Label).ToArray();
            if (argCompletions.Length != 0)
            {
                // otherwise emit a direct list of choices
                completions.Add($"""({String.Join(' ', argCompletions)})""");
            }
        }

        return completions.ToArray();
    }

    /// <summary>
    /// Generates a call to `dotnet complete <string> --position <int>` for dynamic completions where necessary, but in a more generic way
    /// </summary>
    /// <returns></returns>
    private static string GenerateDynamicCall()
    {
        return $$"""${COMP_WORDS[0]} complete --position "${COMP_POINT}" "${COMP_LINE}" 2>/dev/null | tr '\n' ' '""";
    }

    private static string GenerateOptionHandlers(CliCommand command)
    {
        var optionHandlers = command.Options.Where(o => !o.Hidden).Select(GenerateOptionHandler);
        return String.Join("\n", optionHandlers);
    }

    private static string GenerateOptionHandler(CliOption option)
    {
        var optionNames = String.Join('|', option.Names());
        string completionCommand;
        if (option.GetType().IsGenericType &&
            (option.GetType().GetGenericTypeDefinition() == typeof(DynamicOption<int>).GetGenericTypeDefinition()
             || option.GetType().GetGenericTypeDefinition() == typeof(DynamicForwardedOption<string>).GetGenericTypeDefinition()))
        {
            // dynamic options require a call into the app for completions
            completionCommand = $$"""COMPREPLY=( $(compgen -W "$({{GenerateDynamicCall()}})" -- "$cur") )""";
        }
        else
        {
            var completions = option.GetCompletions(CompletionContext.Empty).Select(c => c.Label);
            if (completions.Count() == 0)
            {
                // if no completions, assume that we need to call into the app for completions
                completionCommand = "";
            }
            else
            {
                // otherwise emit a direct list of choices
                completionCommand = $"""COMPREPLY=( $(compgen -W "{String.Join(' ', completions)}" -- "$cur") )""";
            }
        }

        return $"""
                {optionNames})
                    {completionCommand}
                    return
                ;;
                """;
    }
}


public static class HelpExtensions
{
    public static string FunctionName(this CliCommand[] commands) => "_" + String.Join('_', commands.Select(c => c.Name));
    public static string MakeSafeFunctionName(this string functionName) => functionName.Replace('-', '_');
    public static string[] Names(this CliOption option)
    {
        if (option.Aliases.Count == 0)
        {
            return [option.Name];
        }
        else
        {
            return [option.Name, .. option.Aliases];
        }
    }
    public static string[] Names(this CliCommand command)
    {
        if (command.Aliases.Count == 0)
        {
            return [command.Name];
        }
        else
        {
            return [command.Name, .. command.Aliases];
        }
    }

}
