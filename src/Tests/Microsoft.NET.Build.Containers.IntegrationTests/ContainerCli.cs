﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Diagnostics;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

static class ContainerCli
{
    public static bool IsPodman => _isPodman.Value;

    public static RunExeCommand PullCommand(ITestOutputHelper log, params string[] args)
      => CreateCommand(log, "pull", args);

    public static RunExeCommand TagCommand(ITestOutputHelper log, params string[] args)
      => CreateCommand(log, "tag", args);

    public static RunExeCommand PushCommand(ITestOutputHelper log, params string[] args)
      => CreateCommand(log, "push", args);

    public static RunExeCommand StopCommand(ITestOutputHelper log, params string[] args)
      => CreateCommand(log, "stop", args);

    public static RunExeCommand RunCommand(ITestOutputHelper log, params string[] args)
      => CreateCommand(log, "run", args);

    public static RunExeCommand LogsCommand(ITestOutputHelper log, params string[] args)
      => CreateCommand(log, "logs", args);

    private static RunExeCommand CreateCommand(ITestOutputHelper log, string command, string[] args)
    {
        bool isPodman = _isPodman.Value;

        string commandPath = _isPodman.Value ? "podman" : "docker";

        // The local registry is not accessible via https.
        // Podman doesn't want to use it unless we set 'tls-verify' to 'false'.
        if (isPodman && (command == "push" || command == "pull"))
        {
            if (args.Length > 0)
            {
                string image = args[args.Length - 1];
                if (image.StartsWith($"{DockerRegistryManager.LocalRegistry}/"))
                {
                    args = new[] { "--tls-verify=false" }.Concat(args).ToArray();
                }
            }
        }

        return new RunExeCommand(log, commandPath, new[] { command }.Concat(args).ToArray());
    }

    private static readonly Lazy<bool> _isPodman = new(() =>
    {
        try
        {
            ProcessStartInfo psi = new("podman", "version")
            {
                  RedirectStandardOutput = true,
                  RedirectStandardError = true
            };
            using var process = Process.Start(psi)!;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        { }
        return false;
    });
}
