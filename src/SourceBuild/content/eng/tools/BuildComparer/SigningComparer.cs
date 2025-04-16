// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

public class SigningComparer : BuildComparer
{
    private SignCheckExecuter _signCheckExecuter;

    public SigningComparer(
        string vmrManifestPath,
        string vmrAssetBasePath,
        string baseBuildAssetBasePath,
        string issuesReportPath,
        string noIssuesReportPath,
        int parallelTasks,
        string baselineFilePath,
        string exclusionsFilePath,
        string sdkTaskScript)
        : base(
            vmrManifestPath,
            vmrAssetBasePath,
            baseBuildAssetBasePath,
            issuesReportPath,
            noIssuesReportPath,
            parallelTasks,
            baselineFilePath,
            issuesToReport: new List<IssueType>
            {
                IssueType.Unsigned
            })
    {
        _signCheckExecuter = new SignCheckExecuter(exclusionsFilePath, sdkTaskScript);
        InitializeSignCheck().Wait();
    }

    /// <summary>
    /// Initializes the sign tool for signing verification
    /// by restoring the toolsets and setting up the environment without running the tool.
    /// </summary>
    private async Task InitializeSignCheck()
    {
        string logsDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(logsDirectory);
        try
        {
            var initializeSignCheck = _signCheckExecuter.ExecuteAsync(packageBasePath: null, logsDirectory: logsDirectory);
            await ProcessSignCheckExitStatusAsync(initializeSignCheck);
        }
        finally
        {
            Directory.Delete(logsDirectory, true);
        }
    }

    protected override async Task EvaluateAsset(AssetMapping mapping)
    {
        // Filter away assets we don't care about
        if (mapping.IsIgnorableBlob || mapping.IsIgnorablePackage || Path.GetFileName(mapping.Id) == "productVersion.txt")
        {
            return;
        }

        // If either are not found, we skip the signing check
        if (!mapping.DiffFileFound || !File.Exists(mapping.BaseBuildFilePath))
        {
            return;
        }

        string msftBasePath = mapping.BaseBuildFilePath;
        string vmrBasePath = mapping.DiffFilePath;

        string msftLogsDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string vmrLogsDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(msftLogsDirectory);
        Directory.CreateDirectory(vmrLogsDirectory);

        try
        {
            await _throttle.WaitAsync();

            Console.WriteLine($"Evaluating '{mapping.Id}'");

            // Execute the msft and vmr signing checks in parallel
            var msftTask = ProcessSignCheckExitStatusAsync(_signCheckExecuter.ExecuteAsync(msftBasePath, msftLogsDirectory));
            var vmrTask = ProcessSignCheckExitStatusAsync(_signCheckExecuter.ExecuteAsync(vmrBasePath, vmrLogsDirectory));
            await Task.WhenAll(msftTask, vmrTask);

            // Process the results
            string msftResults = _signCheckExecuter.GetResults(msftLogsDirectory);
            string vmrResults = _signCheckExecuter.GetResults(vmrLogsDirectory);
            CompareSignCheckResults(
                DeserializeSignCheckResults(msftResults),
                DeserializeSignCheckResults(vmrResults),
                mapping);
        }
        catch (Exception ex)
        {
            mapping.EvaluationErrors.Add(ex.ToString());
            return;
        }
        finally
        {
            Directory.Delete(msftLogsDirectory, true);
            Directory.Delete(vmrLogsDirectory, true);

            _throttle.Release();
        }
    }

    private async Task ProcessSignCheckExitStatusAsync(Task<(int exitCode, string output, string error)> signCheckExecutionTask)
    {
        var result = await signCheckExecutionTask;
        if (result.exitCode != 0 && !string.IsNullOrWhiteSpace(result.error))
        {
            throw new Exception($"Sign check failed with exit code {result.exitCode}: {result.error} {result.output}");
        }
    }

