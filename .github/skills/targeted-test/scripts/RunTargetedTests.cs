#!/usr/bin/env dotnet

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    Description = "VSTest filter, for example FullyQualifiedName~TestClass."
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
Option<bool> noBuildOption = new("--no-build")
{
    Description = "Do not build the test project before running it."
};
Option<string?> repoRootOption = new("--repo-root")
{
    Arity = ArgumentArity.ExactlyOne,
    Description = "Repository root; inferred from the current directory by default."
};

rootCommand.Options.Add(projectOption);
rootCommand.Options.Add(filterOption);
rootCommand.Options.Add(configurationOption);
rootCommand.Options.Add(noBuildOption);
rootCommand.Options.Add(repoRootOption);

rootCommand.SetAction((parseResult, cancellationToken) => RunAsync(
    parseResult.GetValue(projectOption)!,
    parseResult.GetValue(filterOption),
    parseResult.GetValue(configurationOption)!,
    parseResult.GetValue(noBuildOption),
    parseResult.GetValue(repoRootOption),
    cancellationToken));

return await rootCommand
    .Parse(args)
    .InvokeAsync();

static async Task<int> RunAsync(
    string project,
    string? filter,
    string configuration,
    bool noBuild,
    string? repoRootArgument,
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();

    // Resolve every path from the repository root. Agents may launch this script from a
    // subdirectory, and allowing the current directory to influence individual paths would
    // make the same command target different projects or artifact locations.
    var repoRoot = repoRootArgument is null
        ? FindRepoRoot(Environment.CurrentDirectory)
        : Path.GetFullPath(repoRootArgument);
    if (repoRoot is null || !IsRepoRoot(repoRoot))
    {
        return Fail(
            "Could not find the dotnet/sdk repository root. Run from inside the checkout or pass --repo-root <path>.");
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
    if (!Directory.Exists(redistRoot))
    {
        return Fail(
            $"The {configuration} redist SDK does not exist at {redistRoot}.{Environment.NewLine}"
            + $"Run {(OperatingSystem.IsWindows() ? @".\build.cmd" : "./build.sh")} "
            + $"{(configuration.Equals("Release", StringComparison.OrdinalIgnoreCase) ? "-c Release" : "")} first.");
    }

    // Give each invocation its own directory. The timestamp makes runs easy to inspect while
    // the process ID prevents collisions when agents start the same project in parallel.
    var projectName = Path.GetFileNameWithoutExtension(projectPath);
    var runDirectory = Path.Combine(
        repoRoot,
        "artifacts",
        "log",
        "targeted-tests",
        projectName,
        $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}");
    Directory.CreateDirectory(runDirectory);

    var trxPath = Path.Combine(runDirectory, "test-results.trx");
    var binlogPath = Path.Combine(runDirectory, "build.binlog");

    // The repository contains both MSTest.Sdk/Microsoft.Testing.Platform projects and projects
    // that still run through `dotnet test`. Query evaluated MSBuild properties instead of
    // guessing from package references or project text, which may be supplied by imports.
    var projectProperties = await GetProjectProperties(
        dotnetPath,
        projectPath,
        configuration,
        repoRoot,
        cancellationToken);
    if (projectProperties is null)
    {
        return 1;
    }

    // MSTest.Sdk projects are executable test applications. Build them explicitly so the
    // build failure has its own exit code and binlog, then execute TargetPath below. Running
    // `dotnet test` for these projects can succeed while discovering zero tests.
    if (projectProperties.Value.UsesMSTestSdk && !noBuild)
    {
        var buildArguments = new List<string>
        {
            "build",
            projectPath,
            "--configuration",
            configuration,
            "--nologo",
            $"-bl:{binlogPath}"
        };
        Console.WriteLine($"Build command: {FormatCommand(dotnetPath, buildArguments, repoRoot)}");
        Console.WriteLine();
        var buildExitCode = await RunProcess(dotnetPath, buildArguments, repoRoot, cancellationToken);
        if (buildExitCode != 0)
        {
            Console.Error.WriteLine($"Targeted test project build failed with exit code {buildExitCode}.");
            PrintArtifacts(repoRoot, trxPath, binlogPath);
            return buildExitCode;
        }
    }

    // Microsoft.Testing.Platform accepts test options after the assembly path, while the
    // traditional path accepts them through `dotnet test`. Construct the appropriate command
    // once so logging, filtering, display, and rerun guidance all describe the exact process.
    var testArguments = projectProperties.Value.UsesMSTestSdk
        ? new List<string>
        {
            "exec",
            projectProperties.Value.TargetPath,
            "--report-trx",
            "--report-trx-filename",
            "test-results.trx",
            "--results-directory",
            runDirectory
        }
        : new List<string>
        {
            "test",
            projectPath,
            "--configuration",
            configuration,
            "--logger",
            "console;verbosity=detailed",
            "--logger",
            "trx;LogFileName=test-results.trx",
            "--results-directory",
            runDirectory,
            $"-bl:{binlogPath}"
        };

    // The explicit MSTest.Sdk build above is already skipped when --no-build is set. Only the
    // `dotnet test` command needs the switch forwarded.
    if (noBuild && !projectProperties.Value.UsesMSTestSdk)
    {
        testArguments.Add("--no-build");
    }

    if (!string.IsNullOrWhiteSpace(filter))
    {
        testArguments.Add("--filter");
        testArguments.Add(filter);
    }

    // Print the command before execution so live logs remain useful if the process hangs or is
    // cancelled before it can produce a TRX.
    var displayCommand = FormatCommand(dotnetPath, testArguments, repoRoot);
    Console.WriteLine($"Project: {relativeProjectPath}");
    Console.WriteLine($"Artifacts: {Path.GetRelativePath(repoRoot, runDirectory)}");
    Console.WriteLine($"Command: {displayCommand}");
    Console.WriteLine();

    // In --no-build mode, TargetPath may describe where output would be written even when the
    // file is absent. Detect that case here and explain how to recover instead of forwarding a
    // less actionable dotnet exec "file not found" error.
    if (projectProperties.Value.UsesMSTestSdk && !File.Exists(projectProperties.Value.TargetPath))
    {
        return Fail(
            $"Built MSTest test assembly not found at {projectProperties.Value.TargetPath}. "
            + "Build the project or omit --no-build.");
    }

    var testExitCode = await RunProcess(dotnetPath, testArguments, repoRoot, cancellationToken);

    Console.WriteLine();
    if (testExitCode == 0)
    {
        Console.WriteLine("Targeted tests passed.");
        PrintArtifacts(repoRoot, trxPath, binlogPath);
        return 0;
    }

    Console.Error.WriteLine($"Targeted tests failed with exit code {testExitCode}.");
    if (File.Exists(trxPath))
    {
        // Surface names in the console for immediate triage while retaining the complete TRX
        // for stack traces, output, timings, and larger failure sets.
        PrintFailedTests(trxPath);
    }
    else
    {
        Console.Error.WriteLine("No TRX was produced; the failure occurred before test results were written.");
    }

    // Always show whatever diagnostics exist. Build failures may produce only a binlog, while
    // early test-host failures may produce neither; stating that explicitly is more actionable
    // than making the caller search the artifact tree.
    PrintArtifacts(repoRoot, trxPath, binlogPath);
    Console.Error.WriteLine($"Rerun: {displayCommand}");
    return testExitCode;
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

static async Task<(bool UsesMSTestSdk, string TargetPath)?> GetProjectProperties(
    string dotnetPath,
    string projectPath,
    string configuration,
    string repoRoot,
    CancellationToken cancellationToken)
{
    // -getProperty asks MSBuild for the fully evaluated values and emits a small JSON document.
    // Running the repo-local MSBuild process guarantees evaluation with this checkout's pinned
    // SDK, imports, workload resolvers, and MSBuild version. Using the MSBuild APIs in-process
    // would require package dependencies, toolset registration, assembly-load management, and
    // isolation from MSBuild's global state for the sake of reading only these two properties.
    var arguments = new[]
    {
        "msbuild",
        projectPath,
        "-getProperty:UsingMSTestSdk,TargetPath",
        $"-p:Configuration={configuration}",
        "--nologo"
    };
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

        // UsingMSTestSdk is a string-valued MSBuild property, so compare it using MSBuild's
        // case-insensitive boolean convention instead of relying on JSON boolean parsing.
        var usesMSTestSdk = string.Equals(
            properties.GetProperty("UsingMSTestSdk").GetString(),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var targetPath = properties.GetProperty("TargetPath").GetString();
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            Console.Error.WriteLine("Error: MSBuild returned an empty TargetPath for the test project.");
            return null;
        }

        // TargetPath may be relative depending on project configuration. Normalize it now so the
        // later existence check and dotnet exec invocation are independent of process cwd.
        return (usesMSTestSdk, Path.GetFullPath(targetPath, repoRoot));
    }
}

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
