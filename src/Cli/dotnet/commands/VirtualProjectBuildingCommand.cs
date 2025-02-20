// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Run;

namespace Microsoft.DotNet.Tools;

/// <summary>
/// Used to build a virtual project file in memory to support <c>dotnet run file.cs</c>.
/// </summary>
internal sealed class VirtualProjectBuildingCommand
{
    public Dictionary<string, string> GlobalProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public required string EntryPointFileFullPath { get; init; }

    public int Execute(string[] binaryLoggerArgs, LoggerVerbosity verbosity)
    {
        var binaryLogger = GetBinaryLogger(binaryLoggerArgs);
        var consoleLogger = new ConsoleLogger(verbosity);
        Dictionary<string, string?> savedEnvironmentVariables = new();
        try
        {
            // Set environment variables.
            foreach (var (key, value) in MSBuildForwardingAppWithoutLogging.GetMSBuildRequiredEnvironmentVariables())
            {
                savedEnvironmentVariables[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }

            // Setup MSBuild.
            ReadOnlySpan<ILogger> binaryLoggers = binaryLogger is null ? [] : [binaryLogger];
            var projectCollection = new ProjectCollection(
                GlobalProperties,
                [.. binaryLoggers, consoleLogger],
                ToolsetDefinitionLocations.Default);
            var parameters = new BuildParameters(projectCollection)
            {
                Loggers = projectCollection.Loggers,
                LogTaskInputs = binaryLogger is not null,
            };
            BuildManager.DefaultBuildManager.BeginBuild(parameters);

            // Do a restore first (equivalent to MSBuild's "implicit restore", i.e., `/restore`).
            // See https://github.com/dotnet/msbuild/blob/a1c2e7402ef0abe36bf493e395b04dd2cb1b3540/src/MSBuild/XMake.cs#L1838.
            var restoreRequest = new BuildRequestData(
                CreateProjectInstance(projectCollection, addGlobalProperties: static (globalProperties) =>
                {
                    globalProperties["MSBuildRestoreSessionId"] = Guid.NewGuid().ToString("D");
                    globalProperties["MSBuildIsRestoring"] = bool.TrueString;
                }),
                targetsToBuild: ["Restore"],
                hostServices: null,
                BuildRequestDataFlags.ClearCachesAfterBuild | BuildRequestDataFlags.SkipNonexistentTargets | BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports | BuildRequestDataFlags.FailOnUnresolvedSdk);
            var restoreResult = BuildManager.DefaultBuildManager.BuildRequest(restoreRequest);
            if (restoreResult.OverallResult != BuildResultCode.Success)
            {
                return 1;
            }

            // Then do a build.
            var buildRequest = new BuildRequestData(
                CreateProjectInstance(projectCollection),
                targetsToBuild: ["Build"]);
            var buildResult = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
            if (buildResult.OverallResult != BuildResultCode.Success)
            {
                return 1;
            }

            BuildManager.DefaultBuildManager.EndBuild();
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
        finally
        {
            foreach (var (key, value) in savedEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            binaryLogger?.Shutdown();
            consoleLogger.Shutdown();
        }

        static ILogger? GetBinaryLogger(string[] args)
        {
            for (int i = args.Length - 1; i >= 0; i--)
            {
                var arg = args[i];
                if (RunCommand.IsBinLogArgument(arg))
                {
                    return new BinaryLogger
                    {
                        Parameters = arg.IndexOf(':') is >= 0 and var index
                            ? arg[(index + 1)..]
                            : "msbuild.binlog",
                    };
                }
            }

            return null;
        }
    }

    public ProjectInstance CreateProjectInstance(ProjectCollection projectCollection)
    {
        return CreateProjectInstance(projectCollection, addGlobalProperties: null);
    }

    private ProjectInstance CreateProjectInstance(
        ProjectCollection projectCollection,
        Action<IDictionary<string, string>>? addGlobalProperties = null)
    {
        var projectRoot = CreateProjectRootElement(projectCollection);

        var globalProperties = projectCollection.GlobalProperties;
        if (addGlobalProperties is not null)
        {
            globalProperties = new Dictionary<string, string>(projectCollection.GlobalProperties, StringComparer.OrdinalIgnoreCase);
            addGlobalProperties(globalProperties);
        }

        return ProjectInstance.FromProjectRootElement(projectRoot, new ProjectOptions
        {
            GlobalProperties = globalProperties,
        });
    }

    private ProjectRootElement CreateProjectRootElement(ProjectCollection projectCollection)
    {
        var projectFileFullPath = Path.ChangeExtension(EntryPointFileFullPath, ".csproj");
        var projectFileText = """
            <Project>
                <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net9.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                </PropertyGroup>

                <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

                <!-- Override targets which don't work with project files that are not present on disk. -->

                <Target Name="_FilterRestoreGraphProjectInputItems"
                        DependsOnTargets="_LoadRestoreGraphEntryPoints"
                        Returns="@(FilteredRestoreGraphProjectInputItems)">
                    <ItemGroup>
                        <FilteredRestoreGraphProjectInputItems Include="@(RestoreGraphProjectInputItems)" />
                    </ItemGroup>
                </Target>

                <Target Name="_GetAllRestoreProjectPathItems"
                        DependsOnTargets="_FilterRestoreGraphProjectInputItems"
                        Returns="@(_RestoreProjectPathItems)">
                    <ItemGroup>
                        <_RestoreProjectPathItems Include="@(FilteredRestoreGraphProjectInputItems)" />
                    </ItemGroup>
                </Target>

                <Target Name="_GenerateRestoreGraph"
                        DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GetAllRestoreProjectPathItems;_GenerateRestoreGraphProjectEntry;_GenerateProjectRestoreGraph"
                        Returns="@(_RestoreGraphEntry)">
                    <!-- Output from dependency _GenerateRestoreGraphProjectEntry and _GenerateProjectRestoreGraph -->
                </Target>
            </Project>
            """;
        ProjectRootElement projectRoot;
        using (var xmlReader = XmlReader.Create(new StringReader(projectFileText)))
        {
            projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);
        }
        projectRoot.FullPath = projectFileFullPath;
        return projectRoot;
    }
}
