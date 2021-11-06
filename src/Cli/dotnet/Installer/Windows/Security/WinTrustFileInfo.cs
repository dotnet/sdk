// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Installer.Windows.Security
{
    /// <summary>
    /// The structure to use when calling WinVerifyTrust to verify an individual file. See 
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-wintrust_file_info">this</see> for
    /// furhter details.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WinTrustFileInfo
    {
        /// <summary>
        /// The size, in bytes, of the structure.
        /// </summary>
        public uint cbStruct;

        /// <summary>
        /// The full path and file name of the file to verify. This field cannot be <see langword="null"/>.
        /// </summary>
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pcwszFilePath;

        /// <summary>
        /// Optional handle to an open file to be verified. The handle must have read permissions.
        /// </summary>
        public IntPtr hFile;

        /// <summary>
        /// Optional  pointer to a GUID structure that specifies the subject type.
        /// </summary>
        public IntPtr pgKnownSubject;
    }
}
