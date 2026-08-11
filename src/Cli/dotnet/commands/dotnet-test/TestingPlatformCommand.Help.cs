// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.CommandLine.Help;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand
    {
        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
        {
            yield return (context) =>
            {
                Console.WriteLine("Waiting for options and extensions...");

                Run(context.ParseResult);

                if (_commandLineOptionNameToModuleNames.IsEmpty)
                {
                    return;
                }

                Dictionary<bool, List<CommandLineOptionMessage>> allOptions = GetAllOptions();
                WriteOptionsToConsole(allOptions);

                Console.ForegroundColor = ConsoleColor.Yellow;

                Dictionary<bool, List<(string, string[])>> moduleToMissingOptions = GetModulesToMissingOptions(allOptions);
                WriteModulesToMissingOptionsToConsole(moduleToMissingOptions);

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
            };
        }

        private void OnHelpRequested(object sender, HelpEventArgs args)
        {
            CommandLineOptionMessages commandLineOptionMessages = args.CommandLineOptionMessages;
            string moduleName = commandLineOptionMessages.ModulePath;

            List<string> builtInOptions = [];
            List<string> nonBuiltInOptions = [];

            foreach (CommandLineOptionMessage commandLineOptionMessage in commandLineOptionMessages.CommandLineOptionMessageList)
            {
                if (commandLineOptionMessage.IsHidden.HasValue && commandLineOptionMessage.IsHidden.Value) continue;

                if (commandLineOptionMessage.IsBuiltIn.HasValue && commandLineOptionMessage.IsBuiltIn.Value)
                {
                    builtInOptions.Add(commandLineOptionMessage.Name);
                }
                else
                {
                    nonBuiltInOptions.Add(commandLineOptionMessage.Name);
                }

                _commandLineOptionNameToModuleNames.AddOrUpdate(
                    commandLineOptionMessage.Name,
                    commandLineOptionMessage,
                    (optionName, value) => (value));
            }

            _moduleNamesToCommandLineOptions.AddOrUpdate(true,
                [(moduleName, builtInOptions.ToArray())],
                (isBuiltIn, value) => [.. value, (moduleName, builtInOptions.ToArray())]);

            _moduleNamesToCommandLineOptions.AddOrUpdate(false,
               [(moduleName, nonBuiltInOptions.ToArray())],
               (isBuiltIn, value) => [.. value, (moduleName, nonBuiltInOptions.ToArray())]);
        }

        private Dictionary<bool, List<CommandLineOptionMessage>> GetAllOptions()
        {
            Dictionary<bool, List<CommandLineOptionMessage>> builtInToOptions = [];

            foreach (KeyValuePair<string, CommandLineOptionMessage> option in _commandLineOptionNameToModuleNames)
            {
                if (!builtInToOptions.TryGetValue(option.Value.IsBuiltIn.Value, out List<CommandLineOptionMessage> value))
                {
                    builtInToOptions.Add(option.Value.IsBuiltIn.Value, [option.Value]);
                }
                else
                {
                    value.Add(option.Value);
                }
            }
            return builtInToOptions;
        }

        private Dictionary<bool, List<(string, string[])>> GetModulesToMissingOptions(Dictionary<bool, List<CommandLineOptionMessage>> options)
        {
            IEnumerable<string> builtInOptions = options.TryGetValue(true, out List<CommandLineOptionMessage> builtIn) ? builtIn.Select(option => option.Name) : [];
            IEnumerable<string> nonBuiltInOptions = options.TryGetValue(false, out List<CommandLineOptionMessage> nonBuiltIn) ? nonBuiltIn.Select(option => option.Name) : [];

            Dictionary<bool, List<(string, string[])>> modulesWithMissingOptions = [];

            foreach (KeyValuePair<bool, List<(string, string[])>> modulesToOptions in _moduleNamesToCommandLineOptions)
            {
                foreach ((string module, string[] relatedOptions) in modulesToOptions.Value)
                {
                    IEnumerable<string> allOptions = modulesToOptions.Key ? builtInOptions : nonBuiltInOptions;
                    string[] missingOptions = allOptions.Except(relatedOptions).ToArray();

                    if (missingOptions.Length == 0)
                        continue;

                    if (modulesWithMissingOptions.TryGetValue(modulesToOptions.Key, out List<(string, string[])> value))
                    {
                        value.Add((module, missingOptions));
                    }
                    else
                    {
                        modulesWithMissingOptions.Add(modulesToOptions.Key, [(module, missingOptions)]);
                    }
                }
            }
            return modulesWithMissingOptions;
        }

        private void WriteOptionsToConsole(Dictionary<bool, List<CommandLineOptionMessage>> options)
        {
            int maxOptionNameLength = _commandLineOptionNameToModuleNames.Keys.ToArray().Max(option => option.Length);

            foreach (KeyValuePair<bool, List<CommandLineOptionMessage>> optionGroup in options)
            {
                Console.WriteLine();
                Console.WriteLine(optionGroup.Key ? "Options:" : "Extension options:");

                foreach (CommandLineOptionMessage option in optionGroup.Value)
                {
                    Console.WriteLine($"{new string(' ', 2)}--{option.Name}{new string(' ', maxOptionNameLength - option.Name.Length)} {option.Description}");
                }
            }
        }

        private static void WriteModulesToMissingOptionsToConsole(Dictionary<bool, List<(string, string[])>> modulesWithMissingOptions)
        {
            foreach (KeyValuePair<bool, List<(string, string[])>> groupedModules in modulesWithMissingOptions)
            {
                Console.WriteLine();
                Console.WriteLine(groupedModules.Key ? "Unavailable options:" : "Unavailable extension options:");

                foreach ((string module, string[] missingOptions) in groupedModules.Value)
                {
                    if (module.Length == 0)
                    {
                        continue;
                    }

                    StringBuilder line = new();
                    for (int i = 0; i < missingOptions.Length; i++)
                    {
                        if (i == missingOptions.Length - 1)
                            line.Append($"--{missingOptions[i]}");
                        else
                            line.Append($"--{missingOptions[i]}\n");
                    }

                    string verb = missingOptions.Length == 1 ? "" : "(s)";
                    Console.WriteLine($"{module} is missing the option{verb} below\n{line}\n");
                }
            }
        }
    }
}
