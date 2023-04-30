// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Defines event data for a progress message. 
    /// </summary>
    public class ProgressEventArgs : InstallMessageEventArgs
    {
        /// <summary>
        /// Returns the type of progress message (derived from the first field).
        /// </summary>
        public ProgressType ProgressType => (ProgressType)Fields[0];

        /// <summary>
        /// The message fields.
        /// </summary>
        public readonly int[] Fields;

        public ProgressEventArgs(string message, InstallMessage messageType, MessageBox style) : 
            base(message, messageType, style)
        {            
            // Progress messages have up to 4 fields and are formatted as "<field>: <fieldValue>", e.g.
            // "1: 2 2: 25 3: 0 4: 1". Not all fields may be present and some fields have no meaning
            // depending on the message subtype (even though they are present).
            // 
            // See https://docs.microsoft.com/en-us/windows/win32/msi/parsing-windows-installer-messages
            Match match = Regex.Match(message, @"(?<field>\d):\s+(?<value>\d+)");

            List<int> fields = new();

            while (match.Success)
            {
                fields.Add(Convert.ToInt32(match.Groups["value"].Value));
                match = match.NextMatch();
            }

            Fields = fields.ToArray();
        }
    }
}
