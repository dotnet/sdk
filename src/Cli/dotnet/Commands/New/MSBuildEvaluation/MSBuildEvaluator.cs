// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.MSBuildEvaluation;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using MSBuildProject = Microsoft.Build.Evaluation.Project;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Commands.New.MSBuildEvaluation;

internal class MSBuildEvaluator : IIdentifiedComponent
{
    private readonly DotNetProjectEvaluator _evaluator;
    private readonly object _lockObj = new();

    private IEngineEnvironmentSettings? _settings;
    private ILogger? _logger;
    private MSBuildEvaluationResult? _cachedEvaluationResult;
    private readonly string _outputDirectory;
    private readonly string? _projectFullPath;
    internal MSBuildEvaluator()
    {
        _outputDirectory = Directory.GetCurrentDirectory();
        _evaluator = DotNetProjectEvaluatorFactory.CreateForCommand();
    }

    internal MSBuildEvaluator(string? outputDirectory = null, string? projectPath = null)
    {
        _outputDirectory = outputDirectory ?? Directory.GetCurrentDirectory();
        _projectFullPath = projectPath != null ? Path.GetFullPath(projectPath) : null;
        _evaluator = DotNetProjectEvaluatorFactory.CreateForCommand();
    }

    public Guid Id => Guid.Parse("{6C2CB5CA-06C3-460A-8ADB-5F21E113AB24}");

    /// <summary>
    /// Evaluates the project at current location.
    /// Current location is specified by `--output` or is current working directory.
    /// The location is set once when component is created.
    /// If location changes, recreate the component.
    /// </summary>
    /// <param name="engineEnvironmentSettings"></param>
    /// <returns>MSBuild project evaluation result.</returns>
    internal MSBuildEvaluationResult EvaluateProject(IEngineEnvironmentSettings engineEnvironmentSettings)
    {
        lock (_lockObj)
        {
            //we cache result of evaluation if instance of environment settings used is the same.
            //reason: the component is only used in dotnet CLI, which execution is short lived.
            //we don't care about changes done to project during command execution.
            if (_settings == null || _settings != engineEnvironmentSettings || _cachedEvaluationResult == null)
            {
                _settings = engineEnvironmentSettings;
                _logger = _settings.Host.LoggerFactory.CreateLogger(nameof(MSBuildEvaluator));
                _cachedEvaluationResult = EvaluateProjectInternal(_settings);
            }
            return _cachedEvaluationResult;
        }
    }

    /// <summary>
    /// Forces cache reset.
    /// </summary>
    internal void ResetCache()
    {
        lock (_lockObj)
        {
            _settings = null;
            _cachedEvaluationResult = null;
        }
    }

