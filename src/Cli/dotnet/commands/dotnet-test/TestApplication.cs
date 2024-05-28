// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli.commands.dotnet_test
{
    internal class TestApplication
    {
        private readonly string _moduleName;
        private readonly string _pipeName;
        private readonly string[] _args;

        public event EventHandler<CommandLineOptionMessages> HelpOptionsEvent;

        private const string MSBuildExeName = "MSBuild.dll";

        private const string ServerOptionKey = "server";
        private const string ServerOptionValue = "dotnettestcli";

        private const string DotNetTestPipeOptionKey = "dotnet-test-pipe";

        public TestApplication(string moduleName, string pipeName, string[] args)
        {
            _moduleName = moduleName;
            _pipeName = pipeName;
            _args = args;
        }

        public async Task Run()
        {
            if (!File.Exists(_moduleName))
            {
                LockedConsoleWrite($"Test module '{_moduleName}' not found. Build the test application before or run 'dotnet test'.", ConsoleColor.Yellow);
                return;
            }

            bool isDll = _moduleName.EndsWith(".dll");
            ProcessStartInfo processStartInfo = new()
            {
                FileName = isDll ?
                Environment.ProcessPath :
                _moduleName,
                Arguments = BuildArgs(isDll)
            };

            VSTestTrace.SafeWriteTrace(() => $"Updated args: {processStartInfo.Arguments}");

            await Process.Start(processStartInfo).WaitForExitAsync();
        }

        private string BuildArgs(bool isDll)
        {
            StringBuilder builder = new();

            if (isDll)
                builder.Append($"exec {_moduleName}");

            builder.Append(_args.Any(x => x != "--no-build")
                ? _args.Where(x => x != "--no-build").Aggregate((a, b) => $"{a} {b}")
                : string.Empty);

            builder.Append($" --{ServerOptionKey} {ServerOptionValue} --{DotNetTestPipeOptionKey} {_pipeName}");

            return builder.ToString();
        }

        public void RunHelp(CommandLineOptionMessages commandLineOptionMessages)
        {
            HelpOptionsEvent?.Invoke(this, commandLineOptionMessages);
        }

        private void LockedConsoleWrite(string message, ConsoleColor consoleColor)
        {
            lock (MSBuildExeName)
            {
                ConsoleColor currentColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = consoleColor;
                    Console.WriteLine(message);
                }
                finally
                {
                    Console.ForegroundColor = currentColor;
                }
            }
        }
    }
}
