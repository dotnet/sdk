// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

internal class DotNetHelper
{
    private static readonly object s_lockObj = new();

    public static string DotNetPath { get; } = Path.Combine(Config.DotNetDirectory, "dotnet");
    public static string PackagesDirectory { get; } = Path.Combine(Directory.GetCurrentDirectory(), "packages");
    public static string ProjectsDirectory { get; } = Path.Combine(Directory.GetCurrentDirectory(), $"projects-{DateTime.Now:yyyyMMddHHmmssffff}");

    private ITestOutputHelper OutputHelper { get; }
    public bool IsMonoRuntime { get; }

    public DotNetHelper(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;

        lock (s_lockObj)
        {
            IsMonoRuntime = DetermineIsMonoRuntime(Config.DotNetDirectory);

            if (!Directory.Exists(ProjectsDirectory))
            {
                Directory.CreateDirectory(ProjectsDirectory);
                InitNugetConfig();
            }

            if (!Directory.Exists(PackagesDirectory))
            {
                Directory.CreateDirectory(PackagesDirectory);
            }
        }
    }

    private static void InitNugetConfig()
    {
        bool useCustomPackages = !string.IsNullOrEmpty(Config.CustomPackagesPath);
        string nugetConfigPrefix = useCustomPackages ? "custom" : "default";
        string nugetConfigPath = Path.Combine(ProjectsDirectory, "NuGet.Config");
        File.Copy(
            Path.Combine(BaselineHelper.GetAssetsDirectory(), $"{nugetConfigPrefix}.NuGet.Config"),
            nugetConfigPath);

        if (useCustomPackages)
        {
            // This package feed is optional.  You can use an alternative feed of dependency packages which can be 
            // required in sandboxed scenarios where public feeds need to be avoided.
            if (!Directory.Exists(Config.CustomPackagesPath))
            {
                throw new ArgumentException($"Specified CustomPackagesPath '{Config.CustomPackagesPath}' does not exist.");
            }

            string nugetConfig = File.ReadAllText(nugetConfigPath)
                .Replace("CUSTOM_PACKAGE_FEED", Config.CustomPackagesPath);
            File.WriteAllText(nugetConfigPath, nugetConfig);
        }
    }

    public void ExecuteCmd(string args, string? workingDirectory = null, Action<Process>? processConfigCallback = null,
        int? expectedExitCode = 0, int millisecondTimeout = -1)
    {
        (Process Process, string StdOut, string StdErr) executeResult = ExecuteHelper.ExecuteProcess(
            DotNetPath,
            args,
            OutputHelper,
            configureCallback: (process) => configureProcess(process, workingDirectory),
            millisecondTimeout: millisecondTimeout);
        
        if (expectedExitCode != null) {
            ExecuteHelper.ValidateExitCode(executeResult, (int) expectedExitCode);
        }

        void configureProcess(Process process, string? workingDirectory)
        {
            ConfigureProcess(process, workingDirectory);

            processConfigCallback?.Invoke(process);
        }
    }

    public static void ConfigureProcess(Process process, string? workingDirectory)
    {
        if (workingDirectory != null)
        {
            process.StartInfo.WorkingDirectory = workingDirectory;
        }

        process.StartInfo.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        process.StartInfo.EnvironmentVariables["DOTNET_ROOT"] = Config.DotNetDirectory;
        process.StartInfo.EnvironmentVariables["NUGET_PACKAGES"] = PackagesDirectory;
        process.StartInfo.EnvironmentVariables["PATH"] = $"{Config.DotNetDirectory}:{Environment.GetEnvironmentVariable("PATH")}";
        // Don't use the repo infrastructure
        process.StartInfo.EnvironmentVariables["ImportDirectoryBuildProps"] = "false";
        process.StartInfo.EnvironmentVariables["ImportDirectoryBuildTargets"] = "false";
        process.StartInfo.EnvironmentVariables["ImportDirectoryPackagesProps"] = "false";
    }

    public void ExecuteBuild(string projectName) =>
        ExecuteCmd($"build {GetBinLogOption(projectName, "build")}", GetProjectDirectory(projectName));

    /// <summary>
    /// Create a new .NET project and return the path to the created project folder.
    /// </summary>
    public string ExecuteNew(string projectType, string name, string? language = null, string? customArgs = null)
    {
        string projectDirectory = GetProjectDirectory(name);
        string options = $"--name {name} --output {projectDirectory}";
        if (language != null)
        {
            options += $" --language \"{language}\"";
        }
        if (string.IsNullOrEmpty(customArgs))
        {
            options += $" {customArgs}";
        }

        ExecuteCmd($"new {projectType} {options}");

        return projectDirectory;
    }

