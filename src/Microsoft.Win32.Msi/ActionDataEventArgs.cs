// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Represents event data associated with the ActionData message.
    /// </summary>
    public class ActionDataEventArgs : InstallMessageEventArgs
    {
        public ActionDataEventArgs(string message, InstallMessage messageType, MessageBox style) : 
            base(message, messageType, style)
        {

        }
    }
}
