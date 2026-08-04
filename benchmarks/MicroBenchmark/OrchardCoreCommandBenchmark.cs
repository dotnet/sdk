// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using PackPublishMetricsStartupHook;

namespace Benchmark;

[MinColumn]
[MaxColumn]
[MedianColumn]
[MarkdownExporter]
public abstract class OrchardCoreCommandBenchmark
{
    private const string RunIdEnvironmentVariable = "DOTNET_SDK_PACK_PUBLISH_BENCHMARK_RUN_ID";
    private const string MetricsOutputDirectoryEnvironmentVariable = "DOTNET_CLI_BENCHMARK_METRICS_DIRECTORY";

    internal const int WarmupCount = 3;
    internal const int IterationCount = 12;

    private static CommandBenchmarkOptions? s_options;

    private CommandBenchmarkOptions _options = null!;
    private PreparedConfiguration _configuration = null!;
    private string _resultsPath = null!;
    private string _runId = null!;
    private int _invocation;

    protected abstract string CommandName { get; }
    protected abstract bool IsPublish { get; }

    internal static void Configure(CommandBenchmarkOptions options) => s_options = options;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _options = s_options ?? CommandBenchmarkOptions.Default;
        _runId = Environment.GetEnvironmentVariable(RunIdEnvironmentVariable)
            ?? throw new InvalidOperationException($"{RunIdEnvironmentVariable} was not set by the benchmark host.");
        _resultsPath = _options.GetResultsPath(_runId, CommandName);
        Directory.CreateDirectory(Path.GetDirectoryName(_resultsPath)!);
        if (!File.Exists(_resultsPath))
        {
            File.WriteAllText(_resultsPath, ResultRow.Header + Environment.NewLine);
        }

