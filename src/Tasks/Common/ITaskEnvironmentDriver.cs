// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a polyfill for the ITaskEnvironmentDriver interface from MSBuild.
// See: https://github.com/dotnet/msbuild/blob/main/src/Framework/ITaskEnvironmentDriver.cs

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Internal interface for managing task execution environment, including environment variables and working directory.
    /// </summary>
    internal interface ITaskEnvironmentDriver : IDisposable
    {
        /// <summary>
        /// Gets or sets the current working directory for the task environment.
        /// </summary>
        AbsolutePath ProjectDirectory { get; set; }

        /// <summary>
        /// Gets an absolute path from the specified path, resolving relative paths against the current project directory.
        /// </summary>
        AbsolutePath GetAbsolutePath(string path);

        /// <summary>
        /// Gets the value of the specified environment variable.
        /// </summary>
        string? GetEnvironmentVariable(string name);

        /// <summary>
        /// Gets all environment variables for this task environment.
        /// </summary>
        IReadOnlyDictionary<string, string> GetEnvironmentVariables();

        /// <summary>
        /// Sets an environment variable to the specified value.
        /// </summary>
        void SetEnvironmentVariable(string name, string? value);

        /// <summary>
        /// Sets the environment to match the specified collection of variables.
        /// </summary>
        void SetEnvironment(IDictionary<string, string> newEnvironment);

        /// <summary>
        /// Gets a ProcessStartInfo configured with the current environment and working directory.
        /// </summary>
        ProcessStartInfo GetProcessStartInfo();
    }
}

#endif
