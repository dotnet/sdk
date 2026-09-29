// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class ProcessStartPostActionProcessor : PostActionProcessorBase
    {
        private readonly Func<Process, ICommand> _commandFactory;

        internal static readonly Guid ActionProcessorId = new("3A7C4B45-1F5D-4A30-959A-51B88E82B5D2");

        public ProcessStartPostActionProcessor()
            : this(process => new Command(process))
        {
        }

        internal ProcessStartPostActionProcessor(Func<Process, ICommand> commandFactory)
        {
            _commandFactory = commandFactory;
        }

        public override Guid Id => ActionProcessorId;

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath, CancellationToken cancellationToken)
        {
            if (!actionConfig.Args.TryGetValue("executable", out string? executable) || string.IsNullOrWhiteSpace(executable))
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_ProcessStartProcessor_Error_ConfigMissingExecutable);
                return false;
            }
            actionConfig.Args.TryGetValue("args", out string? args);

            bool redirectStandardOutput = true;
            // By default, standard out is redirected.
            // Only redirect when the configuration says "redirectStandardOutput = false"
            if (actionConfig.Args.TryGetValue("redirectStandardOutput", out string? redirectStandardOutputString)
                    && string.Equals(redirectStandardOutputString, "false", StringComparison.OrdinalIgnoreCase))
            {
                redirectStandardOutput = false;
            }
            bool redirectStandardError = true;
            // By default, standard error is redirected.
            // Only redirect when the configuration says "redirectStandardError = false"
            if (actionConfig.Args.TryGetValue("redirectStandardError", out string? redirectStandardErrorString)
                    && string.Equals(redirectStandardErrorString, "false", StringComparison.OrdinalIgnoreCase))
            {
                redirectStandardError = false;
            }

            try
            {
                string command = executable;
                if (!string.IsNullOrWhiteSpace(args))
                {
                    command = command + " " + args;
                }
                Reporter.Output.WriteLine(LocalizableStrings.RunningCommand, command);
                string resolvedExecutablePath = ResolveExecutableFilePath(environment.Host.FileSystem, executable, outputBasePath);

                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        WorkingDirectory = outputBasePath,
                        FileName = resolvedExecutablePath,
                        Arguments = args
                    }
                };
                ICommand commandToExecute = _commandFactory(process);
                if (redirectStandardOutput)
                {
                    commandToExecute.CaptureStdOut();
                }
                if (redirectStandardError)
                {
                    commandToExecute.CaptureStdErr();
                }
                CommandResult commandResult = commandToExecute.Execute(cancellationToken);

                if (commandResult.ExitCode != 0)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.CommandFailed);
                    Reporter.Error.WriteCommandOutput(commandResult);
                    Reporter.Error.WriteLine(string.Empty);
                    return false;
                }
                else
                {
                    Reporter.Output.WriteLine(LocalizableStrings.CommandSucceeded);
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(LocalizableStrings.CommandFailed);
                Reporter.Error.WriteLine(ex.Message);
                Reporter.Error.WriteLine(string.Empty);
                Reporter.Verbose.WriteLine(LocalizableStrings.Generic_Details, ex.ToString());
                return false;
            }
        }

        private static string ResolveExecutableFilePath(IPhysicalFileSystem fileSystem, string executableFileName, string outputBasePath)
        {
            if (!string.IsNullOrEmpty(outputBasePath) && fileSystem.DirectoryExists(outputBasePath))
            {
                string executableCombinedFileName = Path.Combine(Path.GetFullPath(outputBasePath), executableFileName);
                if (fileSystem.FileExists(executableCombinedFileName))
                {
                    return executableCombinedFileName;
                }
            }

            // The executable has not been found in the template folder, thus do not use the full path to the file.
            // The executable will be further searched in the directories from the PATH environment variable.
            return executableFileName;
        }
    }
}
