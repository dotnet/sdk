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
            Process? process = null;

            try
            {
                if (isLoggingEnabled)
                {
                    process = Process.Start(psi)!;
                    (stdOut, stdErr) = process.ReadAllText(timeout);
                }
                else
                {
                    using SafeFileHandle nullHandle = File.OpenNullHandle();
                    psi.RedirectStandardOutput = false;
                    psi.RedirectStandardError = false;
                    psi.StandardOutputHandle = nullHandle;
                    psi.StandardErrorHandle = nullHandle;
                    process = Process.Start(psi)!;
                }

                if (process.WaitForExit(timeout) && process.ExitCode == 0)
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
                if (process is not null)
                {
                    if (!process.HasExited)
                    {
                        // The dotnet launcher runs the EF tool as a separate process.
                        process.Kill(entireProcessTree: true);
                        process.WaitForExitStatus();
                    }
                    process.Dispose();
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
                    lock (process)
                    {
                        outputBuilder?.AppendLine(e.Data);
                    }
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    lock (process)
                    {
                        errorBuilder?.AppendLine(e.Data);
                    }
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
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException) when (process.HasExited)
                        {
                        }
                        // A finite wait does not join output callbacks on Framework.
                        process.WaitForExit(int.MaxValue);
                    }
                }
            }

            // Output callbacks can still be running, because the waits above don't join them.
            lock (process)
            {
                stdOut = outputBuilder?.ToString();
                stdErr = errorBuilder?.ToString();
            }
#endif

            if (isLoggingEnabled)
            {
                // Output is unavailable when the process fails to start or times out.
                Log.LogMessage(MessageImportance.High, stdOut ?? string.Empty);
                Log.LogMessage(MessageImportance.High, stdErr ?? string.Empty);
                Log.LogError(Resources.EFSCRIPT_GenerationFailed);
            }

            return false;
        }
    }
}
