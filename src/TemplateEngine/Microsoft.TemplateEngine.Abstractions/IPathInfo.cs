// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Contains common folder paths used by TemplateEngine.
    /// </summary>
    public interface IPathInfo
    {
        /// <summary>
        /// The user profile folder.
        /// E.g: "/home/userName/" on Unix and "C:\Users\userName\" on Windows.
        /// </summary>
        string UserProfileDir { get; }

        /// <summary>
        /// Gets the path of template engine global settings directory.
        /// The directory should be used for settings shared between all template engine hosts.
        /// Usually at "C:\Users\userName\.templateengine\".
        /// </summary>
        string GlobalSettingsDir { get; }

        /// <summary>
        /// Gets the path of template engine host settings directory.
        /// The directory should be used for settings shared between all template engine host versions.
        /// E.g.: "C:\Users\userName\.templateengine\dotnet\".
        /// </summary>
        string HostSettingsDir { get; }

        /// <summary>
        /// Gets the path of template engine host version settings directory.
        /// The directory should be used for settings specific to certain host version.
        /// E.g.: "C:\Users\userName\.templateengine\dotnet\v1.0.0.0\".
        /// </summary>
        string HostVersionSettingsDir { get; }
    }
}
