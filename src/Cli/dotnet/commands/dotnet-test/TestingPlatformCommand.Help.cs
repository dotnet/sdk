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

                var allOptions = GetAllOptions();
                WriteOptionsToConsole(allOptions);

                Console.ForegroundColor = ConsoleColor.Yellow;

                Dictionary<bool, Dictionary<string, string[]>> missingOptionToModules = GetMissingOptionsToModules(allOptions);
                WriteMissingOptionsToConsole(missingOptionToModules);

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
            };
        }

        private void OnHelpRequested(object sender, HelpEventArgs args)
        {
            CommandLineOptionMessages commandLineOptionMessages = args.CommandLineOptionMessages;
            string moduleName = commandLineOptionMessages.ModuleName;

            foreach (CommandLineOptionMessage commandLineOptionMessage in commandLineOptionMessages.CommandLineOptionMessageList)
            {
                if (commandLineOptionMessage.IsHidden) continue;

                _commandLineOptionNameToModuleNames.AddOrUpdate(
                    commandLineOptionMessage.Name,
                    (key) => (commandLineOptionMessage, new[] { moduleName }), (optionName, value) => (value.Item1, value.Item2.Concat([moduleName]).ToArray()));
            }
        }

        private Dictionary<bool, List<(CommandLineOptionMessage, string[])>> GetAllOptions()
        {
            Dictionary<bool, List<(CommandLineOptionMessage, string[])>> builtInToOptions = [];

            foreach (KeyValuePair<string, (CommandLineOptionMessage, string[])> option in _commandLineOptionNameToModuleNames)
            {
                if (!builtInToOptions.TryGetValue(option.Value.Item1.IsBuiltIn, out List<(CommandLineOptionMessage, string[])> value))
                {
                    builtInToOptions.Add(option.Value.Item1.IsBuiltIn, [(option.Value.Item1, option.Value.Item2)]);
                }
                else
                {
                    value.Add((option.Value.Item1, option.Value.Item2));
                }
            }
            return builtInToOptions;
        }

        private Dictionary<bool, Dictionary<string, string[]>> GetMissingOptionsToModules(Dictionary<bool, List<(CommandLineOptionMessage, string[])>> options)
        {
            Dictionary<bool, Dictionary<string, string[]>> missingOptionToModules = [];
            var allModuleNames = _testApplications.Keys;

            foreach (KeyValuePair<bool, List<(CommandLineOptionMessage, string[])>> option in options)
            {
                foreach ((CommandLineOptionMessage detail, string[] modules) in option.Value)
                {
                    string optionName = detail.Name;

                    if (modules.Length != allModuleNames.Count)
                    {
                        if (missingOptionToModules.TryGetValue(option.Key, out Dictionary<string, string[]> value))
                        {
                            value.Add(optionName, allModuleNames.Except(modules).ToArray());
                        }
                        else
                        {
                            missingOptionToModules.Add(option.Key, new Dictionary<string, string[]>
                            {
                                { optionName, allModuleNames.Except(modules).ToArray() }
                            });
                        }
                    }
                }
            }
            return missingOptionToModules;
        }

        private void WriteOptionsToConsole(Dictionary<bool, List<(CommandLineOptionMessage, string[])>> allOptions)
        {
            int maxOptionNameLength = _commandLineOptionNameToModuleNames.Keys.ToArray().Max(option => option.Length);

            foreach (KeyValuePair<bool, List<(CommandLineOptionMessage, string[])>> option in allOptions)
            {
                Console.WriteLine();
                Console.WriteLine(option.Key ? "Options:" : "Extension options:");

                foreach ((CommandLineOptionMessage optionDetail, string[] modules) in option.Value)
                {
                    Console.WriteLine($"{new string(' ', 2)}--{optionDetail.Name}{new string(' ', maxOptionNameLength - optionDetail.Name.Length)} {optionDetail.Description}");
                }
            }
        }

        private static void WriteMissingOptionsToConsole(Dictionary<bool, Dictionary<string, string[]>> missingOptionToModules)
        {
            foreach (KeyValuePair<bool, Dictionary<string, string[]>> option in missingOptionToModules)
            {
                Console.WriteLine();
                Console.WriteLine(option.Key ? "Unavailable options:" : "Unavailable extension options:");

                foreach (KeyValuePair<string, string[]> module in option.Value)
                {
                    StringBuilder line = new();

                    for (int i = 0; i < module.Value.Length; i++)
                    {
                        if (i == module.Value.Length - 1)
                            line.Append($"{module.Value[i]}");
                        else
                            line.Append($"{module.Value[i]},");
                    }

                    string verb = module.Value.Length == 1 ? "is" : "are";
                    Console.WriteLine($"{line} {verb} missing the option --{module.Key}");
                }
            }
        }
    }
}
