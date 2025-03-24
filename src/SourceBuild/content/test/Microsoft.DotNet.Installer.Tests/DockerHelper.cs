// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Installer.Tests;

public class DockerHelper
{
    public static string DockerOS => GetDockerOS();
    public static string DockerArchitecture => GetDockerArch();
    public static string ContainerWorkDir => IsLinuxContainerModeEnabled ? "/sandbox" : "c:\\sandbox";
    public static bool IsLinuxContainerModeEnabled => string.Equals(DockerOS, "linux", StringComparison.OrdinalIgnoreCase);
    public static string TestArtifactsDir { get; } = Path.Combine(Directory.GetCurrentDirectory(), "TestAppArtifacts");

    private ITestOutputHelper OutputHelper { get; set; }

    public DockerHelper(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    public void Build(
        string tag,
        string? dockerfile = null,
        string? target = null,
        string contextDir = ".",
        bool pull = false,
        string? platform = null,
        params string[] buildArgs)
    {
        string buildArgsOption = string.Empty;
        if (buildArgs != null)
        {
            foreach (string arg in buildArgs)
            {
                buildArgsOption += $" --build-arg {arg}";
            }
        }

        string platformOption = string.Empty;
        if (platform is not null)
        {
            platformOption = $" --platform {platform}";
        }

        string targetArg = target == null ? string.Empty : $" --target {target}";
        string dockerfileArg = dockerfile == null ? string.Empty : $" -f {dockerfile}";
        string pullArg = pull ? " --pull" : string.Empty;

        ExecuteWithLogging($"build -t {tag}{targetArg}{buildArgsOption}{dockerfileArg}{pullArg}{platformOption} {contextDir}");
    }


    public static bool ContainerExists(string name) => ResourceExists("container", $"-f \"name={name}\"");

    public static bool ContainerIsRunning(string name) => Execute($"inspect --format=\"{{{{.State.Running}}}}\" {name}") == "true";

    public void Copy(string src, string dest) => ExecuteWithLogging($"cp {src} {dest}");

    public void DeleteContainer(string container, bool captureLogs = false)
    {
        if (ContainerExists(container))
        {
            if (captureLogs)
            {
                ExecuteWithLogging($"logs {container}", ignoreErrors: true);
            }

            // If a container is already stopped, running `docker stop` again has no adverse effects.
            // This prevents some issues where containers could fail to be forcibly removed while they're running.
            // e.g. https://github.com/dotnet/dotnet-docker/issues/5127
            StopContainer(container);

            ExecuteWithLogging($"container rm -f {container}");
        }
    }

    public void DeleteImage(string tag)
    {
        if (ImageExists(tag))
        {
            ExecuteWithLogging($"image rm -f {tag}");
        }
    }

    private void StopContainer(string container)
    {
        if (ContainerExists(container))
        {
            ExecuteWithLogging($"stop {container}", autoRetry: true);
        }
    }

    private static string Execute(
        string args, bool ignoreErrors = false, bool autoRetry = false, ITestOutputHelper? outputHelper = null)
    {
        (Process Process, string StdOut, string StdErr) result;
        if (autoRetry)
        {
            result = ExecuteWithRetry(args, outputHelper!, ExecuteProcess);
        }
        else
        {
            result = ExecuteProcess(args, outputHelper!);
        }

        if (!ignoreErrors && result.Process.ExitCode != 0)
        {
            ProcessStartInfo startInfo = result.Process.StartInfo;
            string msg = $"Failed to execute {startInfo.FileName} {startInfo.Arguments}" +
                $"{Environment.NewLine}Exit code: {result.Process.ExitCode}" +
                $"{Environment.NewLine}Standard Error: {result.StdErr}";
            throw new InvalidOperationException(msg);
        }

        return result.StdOut;
    }

    private static (Process Process, string StdOut, string StdErr) ExecuteProcess(
        string args, ITestOutputHelper outputHelper) => ExecuteProcess("docker", args, outputHelper);

    private string ExecuteWithLogging(string args, bool ignoreErrors = false, bool autoRetry = false)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        OutputHelper.WriteLine($"Executing: docker {args}");
        string result = Execute(args, outputHelper: OutputHelper, ignoreErrors: ignoreErrors, autoRetry: autoRetry);

        stopwatch.Stop();
        OutputHelper.WriteLine($"Execution Elapsed Time: {stopwatch.Elapsed}");

        return result;
    }

