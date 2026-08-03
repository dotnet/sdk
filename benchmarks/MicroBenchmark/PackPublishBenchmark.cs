// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using PackPublishMetricsStartupHook;

namespace Benchmark;

/// <summary>
/// Measures paired external <c>dotnet pack</c> and <c>dotnet publish</c> commands on OrchardCore.
/// </summary>
[SimpleJob(
    RunStrategy.Monitoring,
    launchCount: 1,
    warmupCount: WarmupCount,
    iterationCount: IterationCount,
    invocationCount: 1)]
[MinColumn]
[MaxColumn]
[MedianColumn]
[MarkdownExporter]
public class PackPublishBenchmark
{
    private const string ConfigurationEnvironmentVariable = "DOTNET_SDK_PACK_PUBLISH_BENCHMARK_CONFIG";
    private const string RunIdEnvironmentVariable = "DOTNET_SDK_PACK_PUBLISH_BENCHMARK_RUN_ID";
    private const string MetricsOutputDirectoryEnvironmentVariable = "DOTNET_CLI_BENCHMARK_METRICS_DIRECTORY";
    private const int WarmupCount = 3;
    private const int IterationCount = 12;

    private readonly Dictionary<BenchmarkCell, PreparedCell> _preparedCells = [];
    private BenchmarkSettings _settings = null!;
    private string _resultsPath = null!;
    private string _runId = null!;
    private int _invocation;

    [Params(BenchmarkOperation.Pack, BenchmarkOperation.Publish)]
    public BenchmarkOperation Operation { get; set; }

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
        _resultsPath = _settings.GetResultsPath(configurationPath, _runId);
        Directory.CreateDirectory(Path.GetDirectoryName(_resultsPath)!);
        if (!File.Exists(_resultsPath))
        {
            File.WriteAllText(_resultsPath, ResultRow.Header + Environment.NewLine);
        }

        _preparedCells.Add(
            BenchmarkCell.Before,
            PrepareCell(_settings.Before, configurationPath, BenchmarkCell.Before));
        _preparedCells.Add(
            BenchmarkCell.After,
            PrepareCell(_settings.After, configurationPath, BenchmarkCell.After));

        await PrepareOutputsAsync(_preparedCells[BenchmarkCell.Before]).ConfigureAwait(false);
        await PrepareOutputsAsync(_preparedCells[BenchmarkCell.After]).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public Task GlobalCleanup()
    {
        foreach (PreparedCell cell in _preparedCells.Values)
        {
            if (cell.GeneratedPublishSolutionPath is not null)
            {
                File.Delete(cell.GeneratedPublishSolutionPath);
            }
        }

        return Task.CompletedTask;
    }

    [Benchmark]
    public async Task MeasurePair()
    {
        int invocation = Interlocked.Increment(ref _invocation);
        bool isWarmup = invocation <= WarmupCount;
        int phaseIteration = isWarmup ? invocation : invocation - WarmupCount;
        BenchmarkCell[] order = invocation % 2 == 1
            ? [BenchmarkCell.Before, BenchmarkCell.After]
            : [BenchmarkCell.After, BenchmarkCell.Before];

        foreach (BenchmarkCell cellName in order)
        {
            ProcessMeasurement measurement =
                await RunMeasuredCommandAsync(_preparedCells[cellName]).ConfigureAwait(false);
            ResultRow row = new(
                _runId,
                Operation,
                cellName,
                isWarmup ? "Warmup" : "Measured",
                phaseIteration,
                Array.IndexOf(order, cellName) + 1,
                measurement.TotalDuration.TotalSeconds,
                measurement.PreSubmissionDurationSeconds,
                measurement.CliUtilsAssemblyPath,
                measurement.CliUtilsAssemblySha256,
                _preparedCells[cellName].DotNetPath);
            File.AppendAllText(_resultsPath, row.ToCsv() + Environment.NewLine);
        }
    }

