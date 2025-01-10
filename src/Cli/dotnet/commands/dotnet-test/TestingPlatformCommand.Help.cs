// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Concurrent;
using System.CommandLine.Help;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand
    {
        private readonly ConcurrentDictionary<string, CommandLineOption> _commandLineOptionNameToModuleNames = [];
        private readonly ConcurrentDictionary<bool, List<(string, string[])>> _moduleNamesToCommandLineOptions = [];

        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
        {
            yield return async (context) =>
            {
                Console.WriteLine("Waiting for options and extensions...");

                Run(context.ParseResult);

                if (_commandLineOptionNameToModuleNames.IsEmpty)
                {
                    return;
                }

                Dictionary<bool, List<CommandLineOption>> allOptions = GetAllOptions();
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

        private Dictionary<bool, List<CommandLineOption>> GetAllOptions()
        {
            Dictionary<bool, List<CommandLineOption>> builtInToOptions = [];

            foreach (KeyValuePair<string, CommandLineOption> option in _commandLineOptionNameToModuleNames)
            {
                if (!builtInToOptions.TryGetValue(option.Value.IsBuiltIn.Value, out List<CommandLineOption> value))
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

        private Dictionary<bool, List<(string, string[])>> GetModulesToMissingOptions(Dictionary<bool, List<CommandLineOption>> options)
        {
            IEnumerable<string> builtInOptions = options.TryGetValue(true, out List<CommandLineOption> builtIn) ? builtIn.Select(option => option.Name) : [];
            IEnumerable<string> nonBuiltInOptions = options.TryGetValue(false, out List<CommandLineOption> nonBuiltIn) ? nonBuiltIn.Select(option => option.Name) : [];

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

        private void WriteOptionsToConsole(Dictionary<bool, List<CommandLineOption>> options)
        {
            int maxOptionNameLength = _commandLineOptionNameToModuleNames.Keys.ToArray().Max(option => option.Length);

            foreach (KeyValuePair<bool, List<CommandLineOption>> optionGroup in options)
            {
                Console.WriteLine();
                Console.WriteLine(optionGroup.Key ? "Options:" : "Extension options:");

                foreach (CommandLineOption option in optionGroup.Value)
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
