// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.Build.Logging.StructuredLogger;
using MSBuildProject = Microsoft.Build.Logging.StructuredLogger.Project;
using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal record CscInvocation(string ProjectName, string ProjectDirectory, string[] CommandLineArgs);

    internal static class BinlogParser
    {
        public static List<CscInvocation> ExtractCscInvocations(string binlogPath)
        {
            var results = new List<CscInvocation>();
            var build = BinaryLog.ReadBuild(binlogPath);

            build.VisitAllChildren<MSBuildTask>(task =>
            {
                if (task.Name == "Csc")
                {
                    var project = task.GetNearestParent<MSBuildProject>();
                    var projectName = project?.Name ?? "Unknown";
                    var projectDirectory = project?.ProjectDirectory ?? Environment.CurrentDirectory;

                    var commandLine = GetCscCommandLine(task);
                    if (commandLine.Length > 0)
                    {
                        results.Add(new CscInvocation(projectName, projectDirectory, commandLine));
                    }
                }
            });

            return results;
        }

        private static string[] GetCscCommandLine(MSBuildTask cscTask)
        {
            // Look for CommandLineArguments property which contains the full csc command line
            foreach (var child in cscTask.Children)
            {
                if (child is Property prop && prop.Name == "CommandLineArguments")
                {
                    var cmdLine = prop.Value;
                    // Strip the compiler executable path (everything before the first space)
                    var firstSpace = cmdLine.IndexOf(' ');
                    if (firstSpace > 0)
                    {
                        cmdLine = cmdLine.Substring(firstSpace + 1);
                    }
                    return CscCommandLineParser.Parse(cmdLine);
                }
            }

            // Fallback: build from Parameters folder
            var args = new List<string>();
            foreach (var child in cscTask.Children)
            {
                if (child is Folder folder && folder.Name == "Parameters")
                {
                    foreach (var param in folder.Children.OfType<Property>())
                    {
                        if (param.Name == "Sources" || param.Name == "Analyzers")
                            continue;
                        args.Add($"/{param.Name}:{param.Value}");
                    }

                    foreach (var param in folder.Children.OfType<Parameter>())
                    {
                        if (param.Name == "Sources")
                        {
                            foreach (var item in param.Children.OfType<Item>())
                            {
                                args.Add(item.Name);
                            }
                        }
                    }
                }
            }

            return args.ToArray();
        }
    }
}
