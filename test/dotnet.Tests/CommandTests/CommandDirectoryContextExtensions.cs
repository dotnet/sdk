// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

internal static class CommandDirectoryContextExtensions
{
    extension(CommandDirectoryContext)
    {
        /// <summary>
        /// Meant to be used only in unit test to remove dependency on special characters in the absolute repo path
        /// The overwrite will only affect the current thread.
        /// </summary>
        /// <param name="basePath">Directory to be used as base path instead of the current working directory.</param>
        /// <param name="action">Action to be executed with the overwritten directory in place.</param>
        public static void PerformActionWithBasePath(string basePath, Action action)
        {
            if (CommandDirectoryContext.CurrentBaseDirectory_TestOnly != null)
            {
                throw new InvalidOperationException(
                    $"Calls to {nameof(CommandDirectoryContext)}.{nameof(PerformActionWithBasePath)} cannot be nested.");
            }

            CommandDirectoryContext.CurrentBaseDirectory_TestOnly = basePath;
            Telemetry.Telemetry.CurrentSessionId = null;
            try
            {
                action();
            }
            finally
            {
                CommandDirectoryContext.CurrentBaseDirectory_TestOnly = null;
            }
        }
    }
}
