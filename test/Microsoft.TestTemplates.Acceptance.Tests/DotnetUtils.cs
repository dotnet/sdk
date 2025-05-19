// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.TestTemplates.Acceptance.Tests;
public static partial class DotnetUtils
{
    /// <summary>
    /// Invokes <c>dotnet test</c> with specified arguments.
    /// </summary>
    /// <param name="arguments">Arguments provided to <c>dotnet</c>.exe</param>
    public static ExecutionResult InvokeDotnetTest(string arguments)
        => InvokeDotnet("test --logger:Console;Verbosity=Detailed " + arguments);

    /// <summary>
    /// Invokes <c>dotnet new install</c> with specified arguments.
    /// </summary>
    /// <param name="arguments"></param>
    public static ExecutionResult InvokeDotnetNewInstall(string arguments)
        => InvokeDotnet("new install " + arguments);

    /// <summary>
    /// Invokes <c>dotnet new uninstall</c> with specified arguments.
    /// </summary>
    /// <param name="arguments"></param>
    public static ExecutionResult InvokeDotnetNewUninstall(string arguments, bool assertExecution = true)
        => InvokeDotnet("new uninstall " + arguments, assertExecution);

    /// <summary>
    /// Invokes <c>dotnet new</c> with specified arguments.
    /// </summary>
    /// <param name="templateName">Name of project or item template</param>
    /// <param name="nameAs">The name for the output being created</param>
    /// <param name="targetFramework">The target framework for the project</param>
    /// <param name="language">Filters templates based on language and specifies the language of the template to create.</param>
    /// <param name="outputDirectory">Location to place the generated output.</param>
    public static ExecutionResult InvokeDotnetNew(string templateName, string nameAs, string? targetFramework = null, string? language = null,
        string? outputDirectory = null, bool assertExecution = true)
    {
        var targetArgs = string.IsNullOrEmpty(targetFramework) ? "" : $" -f {targetFramework}";
        var languageArgs = string.IsNullOrEmpty(language) ? "" : $" -lang {language}";
        var outputArgs = string.IsNullOrEmpty(outputDirectory) ? "" : $" -o {outputDirectory}";
        return InvokeDotnet($"new {templateName} -n {nameAs}{targetArgs}{languageArgs}{outputArgs}", assertExecution);
    }

    /// <summary>
    /// Invokes <c>dotnet</c> with specified arguments.
    /// </summary>
    /// <param name="arguments">Arguments provided to <c>dotnet</c>.exe</param>
    private static ExecutionResult InvokeDotnet(string args, bool assertExecution = true)
    {
        Execute(args, out var standardTestOutput, out var standardTestError, out var runnerExitCode);

        ExecutionResult executionResult = new(args, standardTestOutput, standardTestError, runnerExitCode);
        if (assertExecution)
        {
            Assert.Equal(0, runnerExitCode);
        }

        return executionResult;
    }

    private static void Execute(string args, out string stdOut, out string stdError, out int exitCode)
    {
        using Process dotnet = new();
        Console.WriteLine("AcceptanceTestBase.Execute: Starting dotnet.exe");
        dotnet.StartInfo.FileName = GetDotnetExePath();
        dotnet.StartInfo.Arguments = args;
        dotnet.StartInfo.UseShellExecute = false;
        dotnet.StartInfo.RedirectStandardError = true;
        dotnet.StartInfo.RedirectStandardOutput = true;
        dotnet.StartInfo.CreateNoWindow = true;

        var stdoutBuffer = new StringBuilder();
        var stderrBuffer = new StringBuilder();
        dotnet.OutputDataReceived += (sender, eventArgs) => stdoutBuffer.Append(eventArgs.Data).Append(Environment.NewLine);
        dotnet.ErrorDataReceived += (sender, eventArgs) => stderrBuffer.Append(eventArgs.Data).Append(Environment.NewLine);

        Console.WriteLine("AcceptanceTestBase.Execute: Path = {0}", dotnet.StartInfo.FileName);
        Console.WriteLine("AcceptanceTestBase.Execute: Arguments = {0}", dotnet.StartInfo.Arguments);

        Stopwatch stopwatch = new();
        stopwatch.Start();

        dotnet.Start();
        dotnet.BeginOutputReadLine();
        dotnet.BeginErrorReadLine();
        if (!dotnet.WaitForExit(80 * 1000))
        {
            Console.WriteLine("AcceptanceTestBase.Execute: Timed out waiting for dotnet.exe. Terminating the process.");
            dotnet.Kill();
        }
        else
        {
            // Ensure async buffers are flushed
            dotnet.WaitForExit();
        }

        stopwatch.Stop();

        Console.WriteLine($"AcceptanceTestBase.Execute: Total execution time: {stopwatch.Elapsed.Duration()}");

        var whiteSpaceRegex = GetMultipleWhitespacesRegex();
        stdError = whiteSpaceRegex.Replace(stderrBuffer.ToString(), " ");
        stdOut = whiteSpaceRegex.Replace(stdoutBuffer.ToString(), " ");
        exitCode = dotnet.ExitCode;

        Console.WriteLine("AcceptanceTestBase.Execute: stdError = {0}", stdError);
        Console.WriteLine("AcceptanceTestBase.Execute: stdOut = {0}", stdOut);
        Console.WriteLine("AcceptanceTestBase.Execute: Stopped dotnet.exe. Exit code = {0}", exitCode);
    }

    private static string GetDotnetExePath()
    {
        var currentDllPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(AcceptanceTests))!.Location)!;
        string[] paths = currentDllPath.Split("\\artifacts");
        if (paths.Length == 2)
        {
            var dotnetPath = Path.Combine(paths[0], ".dotnet", "dotnet.exe");
            if (File.Exists(dotnetPath))
                return dotnetPath;
        }

        return "dotnet";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex GetMultipleWhitespacesRegex();
}
