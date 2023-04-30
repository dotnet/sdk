// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Specifies the level of complexity of the user interface.
    /// </summary>
    [Flags]
    public enum InstallUILevel
    {
        /// <summary>
        /// UI level is unchanged.
        /// </summary>
        NoChange = 0,
        /// <summary>
        /// Default UI is used.
        /// </summary>
        Default = 1,
        /// <summary>
        /// Completely silent installation. 
        /// </summary>
        None = 2,
        /// <summary>
        /// Simple progress and error handling. 
        /// </summary>
        Basic = 3,
        /// <summary>
        /// Authored UI, wizard dialogs suppressed.
        /// </summary>
        Reduced = 4,
        /// <summary>
        /// Authored UI with wizards, progress, and errors.
        /// </summary>
        Full = 5,
        /// <summary>
        /// Display success/failure dialog at the end of the install.
        /// </summary>
        EndDialog = 0x80,
        /// <summary>
        /// Only display the progress dialog. No modal or error dialog boxes are displayed.
        /// </summary>
        ProgressOnly = 0x40,
        /// <summary>
        /// Do not display the cancel button in basic UI.
        /// </summary>
        HideCancel = 0x20,
        /// <summary>
        /// Force display of source resolution when combined with <see cref="None"/>, otherwise it has no effect.
        /// </summary>
        SourceResolutionOnly = 0x100,
        /// <summary>
        /// Show UAC prompt even if quiet. Requires Windows Installer 5.0.
        /// </summary>
        /// <remarks>
        /// Requires Windows Installer 5.0
        /// </remarks>
        UacOnly = 0x200
    }
}
