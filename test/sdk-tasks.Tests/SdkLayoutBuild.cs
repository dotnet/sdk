// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
using Microsoft.Build.Utilities;

namespace Microsoft.CoreSdkTasks.Tests;

internal static class SdkLayoutBuild
{
    public static void Run(
        ProjectCollection collection,
        string projectPath,
        string root,
        IEnumerable<string> targets,
        Logger logger,
        Func<string> getLogText,
        bool success = true)
    {
        ProjectInstance instance = collection.LoadProject(projectPath).CreateProjectInstance();
        string binlog = Path.Combine(root, $"build-{Guid.NewGuid():N}.binlog");
        using var manager = new BuildManager();
        BuildResult result = manager.Build(
            new BuildParameters(collection)
            {
                Loggers = [logger, new BinaryLogger { Parameters = binlog }]
            },
            new BuildRequestData(instance, targets.ToArray()));

        (result.OverallResult == BuildResultCode.Success).Should().Be(
            success,
            "MSBuild log:{0}{1}{0}Binlog: {2}",
            Environment.NewLine,
            getLogText(),
            binlog);
    }
}
