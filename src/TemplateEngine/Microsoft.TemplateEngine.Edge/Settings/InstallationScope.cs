// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Edge.Settings
{
    /// <summary>
    /// Defines the scope that managed by built-in providers.
    /// </summary>
    public enum InstallationScope
    {
        /// <summary>
        /// Template packages are visible to all template hosts.
        /// </summary>
        Global = 0,

        /// <summary>
        /// Template packages are visible to all versions of certain template host.
        /// </summary>
        //        Host = 1,         //not supported at the moment

        /// <summary>
        /// Template packages are visible to only to specific version of the host.
        /// </summary>
        //        Version = 2       //not supported at the moment
    }
}
