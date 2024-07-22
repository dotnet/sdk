﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal class TestApplication
    {
        private readonly string _modulePath;
        private readonly string _pipeName;
        private readonly string[] _args;

        public event EventHandler<HelpEventArgs> HelpRequested;
        public event EventHandler<ErrorEventArgs> ErrorReceived;

        public string ModulePath => _modulePath;

        public TestApplication(string modulePath, string pipeName, string[] args)
        {
            _modulePath = modulePath;
            _pipeName = pipeName;
            _args = args;
        }

        public async Task RunAsync()
        {
            if (!ModulePathExists())
            {
                return;
            }

            bool isDll = _modulePath.EndsWith(".dll");
            ProcessStartInfo processStartInfo = new()
            {
                FileName = isDll ?
                Environment.ProcessPath :
                _modulePath,
                Arguments = BuildArgs(isDll)
            };

            VSTestTrace.SafeWriteTrace(() => $"Updated args: {processStartInfo.Arguments}");

            await Process.Start(processStartInfo).WaitForExitAsync();
        }

        public async Task RunHelpAsync()
        {
            if (!ModulePathExists())
            {
                return;
            }

            bool isDll = _modulePath.EndsWith(".dll");
            ProcessStartInfo processStartInfo = new()
            {
                FileName = isDll ?
                Environment.ProcessPath :
                _modulePath,
                Arguments = BuildHelpArgs(isDll)
            };

            VSTestTrace.SafeWriteTrace(() => $"Updated args: {processStartInfo.Arguments}");

            await Process.Start(processStartInfo).WaitForExitAsync();
        }

        private bool ModulePathExists()
        {
            if (!File.Exists(_modulePath))
            {
                ErrorReceived.Invoke(this, new ErrorEventArgs { ErrorMessage = $"Test module '{_modulePath}' not found. Build the test application before or run 'dotnet test'." });
                return false;
            }
            return true;
        }

        private string BuildArgs(bool isDll)
        {
            StringBuilder builder = new();

            if (isDll)
            {
                builder.Append($"exec {_modulePath} ");
            }

            builder.Append(_args.Length != 0
                ? _args.Aggregate((a, b) => $"{a} {b}")
                : string.Empty);

            builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeName}");

            return builder.ToString();
        }

        private string BuildHelpArgs(bool isDll)
        {
            StringBuilder builder = new();

            if (isDll)
            {
                builder.Append($"exec {_modulePath} ");
            }

            builder.Append($" {CliConstants.HelpOptionKey} {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeName}");

            return builder.ToString();
        }

        public void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
        {
            HelpRequested?.Invoke(this, new HelpEventArgs { CommandLineOptionMessages = commandLineOptionMessages });
        }
    }
}
