// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Shared constants for the dotnetup application.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// Mutex names used for synchronization.
    /// </summary>
    public static class MutexNames
    {
        /// <summary>
        /// Mutex used during the final installation phase to protect the manifest file and extracting folder(s).
        /// Mutex names MUST be valid file names on Unix. https://learn.microsoft.com/dotnet/api/system.threading.mutex.openexisting?view=net-9.0
        /// </summary>
        public const string ModifyInstallationStates = "Global\\DotnetupManifest";
    }

    /// <summary>
    /// Unicode symbols used in console output.
    /// </summary>
    public static class Symbols
    {
        public const string RightArrow = "\u2192"; // →
        public const string UpArrow = "\u2191"; // ↑
        public const string DownArrow = "\u2193"; // ↓
        public const string UpTriangle = "\u25B2"; // ▲
        public const string DownTriangle = "\u25BC"; // ▼
        public const string Bullet = "\u2022"; // •
    }
}
