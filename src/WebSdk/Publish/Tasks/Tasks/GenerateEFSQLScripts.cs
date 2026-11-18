// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

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

        private object _sync = new();
        private StringBuilder _standardOut = new();
        private StringBuilder _standardError = new();
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
            psi.Environment[AspNetCoreEnvironment] = "Development";
#if NET
            psi.InheritedHandles = [];
#endif

            using Process proc = new() { StartInfo = psi };
            proc.OutputDataReceived += Proc_OutputDataReceived;
            proc.ErrorDataReceived += Proc_ErrorDataReceived;
            bool started = false;

            try
            {
                if (isLoggingEnabled)
                {
                    Log.LogMessage(MessageImportance.High, string.Format("Executing command: {0} {1}", psi.FileName, psi.Arguments));
                }

                proc.Start();
                started = true;
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();
            }
            catch (Exception e)
            {
                if (isLoggingEnabled)
                {
                    Log.LogError(e.ToString());
                }
            }

            bool isProcessExited = false;
            if (started)
            {
                isProcessExited = proc.WaitForExit(300000);
                if (!isProcessExited)
                {
#if NET
                    // The dotnet launcher runs the EF tool as a separate process.
                    proc.Kill(entireProcessTree: true);
#else
                    try
                    {
                        proc.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                        // The process exited before it could be killed.
                    }
#endif
                }
            }

            // Descendants can keep the output pipes open after the process exits; stop collecting their output.
            proc.OutputDataReceived -= Proc_OutputDataReceived;
            proc.ErrorDataReceived -= Proc_ErrorDataReceived;

            if (!isProcessExited || proc.ExitCode != 0)
            {
                if (isLoggingEnabled)
                {
                    lock (_sync)
                    {
                        Log.LogMessage(MessageImportance.High, _standardOut.ToString());
                    }
                    Log.LogError(Resources.EFSCRIPT_GenerationFailed);
                }
                return false;
            }

            return true;
        }

        private void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                lock (_sync)
                {
                    _standardError.AppendLine(e.Data);
                }
            }
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                lock (_sync)
                {
                    _standardOut.AppendLine(e.Data);
                }
            }
        }
    }
}
