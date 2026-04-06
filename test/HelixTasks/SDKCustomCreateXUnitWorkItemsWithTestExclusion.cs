// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems given xUnit project publish information
    /// </summary>
    public class SDKCustomCreateXUnitWorkItemsWithTestExclusion : Build.Utilities.Task
    {
        /// <summary>
        /// An array of XUnit project workitems containing the following metadata:
        /// - [Required] PublishDirectory: the publish output directory of the XUnit project
        /// - [Required] TargetPath: the output dll path
        /// - [Required] RuntimeTargetFramework: the target framework to run tests on
        /// - [Optional] Arguments: a string of arguments to be passed to the XUnit console runner
        /// The two required parameters will be automatically created if XUnitProject.Identity is set to the path of the XUnit csproj file
        /// </summary>
        [Required]
        public ITaskItem[]? XUnitProjects { get; set; }

        /// <summary>
        /// The path to the dotnet executable on the Helix agent. Defaults to "dotnet"
        /// </summary>
        public string PathToDotnet { get; set; } = "dotnet";

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        /// <summary>
        /// The runtime identifier of the target Helix queue (e.g. osx-arm64, linux-x64).
        /// </summary>
        public string TargetRid { get; set; } = "";

        /// <summary>
        /// Optional timeout for all created workitems
        /// Defaults to 300s
        /// </summary>
        public string? XUnitWorkItemTimeout { get; set; }

        public string? XUnitArguments { get; set; }

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[]? XUnitWorkItems { get; set; }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItem creation per
        /// provided xUnit project data.
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation per provided xUnit project data.</returns>
        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// The asynchronous execution method for this MSBuild task which verifies the integrity of required properties
        /// and validates their formatting, specifically determining whether the provided xUnit project data have a
        /// one-to-one mapping. It then creates this mapping before asynchronously preparing the HelixWorkItem TaskItem
        /// objects via the PrepareWorkItem method.
        /// </summary>
        /// <returns></returns>
        private async Task ExecuteAsync()
        {
            if(XUnitProjects is null)
            {
                return;
            }

            XUnitWorkItems = (await Task.WhenAll(XUnitProjects.Select(PrepareWorkItem)))
                .SelectMany(i => i ?? new())
                .Where(wi => wi != null)
                .ToArray();
            return;
        }

        /// <summary>
        /// Prepares HelixWorkItem given xUnit project information.
        /// </summary>
        /// <param name="publishPath">The non-relative path to the publish directory.</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<List<ITaskItem>?> PrepareWorkItem(ITaskItem xunitProject)
        {
            // Forces this task to run asynchronously
            await Task.Yield();

            if (!xunitProject.GetRequiredMetadata(Log, "PublishDirectory", out string publishDirectory))
            {
                return null;
            }
            if (!xunitProject.GetRequiredMetadata(Log, "TargetPath", out string targetPath))
            {
                return null;
            }
            if (!xunitProject.GetRequiredMetadata(Log, "RuntimeTargetFramework", out string runtimeTargetFramework))
            {
                return null;
            }

            xunitProject.TryGetMetadata("ExcludeAdditionalParameters", out string ExcludeAdditionalParameters);

            xunitProject.TryGetMetadata("Arguments", out string arguments);
            TimeSpan timeout = TimeSpan.FromMinutes(5);
            if (!string.IsNullOrEmpty(XUnitWorkItemTimeout))
            {
                if (!TimeSpan.TryParse(XUnitWorkItemTimeout, out timeout))
                {
                    Log.LogWarning($"Invalid value \"{XUnitWorkItemTimeout}\" provided for XUnitWorkItemTimeout; falling back to default value of \"00:05:00\" (5 minutes)");
                }
            }

            string assemblyName = Path.GetFileName(targetPath);

            // netfx tests should only run on Windows full framework for testing VS scenarios
            // These tests have to be executed slightly differently and we give them a different Identity so ADO can tell them apart
            var runtimeTargetFrameworkParsed = NuGetFramework.Parse(runtimeTargetFramework);
            var testIdentityDifferentiator = "";
            if (runtimeTargetFrameworkParsed.Framework == ".NETFramework")
            {
                testIdentityDifferentiator = ".netfx";
            }
            else if (runtimeTargetFrameworkParsed.Framework != ".NETCoreApp")
            {
                throw new NotImplementedException("does not support non support the runtime specified");
            }

            // On mac due to https://github.com/dotnet/sdk/issues/3923, we run against workitem directory
            // but on Windows, if we running against working item diretory, we would hit long path.
            string testExecutionDirectory = IsPosixShell ? "DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory" : "DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%";

            string msbuildAdditionalSdkResolverFolder = IsPosixShell ? "" : "DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\\r";

            if (ExcludeAdditionalParameters.Equals("true"))
            {
                testExecutionDirectory = "";
                msbuildAdditionalSdkResolverFolder = "";
            }

            var scheduler = new AssemblyScheduler(methodLimit: !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TestFullMSBuild")) ? 32 : 16);
            var assemblyPartitionInfos = scheduler.Schedule(targetPath);

            var partitionedWorkItem = new List<ITaskItem>();
            bool isNetFramework = runtimeTargetFrameworkParsed.Framework == ".NETFramework";

            foreach (var assemblyPartitionInfo in assemblyPartitionInfos)
            {
                string command;

                if (isNetFramework)
                {
                    // .NET Framework tests run via dotnet test (vstest) since they are DLLs,
                    // not standalone MTP executables. MTP CrashDump/HangDump extensions are
                    // also not compatible with .NET Framework.
                    string enableDiagLogging = $"-d %HELIX_WORKITEM_UPLOAD_ROOT%\\dotnetTestLog.log";
                    var testFilter = string.IsNullOrEmpty(assemblyPartitionInfo.ClassListArgumentString) ? "" : $"--filter \"{assemblyPartitionInfo.ClassListArgumentString}\"";

                    command = $"{PathToDotnet} test {assemblyName} -e HELIX_WORK_ITEM_TIMEOUT={timeout}" +
                              $" -e DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%" +
                              $" -e DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\\r" +
                              $"{(XUnitArguments != null ? " " + XUnitArguments : "")}" +
                              $" --results-directory .{Path.DirectorySeparatorChar} --logger trx --logger \"console;verbosity=detailed\"" +
                              $" --blame-hang --blame-hang-timeout 60m" +
                              $" {testFilter} {enableDiagLogging} {arguments}";
                }
                else
                {
                    // .NET Core tests are standalone MTP executables (xUnit v3 + MTP).
                    // On POSIX, the execute bit is lost when the Helix SDK packages the payload as a zip archive.
                    string testExe = Path.GetFileNameWithoutExtension(assemblyName);
                    string chmodPrefix = IsPosixShell ? $"chmod +x {testExe} && " : "";
                    string testRunner = IsPosixShell ? $"./{testExe}" : $"{testExe}.exe";

                    // Build the test filter using MTP --filter-class syntax
                    string testFilter = "";
                    if (!string.IsNullOrEmpty(assemblyPartitionInfo.ClassListArgumentString))
                    {
                        var classNames = assemblyPartitionInfo.ClassListArgumentString.Split('|');
                        testFilter = string.Join(" ", classNames.Select(c => $"--filter-class \"{c}\""));
                    }

                    // Set environment variables as shell exports/sets before the test command
                    var envVars = new List<string> { $"HELIX_WORK_ITEM_TIMEOUT={timeout}" };
                    if (!string.IsNullOrEmpty(testExecutionDirectory))
                        envVars.Add(testExecutionDirectory);
                    if (!string.IsNullOrEmpty(msbuildAdditionalSdkResolverFolder))
                        envVars.Add(msbuildAdditionalSdkResolverFolder);

                    string envPrefix;
                    if (IsPosixShell)
                    {
                        envPrefix = string.Join(" ", envVars.Select(v => $"export {v} &&")) + " ";
                    }
                    else
                    {
                        envPrefix = string.Join(" & ", envVars.Select(v => $"set \"{v}\"")) + " & ";
                    }

                    command = $"{chmodPrefix}{envPrefix}{testRunner} " +
                              $"{(XUnitArguments != null ? " " + XUnitArguments : "")} --results-directory .{Path.DirectorySeparatorChar} --report-trx --output Detailed --timeout 60m --ignore-exit-code 8" +
                              $" {testFilter} {arguments}";
                }

                Log.LogMessage($"Creating work item with properties Identity: {assemblyName}, PayloadDirectory: {publishDirectory}, Command: {command}");

                partitionedWorkItem.Add(new Microsoft.Build.Utilities.TaskItem(assemblyPartitionInfo.DisplayName + testIdentityDifferentiator, new Dictionary<string, string>()
                {
                    { "Identity", assemblyPartitionInfo.DisplayName + testIdentityDifferentiator},
                    { "PayloadDirectory", publishDirectory },
                    { "Command", command },
                    { "Timeout", timeout.ToString() },
                }));
            }

            return partitionedWorkItem;
        }
    }
}
