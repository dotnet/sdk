// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal class ErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
    }

    internal class HelpEventArgs : EventArgs
    {
        public CommandLineOptionMessages CommandLineOptionMessages { get; set; }
    }
}
