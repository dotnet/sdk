// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Concurrent;
using Microsoft.Build.Framework;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
    internal class RuntimeGraphCache
    {
        private static readonly ConcurrentDictionary<string, object> s_keyLocks = new();

        private IBuildEngine4 _buildEngine;
        private Logger _log;

        public RuntimeGraphCache(TaskBase task)
        {
            _buildEngine = task.BuildEngine4;
            _log = task.Log;
        }

        public RuntimeGraph GetRuntimeGraph(string runtimeJsonPath)
        {
            if (string.IsNullOrEmpty(runtimeJsonPath))
            {
                throw new ArgumentNullException(nameof(runtimeJsonPath));
            }
            if (!Path.IsPathRooted(runtimeJsonPath))
            {
                throw new BuildErrorException("Path not rooted: {0}", runtimeJsonPath);
            }

            string key = GetTaskObjectKey(runtimeJsonPath);

            object keyLock = s_keyLocks.GetOrAdd(key, static _ => new object());
            lock (keyLock)
            {
                RuntimeGraph result;
                object existingRuntimeGraphTaskObject = _buildEngine.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.AppDomain);
                if (existingRuntimeGraphTaskObject == null)
                {
                    result = JsonRuntimeFormat.ReadRuntimeGraph(runtimeJsonPath);

                    _buildEngine.RegisterTaskObject(key, result, RegisteredTaskObjectLifetime.AppDomain, true);
                }
                else
                {
                    result = (RuntimeGraph)existingRuntimeGraphTaskObject;
                }

                return result;
            }
        }

        private static string GetTaskObjectKey(string runtimeJsonPath)
        {
            return $"{nameof(RuntimeGraphCache)}:{runtimeJsonPath}";
        }
    }
}
