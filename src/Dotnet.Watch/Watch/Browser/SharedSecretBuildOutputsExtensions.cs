// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Execution;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal static class SharedSecretBuildOutputsExtensions
{
    extension(SharedSecretBuildOutputs)
    {
        /// <summary>
        /// Returns the build outputs of <paramref name="projectNode"/>, or null when the project does
        /// not produce browser tools assets at all.
        /// </summary>
        public static SharedSecretBuildOutputs? FromProject(ProjectInstance projectInstance, ILogger logger)
        {
            var result = SharedSecretBuildOutputs.FromProjectSettings(
                configuration: projectInstance.GetPropertyValue(PropertyNames.Configuration),
                intermediateOutputDirectory: projectInstance.GetIntermediateOutputDirectory(),
                enableHotReloadInRuntimeConfigDevFile: projectInstance.TryGetBooleanPropertyValue(PropertyNames.EnableHotReloadInRuntimeConfigDevFile),
                browserToolsAssetPrefix: projectInstance.GetPropertyValue(PropertyNames.HotReloadBrowserToolsAssetPrefix),
                staticWebAssetsEnabled: projectInstance.GetBooleanPropertyValue(PropertyNames.StaticWebAssetsEnabled),
                jsModulesEnabled: projectInstance.GetBooleanPropertyValue(PropertyNames.JSModulesEnabled));

            if (result == null)
            {
                logger.Log(LogEvents.BrowserToolsNotSupportedByProjectSettings,
                    $"{PropertyNames.HotReloadBrowserToolsAssetPrefix}='{projectInstance.GetPropertyValue(PropertyNames.HotReloadBrowserToolsAssetPrefix)}', " +
                    $"{PropertyNames.StaticWebAssetsEnabled}='{projectInstance.GetBooleanPropertyValue(PropertyNames.StaticWebAssetsEnabled)}', " +
                    $"{PropertyNames.JSModulesEnabled}='{projectInstance.GetBooleanPropertyValue(PropertyNames.JSModulesEnabled)}'");
            }

            return result;
        }
    }
}
