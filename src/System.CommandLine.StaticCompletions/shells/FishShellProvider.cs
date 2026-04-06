// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CodeDom.Compiler;
using System.CommandLine.Completions;
using System.CommandLine.StaticCompletions.Resources;

namespace System.CommandLine.StaticCompletions.Shells;

public class FishShellProvider : IShellProvider
{
    public string ArgumentName => ShellNames.Fish;

    public string Extension => "fish";

    public string HelpDescription => Strings.FishShellProvider_HelpDescription;

    // override the ToString method to return the argument name so that CLI help is cleaner for 'default' values
    public override string ToString() => ArgumentName;

    public string GenerateCompletions(Command command)
    {
        var safeName = command.Name.MakeSafeFunctionName();

        using var textWriter = new StringWriter { NewLine = "\n" };
        using var writer = new IndentedTextWriter(textWriter);

        writer.WriteLine($"# fish completions for {command.Name}");
        writer.WriteLine();

        // Collect all commands in a flat list and assign numeric state IDs
        var states = new List<(int id, Command cmd)>();
        CollectStates(command, states);

        // Write the main completion function
        writer.WriteLine($"function _{safeName}");
        writer.Indent++;

        WriteTokenization(writer);
        writer.WriteLine();
        WriteStateWalker(writer, states);
        writer.WriteLine();
        WriteOptionValueCompletions(writer, states);
        WriteStateCompletions(writer, states);

        writer.Indent--;
        writer.WriteLine("end");
        writer.WriteLine();

        writer.WriteLine($"complete -c {command.Name} -f -a '(_{safeName})'");

        writer.Flush();
        return textWriter.ToString();
    }

    /// <summary>
    /// Recursively collect all visible commands into a flat list with numeric state IDs.
    /// State 0 is the root command.
    /// </summary>
    private static void CollectStates(Command cmd, List<(int id, Command cmd)> states)
    {
        states.Add((states.Count, cmd));
        foreach (var sub in cmd.Subcommands.Where(c => !c.Hidden))
        {
            CollectStates(sub, states);
        }
    }

    /// <summary>
    /// Write the command line tokenization logic.
    /// Uses fish's commandline builtin to get completed tokens and the current partial word.
    /// </summary>
    private static void WriteTokenization(IndentedTextWriter writer)
    {
        // -opc: tokenize, cut at cursor, only completed tokens (excludes current partial word)
        writer.WriteLine("set -l tokens (commandline -opc)");
        // -ct: the current token being completed (may be empty or partial)
        writer.WriteLine("set -l current (commandline -ct)");
    }

    /// <summary>
    /// Generate the state machine that walks completed tokens to determine which subcommand context we're in.
    /// For each state, we check if the current word matches a known subcommand (transitioning to that subcommand's state)
    /// or a value-taking option (skipping the next token which is the option's value).
    /// </summary>
    private static void WriteStateWalker(IndentedTextWriter writer, List<(int id, Command cmd)> states)
    {
        writer.WriteLine("set -l state 0");
        writer.WriteLine("set -l i 2"); // start after the command name (fish arrays are 1-based)
        writer.WriteLine("while test $i -le (count $tokens)");
        writer.Indent++;
        writer.WriteLine("set -l word $tokens[$i]");

        writer.WriteLine("switch $state");
        writer.Indent++;

        foreach (var (stateId, cmd) in states)
        {
            var visibleSubs = cmd.Subcommands.Where(c => !c.Hidden).ToArray();
            var valueOptionNames = cmd.HierarchicalOptions()
                .Where(o => !o.Hidden && !o.IsFlag())
                .SelectMany(o => o.Names())
                .ToArray();

            // Skip states that have no transitions to emit
            if (visibleSubs.Length == 0 && valueOptionNames.Length == 0)
                continue;

            writer.WriteLine($"case {stateId}");
            writer.Indent++;
            writer.WriteLine("switch $word");
            writer.Indent++;

            // Subcommand transitions
            foreach (var sub in visibleSubs)
            {
                var subStateId = states.First(s => s.cmd == sub).id;
                var names = string.Join(" ", sub.Names());
                writer.WriteLine($"case {names}");
                writer.Indent++;
                writer.WriteLine($"set state {subStateId}");
                writer.Indent--;
            }

            // Value-taking option transitions: skip the next token (the option's value)
            if (valueOptionNames.Length > 0)
            {
                writer.WriteLine($"case {string.Join(" ", valueOptionNames)}");
                writer.Indent++;
                writer.WriteLine("set i (math $i + 1)");
                writer.Indent--;
            }

            writer.Indent--;
            writer.WriteLine("end");
            writer.Indent--;
        }

        writer.Indent--;
        writer.WriteLine("end");

        writer.WriteLine("set i (math $i + 1)");
        writer.Indent--;
        writer.WriteLine("end");
    }

