#!/usr/bin/env dotnet

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Contributor-facing entry point for local dotnet/sdk test execution.

#:package System.CommandLine

using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

RootCommand rootCommand = new("Run one dotnet/sdk test project with detailed output and retained diagnostics.");

Option<string> projectOption = new("--project")
{
    Arity = ArgumentArity.ExactlyOne,
    Description = "Test .csproj path relative to the repository root.",
    Required = true
};
Option<string?> filterOption = new("--filter")
{
    Arity = ArgumentArity.ExactlyOne,
    Description = "Test filter expression, for example FullyQualifiedName~TestClass."
};
Option<string> configurationOption = new("--configuration", "-c")
{
    Arity = ArgumentArity.ExactlyOne,
    DefaultValueFactory = _ => "Debug",
    Description = "Debug (default) or Release."
};
configurationOption.Validators.Add(optionResult =>
{
    string? configuration = optionResult.GetValueOrDefault<string>();
    if (!string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(configuration, "Release", StringComparison.OrdinalIgnoreCase))
    {
        optionResult.AddError($"Unsupported configuration '{configuration}'. Use Debug or Release.");
    }
});
Option<string?> frameworkOption = new("--framework", "-f")
{
    Arity = ArgumentArity.ExactlyOne,
    Description = "Target framework to run. Multi-targeted projects default to SdkTargetFramework, then their first TFM."
};
Option<bool> skipRedistCheckOption = new("--skip-redist-check")
{
    Description = "Allow tests that do not exercise the assembled SDK to run without a redist layout."
};

rootCommand.Options.Add(projectOption);
rootCommand.Options.Add(filterOption);
rootCommand.Options.Add(configurationOption);
rootCommand.Options.Add(frameworkOption);
rootCommand.Options.Add(skipRedistCheckOption);

rootCommand.SetAction((parseResult, cancellationToken) => RunAsync(
    parseResult.GetValue(projectOption)!,
    parseResult.GetValue(filterOption),
    parseResult.GetValue(configurationOption)!,
    parseResult.GetValue(frameworkOption),
    parseResult.GetValue(skipRedistCheckOption),
    cancellationToken));

return await rootCommand
    .Parse(args)
    .InvokeAsync();

