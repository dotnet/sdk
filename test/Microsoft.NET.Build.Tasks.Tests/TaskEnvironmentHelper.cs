// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Thin convenience wrappers around MSBuild's real <see cref="TaskEnvironment"/> factory APIs.
    /// Prefer the underlying calls directly when test intent matters:
    ///   - <see cref="TaskEnvironment.Fallback"/>  → simulates multi-process mode (live process state).
    ///   - <see cref="TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(string, System.Collections.Generic.IDictionary{string, string})"/>
    ///     → simulates multi-threaded mode (isolated working directory + environment snapshot).
    /// </summary>
    public static class TaskEnvironmentHelper
    {
        /// <summary>
        /// Creates an isolated multi-threaded-style <see cref="TaskEnvironment"/> rooted at the
        /// current working directory. Equivalent to
        /// <see cref="TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(string, System.Collections.Generic.IDictionary{string, string})"/>
        /// with <see cref="Directory.GetCurrentDirectory"/> as the project directory.
        /// </summary>
        public static TaskEnvironment CreateForTest() =>
            TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(Directory.GetCurrentDirectory());

        /// <summary>
        /// Creates an isolated multi-threaded-style <see cref="TaskEnvironment"/> rooted at the
        /// specified project directory. Path resolution against this <see cref="TaskEnvironment"/> is
        /// independent of the process current working directory, mirroring what MSBuild injects in
        /// multi-threaded execution mode.
        /// </summary>
        public static TaskEnvironment CreateForTest(string projectDirectory) =>
            TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory);
    }
}
