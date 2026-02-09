// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Helper class for creating TaskEnvironment instances in tests.
// NOT gated with #if — always available in the test project.
// Adapted from: https://github.com/dotnet/msbuild/blob/main/src/UnitTests.Shared/TaskEnvironmentHelper.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Helper class for creating TaskEnvironment instances in tests.
    /// </summary>
    public static class TaskEnvironmentHelper
    {
        /// <summary>
        /// Creates a TaskEnvironment using the current working directory as the project directory.
        /// </summary>
        public static TaskEnvironment CreateForTest()
        {
            return CreateForTest(Directory.GetCurrentDirectory());
        }

        /// <summary>
        /// Creates a TaskEnvironment with the specified project directory.
        /// Uses reflection to work around internal visibility of ITaskEnvironmentDriver and TaskEnvironment ctor.
        /// </summary>
        public static TaskEnvironment CreateForTest(string projectDirectory)
        {
            // Get the internal ITaskEnvironmentDriver type from Microsoft.Build.Framework
            var driverInterfaceType = typeof(TaskEnvironment).Assembly
                .GetType("Microsoft.Build.Framework.ITaskEnvironmentDriver", throwOnError: true)!;

            // Create a DispatchProxy that implements ITaskEnvironmentDriver dynamically.
            // DispatchProxy.Create<TInterface, TProxy>() is called via reflection since TInterface is internal.
            var createMethod = typeof(DispatchProxy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(DispatchProxy.Create) && m.GetGenericArguments().Length == 2)
                .MakeGenericMethod(driverInterfaceType, typeof(TestDriverProxy));

            var proxy = createMethod.Invoke(null, null)!;

            // Initialize the proxy with the project directory
            ((TestDriverProxy)proxy).Initialize(projectDirectory);

            // Call the internal TaskEnvironment(ITaskEnvironmentDriver) constructor via reflection
            var ctor = typeof(TaskEnvironment).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
            return (TaskEnvironment)ctor[0].Invoke(new[] { proxy });
        }
    }

    /// <summary>
    /// DispatchProxy-based implementation of the internal ITaskEnvironmentDriver interface.
    /// Stores its own project directory independently from the process's CWD,
    /// enabling tests to verify tasks resolve paths relative to ProjectDirectory, not CWD.
    /// </summary>
    public class TestDriverProxy : DispatchProxy
    {
        private string _projectDirectory = string.Empty;

        internal void Initialize(string projectDirectory)
        {
            _projectDirectory = projectDirectory;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;

            return targetMethod.Name switch
            {
                "get_ProjectDirectory" => new AbsolutePath(_projectDirectory),
                "set_ProjectDirectory" => SetProjectDir(args),
                "GetAbsolutePath" => ResolveAbsolutePath((string)args![0]!),
                "GetEnvironmentVariable" => Environment.GetEnvironmentVariable((string)args![0]!),
                "GetEnvironmentVariables" => GetEnvVars(),
                "SetEnvironmentVariable" => DoSetEnvVar(args),
                "SetEnvironment" => DoSetEnv(args),
                "GetProcessStartInfo" => CreateProcessStartInfo(),
                "Dispose" => null,
                _ => null,
            };
        }

        private object? SetProjectDir(object?[]? args)
        {
            _projectDirectory = ((AbsolutePath)args![0]!).Value;
            return null;
        }

        private AbsolutePath ResolveAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
                return new AbsolutePath(path);
            return new AbsolutePath(path, new AbsolutePath(_projectDirectory));
        }

        private static IReadOnlyDictionary<string, string> GetEnvVars()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                    result[key] = value;
            }
            return result;
        }

        private static object? DoSetEnvVar(object?[]? args)
        {
            Environment.SetEnvironmentVariable((string)args![0]!, (string?)args[1]);
            return null;
        }

        private static object? DoSetEnv(object?[]? args)
        {
            var newEnv = (IDictionary<string, string>)args![0]!;
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && !newEnv.ContainsKey(key))
                    Environment.SetEnvironmentVariable(key, null);
            }
            foreach (var kvp in newEnv)
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            return null;
        }

        private ProcessStartInfo CreateProcessStartInfo()
        {
            var psi = new ProcessStartInfo { WorkingDirectory = _projectDirectory };
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                    psi.Environment[key] = value;
            }
            return psi;
        }
    }
}