    internal static async Task RunSmokeAsync(BenchmarkOperation? operation = null)
    {
        BenchmarkOperation[] operations = operation is null
            ? [BenchmarkOperation.Pack, BenchmarkOperation.Publish]
            : [operation.Value];
        foreach (BenchmarkOperation selectedOperation in operations)
        {
            PackPublishBenchmark benchmark = new() { Operation = selectedOperation };
            try
            {
                await benchmark.GlobalSetup().ConfigureAwait(false);
                benchmark._invocation = WarmupCount;
                await benchmark.MeasurePair().ConfigureAwait(false);
            }
            finally
            {
                await benchmark.GlobalCleanup().ConfigureAwait(false);
            }
        }
    }

    private PreparedCell PrepareCell(
        ExternalDotNetCell settings,
        string configurationPath,
        BenchmarkCell cellName)
    {
        string configurationDirectory = Path.GetDirectoryName(configurationPath)!;
        string dotNetPath = ResolvePath(configurationDirectory, settings.DotNetPath);
        if (!File.Exists(dotNetPath))
        {
            throw new InvalidDataException(
                $"{cellName}.DotNetPath does not reference an existing file: '{dotNetPath}'.");
        }

        string orchardCoreRoot = ResolvePath(
            configurationDirectory,
            settings.OrchardCoreRoot ?? _settings.OrchardCoreRoot);
        string fullSolutionPath = Path.Combine(orchardCoreRoot, "OrchardCore.slnx");
        if (!File.Exists(fullSolutionPath))
        {
            throw new InvalidDataException(
                $"{cellName}.OrchardCoreRoot does not contain OrchardCore.slnx: '{orchardCoreRoot}'.");
        }

        string workingDirectory = settings.WorkingDirectory is null
            ? orchardCoreRoot
            : ResolvePath(configurationDirectory, settings.WorkingDirectory);
        if (!Directory.Exists(workingDirectory))
        {
            throw new InvalidDataException(
                $"{cellName}.WorkingDirectory does not reference an existing directory: '{workingDirectory}'.");
        }

        string? generatedPublishSolutionPath = null;
        string solutionPath = fullSolutionPath;
        if (Operation == BenchmarkOperation.Publish)
        {
            generatedPublishSolutionPath = CreatePublishSolution(orchardCoreRoot, cellName);
            solutionPath = generatedPublishSolutionPath;
        }

        return new PreparedCell(
            cellName,
            dotNetPath,
            workingDirectory,
            solutionPath,
            generatedPublishSolutionPath,
            settings.EnvironmentVariables ?? [],
            settings.PackArguments ?? [],
            settings.PublishArguments ?? [],
            TimeSpan.FromMinutes(settings.TimeoutMinutes));
    }

    private async Task PrepareOutputsAsync(PreparedCell cell)
    {
        await RunProcessAsync(
            cell,
            ["restore", cell.SolutionPath, "-m", "-v:q"],
            expectedMetricCommand: null,
            timeout: cell.Timeout).ConfigureAwait(false);

        List<string> buildArguments =
        [
            "build",
            cell.SolutionPath,
            "-c",
            "Release",
            "--no-restore",
            "-m",
            "-v:q",
        ];
        if (Operation == BenchmarkOperation.Publish)
        {
            buildArguments.Add("-f");
            buildArguments.Add(_settings.PublishFramework);
        }

        await RunProcessAsync(
            cell,
            buildArguments,
            expectedMetricCommand: null,
            timeout: cell.Timeout).ConfigureAwait(false);
    }

    private async Task<ProcessMeasurement> RunMeasuredCommandAsync(PreparedCell cell)
    {
        List<string> arguments =
        [
            Operation == BenchmarkOperation.Pack ? "pack" : "publish",
            cell.SolutionPath,
            "-c",
            "Release",
            "-m",
            "-v:q",
        ];

        if (Operation == BenchmarkOperation.Publish)
        {
            arguments.Add("-f");
            arguments.Add(_settings.PublishFramework);
            arguments.AddRange(cell.PublishArguments);
        }
        else
        {
            arguments.AddRange(cell.PackArguments);
        }

        return await RunProcessAsync(
            cell,
            arguments,
            expectedMetricCommand:
                Operation == BenchmarkOperation.Pack ? "pack" : "publish",
            timeout: cell.Timeout).ConfigureAwait(false);
    }