        _configuration = PrepareConfiguration(_options);
        await PrepareOutputsAsync().ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_configuration.GeneratedPublishSolutionPath is not null)
        {
            File.Delete(_configuration.GeneratedPublishSolutionPath);
        }
    }

    internal async Task MeasureCommandAsync()
    {
        int invocation = Interlocked.Increment(ref _invocation);
        bool isWarmup = invocation <= WarmupCount;
        int phaseIteration = isWarmup ? invocation : invocation - WarmupCount;
        ProcessMeasurement measurement = await RunMeasuredCommandAsync().ConfigureAwait(false);
        ResultRow row = new(
            _runId,
            CommandName,
            _options.Label,
            isWarmup ? "Warmup" : "Measured",
            phaseIteration,
            measurement.TotalDuration.TotalSeconds,
            measurement.PreSubmissionDurationSeconds,
            measurement.CliUtilsAssemblyPath,
            measurement.CliUtilsAssemblySha256,
            _configuration.DotNetPath);
        File.AppendAllText(_resultsPath, row.ToCsv() + Environment.NewLine);
    }

    internal async Task RunSmokeAsync()
    {
        try
        {
            await GlobalSetup().ConfigureAwait(false);
            _invocation = WarmupCount;
            await MeasureCommandAsync().ConfigureAwait(false);
        }
        finally
        {
            GlobalCleanup();
        }
    }

    private PreparedConfiguration PrepareConfiguration(CommandBenchmarkOptions options)
    {
        string dotNetPath = ResolveExecutable(options.DotNetPath);
        string orchardCoreRoot = ResolvePath(options.OrchardCoreRoot);
        string fullSolutionPath = Path.Combine(orchardCoreRoot, "OrchardCore.slnx");
        if (!File.Exists(fullSolutionPath))
        {
            throw new InvalidDataException(
                $"OrchardCore root does not contain OrchardCore.slnx: '{orchardCoreRoot}'.");
        }

        string workingDirectory = ResolvePath(options.WorkingDirectory ?? orchardCoreRoot);
        if (!Directory.Exists(workingDirectory))
        {
            throw new InvalidDataException(
                $"Working directory does not reference an existing directory: '{workingDirectory}'.");
        }

        string? packTargetsPath = ResolveOptionalFile(options.PackTargetsPath, "--pack-targets");
        string? packPropsPath = ResolveOptionalFile(options.PackPropsPath, "--pack-props");

        string? generatedPublishSolutionPath = null;
        string solutionPath = fullSolutionPath;
        if (IsPublish)
        {
            generatedPublishSolutionPath = CreatePublishSolution(orchardCoreRoot);
            solutionPath = generatedPublishSolutionPath;
        }

        return new PreparedConfiguration(
            dotNetPath,
            workingDirectory,
            solutionPath,
            generatedPublishSolutionPath,
            packTargetsPath,
            packPropsPath,
            TimeSpan.FromMinutes(options.TimeoutMinutes));
    }

    private async Task PrepareOutputsAsync()
    {
        await RunProcessAsync(
            ["restore", _configuration.SolutionPath, "-m", "-v:q"],
            expectedMetricCommand: null).ConfigureAwait(false);

        List<string> buildArguments =
        [
            "build",
            _configuration.SolutionPath,
            "-c",
            "Release",
            "--no-restore",
            "-m",
            "-v:q",
        ];
        if (IsPublish)
        {
            buildArguments.Add("-f");
            buildArguments.Add(_options.PublishFramework);
        }

        await RunProcessAsync(buildArguments, expectedMetricCommand: null).ConfigureAwait(false);
    }

    private Task<ProcessMeasurement> RunMeasuredCommandAsync()
    {
        List<string> arguments =
        [
            CommandName,
            _configuration.SolutionPath,
            "-m",
            "-v:q",
        ];

        if (IsPublish)
        {
            arguments.Add("-f");
            arguments.Add(_options.PublishFramework);
        }
        else
        {
            if (_configuration.PackPropsPath is not null)
            {
                arguments.Add(
                    $"-p:CustomBeforeMicrosoftCommonProps={_configuration.PackPropsPath}");
            }

            if (_configuration.PackTargetsPath is not null)
            {
                arguments.Add(
                    $"-p:NuGetBuildTasksPackTargets={_configuration.PackTargetsPath}");
            }
        }

        return RunProcessAsync(arguments, CommandName);
    }

    private async Task<ProcessMeasurement> RunProcessAsync(
        IReadOnlyCollection<string> arguments,
        string? expectedMetricCommand)
    {
        string? metricsDirectory = null;
        ProcessStartInfo startInfo = new(_configuration.DotNetPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _configuration.WorkingDirectory,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment.Remove("DOTNET_CLI_USE_MSBUILD_SERVER");
        startInfo.Environment.Remove("MSBUILDDISABLENODEREUSE");
        startInfo.Environment.Remove("DOTNET_STARTUP_HOOKS");
        startInfo.Environment.Remove(MetricsOutputDirectoryEnvironmentVariable);
        if (Path.IsPathRooted(_configuration.DotNetPath))
        {
            string dotNetRoot = Path.GetDirectoryName(_configuration.DotNetPath)!;
            startInfo.Environment["DOTNET_ROOT"] = dotNetRoot;
            startInfo.Environment["DOTNET_ROOT_X64"] = dotNetRoot;
        }

        if (expectedMetricCommand is not null)
        {
            metricsDirectory = Path.Combine(
                Path.GetTempPath(),
                "dotnet-sdk-pack-publish-benchmark",
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(metricsDirectory);
            startInfo.Environment["DOTNET_STARTUP_HOOKS"] = typeof(AssemblyMarker).Assembly.Location;
            startInfo.Environment[MetricsOutputDirectoryEnvironmentVariable] = metricsDirectory;
        }

        using Process process = new() { StartInfo = startInfo };
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        Task waitForExitTask = process.WaitForExitAsync();
        bool timedOut = await Task.WhenAny(waitForExitTask, Task.Delay(_configuration.Timeout)).ConfigureAwait(false)
            != waitForExitTask;
        if (timedOut)
        {
            process.Kill(entireProcessTree: true);
        }

        await waitForExitTask.ConfigureAwait(false);
        stopwatch.Stop();
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        try
        {
            if (timedOut)
            {
                throw new TimeoutException(
                    $"'{startInfo.FileName} {string.Join(' ', arguments)}' did not exit within {_configuration.Timeout}.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"'{startInfo.FileName} {string.Join(' ', arguments)}' exited with {process.ExitCode}.");
            }

            if (expectedMetricCommand is null)
            {
                return new ProcessMeasurement(stopwatch.Elapsed, 0, null, null);
            }

            MetricsDocument[] documents =
            [
                .. Directory.EnumerateFiles(metricsDirectory!, "metrics-*.json")
                    .Select(static path => JsonSerializer.Deserialize<MetricsDocument>(
                        File.ReadAllText(path),
                        SerializerOptions))
                    .WhereNotNull(),
            ];
            (MetricsDocument Document, MetricMeasurement Measurement)[] matchingMeasurements =
            [
                .. documents.SelectMany(
                    document => document.Measurements
                        .Where(measurement =>
                            measurement.CommandName == expectedMetricCommand)
                        .Select(measurement => (document, measurement))),
            ];
            if (matchingMeasurements.Length != 1)
            {
                throw new InvalidDataException(
                    $"Expected one process-start-to-MSBuild-submission metric, found {matchingMeasurements.Length}.");
            }

            (MetricsDocument metricDocument, MetricMeasurement metric) = matchingMeasurements[0];
            return new ProcessMeasurement(
                stopwatch.Elapsed,
                metric.DurationSeconds,
                metricDocument.CliUtilsAssemblyPath,
                metricDocument.CliUtilsAssemblySha256);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{exception.Message}{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{TakeTail(stdout)}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{TakeTail(stderr)}",
                exception);
        }
        finally
        {
            if (metricsDirectory is not null)
            {
                Directory.Delete(metricsDirectory, recursive: true);
            }
        }
    }

    private static string CreatePublishSolution(string orchardCoreRoot)
    {
        string[] projectFiles =
        [
            .. Directory.EnumerateFiles(
                Path.Combine(orchardCoreRoot, "src", "OrchardCore.Modules"),
                "*.csproj",
                SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(
                Path.Combine(orchardCoreRoot, "src", "OrchardCore.Themes"),
                "*.csproj",
                SearchOption.AllDirectories),
        ];
        Array.Sort(projectFiles, StringComparer.Ordinal);

        XElement solution = new("Solution");
        foreach (string projectFile in projectFiles)
        {
            solution.Add(
                new XElement(
                    "Project",
                    new XAttribute(
                        "Path",
                        Path.GetRelativePath(orchardCoreRoot, projectFile).Replace('\\', '/'))));
        }

        string path = Path.Combine(
            orchardCoreRoot,
            $"OrchardCore.PublishPerf.Benchmark.{Environment.ProcessId}.slnx");
        new XDocument(solution).Save(path);
        return path;
    }

    private static string ResolveExecutable(string path)
    {
        if (!Path.IsPathRooted(path) &&
            !path.Contains(Path.DirectorySeparatorChar) &&
            !path.Contains(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        string fullPath = ResolvePath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new InvalidDataException(
                $"dotnet executable does not reference an existing file: '{fullPath}'.");
    }

    private static string? ResolveOptionalFile(string? path, string optionName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fullPath = ResolvePath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new InvalidDataException(
                $"{optionName} does not reference an existing file: '{fullPath}'.");
    }

    private static string ResolvePath(string path) => Path.GetFullPath(path);

    private static string TakeTail(string value)
    {
        const int MaximumLength = 4_000;
        return value.Length <= MaximumLength ? value : value[^MaximumLength..];
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record PreparedConfiguration(
        string DotNetPath,
        string WorkingDirectory,
        string SolutionPath,
        string? GeneratedPublishSolutionPath,
        string? PackTargetsPath,
        string? PackPropsPath,
        TimeSpan Timeout);

    private sealed record ProcessMeasurement(
        TimeSpan TotalDuration,
        double PreSubmissionDurationSeconds,
        string? CliUtilsAssemblyPath,
        string? CliUtilsAssemblySha256);

    private sealed record MetricsDocument(
        int ProcessId,
        string CommandLine,
        string? CliUtilsAssemblyPath,
        string? CliUtilsAssemblySha256,
        MetricMeasurement[] Measurements);

    private sealed record MetricMeasurement(string? CommandName, double DurationSeconds);

    private sealed record ResultRow(
        string RunId,
        string Benchmark,
        string Label,
        string Phase,
        int Iteration,
        double TotalDurationSeconds,
        double PreSubmissionDurationSeconds,
        string? CliUtilsAssemblyPath,
        string? CliUtilsAssemblySha256,
        string DotNetPath)
    {
        internal const string Header =
            "RunId,Benchmark,Label,Phase,Iteration,TotalDurationSeconds," +
            "PreSubmissionDurationSeconds,CliUtilsAssemblyPath,CliUtilsAssemblySha256,DotNetPath";

        internal string ToCsv() => string.Join(
            ',',
            Csv(RunId),
            Csv(Benchmark),
            Csv(Label),
            Csv(Phase),
            Iteration.ToString(CultureInfo.InvariantCulture),
            TotalDurationSeconds.ToString("F7", CultureInfo.InvariantCulture),
            PreSubmissionDurationSeconds.ToString("F7", CultureInfo.InvariantCulture),
            Csv(CliUtilsAssemblyPath),
            Csv(CliUtilsAssemblySha256),
            Csv(DotNetPath));

        private static string Csv(string? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}

internal sealed class CommandBenchmarkOptions
{
    internal static CommandBenchmarkOptions Default { get; } = new();

    internal string Label { get; private set; } = "Default";
    internal string DotNetPath { get; private set; } = "dotnet";
    internal string OrchardCoreRoot { get; private set; } = Directory.GetCurrentDirectory();
    internal string? WorkingDirectory { get; private set; }
    internal string PublishFramework { get; private set; } = "net10.0";
    internal string ResultsPath { get; private set; } =
        Path.Combine("BenchmarkDotNet.Artifacts", "{benchmark}-{label}-{runId}.csv");
    internal string? PackTargetsPath { get; private set; }
    internal string? PackPropsPath { get; private set; }
    internal int TimeoutMinutes { get; private set; } = 30;

    internal static CommandBenchmarkOptions Parse(IReadOnlyList<string> arguments)
    {
        CommandBenchmarkOptions options = new();
        for (int index = 0; index < arguments.Count; index++)
        {
            string option = arguments[index];
            string value = GetValue(arguments, ref index, option);
            switch (option)
            {
                case "--label":
                    options.Label = value;
                    break;
                case "--dotnet":
                    options.DotNetPath = value;
                    break;
                case "--orchard-core":
                    options.OrchardCoreRoot = value;
                    break;
                case "--working-directory":
                    options.WorkingDirectory = value;
                    break;
                case "--publish-framework":
                    options.PublishFramework = value;
                    break;
                case "--results":
                    options.ResultsPath = value;
                    break;
                case "--pack-targets":
                    options.PackTargetsPath = value;
                    break;
                case "--pack-props":
                    options.PackPropsPath = value;
                    break;
                case "--timeout-minutes":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int timeoutMinutes) ||
                        timeoutMinutes <= 0)
                    {
                        throw new ArgumentException("--timeout-minutes must be a positive integer.");
                    }

                    options.TimeoutMinutes = timeoutMinutes;
                    break;
                default:
                    throw new ArgumentException($"Unknown benchmark option '{option}'.");
            }
        }

        return options;
    }

    internal string GetResultsPath(string runId, string benchmark)
    {
        string path = ResultsPath
            .Replace("{runId}", runId, StringComparison.Ordinal)
            .Replace("{benchmark}", benchmark, StringComparison.OrdinalIgnoreCase)
            .Replace("{label}", Label, StringComparison.OrdinalIgnoreCase);
        return Path.GetFullPath(path);
    }

    private static string GetValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option)
    {
        if (!option.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Expected an option name, found '{option}'.");
        }

        index++;
        if (index >= arguments.Count)
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return arguments[index];
    }
}

internal static class EnumerableExtensions
{
    internal static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
    {
        foreach (T? item in source)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}
