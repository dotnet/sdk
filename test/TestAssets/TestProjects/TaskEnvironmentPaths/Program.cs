// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

class Program
{
    static int Main(string[] args)
    {
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", bool.Parse(args[0]));
        Validate(args[1]);
        Validate(args[2]);
        Console.WriteLine("Path validation and project isolation preserved.");
        return 0;
    }

    static void Validate(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        var resolver = assembly.GetType("Microsoft.NET.Build.Tasks.TaskEnvironmentExtensions", throwOnError: true)
            .GetMethod("GetAbsolutePathPreservingInvalidArguments", BindingFlags.Public | BindingFlags.Static);
        string root = Path.Combine(Environment.CurrentDirectory, assembly.GetName().Name);
        string originalDirectory = Environment.CurrentDirectory;
        string[] paths = { null, "", " ", "  ", "\t", "\u0085", "\u00A0", "\u2003", "\u3000", "\u2003 ", "ordinary" };

        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                for (int operation = 0; operation < 2; operation++)
                {
                    string directory = Path.Combine(root, i + "-" + operation);
                    string rawDirectory = Directory.CreateDirectory(Path.Combine(directory, "raw")).FullName;
                    string projectDirectory = Directory.CreateDirectory(Path.Combine(directory, "project")).FullName;
                    string decoyDirectory = Directory.CreateDirectory(Path.Combine(directory, "decoy")).FullName;
                    string path = paths[i];
                    Action<string> usePath = operation == 0
                        ? new Action<string>(p => Directory.CreateDirectory(p))
                        : p => File.WriteAllBytes(p, new byte[] { 42 });

                    Environment.CurrentDirectory = rawDirectory;
                    ArgumentException expected = Capture(() => usePath(path));
                    Environment.CurrentDirectory = decoyDirectory;
                    var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDirectory);
                    string resolved = (string)resolver.Invoke(null, new object[] { environment, path });
                    ArgumentException actual = Capture(() => usePath(resolved));

                    if (expected == null)
                    {
                        if (actual != null || Path.GetFullPath(resolved) != Path.GetFullPath(Path.Combine(projectDirectory, path)))
                        {
                            throw new Exception("Accepted path was not resolved against the project: " + i);
                        }
                    }
                    else if (actual == null || actual.GetType() != expected.GetType() ||
                        actual.Message != expected.Message || actual.ParamName != expected.ParamName)
                    {
                        throw new Exception("Argument validation changed for path " + i + " in " + assemblyPath);
                    }

                    if (Directory.GetFileSystemEntries(decoyDirectory).Length != 0 ||
                        (expected != null && Directory.GetFileSystemEntries(projectDirectory).Length != 0))
                    {
                        throw new Exception("Unexpected filesystem side effects for path " + i);
                    }
                }
            }
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    static ArgumentException Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (ArgumentException exception)
        {
            return exception;
        }
    }
}