    private static (Process Process, string StdOut, string StdErr) ExecuteWithRetry(
        string args,
        ITestOutputHelper outputHelper,
        Func<string, ITestOutputHelper, (Process Process, string StdOut, string StdErr)> executor)
    {
        const int maxRetries = 5;
        const int waitFactor = 5;

        int retryCount = 0;

        (Process Process, string StdOut, string StdErr) result = executor(args, outputHelper);
        while (result.Process.ExitCode != 0)
        {
            retryCount++;
            if (retryCount >= maxRetries)
            {
                break;
            }

            int waitTime = Convert.ToInt32(Math.Pow(waitFactor, retryCount - 1));
            if (outputHelper != null)
            {
                outputHelper.WriteLine($"Retry {retryCount}/{maxRetries}, retrying in {waitTime} seconds...");
            }

            Thread.Sleep(waitTime * 1000);
            result = executor(args, outputHelper!);
        }

        return result;
    }

    private static (Process Process, string StdOut, string StdErr) ExecuteProcess(
    string fileName, string args, ITestOutputHelper outputHelper)
    {
        Process process = new Process
        {
            EnableRaisingEvents = true,
            StartInfo =
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        StringBuilder stdOutput = new StringBuilder();
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => stdOutput.AppendLine(e.Data));

        StringBuilder stdError = new StringBuilder();
        process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => stdError.AppendLine(e.Data));

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        string output = stdOutput.ToString().Trim();
        if (outputHelper != null && !string.IsNullOrWhiteSpace(output))
        {
            outputHelper.WriteLine(output);
        }

        string error = stdError.ToString().Trim();
        if (outputHelper != null && !string.IsNullOrWhiteSpace(error))
        {
            outputHelper.WriteLine(error);
        }

        return (process, output, error);
    }

    private static string GetDockerOS() => Execute("version -f \"{{ .Server.Os }}\"");
    private static string GetDockerArch() => Execute("version -f \"{{ .Server.Arch }}\"");

    public string GetImageUser(string image) => ExecuteWithLogging($"inspect -f \"{{{{ .Config.User }}}}\" {image}");

    public static bool ImageExists(string tag) => ResourceExists("image", tag);

    private static bool ResourceExists(string type, string filterArg)
    {
        string output = Execute($"{type} ls -a -q {filterArg}", true);
        return output != "";
    }

    public string Run(
        string image,
        string name,
        string? command = null,
        string? workdir = null,
        string? optionalRunArgs = null,
        bool detach = false,
        string? runAsUser = null,
        bool skipAutoCleanup = false,
        bool useMountedDockerSocket = false,
        bool silenceOutput = false,
        bool tty = true)
    {
        string cleanupArg = skipAutoCleanup ? string.Empty : " --rm";
        string detachArg = detach ? " -d" : string.Empty;
        string ttyArg = detach && tty ? " -t" : string.Empty;
        string userArg = runAsUser != null ? $" -u {runAsUser}" : string.Empty;
        string workdirArg = workdir == null ? string.Empty : $" -w {workdir}";
        string mountedDockerSocketArg = useMountedDockerSocket ? " -v /var/run/docker.sock:/var/run/docker.sock" : string.Empty;
        if (silenceOutput)
        {
            return Execute(
                $"run --name {name}{cleanupArg}{workdirArg}{userArg}{detachArg}{ttyArg}{mountedDockerSocketArg} {optionalRunArgs} {image} {command}");
        }
        return ExecuteWithLogging(
            $"run --name {name}{cleanupArg}{workdirArg}{userArg}{detachArg}{ttyArg}{mountedDockerSocketArg} {optionalRunArgs} {image} {command}", ignoreErrors: true);
    }
}
