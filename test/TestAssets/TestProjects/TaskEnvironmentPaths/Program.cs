// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

class Program
{
    static void Main(string[] args)
    {
        bool legacy = bool.Parse(args[0]);
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", legacy);
        var resolver = Assembly.LoadFrom(args[1])
            .GetType("Microsoft.NET.Build.Tasks.TaskEnvironmentExtensions", throwOnError: true)
            .GetMethod("GetAbsolutePathPreservingInvalidArguments", BindingFlags.Public | BindingFlags.Static);
        string projectDirectory = Path.Combine(Environment.CurrentDirectory, "project");
        var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory);

        Check(" ", " ");
        Check("\u2003", legacy ? Path.Combine(projectDirectory, "\u2003") : "\u2003");

        void Check(string path, string expected)
        {
            string actual = (string)resolver.Invoke(null, new object[] { environment, path });
            if (actual != expected)
            {
                throw new InvalidOperationException($"U+{(int)path[0]:X4} (legacy={legacy}): expected '{expected}', got '{actual}'.");
            }
        }
    }
}
