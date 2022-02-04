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
    public string DotNetPath { get; }
    public string DotNetInstallDirectory { get; }

    public DotNetHelper(ITestOutputHelper outputHelper)
    {
        if (!Directory.Exists(Config.DotNetDirectory))
        {
            if (!File.Exists(Config.DotNetTarballPath))
            {
                throw new InvalidOperationException($"Tarball path '{Config.DotNetTarballPath}' specified in {Config.DotNetTarballPathEnv} does not exist.");
            }

            Directory.CreateDirectory(Config.DotNetDirectory);
            ExecuteHelper.ExecuteProcess("tar", $"xzf {Config.DotNetTarballPath} -C {Config.DotNetDirectory}", outputHelper);
        }

        DotNetInstallDirectory = Path.Combine(Directory.GetCurrentDirectory(), Config.DotNetDirectory);
        DotNetPath = Path.Combine(DotNetInstallDirectory, "dotnet");
    }

    public void ExecuteDotNetCmd(string args, ITestOutputHelper outputHelper)
    {
        (Process Process, string StdOut, string StdErr) executeResult = ExecuteHelper.ExecuteProcess(DotNetPath, args, outputHelper);

        Assert.Equal(0, executeResult.Process.ExitCode);
    }
}
