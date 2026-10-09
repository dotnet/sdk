// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Execution;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal static class BrowserToolsBuildOutputsExtensions
{
    extension(BrowserToolsBuildOutputs)
    {
        /// <summary>
        /// Returns the build outputs of <paramref name="projectNode"/>, or null when the project does
        /// not produce browser tools assets at all. 
        /// </summary>
        public static BrowserToolsBuildOutputs? FromProject(ProjectInstance projectInstance, ILogger logger)
            => BrowserToolsBuildOutputs.FromProjectSettings(
                logger,
                projectInstance.FullPath,
                configuration: projectInstance.GetPropertyValue(PropertyNames.Configuration),
                intermediateOutputDirectory: projectInstance.GetIntermediateOutputDirectory(),
                enableHotReloadInRuntimeConfigDevFile: projectInstance.TryGetBooleanPropertyValue(PropertyNames.EnableHotReloadInRuntimeConfigDevFile),
                dotNetWatchBrowserToolsAssetPrefix: projectInstance.GetPropertyValue(PropertyNames.DotNetWatchBrowserToolsAssetPrefix),
                staticWebAssetsEnabled: projectInstance.GetBooleanPropertyValue(PropertyNames.StaticWebAssetsEnabled),
                jsModulesEnabled: projectInstance.GetBooleanPropertyValue(PropertyNames.JSModulesEnabled));
    }
}
