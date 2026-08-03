// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

public static class StartupHook
{
    private const string OutputDirectoryEnvironmentVariable = "DOTNET_CLI_BENCHMARK_METRICS_DIRECTORY";
    private const string StartupHooksEnvironmentVariable = "DOTNET_STARTUP_HOOKS";
    private const string MeterName = "dotnet-cli";
    private const string InstrumentName = "dotnet.cli.process_start_to_msbuild_submission.duration";
    private const string CommandNameTag = "command.name";

    private static readonly object s_lock = new();
    private static readonly List<Measurement> s_measurements = [];
    private static MeterListener? s_listener;

    public static void Initialize()
    {
        string? outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        Environment.SetEnvironmentVariable(StartupHooksEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(OutputDirectoryEnvironmentVariable, null);

        s_listener = new MeterListener();
        s_listener.InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name == MeterName && instrument.Name == InstrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        s_listener.SetMeasurementEventCallback<double>(static (_, durationSeconds, tags, _) =>
        {
            string? commandName = null;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == CommandNameTag)
                {
                    commandName = tag.Value as string;
                    break;
                }
            }

            lock (s_lock)
            {
                s_measurements.Add(new Measurement(commandName, durationSeconds));
            }
        });
        s_listener.Start();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => WriteResults(outputDirectory);
    }

    private static void WriteResults(string outputDirectory)
    {
        string outputPath = Path.Combine(outputDirectory, $"metrics-{Environment.ProcessId}.json");
        try
        {
            s_listener?.Dispose();

            Measurement[] measurements;
            lock (s_lock)
            {
                measurements = [.. s_measurements];
            }

            Assembly? cliUtilsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(static assembly =>
                    assembly.GetName().Name == "Microsoft.DotNet.Cli.Utils");
            string? cliUtilsAssemblyPath = cliUtilsAssembly?.Location;
            string? cliUtilsAssemblySha256 =
                cliUtilsAssemblyPath is { Length: > 0 } && File.Exists(cliUtilsAssemblyPath)
                    ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cliUtilsAssemblyPath)))
                    : null;

            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(
                    new MetricsDocument(
                        Environment.ProcessId,
                        Environment.CommandLine,
                        cliUtilsAssemblyPath,
                        cliUtilsAssemblySha256,
                        measurements),
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(outputPath + ".error.txt", exception.ToString());
        }
    }

    private sealed record Measurement(string? CommandName, double DurationSeconds);

    private sealed record MetricsDocument(
        int ProcessId,
        string CommandLine,
        string? CliUtilsAssemblyPath,
        string? CliUtilsAssemblySha256,
        Measurement[] Measurements);
}
