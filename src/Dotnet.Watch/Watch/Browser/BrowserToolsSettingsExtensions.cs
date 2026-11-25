// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal static class BrowserToolsSettingsExtensions
{
    public static BrowserToolsSettings? FromProject(ProjectInstance project, ILogger logger)
    {
        if (SharedSecretBuildOutputs.FromProject(project, logger) == null)
        {
            return null;
        }

        return BrowserToolsSettings.FromProjectSettings(project.GetIntermediateOutputDirectory()!);
    }
}
