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

            string args = _args.Where(x => x != "--no-build").Count() > 0
                ? _args.Where(x => x != "--no-build").Aggregate((a, b) => $"{a} {b}")
                : string.Empty;

            args += $" --{ServerOptionKey} {ServerOptionValue} --{DotNetTestPipeOptionKey} {_pipeName}";

            ProcessStartInfo processStartInfo = new();
            if (_moduleName.EndsWith(".dll"))
            {
                processStartInfo.FileName = Environment.ProcessPath;
                processStartInfo.Arguments = $"exec {_moduleName} {args}";

                VSTestTrace.SafeWriteTrace(() => $"Updated args: {processStartInfo.Arguments}");
            }
            else
            {
                processStartInfo.FileName = _moduleName;
                processStartInfo.Arguments = args;

                VSTestTrace.SafeWriteTrace(() => $"Updated args: {args}");
            }

            await Process.Start(processStartInfo).WaitForExitAsync();
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