    private MSBuildEvaluationResult EvaluateProjectInternal(IEngineEnvironmentSettings engineEnvironmentSettings)
    {
        _logger?.LogDebug("Output directory is: {0}.", _outputDirectory);
        _logger?.LogDebug("Project full path is: {0}.", _projectFullPath ?? "<null>");

        string projectPath;
        if (string.IsNullOrEmpty(_projectFullPath))
        {
            IReadOnlyList<string> foundFiles = [];
            try
            {
                foundFiles = FileFindHelpers.FindFilesAtOrAbovePath(engineEnvironmentSettings.Host.FileSystem, _outputDirectory, "*.*proj");
                _logger?.LogDebug("Found project files: {0}.", string.Join("; ", foundFiles));
            }
            catch (Exception e)
            {
                //do nothing
                //in case of exception, no project found result is used.
                _logger?.LogDebug("Exception occurred when searching for the project file: {0}", e.Message);
            }

            if (foundFiles.Count == 0)
            {
                _logger?.LogDebug("No project found.");
                return MSBuildEvaluationResult.CreateNoProjectFound(_outputDirectory);
            }
            if (foundFiles.Count > 1)
            {
                _logger?.LogDebug("Multiple projects found.");
                return MultipleProjectsEvaluationResult.Create(foundFiles);
            }
            projectPath = Path.GetFullPath(foundFiles.Single());
        }
        else
        {
            projectPath = _projectFullPath;
        }

        Stopwatch watch = new();
        Stopwatch innerBuildWatch = new();
        bool IsSdkStyleProject = false;
        IReadOnlyList<NuGetFramework>? targetFrameworks = null;
        NuGetFramework? targetFramework = null;
        MSBuildEvaluationResult? result = null;

        try
        {
            watch.Start();
            _logger?.LogDebug("Evaluating project: {0}", projectPath);
            DotNetProject evaluatedProject = RunEvaluate(projectPath);

            //if project is using Microsoft.NET.Sdk, then it is SDK-style project.
            IsSdkStyleProject = evaluatedProject.GetPropertyValue("UsingMicrosoftNETSDK") == "true";
            _logger?.LogDebug("SDK-style project: {0}", IsSdkStyleProject);

            targetFrameworks = evaluatedProject.TargetFrameworks;
            _logger?.LogDebug("Target frameworks: {0}", string.Join("; ", targetFrameworks ?? []));
            targetFramework = evaluatedProject.TargetFramework;
            _logger?.LogDebug("Target framework: {0}", targetFramework?.GetShortFolderName() ?? "<null>");

            if (!IsSdkStyleProject || targetFramework == null && targetFrameworks == null)
            {
                //For non SDK style project, we cannot evaluate more info. Also there is no indication, whether the project
                //was restored or not, so it is not checked.
                _logger?.LogDebug("Project is non-SDK style, cannot evaluate restore status, succeeding.");
                return result = NonSDKStyleEvaluationResult.CreateSuccess(projectPath, evaluatedProject);
            }

            //For SDK-style project, if the project was restored "RestoreSuccess" property will be set to true.
            if (!evaluatedProject.GetPropertyValue("RestoreSuccess")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                _logger?.LogDebug("Project is not restored, exiting.");
                return result = MSBuildEvaluationResult.CreateNoRestore(projectPath);
            }

            //If target framework is set, no further evaluation is needed.
            if (targetFramework != null)
            {
                _logger?.LogDebug("Project is SDK style, single TFM:{0}, evaluation succeeded.", targetFramework);
                return result = SDKStyleEvaluationResult.CreateSuccess(projectPath, targetFramework, evaluatedProject);
            }

            //If target framework is not set, then presumably it is multi-target project.
            //If there are no target frameworks, it is not expected.
            if (targetFrameworks == null)
            {
                _logger?.LogDebug("Project is SDK style, but does not specify the framework.");
                return result = MSBuildEvaluationResult.CreateFailure(projectPath, string.Format(CliCommandStrings.MSBuildEvaluator_Error_NoTargetFramework, projectPath));
            }

            //For multi-target project, we need to do additional evaluation for each target framework.
            Dictionary<NuGetFramework, DotNetProject> evaluatedTfmBasedProjects = [];
            innerBuildWatch.Start();
            foreach (var tfm in targetFrameworks)
            {
                _logger?.LogDebug("Evaluating project for target framework: {0}", tfm);
                evaluatedTfmBasedProjects[tfm] = RunEvaluate(projectPath, tfm);
            }
            innerBuildWatch.Stop();
            _logger?.LogDebug("Project is SDK style, multi-target, evaluation succeeded.");
            return result = MultiTargetEvaluationResult.CreateSuccess(projectPath, evaluatedProject, evaluatedTfmBasedProjects);

        }
        catch (Exception e)
        {
            _logger?.LogDebug("Unexpected error: {0}", e);
            return result = MSBuildEvaluationResult.CreateFailure(projectPath, e.Message);
        }
        finally
        {
            watch.Stop();
            innerBuildWatch.Stop();

            string? targetFrameworksString = null;

            if (targetFrameworks != null)
            {
                targetFrameworksString = string.Join(",", targetFrameworks.Select(tfm => Sha256Hasher.HashWithNormalizedCasing(tfm.GetShortFolderName())));
            }
            else if (targetFramework != null)
            {
                targetFrameworksString = Sha256Hasher.HashWithNormalizedCasing(targetFramework.GetShortFolderName());
            }

            Dictionary<string, string?> properties = new()
            {
                { "ProjectPath",  Sha256Hasher.HashWithNormalizedCasing(projectPath)},
                { "SdkStyleProject", IsSdkStyleProject.ToString() },
                { "Status", result?.Status.ToString() ?? "<null>"},
                { "TargetFrameworks", targetFrameworksString ?? "<null>"},
            };

            Dictionary<string, double> measurements = new()
            {
                { "EvaluationTime",  watch.ElapsedMilliseconds },
                { "InnerEvaluationTime",  innerBuildWatch.ElapsedMilliseconds }
            };

            TelemetryEventEntry.TrackEvent("new/msbuild-eval", properties, measurements);
        }
    }

    private DotNetProject RunEvaluate(string projectToLoad, NuGetFramework? tfm = null)
    {
        if (!File.Exists(projectToLoad))
        {
            throw new FileNotFoundException(message: null, projectToLoad);
        }

        //We do only best effort here, also the evaluation should be fast vs complete; therefore ignoring imports errors.
        //The result of evaluation is used for the following:
        // - determining if the template can be run in the following context(constraints) based on Project Capabilities
        // - determining properties values that will be used in template content
        //The cost of the error is not substantial:
        //- worst case scenario the user can create a template, which should not be allowed to and it fails to compile / build-- > likely user will remove it or fix it manually then
        //- or the template content will be corrupted and consequent build fails --> the user may fix the issues manually if needed
        //- or the user will not see that template that is expected --> but they can always override it with --force
        //Therefore, we should not fail on missing imports or invalid imports, if this is the case rather restore/build should fail.
        return GetLoadedProject(projectToLoad, tfm);
    }

    private DotNetProject GetLoadedProject(string projectToLoad, NuGetFramework? tfm)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (tfm != null)
        {
            props["TargetFramework"] = tfm.GetShortFolderName();
        }
        return _evaluator.LoadProject(projectToLoad, props, useFlexibleLoading: true);
    }
}