    private SignCheckResults DeserializeSignCheckResults(string xmlContent)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(SignCheckResults));
        using (StringReader reader = new StringReader(xmlContent))
        {
            return (SignCheckResults)serializer.Deserialize(reader);
        }
    }

    private void CompareSignCheckResults(SignCheckResults msftResults, SignCheckResults vmrResults, AssetMapping mapping)
    {
        if (msftResults.Files.Count == 0)
        {
            throw new Exception($"No files found in MSFT sign check results.");
        }

        if (vmrResults.Files.Count == 0)
        {
            throw new Exception($"No files found in VMR sign check results.");
        }

        CompareSignCheckResults(msftResults.Files, vmrResults.Files, mapping);
    }

    private void CompareSignCheckResults(List<SignCheckResultFile> msftFiles, List<SignCheckResultFile> vmrFiles, AssetMapping mapping)
    {
        foreach (SignCheckResultFile msftFile in msftFiles)
        {
            if (Utils.IsIgnorablePackageFile(msftFile.NormalizedName))
            {
                continue;
            }

            SignCheckResultFile vmrFile = vmrFiles.FirstOrDefault(vmrFile => vmrFile.NormalizedName == msftFile.NormalizedName);
            if (vmrFile == null)
            {
                continue;
            }

            // Check the SignType and the nested files
            CompareSignCheckOutput(msftFile, vmrFile, mapping);
            CompareSignCheckResults(msftFile.NestedFiles, vmrFile.NestedFiles, mapping);
        }
    }

    private void CompareSignCheckOutput(SignCheckResultFile msftFile, SignCheckResultFile vmrFile, AssetMapping mapping)
    {
        if (msftFile.Outcome == SignCheckOutcome.Signed && vmrFile.Outcome != SignCheckOutcome.Signed)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.Unsigned,
                Description = $"{vmrFile.NormalizedName} is {vmrFile.Outcome} in the VMR but should be {msftFile.Outcome}.",
            });
        }
    }

    private class SignCheckExecuter
    {
        private string _command;
        private string _argumentsPrefix;
        private string _argumentsTemplate;
        private string _baseArguments;

        private readonly string _exclusionsFilePath;
        private const string _logFileName = "signcheck.log";
        private const string _errorFileName = "signcheck.error";
        private const string _xmlFileName = "signcheck.xml";
        private const int _timeout = 5 * 60 * 1000; // 5 minutes

        public SignCheckExecuter(string exclusionsFilePath, string sdkTaskScript)
        {
            _exclusionsFilePath = exclusionsFilePath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _command = "powershell.exe";
                _argumentsPrefix = "-";
                _argumentsTemplate = "{0}";
                _baseArguments =
                    $"{sdkTaskScript} " +
                    $"{_argumentsPrefix}task SigningValidation " +
                    $"{_argumentsPrefix}msbuildEngine vs " +
                    $"{_argumentsPrefix}excludeCIBinaryLog";
            }
            else
            {
                _command = "/bin/bash";
                _argumentsPrefix = "--";
                // Permissions need to be set to executable - https://github.com/dotnet/arcade/issues/15686
                _argumentsTemplate = $"-c \"" + $"chmod +x '{sdkTaskScript}' && " + "{0}\"";
                _baseArguments =
                    $"{sdkTaskScript} " +
                    $"{_argumentsPrefix}task SigningValidation " +
                    $"{_argumentsPrefix}excludeCIBinaryLog";
            }
        }

        public async Task<(int exitCode, string output, string error)> ExecuteAsync(string packageBasePath, string logsDirectory)
        {
            string arguments = _baseArguments;
            if (!string.IsNullOrEmpty(packageBasePath))
            {
                arguments +=
                    $" /p:PackageBasePath={packageBasePath}" +
                    $" /p:EnableStrongNameCheck=true" +
                    $" /p:SignCheckLog='{GetLogPath(logsDirectory, _logFileName)}' " +
                    $" /p:SignCheckErrorLog='{GetLogPath(logsDirectory, _errorFileName)}' " +
                    $" /p:SignCheckResultsXmlFile='{GetLogPath(logsDirectory, _xmlFileName)}' " +
                    $" /p:SignCheckExclusionsFile='{_exclusionsFilePath}'";
            }
            else
            {
                // If we're not checking anything, we should restore the toolset
                arguments += $" {_argumentsPrefix}restore";
            }
        
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // PowerShell has a line length limit, so we need to write the arguments to a script file
                string scriptFilePath = GetLogPath(logsDirectory, Path.GetRandomFileName() + ".ps1");
                File.WriteAllText(scriptFilePath, string.Format(_argumentsTemplate, arguments));
                arguments = $"-File \"{scriptFilePath}\"";
            }
            else
            {
                // On non-windows platforms, we can just pass the arguments directly
                arguments = string.Format(_argumentsTemplate, arguments);
            }

            using (var process = new Process())
            {
                process.StartInfo.FileName = _command;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                // Send the output and error streams to empty handlers because the text is also written to the log files
                process.OutputDataReceived += (sender, e) => { };
                process.ErrorDataReceived += (sender, e) => { };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for either the process to exit or the timeout to occur
                using (var cts = new CancellationTokenSource(_timeout))
                {
                    try
                    {
                        await process.WaitForExitAsync(cts.Token); // throws if timeout

                        // Read the output and error logs
                        string errorLog = GetLogPath(logsDirectory, _errorFileName);
                        string error = File.Exists(errorLog) ? File.ReadAllText(errorLog).Replace("Signing issues found", string.Empty).Trim() : string.Empty;

                        string outputLog = GetLogPath(logsDirectory, _logFileName);
                        string output = File.Exists(outputLog) ? File.ReadAllText(outputLog).Trim() : string.Empty;

                        return (process.ExitCode, output, error);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"Failed to run SignCheck for {packageBasePath} within the timeout of {_timeout / 1000} seconds. Killing process.");
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                        throw new TimeoutException($"Process timed out after {_timeout / 1000} seconds.");
                    }
                }
            }
        }

        public string GetResults(string logsDirectory)
        {
            string resultsFilePath = Path.Combine(logsDirectory, _xmlFileName);
            if (File.Exists(resultsFilePath))
            {
                return File.ReadAllText(resultsFilePath);
            }
            else
            {
                throw new FileNotFoundException($"Results file not found. This is likely due to a failure in the SignCheck process.");
            }
        }

        private string GetLogPath(string logsDirectory, string logFileName)
        {
            return Path.Combine(logsDirectory, logFileName);
        }
    }
}
