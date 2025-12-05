// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.MSBuildEvaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

internal class MSBuildProject : IProject
{
    private static readonly NuGetFramework s_toolPackageFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;

    private readonly DotNetProject _project;
    private readonly string _msBuildExePath;

    public string? DepsJsonPath => _project.GetPropertyValue("ProjectDepsFilePath");

    public string? RuntimeConfigJsonPath => _project.GetPropertyValue("ProjectRuntimeConfigFilePath");

    public string? FullOutputPath => _project.GetPropertyValue("TargetDir");

    public string ProjectRoot { get; }

    public NuGetFramework DotnetCliToolTargetFramework => _project.GetPropertyValue("DotnetCliToolTargetFramework") is string s && !string.IsNullOrEmpty(s)
        ? NuGetFramework.Parse(s)
        : s_toolPackageFramework;

    public Dictionary<string, string> EnvironmentVariables
    {
        get
        {
            return new Dictionary<string, string>
            {
                { Constants.MSBUILD_EXE_PATH, _msBuildExePath }
            };
        }
    }

    public string? ToolDepsJsonGeneratorProject => _project.GetPropertyValue("ToolDepsJsonGeneratorProject");

    internal MSBuildProject(
        DotNetProjectEvaluator evaluator,
        string msBuildProjectPath,
        NuGetFramework framework,
        string configuration,
        string outputPath,
        string msBuildExePath)
    {
        ProjectRoot = msBuildExePath;

        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
           { "MSBuildExtensionsPath", Path.GetDirectoryName(msBuildExePath)! }
        };

        if (framework != null)
        {
            globalProperties.Add("TargetFramework", framework.GetShortFolderName());
        }

        if (outputPath != null)
        {
            globalProperties.Add("OutputPath", outputPath);
        }

        if (configuration != null)
        {
            globalProperties.Add("Configuration", configuration);
        }

        _project = evaluator.LoadProject(
            msBuildProjectPath,
            globalProperties);

        _msBuildExePath = msBuildExePath;
    }

    public IEnumerable<SingleProjectInfo> GetTools()
    {
        var toolsReferences = _project.GetItems("DotNetCliToolReference");
        var tools = toolsReferences.Select(t => new SingleProjectInfo(t.EvaluatedInclude, t.GetMetadataValue("Version"), []));
        return tools;
    }

    public LockFile GetLockFile()
    {
        var lockFilePath = GetLockFilePathFromProjectLockFileProperty() ??
            GetLockFilePathFromIntermediateBaseOutputPath();

        return new LockFileFormat()
            .ReadWithLock(lockFilePath!)
            .Result;
    }

    public bool TryGetLockFile(out LockFile? lockFile)
    {
        lockFile = null;

        var lockFilePath = GetLockFilePathFromProjectLockFileProperty() ??
            GetLockFilePathFromIntermediateBaseOutputPath();

        if (lockFilePath == null)
        {
            return false;
        }

        if (!File.Exists(lockFilePath))
        {
            return false;
        }

        lockFile = new LockFileFormat()
            .ReadWithLock(lockFilePath)
            .Result;
        return true;
    }

    private string? GetLockFilePathFromProjectLockFileProperty() => _project.ProjectAssetsFile;

    private string? GetLockFilePathFromIntermediateBaseOutputPath()
    {
        var intermediateOutputPath = _project.GetPropertyValue("BaseIntermediateOutputPath");
        if (string.IsNullOrEmpty(intermediateOutputPath))
        {
            return null;
        }
        return Path.Combine(intermediateOutputPath, "project.assets.json");
    }
}
