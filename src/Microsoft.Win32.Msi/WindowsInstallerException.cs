// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Throws an exception for a Win32 error code.
    /// </summary>
    public class WindowsInstallerException : Win32Exception
    {
        public WindowsInstallerException() : base()
        {

        }

        public WindowsInstallerException(int error) : base(error)
        {

        }

        public WindowsInstallerException(string? message) : base(message)
        {

        }

        public WindowsInstallerException(int error, string? message) : base(error, message)
        {

        }

        public WindowsInstallerException(string? message, Exception? innerException) : base(message, innerException)
        {

        }
    }
}