    private static async Task<ProcessMeasurement> RunProcessAsync(
        PreparedCell cell,
        IReadOnlyCollection<string> arguments,
        string? expectedMetricCommand,
        TimeSpan timeout)
    {
        string? metricsDirectory = null;
        ProcessStartInfo startInfo = new(cell.DotNetPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = cell.WorkingDirectory,
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

        foreach ((string name, string? value) in cell.EnvironmentVariables)
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
        bool timedOut = await Task.WhenAny(waitForExitTask, Task.Delay(timeout)).ConfigureAwait(false)
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
                    $"'{startInfo.FileName} {string.Join(' ', arguments)}' did not exit within {timeout}.");
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

    private static string CreatePublishSolution(string orchardCoreRoot, BenchmarkCell cell)
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
            $"OrchardCore.PublishPerf.Benchmark.{Environment.ProcessId}.{cell}.slnx");
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

    public enum BenchmarkOperation
    {
        Pack,
        Publish,
    }

    private enum BenchmarkCell
    {
        Before,
        After,
    }

    private sealed class BenchmarkSettings
    {
        internal static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public string OrchardCoreRoot { get; init; } = string.Empty;
        public string PublishFramework { get; init; } = "net10.0";
        public string ResultsPath { get; init; } = "pack-publish-results.csv";
        public ExternalDotNetCell Before { get; init; } = new();
        public ExternalDotNetCell After { get; init; } = new();

        internal void Validate(string configurationPath)
        {
            if (string.IsNullOrWhiteSpace(OrchardCoreRoot))
            {
                throw new InvalidDataException($"OrchardCoreRoot is missing from '{configurationPath}'.");
            }

            if (string.IsNullOrWhiteSpace(PublishFramework))
            {
                throw new InvalidDataException($"PublishFramework is missing from '{configurationPath}'.");
            }

            Before.Validate(configurationPath, nameof(Before));
            After.Validate(configurationPath, nameof(After));
        }

        internal string GetResultsPath(string configurationPath, string runId)
        {
            string configurationDirectory = Path.GetDirectoryName(configurationPath)!;
            return ResolvePath(
                configurationDirectory,
                ResultsPath.Replace("{runId}", runId, StringComparison.Ordinal));
        }
    }

    private sealed class ExternalDotNetCell
    {
        public string DotNetPath { get; init; } = string.Empty;
        public string? OrchardCoreRoot { get; init; }
        public string? WorkingDirectory { get; init; }
        public Dictionary<string, string?>? EnvironmentVariables { get; init; }
        public string[]? PackArguments { get; init; }
        public string[]? PublishArguments { get; init; }
        public int TimeoutMinutes { get; init; } = 30;

        internal void Validate(string configurationPath, string name)
        {
            if (string.IsNullOrWhiteSpace(DotNetPath))
            {
                throw new InvalidDataException($"{name}.DotNetPath is missing from '{configurationPath}'.");
            }

            if (TimeoutMinutes <= 0)
            {
                throw new InvalidDataException($"{name}.TimeoutMinutes must be positive.");
            }
        }
    }

    private sealed record PreparedCell(
        BenchmarkCell Name,
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
        BenchmarkOperation Operation,
        BenchmarkCell Cell,
        string Phase,
        int Iteration,
        int Sequence,
        double TotalDurationSeconds,
        double PreSubmissionDurationSeconds,
        string? CliUtilsAssemblyPath,
        string? CliUtilsAssemblySha256,
        string DotNetPath)
    {
        internal const string Header =
            "RunId,Operation,Cell,Phase,Iteration,Sequence,TotalDurationSeconds," +
            "PreSubmissionDurationSeconds,CliUtilsAssemblyPath,CliUtilsAssemblySha256,DotNetPath";

        internal string ToCsv() => string.Join(
            ',',
            Csv(RunId),
            Csv(Operation.ToString()),
            Csv(Cell.ToString()),
            Csv(Phase),
            Iteration.ToString(CultureInfo.InvariantCulture),
            Sequence.ToString(CultureInfo.InvariantCulture),
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
