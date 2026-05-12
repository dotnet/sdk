// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a polyfill for the TaskEnvironment class from MSBuild.
// See: https://github.com/dotnet/msbuild/blob/main/src/Framework/TaskEnvironment.cs

#if NETFRAMEWORK

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Provides an <see cref="IMultiThreadableTask"/> with access to a run-time execution environment including
    /// environment variables, file paths, and process management capabilities.
    /// </summary>
    public sealed class TaskEnvironment
    {
        private readonly ITaskEnvironmentDriver _driver;

        /// <summary>
        /// Initializes a new instance of the TaskEnvironment class.
        /// </summary>
        internal TaskEnvironment(ITaskEnvironmentDriver driver)
        {
            _driver = driver;
        }

        /// <summary>
        /// Gets or sets the project directory for the task execution.
        /// </summary>
        public AbsolutePath ProjectDirectory
        {
            get => _driver.ProjectDirectory;
            internal set => _driver.ProjectDirectory = value;
        }

        /// <summary>
        /// Converts a relative or absolute path string to an absolute path.
        /// This function resolves paths relative to <see cref="ProjectDirectory"/>.
        /// </summary>
        public AbsolutePath GetAbsolutePath(string path) => _driver.GetAbsolutePath(path);

        /// <summary>
        /// Gets the value of an environment variable.
        /// </summary>
        public string? GetEnvironmentVariable(string name) => _driver.GetEnvironmentVariable(name);

        /// <summary>
        /// Gets a dictionary containing all environment variables.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables() => _driver.GetEnvironmentVariables();

        /// <summary>
        /// Sets the value of an environment variable.
        /// </summary>
        public void SetEnvironmentVariable(string name, string? value) => _driver.SetEnvironmentVariable(name, value);

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// </summary>
        internal void SetEnvironment(IDictionary<string, string> newEnvironment) => _driver.SetEnvironment(newEnvironment);

        /// <summary>
        /// Creates a new ProcessStartInfo configured for the current task execution environment.
        /// </summary>
        public ProcessStartInfo GetProcessStartInfo() => _driver.GetProcessStartInfo();

        /// <summary>
        /// Disposes the underlying driver, cleaning up any thread-local state.
        /// </summary>
        internal void Dispose() => _driver.Dispose();
    }
}

#endif
