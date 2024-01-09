// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents a type used to log setup operations that can manage a series of named pipes writing to it.
    /// </summary>
    internal interface ISynchronizingLogger : ISetupLogger
    {
        /// <summary>
        /// Starts a new thread to listen for log requests messages from external processes.
        /// </summary>
        /// <param name="pipeName">The name of the pipe.</param>
        void AddNamedPipe(string pipeName);
    }
}