    public void ExecutePublish(string projectName, DotNetTemplate template, bool? selfContained = null, string? rid = null, bool trimmed = false, bool readyToRun = false)
    {
        string options = string.Empty;
        string binlogDifferentiator = string.Empty;

        if (selfContained.HasValue)
        {
            options += $"--self-contained {selfContained.Value.ToString().ToLowerInvariant()}";
            if (selfContained.Value)
            {
                binlogDifferentiator += "self-contained";
                if (!string.IsNullOrEmpty(rid))
                {
                    options += $" -r {rid}";
                    binlogDifferentiator += $"-{rid}";
                }
                if (trimmed)
                {
                    options += " /p:PublishTrimmed=true";
                    binlogDifferentiator += "-trimmed";
                }
                if (readyToRun)
                {
                    options += " /p:PublishReadyToRun=true";
                    binlogDifferentiator += "-R2R";
                }
            }
        }

        string projDir = GetProjectDirectory(projectName);
        string publishDir = Path.Combine(projDir, "bin", "publish");

        ExecuteCmd(
            $"publish {options} {GetBinLogOption(projectName, "publish", binlogDifferentiator)} -o {publishDir}",
            projDir);

        if (template == DotNetTemplate.Console)
        {
            ExecuteCmd($"{projectName}.dll", publishDir, expectedExitCode: 0);
        }
        else if (template == DotNetTemplate.ClassLib || template == DotNetTemplate.BlazorWasm)
        {
            // Can't run the published output of classlib (no entrypoint) or WASM (needs a server)
        }
        // Assume it is a web-based template
        else
        {
            ExecuteWebDll(projectName, publishDir, template);
        }
    }

    public void ExecuteRun(string projectName) =>
        ExecuteCmd($"run {GetBinLogOption(projectName, "run")}", GetProjectDirectory(projectName));

    public void ExecuteRunWeb(string projectName, DotNetTemplate template)
    {
        int expectedExitCode = 0;

        ExecuteWeb(
            projectName,
            $"run --no-launch-profile {GetBinLogOption(projectName, "run")}",
            GetProjectDirectory(projectName),
            template,
            expectedExitCode);
    }

    public void ExecuteWebDll(string projectName, string workingDirectory, DotNetTemplate template) =>
        ExecuteWeb(projectName, $"{projectName}.dll", workingDirectory, template, expectedExitCode: 0);

    public void ExecuteTest(string projectName) =>
        ExecuteCmd($"test {GetBinLogOption(projectName, "test")}", GetProjectDirectory(projectName));

    private void ExecuteWeb(string projectName, string args, string workingDirectory, DotNetTemplate template, int expectedExitCode)
    {
        WebAppValidator validator = new(OutputHelper, template);
        ExecuteCmd(
            args,
            workingDirectory,
            processConfigCallback: validator.Validate,
            expectedExitCode: expectedExitCode,
            millisecondTimeout: 30000);
        Assert.True(validator.IsValidated);
        if (validator.ValidationException is not null)
        {
            throw validator.ValidationException;
        }
    }    

    private static string GetBinLogOption(string projectName, string command, string? differentiator = null)
    {
        string fileName = $"{projectName}-{command}";
        if (!string.IsNullOrEmpty(differentiator))
        {
            fileName += $"-{differentiator}";
        }

        return $"/bl:{Path.Combine(Config.LogsDirectory, $"{fileName}.binlog")}";
    }

    private static bool DetermineIsMonoRuntime(string dotnetRoot)
    {
        string sharedFrameworkRoot = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(sharedFrameworkRoot))
        {
            return false;
        }

        string? version = Directory.GetDirectories(sharedFrameworkRoot).FirstOrDefault();
        if (version is null)
        {
            return false;
        }

        string sharedFramework = Path.Combine(sharedFrameworkRoot, version);

        // Check the presence of one of the mono header files.
        return File.Exists(Path.Combine(sharedFramework, "mono-gc.h"));
    }

    private static string GetProjectDirectory(string projectName) => Path.Combine(ProjectsDirectory, projectName);

    public static bool ShouldPublishComplex() =>
        !string.Equals(Config.TargetArchitecture,"ppc64le") && !string.Equals(Config.TargetArchitecture,"s390x");

    private class WebAppValidator
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DotNetTemplate _template;

        public WebAppValidator(ITestOutputHelper outputHelper, DotNetTemplate template)
        {
            _outputHelper = outputHelper;
            _template = template;
        }

        public bool IsValidated { get; set; }
        public Exception? ValidationException { get; set; }

        private static int GetAvailablePort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Validate(Process process)
        {
            int port = GetAvailablePort();
            process.StartInfo.EnvironmentVariables.Add("ASPNETCORE_HTTP_PORTS", port.ToString());
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                try
                {
                    if (e.Data?.Contains("Application started. Press Ctrl+C to shut down.") ?? false)
                    {
                        _outputHelper.WriteLine("Detected app has started. Sending web request to validate...");

                        using HttpClient httpClient = new();
                        string url = $"http://localhost:{port}";
                        if (_template == DotNetTemplate.WebApi)
                        {
                            url += "/WeatherForecast";
                        }

                        using HttpResponseMessage resultMsg = httpClient.GetAsync(new Uri(url)).Result;
                        _outputHelper.WriteLine($"Status code returned: {resultMsg.StatusCode}");
                        resultMsg.EnsureSuccessStatusCode();
                        IsValidated = true;

                        ExecuteHelper.ExecuteProcessValidateExitCode("kill", $"-s TERM {process.Id}", _outputHelper);
                    }
                }
                catch (Exception ex)
                {
                    ValidationException = ex;
                }
            });
        }
    }
}
