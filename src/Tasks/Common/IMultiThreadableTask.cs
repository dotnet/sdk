// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a polyfill for the IMultiThreadableTask interface from MSBuild.
// MSBuild detects this interface by its namespace and name only, ignoring the defining assembly.
// This allows us to use the interface before the MSBuild version containing it is available.
// See: https://github.com/dotnet/msbuild/blob/main/src/Framework/IMultiThreadableTask.cs

// This polyfill is only needed for .NET Framework builds since the newer MSBuild packages
// for .NET Core already include the interface. When targeting .NET Core, we use the
// interface from Microsoft.Build.Framework directly.

#if NETFRAMEWORK

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks that can execute in a thread-safe manner within MSBuild's multithreaded execution model.
    /// Tasks that implement this interface declare their capability to run in multiple threads within one process.
    /// </summary>
    internal interface IMultiThreadableTask : ITask
    {
        /// <summary>
        /// Gets or sets the task execution environment, which provides access to project current directory
        /// and environment variables in a thread-safe manner.
        /// </summary>
        TaskEnvironment TaskEnvironment { get; set; }
    }
}

#endif
