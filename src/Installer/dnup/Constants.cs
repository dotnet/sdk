// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    /// <summary>
    /// Shared constants for the dnup application.
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
            /// </summary>
            public const string ModifyInstallationStates = "Global\\DnupFinalize";
        }
    }
}
