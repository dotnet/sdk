// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

internal class DotNetHelper
{
    private static readonly object s_lockObj = new object();
    private static readonly string s_timestamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");

    public string DotNetPath { get; }

    public DotNetHelper(ITestOutputHelper outputHelper)
    {
        lock (s_lockObj)
        {
            if (!Directory.Exists(Config.DotNetDirectory))
            {
                if (!File.Exists(Config.DotNetTarballPath))
                {
                    throw new InvalidOperationException($"Tarball path '{Config.DotNetTarballPath}' specified in {Config.DotNetTarballPathEnv} does not exist.");
                }

                Directory.CreateDirectory(Config.DotNetDirectory);
                ExecuteHelper.ExecuteProcessValidateExitCode("tar", $"xzf {Config.DotNetTarballPath} -C {Config.DotNetDirectory}", outputHelper);
            }
        }

        DotNetPath = Path.Combine(Config.DotNetDirectory, "dotnet");
    }

    public void ExecuteCmd(string args, ITestOutputHelper outputHelper)
    {
        (Process Process, string StdOut, string StdErr) executeResult = ExecuteHelper.ExecuteProcess(DotNetPath, args, outputHelper);

        Assert.Equal(0, executeResult.Process.ExitCode);
    }

    /// <summary>
    /// Create a new .NET project and return the path to the created project folder.
    /// </summary>
    public string ExecuteNew(string projectType, string language, string name, ITestOutputHelper outputHelper)
    {
        string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), $"projects-{s_timestamp}", name);

        ExecuteCmd($"new {projectType} --language \"{language}\" --name {name} --output {outputDirectory}", outputHelper);

        return outputDirectory;
    }
}
