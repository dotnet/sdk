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

                Dictionary<bool, Dictionary<string, string[]>> missingOptionToModules = [];

                int maxOptionNameLength = _commandLineOptionNameToModuleNames.Keys.ToArray().Max(option => option.Length);
                var moduleNames = _testApplications.Keys;

                IEnumerable<IGrouping<bool, KeyValuePair<string, (CommandLineOptionMessage optionMessage, string[] modules)>>> optionsByBuiltIn = _commandLineOptionNameToModuleNames.GroupBy(option => option.Value.Item1.IsBuiltIn);
                foreach (IGrouping<bool, KeyValuePair<string, (CommandLineOptionMessage optionMessage, string[] modules)>> option in optionsByBuiltIn)
                {
                    Console.WriteLine();
                    Console.WriteLine(option.Key ? "Options:" : "Extension options:");

                    foreach (KeyValuePair<string, (CommandLineOptionMessage optionMessage, string[] modules)> optionDetail in option)
                    {
                        string optionName = optionDetail.Key;
                        Console.WriteLine($"{new string(' ', 2)}--{optionName}{new string(' ', maxOptionNameLength - optionName.Length)} {optionDetail.Value.optionMessage.Description}");

                        string[] modules = optionDetail.Value.modules;
                        if (modules.Length != moduleNames.Count)
                        {
                            if (missingOptionToModules.TryGetValue(option.Key, out Dictionary<string, string[]> value))
                            {
                                value.Add(optionName, moduleNames.Except(modules).ToArray());
                            }
                            else
                            {
                                missingOptionToModules.Add(option.Key, new Dictionary<string, string[]>
                                {
                                    { optionName, moduleNames.Except(modules).ToArray() }
                                });
                            }
                        }
                    }
                }

                foreach (var option in missingOptionToModules)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine();
                    Console.WriteLine(option.Key ? "Unavailable options:" : "Unavailable extension options:");

                    foreach (var module in option.Value)
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

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
            };
        }
    }
}
