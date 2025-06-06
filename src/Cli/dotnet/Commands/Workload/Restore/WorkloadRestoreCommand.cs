// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.Update;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Restore;

internal class WorkloadRestoreCommand(
    ParseResult result,
    IReporter reporter = null) : WorkloadCommandBase(result, reporter: reporter)
{
    private readonly ParseResult _result = result;
    private readonly IEnumerable<string> _slnOrProjectArgument =
            result.GetValue(WorkloadRestoreCommandParser.SlnOrProjectArgument);

    public override int Execute()
    {
        var workloadResolverFactory = new WorkloadResolverFactory();
        var creationResult = workloadResolverFactory.Create();
        var workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(NullReporter.Instance, new SdkFeatureBand(creationResult.SdkVersion),
                                    creationResult.WorkloadResolver, Verbosity, creationResult.UserProfileDir, VerifySignatures, PackageDownloader,
                                    creationResult.DotnetPath, TempDirectoryPath, null, RestoreActionConfiguration, elevationRequired: true);
        var recorder = new WorkloadHistoryRecorder(
                           creationResult.WorkloadResolver,
                           workloadInstaller,
                           () => workloadResolverFactory.CreateForWorkloadSet(
                               creationResult.DotnetPath,
                               creationResult.SdkVersion.ToString(),
                               creationResult.UserProfileDir,
                               null));
        recorder.HistoryRecord.CommandName = "restore";

        recorder.Run(() =>
        {
            // First discover projects. This may return an error if no projects are found, and we shouldn't delay until after Update if that's the case.
            var allProjects = DiscoverAllProjects(Directory.GetCurrentDirectory(), _slnOrProjectArgument).Distinct();

            // Then update manifests and install a workload set as necessary
            new WorkloadUpdateCommand(_result, recorder: recorder, isRestoring: true).Execute();

            List<WorkloadId> allWorkloadId = RunTargetToGetWorkloadIds(allProjects);
            Reporter.WriteLine(string.Format(CliCommandStrings.InstallingWorkloads, string.Join(" ", allWorkloadId)));

            new WorkloadInstallCommand(_result,
                workloadIds: allWorkloadId.Select(a => a.ToString()).ToList().AsReadOnly(),
                skipWorkloadManifestUpdate: true)
            {
                IsRunningRestore = true
            }.Execute();
        });

        workloadInstaller.Shutdown();
        
        return 0;
    }

    private static readonly string GetRequiredWorkloadsTargetName = "_GetRequiredWorkloads";

    private List<WorkloadId> RunTargetToGetWorkloadIds(IEnumerable<string> allProjects)
    {
        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"SkipResolvePackageAssets", "true"}
        };

        var allWorkloadId = new List<WorkloadId>();
        foreach (string projectFile in allProjects)
        {
            var project = new ProjectInstance(projectFile, globalProperties, null);
            if (!project.Targets.ContainsKey(GetRequiredWorkloadsTargetName))
            {
                continue;
            }

            bool buildResult = project.Build([GetRequiredWorkloadsTargetName],
                loggers: [
                    new ConsoleLogger(Verbosity.ToLoggerVerbosity())
                ],
                remoteLoggers: [],
                targetOutputs: out var targetOutputs);

            if (buildResult == false)
            {
                throw new GracefulException(
                    string.Format(
                        CliCommandStrings.FailedToRunTarget,
                        projectFile),
                    isUserError: false);
            }

            var targetResult = targetOutputs[GetRequiredWorkloadsTargetName];
            allWorkloadId.AddRange(targetResult.Items.Select(item => new WorkloadId(item.ItemSpec)));
        }

        allWorkloadId = [.. allWorkloadId.Distinct()];
        return allWorkloadId;
    }


    internal static List<string> DiscoverAllProjects(string currentDirectory,
        IEnumerable<string> slnOrProjectArgument = null)
    {
        var slnFiles = new List<string>();
        var projectFiles = new List<string>();
        if (slnOrProjectArgument == null || !slnOrProjectArgument.Any())
        {
            slnFiles = [.. SlnFileFactory.ListSolutionFilesInDirectory(currentDirectory, false)];
            projectFiles.AddRange(Directory.GetFiles(currentDirectory, "*.*proj"));
        }
        else
        {
            slnFiles = [.. slnOrProjectArgument
                .Where(s => Path.GetExtension(s).Equals(".sln", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(s).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)];
            projectFiles = [.. slnOrProjectArgument
                .Where(s => Path.GetExtension(s).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)];
        }

        foreach (string solutionFilePath in slnFiles)
        {
            var solutionFile = SlnFileFactory.CreateFromFileOrDirectory(solutionFilePath);
            projectFiles.AddRange(solutionFile.SolutionProjects.Select(
                p => Path.GetFullPath(p.FilePath, Path.GetDirectoryName(solutionFilePath))));
        }

        if (projectFiles.Count == 0)
        {
            throw new GracefulException(
                CliCommandStrings.CouldNotFindAProject,
                currentDirectory, "--project");
        }

        return projectFiles;
    }
}