static async Task<int> RunAsync(
    string project,
    string? filter,
    string configuration,
    string? framework,
    bool skipRedistCheck,
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();

    // Resolve every path from the repository root. Agents may launch this script from a
    // subdirectory, and allowing the current directory to influence individual paths would
    // make the same command target different projects or artifact locations.
    var repoRoot = FindRepoRoot(Environment.CurrentDirectory);
    if (repoRoot is null)
    {
        return Fail("Could not find the dotnet/sdk repository root. Run from inside the checkout.");
    }

    var projectPath = Path.GetFullPath(project, repoRoot);
    if (!File.Exists(projectPath))
    {
        return Fail($"Test project does not exist: {projectPath}");
    }

    if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
    {
        return Fail($"Expected a .csproj test project, but got: {projectPath}");
    }

    // Besides catching accidental typos, this boundary prevents a caller from using the
    // runner as a generic way to execute and write artifacts for projects outside dotnet/sdk.
    var relativeProjectPath = Path.GetRelativePath(repoRoot, projectPath);
    if (relativeProjectPath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || Path.IsPathRooted(relativeProjectPath))
    {
        return Fail($"Test project must be inside the repository: {projectPath}");
    }

    // Use the bootstrap SDK pinned by this checkout rather than an arbitrary dotnet on PATH.
    // This keeps MSBuild evaluation and test execution aligned with global.json.
    var dotnetPath = Path.Combine(
        repoRoot,
        ".dotnet",
        OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
    if (!File.Exists(dotnetPath))
    {
        return Fail(
            $"Repo-local SDK not found at {dotnetPath}.{Environment.NewLine}"
            + $"Run {(OperatingSystem.IsWindows() ? @".\restore.cmd" : "./restore.sh")} to install it.");
    }

    // SDK tests exercise the assembled redist layout. A test project can build successfully
    // while still testing stale or missing product bits, so fail early when that layout does
    // not exist instead of producing a misleading test result.
    var redistRoot = Path.Combine(repoRoot, "artifacts", "bin", "redist", configuration, "dotnet");
    if (!skipRedistCheck && !Directory.Exists(redistRoot))
    {
        return Fail(
            $"The {configuration} redist SDK does not exist at {redistRoot}.{Environment.NewLine}"
            + $"Run {(OperatingSystem.IsWindows() ? @".\build.cmd" : "./build.sh")}"
            + $"{(configuration.Equals("Release", StringComparison.OrdinalIgnoreCase) ? " -c Release" : string.Empty)} first.");
    }

    // Give each invocation its own directory. The timestamp makes runs easy to inspect while
    // the process ID prevents collisions when agents start the same project in parallel.
    var projectName = Path.GetFileNameWithoutExtension(projectPath);
    var runDirectory = Path.Combine(
        repoRoot,
        "artifacts",
        "log",
        "test-runs",
        projectName,
        $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}");
    Directory.CreateDirectory(runDirectory);

    var trxPath = Path.Combine(runDirectory, "test-results.trx");
    var binlogPath = Path.Combine(runDirectory, "build.binlog");
    var rerunCommand = GetRerunCommand(
        dotnetPath,
        repoRoot,
        relativeProjectPath,
        configuration,
        framework,
        filter,
        skipRedistCheck);

    // Query evaluated MSBuild properties instead of guessing from project text, which may be
    // supplied by imports.
    var projectProperties = await GetProjectProperties(
        dotnetPath,
        projectPath,
        configuration,
        framework,
        repoRoot,
        cancellationToken);
    if (projectProperties is null)
    {
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        Console.Error.WriteLine($"Rerun: {rerunCommand}");
        return 1;
    }
    framework = projectProperties.TargetFramework;
    rerunCommand = GetRerunCommand(
        dotnetPath,
        repoRoot,
        relativeProjectPath,
        configuration,
        framework,
        filter,
        skipRedistCheck);

    if (!projectProperties.UsesMSTestSdk)
    {
        var exitCode = Fail(
            "The project does not use MSTest.Sdk. All supported dotnet/sdk test projects "
            + "must use the repository's Microsoft.Testing.Platform configuration.");
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        Console.Error.WriteLine($"Rerun: {rerunCommand}");
        return exitCode;
    }

    int buildExitCode = await BuildTestProject(
        dotnetPath,
        projectPath,
        configuration,
        framework,
        binlogPath,
        repoRoot,
        cancellationToken);
    if (buildExitCode != 0)
    {
        Console.Error.WriteLine($"Test project build failed with exit code {buildExitCode}.");
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        Console.Error.WriteLine($"Rerun: {rerunCommand}");
        return buildExitCode;
    }

    if (!projectProperties.IsTestApplication)
    {
        var exitCode = Fail(
            $"The project uses MSTest.Sdk but IsTestApplication is false for this configuration. "
            + "It cannot be executed as a Microsoft.Testing.Platform test application on this platform.");
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        Console.Error.WriteLine($"Rerun: {rerunCommand}");
        return exitCode;
    }

    if (string.Equals(
        projectProperties.TargetFrameworkIdentifier,
        ".NETFramework",
        StringComparison.OrdinalIgnoreCase)
        && !OperatingSystem.IsWindows())
    {
        var exitCode = Fail(
            $"Target framework '{framework}' requires a .NET Framework test executable, "
            + "which can only run on Windows.");
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        Console.Error.WriteLine($"Rerun: {rerunCommand}");
        return exitCode;
    }

    if (!File.Exists(projectProperties.TargetPath))
    {
        var exitCode = Fail(
            $"Built MSTest test assembly not found at {projectProperties.TargetPath}. "
            + "The test project build completed without producing its expected output.");
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        Console.Error.WriteLine($"Rerun: {rerunCommand}");
        return exitCode;
    }

    TestInvocation invocation = CreateTestInvocation(
        dotnetPath,
        projectProperties,
        runDirectory,
        filter);
    Console.WriteLine($"Project: {relativeProjectPath}");
    Console.WriteLine($"Framework: {framework}");
    Console.WriteLine($"Run directory: {Path.GetRelativePath(repoRoot, runDirectory)}");
    Console.WriteLine($"Command: {FormatCommand(invocation.Executable, invocation.Arguments, repoRoot)}");
    Console.WriteLine();

    int testExitCode = await RunProcess(
        invocation.Executable,
        invocation.Arguments,
        repoRoot,
        cancellationToken);
    return ReportTestResult(
        testExitCode,
        repoRoot,
        trxPath,
        binlogPath,
        rerunCommand);
}

static async Task<int> BuildTestProject(
    string dotnetPath,
    string projectPath,
    string configuration,
    string? framework,
    string binlogPath,
    string repoRoot,
    CancellationToken cancellationToken)
{
    var arguments = new List<string>
    {
        "build",
        projectPath,
        "--configuration",
        configuration,
        "--nologo",
        $"-bl:{binlogPath}"
    };
    if (!string.IsNullOrWhiteSpace(framework))
    {
        arguments.Add("--framework");
        arguments.Add(framework);
    }

    Console.WriteLine($"Build command: {FormatCommand(dotnetPath, arguments, repoRoot)}");
    Console.WriteLine();
    return await RunProcess(dotnetPath, arguments, repoRoot, cancellationToken);
}

static TestInvocation CreateTestInvocation(
    string dotnetPath,
    TestProjectProperties projectProperties,
    string runDirectory,
    string? filter)
{
    string executable;
    List<string> arguments;
    if (string.Equals(
        projectProperties.TargetFrameworkIdentifier,
        ".NETFramework",
        StringComparison.OrdinalIgnoreCase))
    {
        executable = projectProperties.TargetPath;
        arguments = [];
    }
    else
    {
        executable = dotnetPath;
        arguments = ["exec", projectProperties.TargetPath];
    }

    if (projectProperties.TrxReportEnabled)
    {
        arguments.Add("--report-trx");
        arguments.Add("--report-trx-filename");
        arguments.Add("test-results.trx");
        arguments.Add("--results-directory");
        arguments.Add(runDirectory);
    }
    else
    {
        Console.Error.WriteLine(
            "The project does not enable Microsoft.Testing.Extensions.TrxReport; "
            + "the test run will continue without a TRX.");
    }

    if (!string.IsNullOrWhiteSpace(filter))
    {
        arguments.Add("--filter");
        arguments.Add(filter);
    }

    return new TestInvocation(executable, arguments);
}

static int ReportTestResult(
    int exitCode,
    string repoRoot,
    string trxPath,
    string binlogPath,
    string rerunCommand)
{
    Console.WriteLine();
    if (exitCode == 0)
    {
        Console.WriteLine("Tests passed.");
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        return 0;
    }

    Console.Error.WriteLine($"Tests failed with exit code {exitCode}.");
    if (File.Exists(trxPath))
    {
        PrintFailedTests(trxPath);
    }
    else
    {
        Console.Error.WriteLine("No TRX was produced; the failure occurred before test results were written.");
    }

    PrintArtifacts(repoRoot, trxPath, binlogPath);
    Console.Error.WriteLine($"Rerun: {rerunCommand}");
    return exitCode;
}

static string? FindRepoRoot(string startDirectory)
{
    // Walking upward supports invocation from anywhere in the checkout without relying on git,
    // which also keeps this file-based app usable in source archives and constrained agents.
    for (var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
         directory is not null;
         directory = directory.Parent)
    {
        if (IsRepoRoot(directory.FullName))
        {
            return directory.FullName;
        }
    }

    return null;
}

// Use SDK-specific sentinels rather than accepting any directory containing a .git entry.
// Worktrees store .git as a file, while ordinary checkouts use a directory.
static bool IsRepoRoot(string path) =>
    File.Exists(Path.Combine(path, "global.json"))
    && File.Exists(Path.Combine(path, "sdk.slnx"))
    && (Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git")));

static async Task<TestProjectProperties?> GetProjectProperties(
    string dotnetPath,
    string projectPath,
    string configuration,
    string? requestedFramework,
    string repoRoot,
    CancellationToken cancellationToken)
{
    Dictionary<string, string>? properties = await EvaluateProjectProperties(
        dotnetPath,
        projectPath,
        configuration,
        requestedFramework,
        repoRoot,
        cancellationToken);
    if (properties is null)
    {
        return null;
    }

    string targetFramework = properties["TargetFramework"];
    string targetFrameworks = properties["TargetFrameworks"];
    string[] frameworks = targetFrameworks.Split(
        ';',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (requestedFramework is not null
        && frameworks.Length > 0
        && !frameworks.Contains(requestedFramework, StringComparer.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine(
            $"Error: Target framework '{requestedFramework}' is not listed in TargetFrameworks "
            + $"('{targetFrameworks}') for {Path.GetRelativePath(repoRoot, projectPath)}.");
        return null;
    }

    if (string.IsNullOrWhiteSpace(targetFramework) && !string.IsNullOrWhiteSpace(targetFrameworks))
    {
        string? selectedFramework = requestedFramework;
        selectedFramework ??= frameworks.FirstOrDefault(framework =>
            string.Equals(
                framework,
                properties["SdkTargetFramework"],
                StringComparison.OrdinalIgnoreCase));
        selectedFramework ??= frameworks.First();

        properties = await EvaluateProjectProperties(
            dotnetPath,
            projectPath,
            configuration,
            selectedFramework,
            repoRoot,
            cancellationToken);
        if (properties is null)
        {
            return null;
        }

        targetFramework = properties["TargetFramework"];
    }

    bool usesMSTestSdk = IsTrue(properties["UsingMSTestSdk"]);
    string targetPath = properties["TargetPath"];
    if (usesMSTestSdk && string.IsNullOrWhiteSpace(targetPath))
    {
        Console.Error.WriteLine(
            "Error: MSBuild returned an empty TargetPath for the selected MSTest.Sdk target framework.");
        return null;
    }

    if (!string.IsNullOrWhiteSpace(targetPath))
    {
        targetPath = Path.GetFullPath(
            targetPath,
            Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException($"Could not determine project directory for {projectPath}."));
    }

    return new TestProjectProperties(
        usesMSTestSdk,
        IsTrue(properties["IsTestApplication"]),
        IsTrue(properties["EnableMicrosoftTestingExtensionsTrxReport"]),
        targetPath,
        targetFramework,
        properties["TargetFrameworkIdentifier"]);
}

static async Task<Dictionary<string, string>?> EvaluateProjectProperties(
    string dotnetPath,
    string projectPath,
    string configuration,
    string? framework,
    string repoRoot,
    CancellationToken cancellationToken)
{
    // -getProperty asks MSBuild for the fully evaluated values and emits a small JSON document.
    // Running the repo-local MSBuild process guarantees evaluation with this checkout's pinned
    // SDK, imports, workload resolvers, and MSBuild version.
    var arguments = new List<string>
    {
        "msbuild",
        projectPath,
        "-getProperty:UsingMSTestSdk,IsTestApplication,EnableMicrosoftTestingExtensionsTrxReport,TargetPath,TargetFramework,TargetFrameworks,SdkTargetFramework,TargetFrameworkIdentifier",
        $"-p:Configuration={configuration}",
        "--nologo"
    };
    if (!string.IsNullOrWhiteSpace(framework))
    {
        arguments.Add($"-p:TargetFramework={framework}");
    }

    var startInfo = CreateProcessStartInfo(dotnetPath, arguments, repoRoot);
    startInfo.RedirectStandardOutput = true;
    startInfo.RedirectStandardError = true;

    // Property evaluation output must be captured for JSON parsing. The actual build and test
    // processes intentionally inherit the console instead so their detailed output streams live.
    var processOutput = await Process.RunAndCaptureTextAsync(startInfo, cancellationToken);

    if (processOutput.ExitStatus.ExitCode != 0)
    {
        Console.Error.WriteLine(
            $"Error: Could not evaluate test project properties (exit code {processOutput.ExitStatus.ExitCode}).");
        Console.Error.WriteLine(processOutput.StandardError);
        return null;
    }

    var output = processOutput.StandardOutput;

    // MSBuild may write SDK or workload messages around the JSON payload. Extract the outer JSON
    // object rather than requiring stdout to contain JSON and nothing else.
    var jsonStart = output.IndexOf('{');
    var jsonEnd = output.LastIndexOf('}');
    if (jsonStart < 0 || jsonEnd < jsonStart)
    {
        Console.Error.WriteLine("Error: MSBuild did not return the requested test project properties.");
        Console.Error.WriteLine(output);
        return null;
    }

    JsonDocument document;
    try
    {
        document = JsonDocument.Parse(output[jsonStart..(jsonEnd + 1)]);
    }
    catch (JsonException exception)
    {
        Console.Error.WriteLine($"Error: Could not parse MSBuild test project properties: {exception.Message}");
        return null;
    }

    using (document)
    {
        var properties = document.RootElement.GetProperty("Properties");
        return properties
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }
}

static bool IsTrue(string value) =>
    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

static async Task<int> RunProcess(
    string executable,
    IEnumerable<string> arguments,
    string workingDirectory,
    CancellationToken cancellationToken)
{
    // Do not redirect output here. The runner promises detailed live diagnostics, and inheriting
    // stdout/stderr preserves test-host formatting and avoids buffering large build logs in memory.
    var exitStatus = await Process.RunAsync(
        CreateProcessStartInfo(executable, arguments, workingDirectory),
        cancellationToken);
    return exitStatus.ExitCode;
}

static ProcessStartInfo CreateProcessStartInfo(
    string executable,
    IEnumerable<string> arguments,
    string workingDirectory)
{
    var startInfo = new ProcessStartInfo(executable)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false
    };

    // ArgumentList delegates platform-specific escaping to ProcessStartInfo. Building one command
    // string would be fragile for filters, spaces, quotes, and Windows paths.
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    return startInfo;
}

static void PrintFailedTests(string trxPath)
{
    const int MaximumDisplayedFailures = 20;

    try
    {
        var document = XDocument.Load(trxPath);

        // TRX elements are namespace-qualified, and the namespace version can vary with the test
        // platform. LocalName keeps this summary compatible without hardcoding a schema URI.
        var failures = document
            .Descendants()
            .Where(element => element.Name.LocalName == "UnitTestResult"
                && string.Equals((string?)element.Attribute("outcome"), "Failed", StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("testName"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (failures.Count == 0)
        {
            return;
        }

        Console.Error.WriteLine($"Failed tests ({failures.Count}):");

        // Keep terminal output actionable but bounded. The TRX remains the source of truth when a
        // broad filter fails hundreds of tests.
        foreach (var failure in failures.Take(MaximumDisplayedFailures))
        {
            Console.Error.WriteLine($"  {failure}");
        }
        if (failures.Count > MaximumDisplayedFailures)
        {
            Console.Error.WriteLine(
                $"  ... and {failures.Count - MaximumDisplayedFailures} more; see the TRX for the complete list.");
        }
    }
    catch (IOException exception)
    {
        Console.Error.WriteLine($"Could not read the TRX failure summary: {exception.Message}");
    }
    catch (XmlException exception)
    {
        Console.Error.WriteLine($"Could not parse the TRX failure summary: {exception.Message}");
    }
}

static void PrintArtifacts(string repoRoot, string trxPath, string binlogPath)
{
    // Relative paths are easier to copy into a follow-up command and do not leak machine-specific
    // checkout locations into logs or agent responses.
    Console.WriteLine("Diagnostic artifacts:");
    Console.WriteLine(
        File.Exists(trxPath)
            ? $"  TRX: {Path.GetRelativePath(repoRoot, trxPath)}"
            : "  TRX: not produced");
    Console.WriteLine(
        File.Exists(binlogPath)
            ? $"  Binlog: {Path.GetRelativePath(repoRoot, binlogPath)}"
            : "  Binlog: not produced");
}

static string GetRerunCommand(
    string dotnetPath,
    string repoRoot,
    string relativeProjectPath,
    string configuration,
    string? framework,
    string? filter,
    bool skipRedistCheck)
{
    var arguments = new List<string>
    {
        Path.Combine("scripts", "RunTests.cs"),
        "--",
        "--project",
        relativeProjectPath,
        "--configuration",
        configuration
    };
    if (!string.IsNullOrWhiteSpace(framework))
    {
        arguments.Add("--framework");
        arguments.Add(framework);
    }
    if (skipRedistCheck)
    {
        arguments.Add("--skip-redist-check");
    }
    if (!string.IsNullOrWhiteSpace(filter))
    {
        arguments.Add("--filter");
        arguments.Add(filter);
    }

    return FormatCommand(dotnetPath, arguments, repoRoot);
}

static string FormatCommand(string executable, IEnumerable<string> arguments, string repoRoot)
{
    // Display the repo-local executable as a relative path so the rerun command is portable to
    // another checkout and visibly cannot resolve to a global dotnet installation.
    var relativeExecutable = Path.GetRelativePath(repoRoot, executable);
    if (!relativeExecutable.StartsWith($".{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !relativeExecutable.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    {
        relativeExecutable = $".{Path.DirectorySeparatorChar}{relativeExecutable}";
    }

    return string.Join(" ", new[] { relativeExecutable }.Concat(arguments).Select(QuoteArgument));
}

static string QuoteArgument(string argument)
{
    // This formatter is for human-readable rerun guidance only; ProcessStartInfo.ArgumentList
    // performs the real process escaping. Leave simple arguments readable and quote the rest.
    if (argument.Length > 0 && argument.All(IsShellSafe))
    {
        return argument;
    }

    return $"\"{argument.Replace("\"", "\\\"")}\"";
}

static bool IsShellSafe(char value) =>
    // These characters are common in paths, properties, and test filters and do not require
    // quoting in the supported shells. Everything else takes the conservative quoted path.
    char.IsLetterOrDigit(value)
    || value is '_' or '-' or '.' or ':' or '\\' or '/' or '~' or '=' or '+';

static int Fail(string message)
{
    Console.Error.WriteLine($"Error: {message}");
    return 1;
}

sealed record TestProjectProperties(
    bool UsesMSTestSdk,
    bool IsTestApplication,
    bool TrxReportEnabled,
    string TargetPath,
    string TargetFramework,
    string TargetFrameworkIdentifier);

sealed record TestInvocation(string Executable, IReadOnlyList<string> Arguments);