    /// <summary>
    /// Generate option value completions.
    /// When the previous completed token is a value-taking option, we emit completions for that option's values
    /// instead of the general completions for the current state.
    /// </summary>
    private static void WriteOptionValueCompletions(IndentedTextWriter writer, List<(int id, Command cmd)> states)
    {
        var hasAnyValueOptions = states.Any(s =>
            s.cmd.HierarchicalOptions().Any(o => !o.Hidden && !o.IsFlag()));

        if (!hasAnyValueOptions) return;

        writer.WriteLine("if set -q tokens[2]");
        writer.Indent++;
        writer.WriteLine("set -l prev $tokens[-1]");
        writer.WriteLine("switch $state");
        writer.Indent++;

        foreach (var (stateId, cmd) in states)
        {
            var valueOptions = cmd.HierarchicalOptions()
                .Where(o => !o.Hidden && !o.IsFlag())
                .ToArray();

            if (valueOptions.Length == 0)
                continue;

            writer.WriteLine($"case {stateId}");
            writer.Indent++;
            writer.WriteLine("switch $prev");
            writer.Indent++;

            foreach (var option in valueOptions)
            {
                var names = string.Join(" ", option.Names());
                writer.WriteLine($"case {names}");
                writer.Indent++;

                if (option.IsDynamic)
                {
                    writer.WriteLine(GenerateDynamicCall());
                }
                else
                {
                    var completions = option.GetCompletions(CompletionContext.Empty).ToArray();
                    foreach (var c in completions)
                    {
                        WriteCompletionCandidate(writer, c);
                    }
                }
                writer.WriteLine("return");
                writer.Indent--;
            }

            writer.Indent--;
            writer.WriteLine("end");
            writer.Indent--;
        }

        writer.Indent--;
        writer.WriteLine("end");
        writer.Indent--;
        writer.WriteLine("end");
        writer.WriteLine();
    }

    /// <summary>
    /// Generate the main completion output for each state.
    /// Emits subcommands, options, and positional argument completions for the current context.
    /// </summary>
    private static void WriteStateCompletions(IndentedTextWriter writer, List<(int id, Command cmd)> states)
    {
        writer.WriteLine("switch $state");
        writer.Indent++;

        foreach (var (stateId, cmd) in states)
        {
            writer.WriteLine($"case {stateId}");
            writer.Indent++;

            // Subcommand completions
            foreach (var sub in cmd.Subcommands.Where(c => !c.Hidden))
            {
                WriteCandidate(writer, sub.Name, SanitizeDescription(sub.Description));
            }

            // Option completions - emit all aliases so both -h and --help are completable
            foreach (var option in cmd.HierarchicalOptions().Where(o => !o.Hidden))
            {
                var desc = SanitizeDescription(option.Description);
                foreach (var name in option.Names())
                {
                    WriteCandidate(writer, name, desc);
                }
            }

            // Positional argument completions
            foreach (var arg in cmd.Arguments.Where(a => !a.Hidden))
            {
                if (arg.IsDynamic)
                {
                    writer.WriteLine(GenerateDynamicCall());
                }
                else
                {
                    var completions = arg.GetCompletions(CompletionContext.Empty).ToArray();
                    foreach (var c in completions)
                    {
                        WriteCompletionCandidate(writer, c);
                    }
                }
            }

            writer.Indent--;
        }

        writer.Indent--;
        writer.WriteLine("end");
    }

    /// <summary>
    /// Write a single completion candidate line with an optional description.
    /// Fish uses tab-separated format: candidate\tdescription
    /// </summary>
    private static void WriteCompletionCandidate(IndentedTextWriter writer, CompletionItem completion)
    {
        var label = completion.InsertText ?? completion.Label;
        var desc = completion.Documentation ?? completion.Detail;
        WriteCandidate(writer, label, desc);
    }

    private static void WriteCandidate(IndentedTextWriter writer, string label, string? description)
    {
        if (!string.IsNullOrEmpty(description))
            writer.WriteLine($"printf '%s\\t%s\\n' {FishEscape(label)} {FishEscape(description)}");
        else
            writer.WriteLine($"printf '%s\\n' {FishEscape(label)}");
    }

    /// <summary>
    /// Generate a dynamic completion call that invokes the binary's 'complete' subcommand.
    /// Uses the actual command from the command line ($tokens[1]) so it works regardless of how the binary was invoked.
    /// </summary>
    /// <remarks>TODO: this is currently bound to the .NET CLI's 'complete' command pattern - this should be definable/injectable per-host instead.</remarks>
    internal static string GenerateDynamicCall()
    {
        return "command $tokens[1] complete --position (commandline -C) (commandline -cp) 2>/dev/null";
    }

    /// <summary>
    /// Escape a string for use in a fish single-quoted string.
    /// Fish single-quoted strings support \' and \\ as escape sequences.
    /// </summary>
    internal static string FishEscape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "''";
        return "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }

    private static string SanitizeDescription(string? s) =>
        s?.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ') ?? "";
}
