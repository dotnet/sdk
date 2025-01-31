// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Json;
using Xunit.Sdk;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class DockerSupportsArchInlineData : DataAttribute
{
    private readonly string _arch;
    private readonly object[] _data;

    public DockerSupportsArchInlineData(string arch, params object[] data)
    {
        _arch = arch;
        _data = data;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (DockerSupportsArchHelper.DaemonSupportsArch(_arch))
        {
            return new object[][] { _data.Prepend(_arch).ToArray() };
        }
        else
        {
            base.Skip = $"Skipping test because Docker daemon does not support {_arch}.";
        }
        return Array.Empty<object[]>();
    }
}

internal static class DockerSupportsArchHelper
{
    internal static bool DaemonIsAvailable => ContainerCli.IsAvailable;

    internal static bool DaemonSupportsArch(string arch)
    {
        // an optimization - this doesn't change over time so we can compute it once
        string[] LinuxPlatforms = GetSupportedLinuxPlatforms();

        if (LinuxPlatforms.Contains(arch))
        {
            return true;
        }
        else
        {
            // another optimization - daemons don't switch types easily or quickly, so this is as good as static
            bool IsWindowsDockerDaemon = GetIsWindowsDockerDaemon();

            if (IsWindowsDockerDaemon && arch.StartsWith("windows", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
    }

    private static string[] GetSupportedLinuxPlatforms()
    {
        if (ContainerCli.IsPodman)
        {
            var inspectResult = new RunExeCommand(NullLogger.Instance, "podman", "info").Execute();
            inspectResult.Should().Pass();
            var platformsLine = inspectResult.StdOut!.Split(Environment.NewLine).First(x => x.Contains("OsArch:", StringComparison.OrdinalIgnoreCase));
            return new[] { platformsLine.Trim().Substring("OsArch: ".Length) };
        }
        else
        {
            var inspectResult = new RunExeCommand(NullLogger.Instance, "docker", "buildx", "inspect", "default").Execute();
            inspectResult.Should().Pass();
            var platformsLine = inspectResult.StdOut!.Split(Environment.NewLine).First(x => x.StartsWith("Platforms:", StringComparison.OrdinalIgnoreCase));
            return platformsLine.Substring("Platforms: ".Length).Split(",", StringSplitOptions.TrimEntries);
        }
    }

    private static bool GetIsWindowsDockerDaemon()
    {
        if (ContainerCli.IsPodman)
        {
            return false;
        }
        // the config json has an OSType property that is either "linux" or "windows" -
        // we can't use this for linux arch detection because that isn't enough information.
        var config = DockerCli.GetDockerConfig();
        if (config.RootElement.TryGetProperty("OSType", out JsonElement osTypeProperty))
        {
            return osTypeProperty.GetString() == "windows";
        }
        else
        {
            return false;
        }
    }

    private class NullLogger : ITestOutputHelper
    {
        private NullLogger() { }

        public static NullLogger Instance { get; } = new NullLogger();

        public void WriteLine(string message)
        {
            //do nothing
        }
        public void WriteLine(string format, params object[] args)
        {
            //do nothing
        }
    }
}
