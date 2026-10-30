// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
#if NET
using Microsoft.Win32.SafeHandles;
#endif

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public class GenerateEFSQLScripts : Task
    {
        [Required]
        public string? ProjectDirectory { get; set; }
        [Required]
        public string? EFPublishDirectory { get; set; }
        [Required]
        public ITaskItem[]? EFMigrations { get; set; }
        [Required]
        public string? Configuration { get; set; }
        public string? EFSQLScriptsFolderName { get; set; }
        public string? EFMigrationsAdditionalArgs { get; set; }
        [Output]
        public ITaskItem[]? EFSQLScripts { get; set; }

        public override bool Execute()
        {
            bool isSuccess = true;
            Log.LogMessage(MessageImportance.High, Resources.EFSCRIPT_Generating);
            isSuccess = GenerateEFSQLScriptsInternal();
            if (isSuccess)
            {
                Log.LogMessage(MessageImportance.High, Resources.EFSCRIPT_GenerationCompleted);
            }

            return isSuccess;
        }

        public bool GenerateEFSQLScriptsInternal(bool isLoggingEnabled = true)
        {
            InitializeProperties();
            if (EFMigrations is null || EFPublishDirectory is null || EFSQLScriptsFolderName is null)
            {
                return false;
            }
            EFSQLScripts = new ITaskItem[EFMigrations.Length];
            int index = 0;
            foreach (ITaskItem dbContext in EFMigrations)
            {
                string outputFileFullPath = Path.Combine(EFPublishDirectory, EFSQLScriptsFolderName, dbContext.ItemSpec + ".sql");
                bool isScriptGenerationSuccessful = GenerateSQLScript(outputFileFullPath, dbContext.ItemSpec, isLoggingEnabled);
                if (!isScriptGenerationSuccessful)
                {
                    return false;
                }

                ITaskItem sqlScriptItem = new TaskItem(outputFileFullPath);
                sqlScriptItem.SetMetadata("DBContext", dbContext.ItemSpec);
                string connectionStringEscaped = ProjectCollection.Escape(dbContext.GetMetadata("Value"));
                sqlScriptItem.SetMetadata("ConnectionString", connectionStringEscaped);
                EFSQLScripts[index] = sqlScriptItem;

                index++;
            }

            return true;
        }

        private void InitializeProperties()
        {
            if (string.IsNullOrEmpty(EFSQLScriptsFolderName))
            {
                EFSQLScriptsFolderName = "EFSQLScripts";
            }
        }

        private const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
        private bool GenerateSQLScript(string sqlFileFullPath, string dbContextName, bool isLoggingEnabled = true)
        {
            ProcessStartInfo psi = new("dotnet", $@"ef migrations script --no-build --idempotent --configuration {Configuration} --output ""{sqlFileFullPath}"" --context {dbContextName} {EFMigrationsAdditionalArgs}")
            {
                WorkingDirectory = ProjectDirectory,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            psi.EnvironmentVariables[AspNetCoreEnvironment] = "Development";
            return ExecuteProcess(psi, TimeSpan.FromMinutes(5), isLoggingEnabled);
        }

        internal bool ExecuteProcess(ProcessStartInfo psi, TimeSpan timeout, bool isLoggingEnabled)
        {
            if (isLoggingEnabled)
            {
                Log.LogMessage(MessageImportance.High, string.Format("Executing command: {0} {1}", psi.FileName, psi.Arguments));
            }

            string? stdOut = null, stdErr = null;

#if NET
            psi.InheritedHandles ??= [];

            try
            {
                if (!isLoggingEnabled)
                {
                    using SafeFileHandle nullHandle = File.OpenNullHandle();
                    psi.StandardOutputHandle = nullHandle;
                    psi.StandardErrorHandle = nullHandle;

                    return Process.Run(psi, timeout).ExitCode == 0;
                }

                ProcessTextOutput output = Process.RunAndCaptureText(psi, timeout);
                if (output.ExitStatus.ExitCode == 0)
                {
                    return true;
                }

                stdOut = output.StandardOutput;
                stdErr = output.StandardError;
            }
            catch (Exception e)
            {
                if (isLoggingEnabled)
                {
                    Log.LogError(e.ToString());
                }
            }
#else
            using Process process = new() { StartInfo = psi };
            StringBuilder? outputBuilder = isLoggingEnabled ? new() : null;
            StringBuilder? errorBuilder = isLoggingEnabled ? new() : null;
            bool started = false;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    outputBuilder?.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    errorBuilder?.AppendLine(e.Data);
                }
            };

            try
            {
                process.Start();
                started = true;

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                bool exited = process.WaitForExit((int)timeout.TotalMilliseconds);
                if (exited && process.ExitCode == 0)
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                if (isLoggingEnabled)
                {
                    Log.LogError(e.ToString());
                }
            }
            finally
            {
                if (started)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }

                    process.WaitForExit(); // The finite wait doesn't join the output callbacks on .NET Framework.
                }
            }

            stdOut = outputBuilder?.ToString();
            stdErr = errorBuilder?.ToString();
#endif

            if (isLoggingEnabled)
            {
                Log.LogMessage(MessageImportance.High, stdOut);
                Log.LogMessage(MessageImportance.High, stdErr);
                Log.LogError(Resources.EFSCRIPT_GenerationFailed);
            }

            return false;
        }
    }
}
