// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Provides a default TaskEnvironment for single-threaded MSBuild execution.
// When MSBuild supports IMultiThreadableTask, it sets TaskEnvironment directly.
// This fallback ensures tasks work with older MSBuild versions that do not set it.

#if NETFRAMEWORK

using System;

namespace Microsoft.Build.Framework
{
    internal static class TaskEnvironmentDefaults
    {
        /// <summary>
        /// Creates a default TaskEnvironment backed by the current process environment.
        /// Uses Environment.CurrentDirectory as the project directory, which in single-threaded
        /// MSBuild is set to the project directory before task execution.
        /// </summary>
        internal static TaskEnvironment Create() =>
            new TaskEnvironment(new ProcessTaskEnvironmentDriver(Environment.CurrentDirectory));
    }
}

#endif
