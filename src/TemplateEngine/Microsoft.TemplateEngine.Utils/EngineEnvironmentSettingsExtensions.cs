// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Utils
{
    public static class EngineEnvironmentSettingsExtensions
    {
        public static bool TryGetMountPoint(this IEngineEnvironmentSettings engineEnvironment, string mountPointUri, out IMountPoint mountPoint)
        {
            foreach (var factory in engineEnvironment.Components.OfType<IMountPointFactory>())
            {
                if (factory.TryMount(engineEnvironment, null, mountPointUri, out var myMountPoint))
                {
                    mountPoint = myMountPoint;
                    return true;
                }
            }

            mountPoint = null;
            return false;
        }
    }
}
