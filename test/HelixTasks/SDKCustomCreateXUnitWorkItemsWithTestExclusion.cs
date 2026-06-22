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
        /// - [Optional] MethodLimitMultiplier: a positive integer multiplier applied to BaseMethodLimit
        ///   used for partitioning tests into Helix shards
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
        /// Base number of test methods per Helix work item shard.
        /// Per-project MethodLimitMultiplier scales this value.
        /// Defaults to 32.
        /// </summary>
        public int BaseMethodLimit { get; set; } = 32;

        /// <summary>
        /// When true, uses time-based scheduling with AzDO historical data
        /// instead of count-based partitioning.
        /// </summary>
        public bool UseTimeBasedScheduling { get; set; }

        /// <summary>
        /// AzDO project URI for test history queries (e.g. "https://dev.azure.com/dnceng/public").
        /// Required when UseTimeBasedScheduling is true.
        /// </summary>
        public string? AzdoProjectUri { get; set; }

        /// <summary>
        /// Access token for AzDO REST API. Typically $(System.AccessToken) in pipelines.
        /// Required when UseTimeBasedScheduling is true.
        /// </summary>
        public string? AzdoAccessToken { get; set; }

        /// <summary>
        /// AzDO pipeline definition ID to query for historical test data.
        /// Required when UseTimeBasedScheduling is true.
        /// </summary>
        public int AzdoDefinitionId { get; set; }

        /// <summary>
        /// Target branch for finding the last successful build (without refs/heads/ prefix).
        /// Defaults to "main".
        /// </summary>
        public string AzdoTargetBranch { get; set; } = "main";

        /// <summary>
        /// Optional phase/stage name to filter test runs in the historical build.
        /// </summary>
        public string? AzdoPhaseName { get; set; }

        /// <summary>
        /// Target time per work item in minutes for time-based scheduling.
        /// Defaults to 10.
        /// </summary>
        public int TargetMinutesPerWorkItem { get; set; } = 10;

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

            if (UseTimeBasedScheduling)
            {
                XUnitWorkItems = await PrepareWorkItemsWithTimeBasedScheduling();
            }
            else
            {
                XUnitWorkItems = (await Task.WhenAll(XUnitProjects.Select(PrepareWorkItem)))
                    .SelectMany(i => i ?? new())
                    .Where(wi => wi != null)
                    .ToArray();
            }
        }

        /// <summary>
        /// Prepares Helix work items using time-based scheduling with AzDO test history.
        /// Falls back to per-project count-based scheduling if history is unavailable.
        /// </summary>
        private async Task<ITaskItem[]> PrepareWorkItemsWithTimeBasedScheduling()
        {
            // Fetch test history from AzDO
            Dictionary<string, TestExecutionInfo>? history = null;
            if (!string.IsNullOrEmpty(AzdoProjectUri) && !string.IsNullOrEmpty(AzdoAccessToken) && AzdoDefinitionId > 0)
            {
                var historyManager = new TestHistoryManager(
                    AzdoProjectUri!,
                    AzdoAccessToken!,
                    AzdoDefinitionId,
                    AzdoTargetBranch,
                    AzdoPhaseName,
                    Log);

                history = await historyManager.GetTestHistoryAsync();
            }
            else
            {
                Log.LogMessage("Time-based scheduling requested but AzDO parameters are incomplete; falling back to count-based.");
            }

            // If we couldn't get history, fall back to per-project count-based scheduling
            if (history is null)
            {
                Log.LogMessage("No test history available; falling back to count-based partitioning.");
                var fallbackItems = await Task.WhenAll(XUnitProjects!.Select(PrepareWorkItem));
                return fallbackItems.SelectMany(i => i ?? new()).Where(wi => wi != null).ToArray();
            }

            // Discover test methods from all assemblies
            var allTestMethods = new List<TestMethodDiscovery.TestMethodInfo>();
            var projectMetadata = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in XUnitProjects!)
            {
                if (!project.GetRequiredMetadata(Log, "TargetPath", out string targetPath))
                    continue;

                projectMetadata[targetPath] = project;

                try
                {
                    var methods = TestMethodDiscovery.DiscoverTestMethods(targetPath);
                    allTestMethods.AddRange(methods);
                    Log.LogMessage("Discovered {0} test methods in {1}.", methods.Count, Path.GetFileName(targetPath));
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Failed to discover test methods in {0}: {1}", targetPath, ex.Message);
                }
            }

            if (allTestMethods.Count == 0)
            {
                Log.LogWarning("No test methods discovered; falling back to count-based partitioning.");
                var fallbackItems = await Task.WhenAll(XUnitProjects!.Select(PrepareWorkItem));
                return fallbackItems.SelectMany(i => i ?? new()).Where(wi => wi != null).ToArray();
            }

            // Schedule using time-based bin-packing
            var targetTime = TimeSpan.FromMinutes(TargetMinutesPerWorkItem);
            var scheduler = new TimeBasedScheduler(targetTime);
            var workItems = scheduler.Schedule(allTestMethods, history);

            Log.LogMessage("Time-based scheduler produced {0} work items for {1} test methods.", workItems.Count, allTestMethods.Count);

            // Convert scheduled work items to MSBuild task items
            var taskItems = new List<ITaskItem>();
            var timeout = targetTime * 3; // 3× target for timeout

            if (!string.IsNullOrEmpty(XUnitWorkItemTimeout) && TimeSpan.TryParse(XUnitWorkItemTimeout, out var parsedTimeout))
            {
                timeout = parsedTimeout;
            }

            foreach (var workItem in workItems)
            {
                // For now, use the first assembly's project metadata for command generation
                var primaryAssembly = workItem.GetAssemblyPaths().First();
                if (!projectMetadata.TryGetValue(primaryAssembly, out var xunitProject))
                    continue;

                if (!xunitProject.GetRequiredMetadata(Log, "PublishDirectory", out string publishDirectory))
                    continue;

                // Write the filter to a response file in the publish directory,
                // bypassing cmd.exe's 8191-char command line limit entirely.
                string? rspFileName = null;
                string filterString = workItem.GetFilterString();
                if (!string.IsNullOrEmpty(filterString))
                {
                    rspFileName = $"{workItem.DisplayName}.filter.rsp";
                    var rspPath = Path.Combine(publishDirectory, rspFileName);
                    File.WriteAllText(rspPath, $"--filter\n\"{filterString}\"");
                }

                var command = BuildTimeBasedCommand(xunitProject, workItem, timeout, rspFileName);
                if (command is null)
                    continue;

                taskItems.Add(new Microsoft.Build.Utilities.TaskItem(workItem.DisplayName, new Dictionary<string, string>()
                {
                    { "Identity", workItem.DisplayName },
                    { "PayloadDirectory", publishDirectory },
                    { "Command", command },
                    { "Timeout", timeout.ToString() },
                }));
            }

            return taskItems.ToArray();
        }

        /// <summary>
        /// Builds the execution command for a time-based work item.
        /// The filter is passed via a response file (@file.rsp) to avoid
        /// cmd.exe command line length limits on Windows.
        /// </summary>
        private string? BuildTimeBasedCommand(ITaskItem xunitProject, ScheduledWorkItem workItem, TimeSpan timeout, string? rspFileName)
        {
            if (!xunitProject.GetRequiredMetadata(Log, "PublishDirectory", out string publishDirectory))
                return null;
            if (!xunitProject.GetRequiredMetadata(Log, "TargetPath", out string targetPath))
                return null;

            xunitProject.TryGetMetadata("IsMTPProject", out string isMTPProjectMetadata);
            bool isMTPProject = string.Equals(isMTPProjectMetadata, "true", StringComparison.OrdinalIgnoreCase);

            xunitProject.TryGetMetadata("EnableTrxReport", out string enableTrxReportMetadata);
            bool enableTrxReport = string.Equals(enableTrxReportMetadata, "true", StringComparison.OrdinalIgnoreCase);

            xunitProject.TryGetMetadata("ExcludeAdditionalParameters", out string excludeAdditionalParameters);

            string assemblyName = Path.GetFileName(targetPath);

            // Reference the response file instead of inline filter
            string testFilter = rspFileName is not null ? $"@{rspFileName}" : "";

            string testExecutionDirectory = string.Empty;
            string msbuildAdditionalSdkResolverFolder = string.Empty;

            if (!string.Equals(excludeAdditionalParameters, "true", StringComparison.OrdinalIgnoreCase))
            {
                testExecutionDirectory = IsPosixShell
                    ? "-e DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory"
                    : "-e DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%";
                msbuildAdditionalSdkResolverFolder = IsPosixShell
                    ? ""
                    : "-e DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\\r";
            }

            string exeName = Path.GetFileNameWithoutExtension(assemblyName);
            string chmodPrefix = IsPosixShell ? $"chmod +x {exeName} && " : "";
            string codesignPrefix = IsPosixShell && TargetRid.StartsWith("osx")
                ? $"codesign -s - -f --entitlements $HELIX_CORRELATION_PAYLOAD/t/helix-debug-entitlements.plist {exeName} && "
                : "";

            if (isMTPProject)
            {
                string envPrefix;
                if (IsPosixShell)
                {
                    string testExecEnv = string.IsNullOrEmpty(testExecutionDirectory) ? "" : "DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory ";
                    envPrefix = $"HELIX_WORK_ITEM_TIMEOUT={timeout} {testExecEnv}";
                }
                else
                {
                    string testExecEnv = string.IsNullOrEmpty(testExecutionDirectory) ? "" : "set DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%&& ";
                    string msbuildEnv = string.IsNullOrEmpty(msbuildAdditionalSdkResolverFolder) ? "" : "set DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\\r&& ";
                    envPrefix = $"set HELIX_WORK_ITEM_TIMEOUT={timeout}&& {testExecEnv}{msbuildEnv}";
                }

                string diagArg = IsPosixShell
                    ? "--diagnostic --diagnostic-output-directory $HELIX_WORKITEM_UPLOAD_ROOT"
                    : "--diagnostic --diagnostic-output-directory %HELIX_WORKITEM_UPLOAD_ROOT%";

                string trxArg = enableTrxReport ? "--report-trx " : "";

                return $"{chmodPrefix}{codesignPrefix}{envPrefix}{PathToDotnet} exec {assemblyName} " +
                       $"--results-directory .{Path.DirectorySeparatorChar} {trxArg}{testFilter} {diagArg}";
            }
            else
            {
                var blameHangTimeout = TimeSpan.FromMilliseconds(timeout.TotalMilliseconds * 0.8);
                string enableDiagLogging = IsPosixShell
                    ? "-d $HELIX_WORKITEM_UPLOAD_ROOT//dotnetTestLog.log"
                    : "-d %HELIX_WORKITEM_UPLOAD_ROOT%\\dotnetTestLog.log";

                return $"{chmodPrefix}{codesignPrefix}{PathToDotnet} test {assemblyName} -e HELIX_WORK_ITEM_TIMEOUT={timeout} {testExecutionDirectory} {msbuildAdditionalSdkResolverFolder} " +
                       $"--results-directory .{Path.DirectorySeparatorChar} --logger trx --logger \"console;verbosity=detailed\" --blame-hang --blame-hang-timeout {blameHangTimeout.TotalMinutes:0}m {testFilter} {enableDiagLogging}";
            }
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

            // Handle additional payload files that should be included in this project's work items.
            // Files are copied into the publish directory and a pre-command copies them to the
            // correlation payload destination at runtime.
            string additionalPayloadPreCommand = "";
            xunitProject.TryGetMetadata("AdditionalPayloadDir", out string additionalPayloadDir);
            xunitProject.TryGetMetadata("AdditionalPayloadDestination", out string additionalPayloadDestination);

            if (!string.IsNullOrEmpty(additionalPayloadDir) && Directory.Exists(additionalPayloadDir))
            {
                string payloadSubdir = Path.Combine(publishDirectory, "_additionalPayload");
                Directory.CreateDirectory(payloadSubdir);

                foreach (string sourceFile in Directory.GetFiles(additionalPayloadDir, "*", SearchOption.AllDirectories))
                {
                    string relativePath = sourceFile.Substring(additionalPayloadDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1);
                    string destFile = Path.Combine(payloadSubdir, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    File.Copy(sourceFile, destFile, overwrite: true);
                }

                if (!string.IsNullOrEmpty(additionalPayloadDestination))
                {
                    if (IsPosixShell)
                    {
                        additionalPayloadPreCommand = $"cp -r $HELIX_WORKITEM_PAYLOAD/_additionalPayload/* $HELIX_CORRELATION_PAYLOAD/{additionalPayloadDestination}/ && ";
                    }
                    else
                    {
                        string destPath = $"%HELIX_CORRELATION_PAYLOAD%\\{additionalPayloadDestination.Replace('/', '\\')}";
                        additionalPayloadPreCommand = $"robocopy %HELIX_WORKITEM_PAYLOAD%\\_additionalPayload {destPath} /E /NP /NJH /NJS /NDL >nul & ";
                    }
                }
            }

            string assemblyName = Path.GetFileName(targetPath);

            string driver = $"{PathToDotnet}";

            // True when the test project is a Microsoft.Testing.Platform (MTP) project that
            // must be invoked via 'dotnet exec <dll>' with MTP-native CLI rather than the
            // VSTest-style 'dotnet test <dll>' command.
            xunitProject.TryGetMetadata("IsMTPProject", out string isMTPProjectMetadata);
            bool isMTPProject = string.Equals(isMTPProjectMetadata, "true", StringComparison.OrdinalIgnoreCase);

            // True when the Microsoft.Testing.Extensions.TrxReport extension is loaded on the
            // project. MSTest.Sdk enables it by default; xUnit v3 MTP projects do not bundle
            // it unless added explicitly. Passing '--report-trx' to an MTP host without the
            // extension fails with an 'unknown argument' error, so only emit it when known safe.
            xunitProject.TryGetMetadata("EnableTrxReport", out string enableTrxReportMetadata);
            bool enableTrxReport = string.Equals(enableTrxReportMetadata, "true", StringComparison.OrdinalIgnoreCase);

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
            string testExecutionDirectory = IsPosixShell ? "-e DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory" : "-e DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%";

            string msbuildAdditionalSdkResolverFolder = IsPosixShell ? "" : "-e DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\\r";

            if (ExcludeAdditionalParameters.Equals("true"))
            {
                testExecutionDirectory = "";
                msbuildAdditionalSdkResolverFolder = "";
            }

            var methodLimit = BaseMethodLimit;
            if (xunitProject.TryGetMetadata("MethodLimitMultiplier", out string multiplierStr))
            {
                if (int.TryParse(multiplierStr, out int multiplier) && multiplier > 0)
                {
                    methodLimit *= multiplier;
                }
                else
                {
                    Log.LogWarning($"Invalid MethodLimitMultiplier \"{multiplierStr}\" for {assemblyName}; must be a positive integer. Using default method limit.");
                }
            }

            var scheduler = new AssemblyScheduler(methodLimit: methodLimit);
            var assemblyPartitionInfos = scheduler.Schedule(targetPath);

            var partitionedWorkItem = new List<ITaskItem>();
            foreach (var assemblyPartitionInfo in assemblyPartitionInfos)
            {
                string enableDiagLogging = IsPosixShell ? "-d $HELIX_WORKITEM_UPLOAD_ROOT//dotnetTestLog.log" : "-d %HELIX_WORKITEM_UPLOAD_ROOT%\\dotnetTestLog.log";
                arguments = string.IsNullOrEmpty(arguments) ? "" : "-- " + arguments;

                var testFilter = string.IsNullOrEmpty(assemblyPartitionInfo.ClassListArgumentString) ? "" : $"--filter \"{assemblyPartitionInfo.ClassListArgumentString}\"";

                // xUnit v3 tests run out-of-process: the VSTest adapter launches the AppHost executable.
                // On POSIX, the execute bit is lost when the Helix SDK packages the payload as a zip archive,
                // so we need to restore it before running.
                string exeName = Path.GetFileNameWithoutExtension(assemblyName);
                string chmodPrefix = IsPosixShell ? $"chmod +x {exeName} && " : "";
                // On macOS, ad-hoc sign the test exe with get-task-allow entitlement so createdump can attach via task_for_pid for crash dumps.
                string codesignPrefix = IsPosixShell && TargetRid.StartsWith("osx") ? $"codesign -s - -f --entitlements $HELIX_CORRELATION_PAYLOAD/t/helix-debug-entitlements.plist {exeName} && " : "";

                string command;
                if (isMTPProject)
                {
                    // Microsoft.Testing.Platform (MTP) projects (MSTest.Sdk-based) ship as a
                    // self-contained executable with no testhost.dll, so 'dotnet test <dll>' fails.
                    // Invoke the test assembly directly via 'dotnet exec' and use MTP-native CLI:
                    //   --filter                replaces VSTest --filter (same MSTest filter syntax)
                    //   --results-directory     same as VSTest
                    //   --report-trx            replaces '--logger trx' -- only emitted when the
                    //                           Microsoft.Testing.Extensions.TrxReport extension is
                    //                           loaded (always true for MSTest.Sdk default profile;
                    //                           not bundled with xUnit v3 MTP)
                    //   --diagnostic            replaces VSTest '-d <log>'
                    // Note: --logger "console;verbosity=detailed" and --blame-hang* have no direct MTP
                    // equivalent in the MSTest.Sdk default extension set; the Helix work-item timeout
                    // (XUnitWorkItemTimeout / HELIX_WORK_ITEM_TIMEOUT) still terminates runaway runs.
                    string envPrefix;
                    if (IsPosixShell)
                    {
                        string testExecutionDirectoryEnv = string.IsNullOrEmpty(testExecutionDirectory) ? "" : "DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory ";
                        envPrefix = $"HELIX_WORK_ITEM_TIMEOUT={timeout} {testExecutionDirectoryEnv}";
                    }
                    else
                    {
                        string testExecutionDirectoryEnv = string.IsNullOrEmpty(testExecutionDirectory) ? "" : "set DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%&& ";
                        string msbuildAdditionalSdkResolverFolderEnv = string.IsNullOrEmpty(msbuildAdditionalSdkResolverFolder) ? "" : "set DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\\r&& ";
                        envPrefix = $"set HELIX_WORK_ITEM_TIMEOUT={timeout}&& {testExecutionDirectoryEnv}{msbuildAdditionalSdkResolverFolderEnv}";
                    }

                    string diagArg = IsPosixShell
                        ? "--diagnostic --diagnostic-output-directory $HELIX_WORKITEM_UPLOAD_ROOT"
                        : "--diagnostic --diagnostic-output-directory %HELIX_WORKITEM_UPLOAD_ROOT%";

                    string trxArg = enableTrxReport ? "--report-trx " : "";

                    command = $"{additionalPayloadPreCommand}{chmodPrefix}{codesignPrefix}{envPrefix}{driver} exec {assemblyName} " +
                              $"--results-directory .{Path.DirectorySeparatorChar} {trxArg}{testFilter} {diagArg}";
                }
                else
                {
                    // blame-hang-timeout is set to a % of the Helix work item timeout so that blame can
                    // collect hang dumps and write the TRX file before Helix hard-kills the process.
                    var blameHangTimeout = TimeSpan.FromMilliseconds(timeout.TotalMilliseconds * 0.8);
                    command = $"{additionalPayloadPreCommand}{chmodPrefix}{codesignPrefix}{driver} test {assemblyName} -e HELIX_WORK_ITEM_TIMEOUT={timeout} {testExecutionDirectory} {msbuildAdditionalSdkResolverFolder} " +
                              $"{(XUnitArguments != null ? " " + XUnitArguments : "")} --results-directory .{Path.DirectorySeparatorChar} --logger trx --logger \"console;verbosity=detailed\" --blame-hang --blame-hang-timeout {blameHangTimeout.TotalMinutes:0}m {testFilter} {enableDiagLogging} {arguments}";
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
