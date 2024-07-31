// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions.Shells;

#nullable enable

using System.CodeDom.Compiler;
using System.CommandLine;
using System.CommandLine.Completions;
using System.IO;

public class BashShellProvider : IShellProvider
{
    public string ArgumentName => "bash";

    public string GenerateCompletions(System.CommandLine.CliCommand command)
    {
        var initialFunctionName = command.FunctionName().MakeSafeFunctionName();
        return
            $"""
            #! /bin/bash
            {GenerateCommandsCompletions([], command, isNestedCommand: false)}

            complete -F {initialFunctionName} {command.Name}
            """;
    }

    private string GenerateCommandsCompletions(string[] parentCommandNames, CliCommand command, bool isNestedCommand)
    {
        var functionName = command.FunctionName(parentCommandNames).MakeSafeFunctionName();

        var dollarOne = !isNestedCommand ? "1" : "$1";
        var subcommandArgument = !isNestedCommand ? "2" : "$(($1+1))";

        // generate the words for options and subcommands
        var visibleSubcommands = command.Subcommands.Where(c => !c.Hidden).ToArray();
        // notably, do not generate completions for all option aliases - since a user is tab-completing we can use the longest forms
        var completionOptions = AllOptionsForCommand(command).Where(o => !o.Hidden).Select(o => o.Name).ToArray();
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
        writer.WriteLine($"""opts="{string.Join(' ', completionWords)}" """);
        foreach (var positionalArgumentCompletion in positionalArgumentCompletions)
        {
            writer.WriteLine($"""opts="$opts {positionalArgumentCompletion}" """);
        }
        writer.WriteLine();

        // emit a short-circuit for when the first argument index (COMP_CWORD) is 1 (aka "dotnet")
        // in this short-circuit we'll just use the choices we set up above in $opts
        writer.WriteLine($"""if [[ $COMP_CWORD == "{dollarOne}" ]]; then""");
        writer.Indent++;
        writer.WriteLine(GenerateChoicesPrompt("$opts"));
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
        writer.WriteLine(GenerateChoicesPrompt("$opts"));
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();

        // annnnd flush!
        writer.Flush();
        string[] parentCommandNamesForSubcommands = [.. parentCommandNames, command.Name];
        return textWriter.ToString() + string.Join('\n', visibleSubcommands.Select(c => GenerateCommandsCompletions(parentCommandNamesForSubcommands, c, isNestedCommand: true)));
    }

    private static IEnumerable<CliOption> AllOptionsForCommand(CliCommand c)
    {
        if (c.Parents.Count() == 0)
        {
            return c.Options;
        }
        else
        {
            return c.Options.Concat(c.Parents.OfType<CliCommand>().SelectMany(OptionsForParent));
        }

    }

    private static IEnumerable<CliOption> OptionsForParent(CliCommand c)
    {
        foreach (var o in c.Options)
        {
            if (o.Recursive) yield return o;
        }
        foreach (var p in c.Parents.OfType<CliCommand>())
        {
            foreach (var o in OptionsForParent(p))
            {
                yield return o;
            }
        }
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
                completions.Add($"""({string.Join(' ', argCompletions)})""");
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
        return $$"""${COMP_WORDS[0]} complete --position ${COMP_POINT} ${COMP_LINE} 2>/dev/null | tr '\n' ' '""";
    }

    private static string? GenerateOptionHandlers(CliCommand command)
    {
        var optionHandlers = command.Options.Where(o => !o.Hidden).Select(GenerateOptionHandler).Where(handler => handler is not null).ToArray();
        if (optionHandlers.Length == 0)
        {
            return null;
        }
        return string.Join("\n", optionHandlers);
    }

    /// <summary>
    /// Emit a bash command that calls compgen with a set of choices given the current work/stem, and sets those choices to COMPREPLY.
    /// Think of this like a 'return' from a function.
    /// </summary>
    /// <param name="choicesInvocation">The expression used to generate the set of choices - will be passed to compgen with the -W flag, so should be either
    /// * a concrete set of choices in a bash array already ($opts), or
    /// * a subprocess that will return such an array (aka '(dotnet complete --position 10 'dotnet ad')') </param>
    /// <returns></returns>
    private static string GenerateChoicesPrompt(string choicesInvocation) => $$"""COMPREPLY=( $(compgen -W "{{choicesInvocation}}" -- "$cur") )""";

    /// <summary>
    /// Generates a concrete set of bash completion selection for a given option.
    /// If the option's completions are dynamic, this will emit a call to the dynamic completion function (dotnet complete)
    /// to get completions when the user requests completions for this option.
    /// </summary>
    /// <param name="option"></param>
    /// <returns>a bash switch case expression for providing completions for this option</returns>
    private static string? GenerateOptionHandler(CliOption option)
    {
        // unlike the completion-options generation, for actually implementing suggestions we should be able to handle all of the options' aliases.
        // this ensures if the user manually enters an alias we can support that usage.
        var optionNames = string.Join('|', option.Names());
        string completionCommand;
        if (option.GetType().IsGenericType &&
            (option.GetType().GetGenericTypeDefinition() == typeof(DynamicOption<int>).GetGenericTypeDefinition()
             || option.GetType().GetGenericTypeDefinition() == typeof(DynamicForwardedOption<string>).GetGenericTypeDefinition()))
        {
            // dynamic options require a call into the app for completions
            completionCommand = GenerateChoicesPrompt($"({GenerateDynamicCall()})");
        }
        else if (option.Arity.MaximumNumberOfValues == 0)
        {
            // do not generate completions for flags-style options - the only completion is the option's _name_
            return null;
        }
        else
        {
            var completions = option.GetCompletions(CompletionContext.Empty).Select(c => c.Label);
            if (completions.Count() == 0)
            {
                // if no static completions are available, then don't emit anything
                return null;
            }
            else
            {
                // otherwise emit a direct list of choices
                completionCommand = GenerateChoicesPrompt($"{string.Join(' ', completions)}");
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
    /// <summary>
    /// Create a unique shell function name for a command - these names should be
    /// * distinct from the 'root' command's name (i.e. we should not generate the function name 'dotnet' for the binary 'dotnet')
    /// * distinct based on 'path' to get to this function (hence the parentCommandNames)
    /// </summary>
    /// <param name="command"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    public static string FunctionName(this CliCommand command, string[]? parentCommandNames = null) => parentCommandNames switch
    {
        null => "_" + command.Name,
        [] => "_" + command.Name,
        var names => "_" + string.Join('_', names) + "_" + command.Name
    };

    /// <summary>
    /// sanitizes a function name to be safe for bash
    /// </summary>
    /// <param name="functionName"></param>
    /// <returns></returns>
    public static string MakeSafeFunctionName(this string functionName) => functionName.Replace('-', '_');

    /// <summary>
    /// Get all names for an option, including the primary name and all aliases
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public static string[] Names(this CliOption option)
    {
        if (option.Aliases.Count == 0)
        {
            return [option.Name];
        }
        else if (option is System.CommandLine.Help.HelpOption) // some of the help aliases are truly horrible
        {
            return ["--help", "-h"];
        }
        else
        {
            return [option.Name, .. option.Aliases];
        }
    }

    /// <summary>
    /// Get all names for a command, including the primary name and all aliases
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
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
