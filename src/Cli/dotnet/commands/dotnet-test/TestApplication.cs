// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal class TestApplication
    {
        private readonly string _moduleName;
        private readonly string _pipeName;
        private readonly string[] _args;

        public event EventHandler<HelpEventArgs> HelpRequested;
        public event EventHandler<ErrorEventArgs> ErrorReceived;

        public string ModuleName => _moduleName;

        public TestApplication(string moduleName, string pipeName, string[] args)
        {
            _moduleName = moduleName;
            _pipeName = pipeName;
            _args = args;
        }

        public async Task RunAsync()
        {
            if (!File.Exists(_moduleName))
            {
                ErrorReceived.Invoke(this, new ErrorEventArgs { ErrorMessage = $"Test module '{_moduleName}' not found. Build the test application before or run 'dotnet test'." });
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
                builder.Append($"exec {_moduleName} ");

            builder.Append(_args.Length != 0
                ? _args.Aggregate((a, b) => $"{a} {b}")
                : string.Empty);

            builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeName}");

            return builder.ToString();
        }

        public void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
        {
            HelpRequested?.Invoke(this, new HelpEventArgs { CommandLineOptionMessages = commandLineOptionMessages });
        }
    }
}
