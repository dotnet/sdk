// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Help;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal partial class TestingPlatformCommand
{
    private readonly ConcurrentDictionary<string, CommandLineOption> _commandLineOptionNameToModuleNames = [];
    private readonly ConcurrentDictionary<bool, List<(string, string[])>> _moduleNamesToCommandLineOptions = [];
    private static readonly string Indent = "  ";

    public IEnumerable<Action<HelpContext>> CustomHelpLayout()
    {
        yield return (context) =>
        {
            WriteHelpOptions(context);
            Console.WriteLine(CliCommandStrings.HelpWaitingForOptionsAndExtensions);

            Run(context.ParseResult);

            if (_commandLineOptionNameToModuleNames.IsEmpty)
            {
                return;
            }

            Dictionary<bool, List<CommandLineOption>> allOptions = GetAllOptions(context.Command.Options);
            allOptions.TryGetValue(true, out List<CommandLineOption> builtInOptions);
            allOptions.TryGetValue(false, out List<CommandLineOption> nonBuiltInOptions);

            Dictionary<bool, List<(string[], string[])>> moduleToMissingOptions = GetModulesToMissingOptions(_moduleNamesToCommandLineOptions, builtInOptions.Select(option => option.Name), nonBuiltInOptions.Select(option => option.Name));

            _output.WritePlatformAndExtensionOptions(context, builtInOptions, nonBuiltInOptions, moduleToMissingOptions);
        };
    }

    private static void WriteHelpOptions(HelpContext context)
    {
        HelpBuilder.Default.SynopsisSection()(context);
        context.Output.WriteLine();
        WriteUsageSection(context);
        context.Output.WriteLine();
        HelpBuilder.Default.OptionsSection()(context);
        context.Output.WriteLine();
    }

    private static void WriteUsageSection(HelpContext context)
    {
        context.Output.WriteLine(CliCommandStrings.CmdHelpUsageTitle);
        context.Output.WriteLine(Indent + string.Join(" ", GetCustomUsageParts(context.Command)));
    }

    private static IEnumerable<string> GetCustomUsageParts(Command command, bool showOptions = true, bool showPlatformOptions = true, bool showExtensionOptions = true)
    {
        var parentCommands = new List<Command>();
        var nextCommand = command;
        while (nextCommand is not null)
        {
            parentCommands.Add(nextCommand);
            nextCommand = nextCommand.Parents.FirstOrDefault(c => c is Command) as Command;
        }
        parentCommands.Reverse();

        foreach (var parentCommand in parentCommands)
        {
            yield return parentCommand.Name;
        }

        if (showOptions)
        {
            yield return FormatHelpOption(CliCommandStrings.HelpOptions);
        }

        if (showPlatformOptions)
        {
            yield return FormatHelpOption(CliCommandStrings.HelpPlatformOptions);
        }

        if (showExtensionOptions)
        {
            yield return FormatHelpOption(CliCommandStrings.HelpExtensionOptions);
        }
    }

    private static string FormatHelpOption(string option)
    {
        return $"[{option.Trim(':').ToLower()}]";
    }

    private void OnHelpRequested(object sender, HelpEventArgs args)
    {
        CommandLineOption[] commandLineOptionMessages = args.CommandLineOptions;
        string moduleName = args.ModulePath;

        List<string> builtInOptions = [];
        List<string> nonBuiltInOptions = [];

        foreach (CommandLineOption commandLineOption in commandLineOptionMessages)
        {
            if (commandLineOption.IsHidden.HasValue && commandLineOption.IsHidden.Value) continue;

            if (commandLineOption.IsBuiltIn.HasValue && commandLineOption.IsBuiltIn.Value)
            {
                builtInOptions.Add(commandLineOption.Name);
            }
            else
            {
                nonBuiltInOptions.Add(commandLineOption.Name);
            }

            _commandLineOptionNameToModuleNames.AddOrUpdate(
                commandLineOption.Name,
                commandLineOption,
                (optionName, value) => (value));
        }

        _moduleNamesToCommandLineOptions.AddOrUpdate(true,
            [(moduleName, builtInOptions.ToArray())],
            (isBuiltIn, value) => [.. value, (moduleName, builtInOptions.ToArray())]);

        _moduleNamesToCommandLineOptions.AddOrUpdate(false,
           [(moduleName, nonBuiltInOptions.ToArray())],
           (isBuiltIn, value) => [.. value, (moduleName, nonBuiltInOptions.ToArray())]);
    }

    private Dictionary<bool, List<CommandLineOption>> GetAllOptions(IList<Option> commandOptions)
    {
        Dictionary<bool, List<CommandLineOption>> filteredOptions = [];

        // Create a set of option names from the command's options for efficient lookup
        var commandOptionNames = commandOptions.Select(o => o.Name.TrimStart('-')).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, CommandLineOption> option in _commandLineOptionNameToModuleNames)
        {
            // Only include options that are NOT already present in the command's options
            if (!commandOptionNames.Contains(option.Value.Name))
            {
                if (!filteredOptions.TryGetValue(option.Value.IsBuiltIn.Value, out List<CommandLineOption> value))
                {
                    filteredOptions.Add(option.Value.IsBuiltIn.Value, [option.Value]);
                }
                else
                {
                    value.Add(option.Value);
                }
            }
        }

        // Sort options alphabetically by name
        foreach (var optionsList in filteredOptions.Values)
        {
            optionsList.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
        }

        return filteredOptions;
    }

    private static Dictionary<bool, List<(string[], string[])>> GetModulesToMissingOptions(
        ConcurrentDictionary<bool, List<(string, string[])>> moduleNamesToCommandLineOptions,
        IEnumerable<string> builtInOptions,
        IEnumerable<string> nonBuiltInOptions)
    {
        var modulesWithMissingOptions = new Dictionary<bool, List<(string[], string[])>>();

        foreach (var group in moduleNamesToCommandLineOptions)
        {
            bool isBuiltIn = group.Key;
            var groupedModules = new List<(string[], string[])>();
            var missingOptionsToModules = new Dictionary<string, List<string>>();

            var allOptions = new HashSet<string>(isBuiltIn ? builtInOptions : nonBuiltInOptions);

            foreach ((string module, string[] relatedOptions) in group.Value)
            {
                var missingOptions = new HashSet<string>(allOptions);
                missingOptions.ExceptWith(relatedOptions);

                if (missingOptions.Count > 0)
                {
                    var missingKey = string.Join(",", missingOptions.OrderBy(option => option));

                    if (!missingOptionsToModules.TryGetValue(missingKey, out var modules))
                    {
                        modules = [];
                        missingOptionsToModules[missingKey] = modules;
                    }
                    modules.Add(module);
                }
            }
            foreach (var kvp in missingOptionsToModules)
            {
                groupedModules.Add(([.. kvp.Value], kvp.Key.Split(',')));
            }

            if (groupedModules.Count > 0)
            {
                modulesWithMissingOptions.Add(isBuiltIn, groupedModules);
            }
        }

        return modulesWithMissingOptions;
    }
}
