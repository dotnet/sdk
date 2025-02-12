// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Concurrent;
using System.CommandLine.Help;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand
    {
        private readonly ConcurrentDictionary<string, CommandLineOption> _commandLineOptionNameToModuleNames = [];
        private readonly ConcurrentDictionary<bool, List<(string, string[])>> _moduleNamesToCommandLineOptions = [];

        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
        {
            yield return (context) =>
            {
                Console.WriteLine(LocalizableStrings.HelpWaitingForOptionsAndExtensions);

                Run(context.ParseResult);

                if (_commandLineOptionNameToModuleNames.IsEmpty)
                {
                    return;
                }

                Dictionary<bool, List<CommandLineOption>> allOptions = GetAllOptions();

                Dictionary<bool, List<(string, string[])>> moduleToMissingOptions = GetModulesToMissingOptions(allOptions);

                _output.WriteHelpOptions(_commandLineOptionNameToModuleNames, allOptions, moduleToMissingOptions);
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
    }
}
