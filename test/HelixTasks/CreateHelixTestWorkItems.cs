// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems given test project publish information
    /// </summary>
    public class CreateHelixTestWorkItems : Build.Utilities.Task
    {
        /// <summary>
        /// An array of test project workitems containing the following metadata:
        /// - [Required] PublishDirectory: the publish output directory of the test project
        /// - [Required] TargetPath: the output dll path
        /// - [Required] RuntimeTargetFramework: the target framework to run tests on
        /// - [Optional] Arguments: a string of arguments to be passed to the test runner
        /// - [Optional] MethodLimitMultiplier: a positive integer multiplier applied to BaseMethodLimit
        ///   used for partitioning tests into Helix shards
        /// The two required parameters will be automatically created if TestProject.Identity is set to the path of the test csproj file
        /// </summary>
        [Required]
        public ITaskItem[]? TestProjects { get; set; }

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
        /// Optional timeout for all created workitems
        /// Defaults to 300s
        /// </summary>
        public string? TestWorkItemTimeout { get; set; }

        public string? TestArguments { get; set; }

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[]? TestWorkItems { get; set; }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItem creation per
        /// provided test project data.
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation per provided test project data.</returns>
        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// The asynchronous execution method for this MSBuild task which verifies the integrity of required properties
        /// and validates their formatting, specifically determining whether the provided test project data have a
        /// one-to-one mapping. It then creates this mapping before asynchronously preparing the HelixWorkItem TaskItem
        /// objects via the PrepareWorkItem method.
        /// </summary>
        /// <returns></returns>
        private async Task ExecuteAsync()
        {
            if(TestProjects is null)
            {
                return;
            }

            TestWorkItems = (await Task.WhenAll(TestProjects.Select(PrepareWorkItem)))
                .SelectMany(i => i ?? new())
                .Where(wi => wi != null)
                .ToArray();
            return;
        }

        /// <summary>
        /// Prepares HelixWorkItem given test project information.
        /// </summary>
        /// <param name="publishPath">The non-relative path to the publish directory.</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<List<ITaskItem>?> PrepareWorkItem(ITaskItem testProject)
        {
            // Forces this task to run asynchronously
            await Task.Yield();

            if (!testProject.GetRequiredMetadata(Log, "PublishDirectory", out string publishDirectory))
            {
                return null;
            }
            if (!testProject.GetRequiredMetadata(Log, "TargetPath", out string targetPath))
            {
                return null;
            }
            if (!testProject.GetRequiredMetadata(Log, "RuntimeTargetFramework", out string runtimeTargetFramework))
            {
                return null;
            }

            testProject.TryGetMetadata("ExcludeAdditionalParameters", out string ExcludeAdditionalParameters);

            testProject.TryGetMetadata("Arguments", out string arguments);
            TimeSpan timeout = TimeSpan.FromMinutes(5);
            if (!string.IsNullOrEmpty(TestWorkItemTimeout))
            {
                if (!TimeSpan.TryParse(TestWorkItemTimeout, out timeout))
                {
                    Log.LogWarning($"Invalid value \"{TestWorkItemTimeout}\" provided for TestWorkItemTimeout; falling back to default value of \"00:05:00\" (5 minutes)");
                }
            }

            // Handle additional payload files that should be included in this project's work items.
            // Files are copied into the publish directory and a pre-command copies them to the
            // correlation payload destination at runtime.
            string additionalPayloadPreCommand = "";
            testProject.TryGetMetadata("AdditionalPayloadDir", out string additionalPayloadDir);
            testProject.TryGetMetadata("AdditionalPayloadDestination", out string additionalPayloadDestination);

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
            testProject.TryGetMetadata("IsMTPProject", out string isMTPProjectMetadata);
            bool isMTPProject = string.Equals(isMTPProjectMetadata, "true", StringComparison.OrdinalIgnoreCase);

            // True when the Microsoft.Testing.Extensions.TrxReport extension is loaded on the
            // project. MSTest.Sdk enables it by default. Passing '--report-trx' to an MTP host
            // without the extension fails with an 'unknown argument' error, so only emit it when
            // known safe.
            testProject.TryGetMetadata("EnableTrxReport", out string enableTrxReportMetadata);
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
            if (testProject.TryGetMetadata("MethodLimitMultiplier", out string multiplierStr))
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

                // Test executables run out-of-process (MTP hosts are launched directly; the legacy
                // VSTest path launches the AppHost executable). On POSIX, the execute bit is lost
                // when the Helix SDK packages the payload as a zip archive, so restore it before running.
                string exeName = Path.GetFileNameWithoutExtension(assemblyName);
                string chmodPrefix = IsPosixShell ? $"chmod +x {exeName} && " : "";
                // On macOS, ad-hoc sign the test exe with get-task-allow entitlement so createdump can attach via task_for_pid for crash dumps.
                string codesignPrefix = IsPosixShell && TargetRid.StartsWith("osx") ? $"codesign -s - -f --entitlements $HELIX_CORRELATION_PAYLOAD/t/helix-debug-entitlements.plist {exeName} && " : "";

                // blame-hang-timeout / hangdump-timeout is set to a % of the Helix work item timeout so
                // that a hang dump can be captured and the results written before Helix hard-kills the
                // process. Shared by both the MTP (--hangdump) and legacy VSTest (--blame-hang) branches.
                var blameHangTimeout = TimeSpan.FromMilliseconds(timeout.TotalMilliseconds * 0.8);

                string command;
                if (isMTPProject)
                {
                    // Microsoft.Testing.Platform (MTP) projects (MSTest.Sdk-based) ship as a
                    // self-contained executable with no testhost.dll, so 'dotnet test <dll>' fails.
                    // .NET (Core) targets are invoked via 'dotnet exec <dll>'; .NET Framework targets
                    // are native Windows executables with no runtimeconfig.json, so 'dotnet exec' fails
                    // (the host treats them as self-contained .NET Core apps and cannot find
                    // hostpolicy.dll) -- those must be launched as the '.exe' directly.
                    // Either way we use the MTP-native CLI:
                    //   --filter                replaces VSTest --filter (same MSTest filter syntax)
                    //   --results-directory     same as VSTest
                    //   --report-trx            replaces '--logger trx' -- only emitted when the
                    //                           Microsoft.Testing.Extensions.TrxReport extension is
                    //                           loaded (always true for MSTest.Sdk default profile)
                    //   --diagnostic            replaces VSTest '-d <log>'
                    //   --crashdump             captures a dump if the test host crashes
                    //   --hangdump              captures a dump if the run stops making progress
                    // Note: --logger "console;verbosity=detailed" has no direct MTP equivalent in the
                    // MSTest.Sdk default extension set; the Helix work-item timeout (TestWorkItemTimeout
                    // / HELIX_WORK_ITEM_TIMEOUT) still terminates runaway runs.
                    // Carry over the same execution-directory / MSBuild SDK resolver environment
                    // variables that the 'dotnet test' path sets via '-e'. They are required for
                    // macOS workitem-directory execution (DOTNET_SDK_TEST_EXECUTION_DIRECTORY) and
                    // Windows MSBuild resolver behavior (DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER).
                    // MTP runs the host directly (no 'dotnet test -e'), so they have to be supplied
                    // as shell environment-variable prefixes, honoring ExcludeAdditionalParameters
                    // (testExecutionDirectory / msbuildAdditionalSdkResolverFolder are emptied above
                    // when ExcludeAdditionalParameters is true).
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

                    // Sharding by class filter can produce work items whose tests are all skipped
                    // (or a project may legitimately run zero tests on a given platform, e.g.
                    // Windows-only Msi tests on Linux/macOS). MTP returns exit code 8 ("zero tests
                    // ran") in that case, which fails the Helix work item even though nothing is
                    // actually broken. Treat exit code 8 as success so these runs report green.
                    // See https://github.com/dotnet/sdk/issues/54963.
                    string ignoreZeroTestsArg = "--ignore-exit-code 8";

                    // Capture a crash dump on test-host crash and a hang dump when the run stops making
                    // progress. --hangdump-timeout is a % of the work item timeout (see blameHangTimeout
                    // above) so the dump is written before Helix hard-kills the work item. The dump files
                    // land in the results directory and are copied to HELIX_WORKITEM_UPLOAD_ROOT by the
                    // HelixPostCommands *.dmp glob in UnitTests.proj. Enabled repo-wide via
                    // EnableMicrosoftTestingExtensions{Crash,Hang}Dump in test/Directory.Build.props.
                    // --crashdump is recognized (and silently no-ops) on the .NET Framework MTP hosts.
                    string dumpArgs = $"--crashdump --hangdump --hangdump-timeout {blameHangTimeout.TotalMinutes:0}m";

                    // .NET Framework apphosts (TargetPath is the '.exe') run directly; .NET (Core)
                    // assemblies (TargetPath is the '.dll') run via 'dotnet exec'.
                    string mtpLauncher = runtimeTargetFrameworkParsed.Framework == ".NETFramework"
                        ? assemblyName
                        : $"{driver} exec {assemblyName}";

                    command = $"{additionalPayloadPreCommand}{chmodPrefix}{codesignPrefix}{envPrefix}{mtpLauncher} " +
                              $"--results-directory .{Path.DirectorySeparatorChar} {trxArg}{testFilter} {diagArg} {dumpArgs} {ignoreZeroTestsArg}";
                }
                else
                {
                    command = $"{additionalPayloadPreCommand}{chmodPrefix}{codesignPrefix}{driver} test {assemblyName} -e HELIX_WORK_ITEM_TIMEOUT={timeout} {testExecutionDirectory} {msbuildAdditionalSdkResolverFolder} " +
                              $"{(TestArguments != null ? " " + TestArguments : "")} --results-directory .{Path.DirectorySeparatorChar} --logger trx --logger \"console;verbosity=detailed\" --blame-hang --blame-hang-timeout {blameHangTimeout.TotalMinutes:0}m {testFilter} {enableDiagLogging} {arguments}";
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
