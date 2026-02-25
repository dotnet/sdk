// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This is a polyfill for the MultiProcessTaskEnvironmentDriver from MSBuild.
// Adapted for use in the SDK tasks project where NativeMethodsShared is not available.
// See: https://github.com/dotnet/msbuild/blob/main/src/Build/BackEnd/TaskExecutionHost/MultiProcessTaskEnvironmentDriver.cs

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Default implementation of <see cref="ITaskEnvironmentDriver"/> that directly interacts with the file system
    /// and environment variables. Used for multi-process mode and as a test helper.
    /// </summary>
    internal sealed class ProcessTaskEnvironmentDriver : ITaskEnvironmentDriver
    {
        private AbsolutePath _projectDirectory;
        private readonly Dictionary<string, string> _environmentVariables;

        /// <summary>
        /// Initializes a new instance with the specified project directory.
        /// </summary>
        public ProcessTaskEnvironmentDriver(string projectDirectory)
        {
            _projectDirectory = new AbsolutePath(projectDirectory);

            // Seed from the current process environment
            _environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    _environmentVariables[key] = value;
                }
            }
        }

        /// <inheritdoc/>
        public AbsolutePath ProjectDirectory
        {
            get => _projectDirectory;
            set => _projectDirectory = value;
        }

        /// <inheritdoc/>
        public AbsolutePath GetAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return new AbsolutePath(path);
            }

            return new AbsolutePath(path, _projectDirectory);
        }

        /// <inheritdoc/>
        public string? GetEnvironmentVariable(string name)
        {
            return _environmentVariables.TryGetValue(name, out var value) ? value : null;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return new Dictionary<string, string>(_environmentVariables, StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public void SetEnvironmentVariable(string name, string? value)
        {
            if (value == null)
            {
                _environmentVariables.Remove(name);
            }
            else
            {
                _environmentVariables[name] = value;
            }
        }

        /// <inheritdoc/>
        public void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            _environmentVariables.Clear();
            foreach (var kvp in newEnvironment)
            {
                _environmentVariables[kvp.Key] = kvp.Value;
            }
        }

        /// <inheritdoc/>
        public ProcessStartInfo GetProcessStartInfo()
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = _projectDirectory.Value,
            };

            // Populate environment from the scoped environment dictionary
            foreach (var kvp in _environmentVariables)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            return startInfo;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // No resources to clean up in this implementation.
        }
    }
}

#endif
