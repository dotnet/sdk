// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class TestScenario
{
    public DotNetActions Commands { get; }
    public DotNetLanguage Language { get; }
    public bool NoHttps { get; set; } = Config.TargetRid.Contains("osx");
    public string ScenarioName { get; }
    public DotNetTemplate Template { get; }
    public Action<string>? Validate { get; }

    public TestScenario(string scenarioName, DotNetLanguage language, DotNetTemplate template, DotNetActions commands = DotNetActions.None, Action<string>? validate = null)
    {
        ScenarioName = scenarioName;
        Template = template;
        Language = language;
        Commands = commands;
        Validate = validate;
    }

    internal void Execute(DotNetHelper dotNetHelper)
    {
        // Don't use the cli language name in the project name because it may contain '#': https://github.com/dotnet/roslyn/issues/51692
        string projectName = $"{ScenarioName}_{Template}_{Language}";
        string customNewArgs = Template.IsAspNetCore() && NoHttps ? "--no-https" : string.Empty;
        dotNetHelper.ExecuteNew(Template.GetName(), projectName, Language.ToCliName(), customArgs: customNewArgs);

        if (Commands.HasFlag(DotNetActions.Build))
        {
            dotNetHelper.ExecuteBuild(projectName);
        }
        if (Commands.HasFlag(DotNetActions.Run))
        {
            if (Template.IsAspNetCore())
            {
                dotNetHelper.ExecuteRunWeb(projectName, Template);
            }
            else
            {
                dotNetHelper.ExecuteRun(projectName);
            }
        }
        if (Commands.HasFlag(DotNetActions.Publish))
        {
            dotNetHelper.ExecutePublish(projectName, Template);
        }
        if (Commands.HasFlag(DotNetActions.PublishSelfContained))
        {
            dotNetHelper.ExecutePublish(projectName, Template, selfContained: true, rid: Config.TargetRid);
        }
        if (Commands.HasFlag(DotNetActions.PublishComplex))
        {
            dotNetHelper.ExecutePublish(projectName, Template, selfContained: false);
            dotNetHelper.ExecutePublish(projectName, Template, selfContained: true, rid: Config.TargetRid);
            dotNetHelper.ExecutePublish(projectName, Template, selfContained: true, rid: Config.PortableRid);
        }
        if (Commands.HasFlag(DotNetActions.PublishR2R))
        {
            dotNetHelper.ExecutePublish(projectName, Template, selfContained: true, rid: Config.PortableRid, trimmed: true, readyToRun: true);
        }
        if (Commands.HasFlag(DotNetActions.Test))
        {
            dotNetHelper.ExecuteTest(projectName);
        }

        string projectPath = Path.Combine(DotNetHelper.ProjectsDirectory, projectName);
        Validate?.Invoke(projectPath);
    }
}
