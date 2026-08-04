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
    private const string ConfigurationEnvironmentVariable = "DOTNET_SDK_PACK_PUBLISH_BENCHMARK_CONFIG";
    private const string RunIdEnvironmentVariable = "DOTNET_SDK_PACK_PUBLISH_BENCHMARK_RUN_ID";
    private const string MetricsOutputDirectoryEnvironmentVariable = "DOTNET_CLI_BENCHMARK_METRICS_DIRECTORY";

    internal const int WarmupCount = 3;
    internal const int IterationCount = 12;

    private BenchmarkSettings _settings = null!;
    private PreparedConfiguration _configuration = null!;
    private string _resultsPath = null!;
    private string _runId = null!;
    private int _invocation;

    protected abstract string CommandName { get; }
    protected abstract bool IsPublish { get; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        string configurationPath = Environment.GetEnvironmentVariable(ConfigurationEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"{ConfigurationEnvironmentVariable} must point to a benchmark JSON configuration.");

        configurationPath = Path.GetFullPath(configurationPath);
        _settings = JsonSerializer.Deserialize<BenchmarkSettings>(
            File.ReadAllText(configurationPath),
            BenchmarkSettings.SerializerOptions)
            ?? throw new InvalidDataException($"Could not deserialize '{configurationPath}'.");
        _settings.Validate(configurationPath);

        _runId = Environment.GetEnvironmentVariable(RunIdEnvironmentVariable)
            ?? throw new InvalidOperationException($"{RunIdEnvironmentVariable} was not set by the benchmark host.");
        _resultsPath = _settings.GetResultsPath(configurationPath, _runId, CommandName);
        Directory.CreateDirectory(Path.GetDirectoryName(_resultsPath)!);
        if (!File.Exists(_resultsPath))
        {
            File.WriteAllText(_resultsPath, ResultRow.Header + Environment.NewLine);
        }

        _configuration = PrepareConfiguration(_settings, configurationPath);
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
            _settings.Label,
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

    private PreparedConfiguration PrepareConfiguration(
        BenchmarkSettings settings,
        string configurationPath)
    {
        string configurationDirectory = Path.GetDirectoryName(configurationPath)!;
        string dotNetPath = ResolvePath(configurationDirectory, settings.DotNetPath);
        if (!File.Exists(dotNetPath))
        {
            throw new InvalidDataException(
                $"DotNetPath does not reference an existing file: '{dotNetPath}'.");
        }

        string orchardCoreRoot = ResolvePath(configurationDirectory, settings.OrchardCoreRoot);
        string fullSolutionPath = Path.Combine(orchardCoreRoot, "OrchardCore.slnx");
        if (!File.Exists(fullSolutionPath))
        {
            throw new InvalidDataException(
                $"OrchardCoreRoot does not contain OrchardCore.slnx: '{orchardCoreRoot}'.");
        }

        string workingDirectory = settings.WorkingDirectory is null
            ? orchardCoreRoot
            : ResolvePath(configurationDirectory, settings.WorkingDirectory);
        if (!Directory.Exists(workingDirectory))
        {
            throw new InvalidDataException(
                $"WorkingDirectory does not reference an existing directory: '{workingDirectory}'.");
        }

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
            settings.EnvironmentVariables ?? [],
            settings.PackArguments ?? [],
            settings.PublishArguments ?? [],
            TimeSpan.FromMinutes(settings.TimeoutMinutes));
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
            buildArguments.Add(_settings.PublishFramework);
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
            arguments.Add(_settings.PublishFramework);
            arguments.AddRange(_configuration.PublishArguments);
        }
        else
        {
            arguments.AddRange(_configuration.PackArguments);
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

        foreach ((string name, string? value) in _configuration.EnvironmentVariables)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
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
                        BenchmarkSettings.SerializerOptions))
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

    private static string ResolvePath(string configurationDirectory, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(configurationDirectory, path));

    private static string TakeTail(string value)
    {
        const int MaximumLength = 4_000;
        return value.Length <= MaximumLength ? value : value[^MaximumLength..];
    }

    private sealed class BenchmarkSettings
    {
        internal static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public string Label { get; init; } = string.Empty;
        public string OrchardCoreRoot { get; init; } = string.Empty;
        public string PublishFramework { get; init; } = "net10.0";
        public string ResultsPath { get; init; } =
            "{benchmark}-{label}-results-{runId}.csv";
        public string DotNetPath { get; init; } = string.Empty;
        public string? WorkingDirectory { get; init; }
        public Dictionary<string, string?>? EnvironmentVariables { get; init; }
        public string[]? PackArguments { get; init; }
        public string[]? PublishArguments { get; init; }
        public int TimeoutMinutes { get; init; } = 30;

        internal void Validate(string configurationPath)
        {
            if (string.IsNullOrWhiteSpace(Label))
            {
                throw new InvalidDataException($"Label is missing from '{configurationPath}'.");
            }

            if (string.IsNullOrWhiteSpace(OrchardCoreRoot))
            {
                throw new InvalidDataException($"OrchardCoreRoot is missing from '{configurationPath}'.");
            }

            if (string.IsNullOrWhiteSpace(PublishFramework))
            {
                throw new InvalidDataException($"PublishFramework is missing from '{configurationPath}'.");
            }

            if (string.IsNullOrWhiteSpace(DotNetPath))
            {
                throw new InvalidDataException($"DotNetPath is missing from '{configurationPath}'.");
            }

            if (TimeoutMinutes <= 0)
            {
                throw new InvalidDataException($"TimeoutMinutes in '{configurationPath}' must be positive.");
            }
        }

        internal string GetResultsPath(
            string configurationPath,
            string runId,
            string benchmark)
        {
            string configurationDirectory = Path.GetDirectoryName(configurationPath)!;
            string path = ResultsPath
                .Replace("{runId}", runId, StringComparison.Ordinal)
                .Replace("{benchmark}", benchmark, StringComparison.OrdinalIgnoreCase)
                .Replace("{label}", Label, StringComparison.OrdinalIgnoreCase);
            return ResolvePath(configurationDirectory, path);
        }
    }

    private sealed record PreparedConfiguration(
        string DotNetPath,
        string WorkingDirectory,
        string SolutionPath,
        string? GeneratedPublishSolutionPath,
        IReadOnlyDictionary<string, string?> EnvironmentVariables,
        IReadOnlyCollection<string> PackArguments,
        IReadOnlyCollection<string> PublishArguments,
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
