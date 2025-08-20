// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using ConcurrencyUtilities = NuGet.Common.ConcurrencyUtilities;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class ProjectToolsCommandResolver(
    IPackagedCommandSpecFactory packagedCommandSpecFactory,
    IEnvironmentProvider environment) : ICommandResolver
{
    private const string ProjectToolsCommandResolverName = "projecttoolscommandresolver";

    private readonly List<string> _allowedCommandExtensions = [FileNameSuffixes.DotNet.DynamicLib];
    private readonly IPackagedCommandSpecFactory _packagedCommandSpecFactory = packagedCommandSpecFactory;

    private readonly IEnvironmentProvider _environment = environment;

    public CommandSpec? Resolve(CommandResolverArguments commandResolverArguments)
    {
        if (commandResolverArguments.CommandName == null
            || commandResolverArguments.ProjectDirectory == null)
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.InvalidCommandResolverArguments,
                ProjectToolsCommandResolverName));

            return null;
        }

        return ResolveFromProjectTools(commandResolverArguments);
    }

    private CommandSpec? ResolveFromProjectTools(CommandResolverArguments commandResolverArguments)
    {
        var projectFactory = new ProjectFactory(_environment);

        var project = projectFactory.GetProject(
            commandResolverArguments.ProjectDirectory,
            commandResolverArguments.Framework,
            commandResolverArguments.Configuration,
            commandResolverArguments.BuildBasePath,
            commandResolverArguments.OutputPath);

        if (project == null)
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.DidNotFindProject, ProjectToolsCommandResolverName));

            return null;
        }

        var tools = project.GetTools();

        return ResolveCommandSpecFromAllToolLibraries(
            tools,
            commandResolverArguments.CommandName,
            commandResolverArguments.CommandArguments.OrEmptyIfNull(),
            project);
    }

    private CommandSpec? ResolveCommandSpecFromAllToolLibraries(
        IEnumerable<SingleProjectInfo> toolsLibraries,
        string commandName,
        IEnumerable<string> args,
        IProject project)
    {
        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.ResolvingCommandSpec,
            ProjectToolsCommandResolverName,
            toolsLibraries.Count()));

        foreach (var toolLibrary in toolsLibraries)
        {
            var commandSpec = ResolveCommandSpecFromToolLibrary(
                toolLibrary,
                commandName,
                args,
                project);

            if (commandSpec != null)
            {
                return commandSpec;
            }
        }

        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.FailedToResolveCommandSpec,
            ProjectToolsCommandResolverName));

        return null;
    }

    private CommandSpec? ResolveCommandSpecFromToolLibrary(
        SingleProjectInfo toolLibraryRange,
        string commandName,
        IEnumerable<string> args,
        IProject project)
    {
        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.AttemptingToResolveCommandSpec,
            ProjectToolsCommandResolverName,
            toolLibraryRange.Name));

        var possiblePackageRoots = GetPossiblePackageRoots(project).ToList();
        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.NuGetPackagesRoot,
            ProjectToolsCommandResolverName,
            string.Join(Environment.NewLine, possiblePackageRoots.Select((p) => $"- {p}"))));

        List<NuGetFramework> toolFrameworksToCheck = [project.DotnetCliToolTargetFramework];

        //  NuGet restore in Visual Studio may restore for netcoreapp1.0. So if that happens, fall back to
        //  looking for a netcoreapp1.0 or netcoreapp1.1 tool restore.
        if (project.DotnetCliToolTargetFramework.Framework == FrameworkConstants.FrameworkIdentifiers.NetCoreApp &&
            project.DotnetCliToolTargetFramework.Version >= new Version(2, 0, 0))
        {
            toolFrameworksToCheck.Add(NuGetFramework.Parse("netcoreapp1.1"));
            toolFrameworksToCheck.Add(NuGetFramework.Parse("netcoreapp1.0"));
        }

        LockFile? toolLockFile = null;
        NuGetFramework? toolTargetFramework = null;

        foreach (var toolFramework in toolFrameworksToCheck)
        {
            toolLockFile = GetToolLockFile(
                toolLibraryRange,
                toolFramework,
                possiblePackageRoots);

            if (toolLockFile != null)
            {
                toolTargetFramework = toolFramework;
                break;
            }
        }

        if (toolLockFile == null)
        {
            return null;
        }

        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.FoundToolLockFile,
            ProjectToolsCommandResolverName,
            toolLockFile.Path));

        var toolLibrary = toolLockFile.Targets
            .FirstOrDefault(t => toolTargetFramework == t.TargetFramework)
            ?.Libraries.FirstOrDefault(
                l => StringComparer.OrdinalIgnoreCase.Equals(l.Name, toolLibraryRange.Name));
        if (toolLibrary == null)
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.LibraryNotFoundInLockFile,
                ProjectToolsCommandResolverName));

            return null;
        }

        var depsFileRoot = Path.GetDirectoryName(toolLockFile.Path)!;

        var depsFilePath = GetToolDepsFilePath(
            toolLibraryRange,
            toolTargetFramework!, // should be safe now because we found the toolLockFile
            toolLockFile,
            depsFileRoot,
            project.ToolDepsJsonGeneratorProject);

        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.AttemptingToCreateCommandSpec,
            ProjectToolsCommandResolverName));

        var commandSpec = _packagedCommandSpecFactory.CreateCommandSpecFromLibrary(
                toolLibrary,
                commandName,
                args,
                _allowedCommandExtensions,
                toolLockFile,
                depsFilePath,
                null);

        if (commandSpec == null)
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.CommandSpecIsNull,
                ProjectToolsCommandResolverName));
        }

        commandSpec?.AddEnvironmentVariablesFromProject(project);

        return commandSpec;
    }

    private static IEnumerable<string> GetPossiblePackageRoots(IProject project)
    {
        if (project.TryGetLockFile(out LockFile lockFile))
        {
            return lockFile.PackageFolders.Select((packageFolder) => packageFolder.Path);
        }

        return [];
    }

    private LockFile? GetToolLockFile(
        SingleProjectInfo toolLibrary,
        NuGetFramework framework,
        IEnumerable<string> possibleNugetPackagesRoot)
    {
        foreach (var packagesRoot in possibleNugetPackagesRoot)
        {
            if (TryGetToolLockFile(toolLibrary, framework, packagesRoot, out var lockFile))
            {
                return lockFile;
            }
        }

        return null;
    }


    private static async Task<bool> FileExistsWithLock(string path)
    {
        return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
            path,
            lockedToken => Task.FromResult(File.Exists(path)),
            CancellationToken.None);
    }

    private bool TryGetToolLockFile(
        SingleProjectInfo toolLibrary,
        NuGetFramework framework,
        string nugetPackagesRoot,
        [NotNullWhen(true)] out LockFile? lockFile)
    {
        lockFile = null;
        var lockFilePath = GetToolLockFilePath(toolLibrary, framework, nugetPackagesRoot);

        if (!FileExistsWithLock(lockFilePath).Result)
        {
            return false;
        }

        try
        {
            lockFile = new LockFileFormat()
                .ReadWithLock(lockFilePath)
                .Result;
        }
        catch (FileFormatException)
        {
            throw;
        }

        return true;
    }

    private static string GetToolLockFilePath(
        SingleProjectInfo toolLibrary,
        NuGetFramework framework,
        string nugetPackagesRoot)
    {
        var toolPathCalculator = new ToolPathCalculator(nugetPackagesRoot);

        return toolPathCalculator.GetBestLockFilePath(
            toolLibrary.Name,
            VersionRange.Parse(toolLibrary.Version),
            framework);
    }

    private string GetToolDepsFilePath(
        SingleProjectInfo toolLibrary,
        NuGetFramework framework,
        LockFile toolLockFile,
        string depsPathRoot,
        string toolDepsJsonGeneratorProject)
    {
        var depsJsonPath = Path.Combine(
            depsPathRoot,
            toolLibrary.Name + FileNameSuffixes.DepsJson);

        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.ExpectDepsJsonAt,
            ProjectToolsCommandResolverName,
            depsJsonPath));

        EnsureToolJsonDepsFileExists(toolLockFile, framework, depsJsonPath, toolLibrary, toolDepsJsonGeneratorProject);

        return depsJsonPath;
    }

    private void EnsureToolJsonDepsFileExists(
        LockFile toolLockFile,
        NuGetFramework framework,
        string depsPath,
        SingleProjectInfo toolLibrary,
        string toolDepsJsonGeneratorProject)
    {
        if (!File.Exists(depsPath))
        {
            GenerateDepsJsonFile(toolLockFile, framework, depsPath, toolLibrary, toolDepsJsonGeneratorProject);
        }
    }

    internal void GenerateDepsJsonFile(
        LockFile toolLockFile,
        NuGetFramework framework,
        string depsPath,
        SingleProjectInfo toolLibrary,
        string toolDepsJsonGeneratorProject)
    {
        if (string.IsNullOrEmpty(toolDepsJsonGeneratorProject) ||
            !File.Exists(toolDepsJsonGeneratorProject))
        {
            throw new GracefulException(CliStrings.DepsJsonGeneratorProjectNotSet);
        }

        Reporter.Verbose.WriteLine(string.Format(
            CliStrings.GeneratingDepsJson,
            depsPath));

        var tempDepsFile = Path.Combine(PathUtilities.CreateTempSubdirectory(), Path.GetRandomFileName());

        List<string> args =
        [
            toolDepsJsonGeneratorProject,
            $"-property:ProjectAssetsFile=\"{toolLockFile.Path}\"",
            $"-property:ToolName={toolLibrary.Name}",
            $"-property:ProjectDepsFilePath={tempDepsFile}"
        ];

        var toolTargetFramework = toolLockFile.Targets.First().TargetFramework.GetShortFolderName();
        args.Add($"-property:TargetFramework={toolTargetFramework}");


        //  Look for the .props file in the Microsoft.NETCore.App package, until NuGet
        //  generates .props and .targets files for tool restores (https://github.com/NuGet/Home/issues/5037)
        var platformLibrary = toolLockFile.Targets
            .FirstOrDefault(t => framework == t.TargetFramework)
            ?.GetPlatformLibrary();

        if (platformLibrary != null)
        {
            string? buildRelativePath = platformLibrary.Build.FirstOrDefault()?.Path;

            var platformLibraryPath = toolLockFile.GetPackageDirectory(platformLibrary);

            if (platformLibraryPath != null && buildRelativePath != null)
            {
                //  Get rid of "_._" filename
                buildRelativePath = Path.GetDirectoryName(buildRelativePath)!;

                string platformLibraryBuildFolderPath = Path.Combine(platformLibraryPath, buildRelativePath);
                var platformLibraryPropsFile = Directory.GetFiles(platformLibraryBuildFolderPath, "*.props").FirstOrDefault();

                if (platformLibraryPropsFile != null)
                {
                    args.Add($"-property:AdditionalImport={platformLibraryPropsFile}");
                }
            }
        }

        //  Delete temporary file created by Path.GetTempFileName(), otherwise the GenerateBuildDependencyFile target
        //  will think the deps file is up-to-date and skip executing
        File.Delete(tempDepsFile);

        var msBuildExePath = _environment.GetEnvironmentVariable(Constants.MSBUILD_EXE_PATH);

        msBuildExePath = string.IsNullOrEmpty(msBuildExePath) ?
            Path.Combine(AppContext.BaseDirectory, "MSBuild.dll") :
            msBuildExePath;

        Reporter.Verbose.WriteLine(string.Format(CliStrings.MSBuildArgs,
            ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args)));

        int result;
        string? stdOut;
        string? stdErr;

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([..args], CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, BuildCommandParser.TargetOption, BuildCommandParser.VerbosityOption);
        var forwardingAppWithoutLogging = new MSBuildForwardingAppWithoutLogging(msbuildArgs, msBuildExePath);
        if (forwardingAppWithoutLogging.ExecuteMSBuildOutOfProc)
        {
            result = forwardingAppWithoutLogging
                .GetProcessStartInfo()
                .ExecuteAndCaptureOutput(out stdOut, out stdErr);
        }
        else
        {
            // Execute and capture output of MSBuild running in-process.
            var outWriter = new StringWriter();
            var errWriter = new StringWriter();
            var savedOutWriter = Console.Out;
            var savedErrWriter = Console.Error;
            try
            {
                Console.SetOut(outWriter);
                Console.SetError(errWriter);

                result = forwardingAppWithoutLogging.Execute();

                stdOut = outWriter.ToString();
                stdErr = errWriter.ToString();
            }
            finally
            {
                Console.SetOut(savedOutWriter);
                Console.SetError(savedErrWriter);
            }
        }

        if (result != 0)
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.UnableToGenerateDepsJson,
                stdOut + Environment.NewLine + stdErr));

            throw new GracefulException(string.Format(CliStrings.UnableToGenerateDepsJson, toolDepsJsonGeneratorProject));
        }

        try
        {
            File.Move(tempDepsFile, depsPath);
        }
        catch (Exception e)
        {
            Reporter.Verbose.WriteLine(string.Format(
                CliStrings.UnableToGenerateDepsJson,
                e.Message));

            try
            {
                File.Delete(tempDepsFile);
            }
            catch (Exception e2)
            {
                Reporter.Verbose.WriteLine(string.Format(
                    CliStrings.UnableToDeleteTemporaryDepsJson,
                    e2.Message));
            }
        }
    }
}
