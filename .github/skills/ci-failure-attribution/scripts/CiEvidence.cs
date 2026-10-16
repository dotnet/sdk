#!/usr/bin/env dotnet

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:property PublishAot=false

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

return await CiEvidence.RunAsync(args);

static class CiEvidence
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args is ["--help"] or ["-h"])
            {
                Console.WriteLine("""
                    Collect and compare dotnet/sdk CI evidence (read-only).

                    dotnet CiEvidence.cs -- collect --build ID_OR_URL --output-directory PATH
                    dotnet CiEvidence.cs -- collect --pull-request NUMBER_OR_URL [--build ID_OR_URL] --output-directory PATH
                    dotnet CiEvidence.cs -- compare --evidence-path PATH --output-directory PATH

                    Collection options:
                      --baseline-build ID_OR_URL    Earlier standalone scenario execution
                      --history-limit N            Candidate builds per pipeline/ref (1-100; default 20)
                      --lookback-days N            History window (1-365; default 7)
                      --test-result-limit N        Estimated results per build (1-1000000; default 100000)
                      --timeout-seconds N          Per acquisition (1-3600; default 120)
                      --gh-path PATH               gh executable (default gh)
                      --hlx-path PATH              hlx 0.11.x executable (default hlx)
                      --az-path PATH               Azure CLI executable (default az)
                      --replay-directory PATH      Recorded snapshots; no service access
                    """);
                return args.Length == 0 ? 1 : 0;
            }

            Options options = Options.Parse(args);
            if (args[0] == "collect")
            {
                await new Collector(options).CollectAsync();
            }
            else
            {
                JsonObject comparison = Comparison.Compare(JsonData.Read(options.Required("evidence-path")));
                Comparison.Write(comparison, options.Required("output-directory"));
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

sealed class Options
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public string? this[string key] => _values.GetValueOrDefault(key);
    public string Required(string key) => this[key] ?? throw new ArgumentException($"Missing --{key}.");
    public int Number(string key, int fallback, int maximum)
    {
        if (this[key] is not string value)
        {
            return fallback;
        }
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= maximum
            ? number : throw new ArgumentException($"--{key} must be between 1 and {maximum}.");
    }

    public static Options Parse(string[] args)
    {
        string[] allowed = args[0] switch
        {
            "collect" => ["pull-request", "build", "baseline-build", "output-directory", "history-limit", "lookback-days",
                "test-result-limit", "timeout-seconds", "gh-path", "hlx-path", "az-path", "replay-directory"],
            "compare" => ["evidence-path", "output-directory"],
            _ => throw new ArgumentException("Use collect or compare. See --help.")
        };
        Options options = new();
        for (int i = 1; i < args.Length; i += 2)
        {
            string key = args[i].StartsWith("--", StringComparison.Ordinal) ? args[i][2..] : "";
            if (!allowed.Contains(key, StringComparer.Ordinal) || i + 1 == args.Length ||
                string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal) ||
                !options._values.TryAdd(key, args[i + 1]))
            {
                throw new ArgumentException($"Invalid, duplicate, or missing value for option '{args[i]}'. See --help.");
            }
        }
        _ = options.Required("output-directory");
        if (args[0] == "collect" && options["pull-request"] is null && options["build"] is null)
        {
            throw new ArgumentException("Supply --pull-request or --build.");
        }
        if (options["baseline-build"] is not null && options["pull-request"] is not null)
        {
            throw new ArgumentException("--baseline-build is for standalone builds only.");
        }
        return options;
    }
}

static class JsonData
{
    public const string GuidPattern = @"[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b";
    public const string MonitorJobPattern =
        $@"(?i)\bWork item\s+'[^'\r\n]+'\s+in job\s+'[^'\r\n]*\((?<job>{GuidPattern})\)'\s+failed";
    public static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 100 };
    private static readonly JsonSerializerOptions s_indented = new(Compact) { WriteIndented = true };

    public static string Text(this JsonNode? node, string key) => Scalar(node?[key]);
    public static string Scalar(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text)
        ? text : node?.ToJsonString(Compact) ?? "";
    public static bool Flag(this JsonNode? node, string key) => node?[key] is JsonValue value &&
        value.TryGetValue(out bool result) && result;
    public static long Number(this JsonNode? node, string key) => node?[key] is JsonValue value &&
        value.TryGetValue(out long result) ? result : throw new InvalidDataException($"Expected integer '{key}'.");
    public static JsonArray Rows(JsonNode? node) => node as JsonArray ?? throw new InvalidDataException("Expected a JSON array.");
    public static JsonArray Rows(this JsonNode node, string key) => Rows(node[key]);
    public static IEnumerable<JsonObject> Objects(this JsonArray array) => array.Select(node =>
        node as JsonObject ?? throw new InvalidDataException("Expected a JSON object array entry."));
    public static JsonObject Object(object value) => JsonSerializer.SerializeToNode(value, Compact)!.AsObject();
    public static JsonArray Array(IEnumerable<JsonNode?> values) => new(values.Select(n => n?.DeepClone()).ToArray());
    public static JsonNode? Parse(string text) => JsonNode.Parse(text, documentOptions: new() { MaxDepth = 100 });
    public static JsonObject Read(string path) => Parse(File.ReadAllText(path)) as JsonObject ??
        throw new InvalidDataException($"Expected a JSON object in {path}.");
    public static JsonNode? BuildParameters(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue(out string? text))
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            try
            {
                node = JsonNode.Parse(text, documentOptions: new() { MaxDepth = 100 });
            }
            catch (JsonException)
            {
                throw new InvalidDataException("Invalid JSON-encoded build parameters.");
            }
        }
        if (node is not null and not JsonObject)
        {
            throw new InvalidDataException("Expected build parameters to be a JSON object.");
        }
        return node?.DeepClone();
    }
    public static string NormalizeLogTimestamps(string text) => Regex.Replace(text,
        @"(?m)^(\[?)\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})(\]?[ \t]+)(?=\S)",
        "$1<timestamp>$2");
    public static void Write(JsonNode node, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, node.ToJsonString(s_indented) + "\n", new UTF8Encoding(false));
    }
    public static DateTimeOffset Time(JsonNode node) => DateTimeOffset.Parse(node.Text("queuedAt"), CultureInfo.InvariantCulture);
}

sealed record Acquisition(string Description, string Snapshot, int ExitCode, bool Ok, bool Complete,
    JsonNode? Data, string Error, bool Replayed, string? ReplaySnapshot)
{
    public JsonObject Summary() => JsonData.Object(new
    {
        description = Description, snapshot = Snapshot, exitCode = ExitCode, ok = Ok,
        complete = Complete, error = Error, replayed = Replayed
    });
}

sealed class Reader(string snapshots, string? replayDirectory, string ghPath, string hlxPath, int timeoutSeconds, string azPath = "az")
{
    private Dictionary<string, string>? _replayIndex;

    public static string Key(string tool, string[] arguments) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { tool }.Concat(arguments), JsonData.Compact))));

    private string ReplayPath(string tool, string[] arguments)
    {
        string key = Key(tool, arguments);
        string direct = Path.Combine(replayDirectory!, key + ".json");
        if (File.Exists(direct))
        {
            return direct;
        }
        // Older PowerShell snapshots may use different JSON escaping in their hash input.
        if (_replayIndex is null)
        {
            _replayIndex = new(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(replayDirectory!, "*.json"))
            {
                JsonObject snapshot = JsonData.Read(file);
                if (snapshot["arguments"] is JsonArray args && snapshot["tool"] is not null)
                {
                    _replayIndex.Add(Key(snapshot.Text("tool"), args.Select(JsonData.Scalar).ToArray()), file);
                }
            }
        }
        return _replayIndex.GetValueOrDefault(key) ??
            throw new InvalidDataException($"Missing replay command: {tool} {string.Join(' ', arguments)}");
    }

    private static void RequireRead(string tool, string[] arguments)
    {
        bool method = false;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == "--method")
            {
                if (method || i + 1 == arguments.Length || arguments[++i] != "GET")
                {
                    throw new ArgumentException("Only read-only acquisition commands are permitted.");
                }
                method = true;
            }
            else if (Regex.IsMatch(arguments[i], "^(-X|--method=|--input|--field|--raw-field|-f|-F)"))
            {
                throw new ArgumentException("Only read-only acquisition commands are permitted.");
            }
        }
        bool supported = arguments.Length > 0 && (tool switch
        {
            "gh" => arguments[0] is "api" && method,
            "az" => arguments is ["rest", "--method", "GET", "--url", string url, "--resource",
                "499b84ac-1321-427f-aa17-267ca6975798", "--output", "json", "--only-show-errors"] &&
                Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == "https" &&
                uri.Host == "dev.azure.com" && uri.UserInfo.Length == 0 &&
                Regex.IsMatch(uri.AbsolutePath, @"^/[^/]+/[^/]+/_apis/"),
            "hlx" => arguments[0] is "--version" or "status" or "files" or "logs" ||
                arguments[0] == "azdo" && arguments.Length > 1 &&
                arguments[1] is "build" or "builds" or "changes" or "timeline" or "test-runs" or "test-results" or "artifacts" or "log",
            _ => false
        });
        if (!supported)
        {
            throw new ArgumentException("Only supported read-only acquisition commands are permitted.");
        }
    }

    public async Task<Acquisition> ReadAsync(string tool, string[] arguments, bool text = false, bool list = false)
    {
        RequireRead(tool, arguments);
        string snapshotPath = Path.Combine(snapshots, Key(tool, arguments) + ".json");
        string? replayPath = null;
        string stdout;
        string stderr;
        int exitCode;
        if (replayDirectory is not null)
        {
            replayPath = ReplayPath(tool, arguments);
            JsonObject snapshot = JsonData.Read(replayPath);
            exitCode = checked((int)snapshot.Number("exitCode"));
            stdout = snapshot.Text("stdout");
            stderr = snapshot.Text("stderr");
        }
        else
        {
            ProcessStartInfo start = new(tool switch { "gh" => ghPath, "az" => azPath, _ => hlxPath })
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            if (tool == "az" && OperatingSystem.IsWindows())
            {
                string? launcher = Path.IsPathRooted(azPath) ? azPath :
                    (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                        .SelectMany(p => new[] { Path.Combine(p, azPath + ".exe"), Path.Combine(p, azPath + ".cmd") })
                        .FirstOrDefault(File.Exists);
                if (launcher is not null && Path.GetExtension(launcher).Equals(".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    string python = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(launcher)!, "..", "python.exe"));
                    if (!File.Exists(python))
                    {
                        throw new InvalidDataException("Azure CLI launcher has no companion python.exe; supply --az-path to an executable.");
                    }
                    start.FileName = python;
                    start.ArgumentList.Add("-IBm");
                    start.ArgumentList.Add("azure.cli");
                }
            }
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            start.Environment["GCM_INTERACTIVE"] = "never";
            if (tool == "az")
            {
                start.Environment["PYTHONIOENCODING"] = "utf-8";
                start.StandardOutputEncoding = Encoding.UTF8;
                start.StandardErrorEncoding = Encoding.UTF8;
            }
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            using Process process = new() { StartInfo = start };
            process.Start();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds));
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                stdout = await output.WaitAsync(timeout.Token);
                stderr = await error.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                await process.WaitForExitAsync();
                throw new TimeoutException($"{tool} acquisition exceeded {timeoutSeconds} seconds.");
            }
            exitCode = process.ExitCode;
        }
        JsonNode? data = null;
        string errorMessage = "";
        if (text)
        {
            data = JsonValue.Create(stdout);
        }
        else if (!string.IsNullOrWhiteSpace(stdout))
        {
            try
            {
                data = JsonData.Parse(stdout);
                if (tool == "hlx" && arguments is ["azdo", "builds", ..] && data is JsonArray builds)
                {
                    foreach (JsonObject build in builds.Objects().Where(b => b.ContainsKey("parameters")))
                    {
                        build["parameters"] = JsonData.BuildParameters(build["parameters"]);
                    }
                }
                if (tool == "az" && data is JsonObject metadata && arguments.Length > 4 &&
                    Regex.IsMatch(arguments[4], @"/_apis/(?:build/builds/\d+|pipelines/\d+/runs/\d+)\?"))
                {
                    JsonObject inputs = [];
                    foreach (string key in new[]
                    {
                        "id", "sourceVersion", "sourceBranch", "repository", "definition", "queueTime",
                        "status", "result", "reason", "triggerInfo", "pipeline", "parameters",
                        "templateParameters", "resources"
                    })
                    {
                        if (metadata.ContainsKey(key))
                        {
                            inputs[key] = key == "parameters" ? JsonData.BuildParameters(metadata[key]) : metadata[key]?.DeepClone();
                        }
                    }
                    data = inputs;
                }
                stdout = data?.ToJsonString(JsonData.Compact) ?? "null";
            }
            catch (JsonException ex)
            {
                errorMessage = $"Invalid JSON from {tool}: {ex.Message}";
            }
        }
        else
        {
            errorMessage = $"Empty JSON response from {tool}.";
        }
        JsonData.Write(JsonData.Object(new
        {
            tool, arguments, exitCode, stdout, stderr, replayed = replayDirectory is not null
        }), snapshotPath);
        bool ok = exitCode == 0 && errorMessage.Length == 0;
        if (exitCode != 0 && errorMessage.Length == 0)
        {
            errorMessage = data is JsonObject && data["error"] is JsonNode error
                ? error.ToJsonString(JsonData.Compact) : $"Producer exited {exitCode}. Inspect {snapshotPath}.";
        }
        bool complete = ok;
        if (list)
        {
            complete = ok && data is JsonObject && data.Flag("ok") && data.Flag("complete") &&
                data["truncated"] is JsonValue truncated && truncated.TryGetValue(out bool flag) && !flag &&
                data["results"] is JsonArray;
            ok &= data is JsonObject && data.Flag("ok");
            if (!complete)
            {
                errorMessage = $"List acquisition failed or is incomplete; no negative finding is supported. {errorMessage}";
            }
        }
        return new($"{tool} {string.Join(' ', arguments)}", snapshotPath, exitCode, ok, complete, data,
            $"{errorMessage} {stderr}", replayDirectory is not null, replayPath);
    }
}

sealed record Compatibility(bool Eligible, string[] Differences, string[] Limitations, JsonNode[] Executions);

static class Comparison
{
    public static string? Signature(JsonNode observation)
    {
        string text = observation["signatureMechanism"] is null ? observation.Text("mechanism") : observation.Text("signatureMechanism");
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        const string GuidPattern = JsonData.GuidPattern;
        text = Regex.Replace(text, $@"(?i)(\b(?:HelixJob|(?:Correlation|Activity|Request)[ _-]?Id)\s*[:=]\s*){GuidPattern}", "$1<guid>");
        text = Regex.Replace(text, $@"(?i)(https://helix\.dot\.net/(?:api/(?:\d{{4}}-\d{{2}}-\d{{2}}/)?)?jobs/){GuidPattern}", "$1<guid>");
        text = Regex.Replace(text, JsonData.MonitorJobPattern, match =>
        {
            Group job = match.Groups["job"];
            int offset = job.Index - match.Index;
            return match.Value[..offset] + "<guid>" + match.Value[(offset + job.Length)..];
        });
        text = Regex.Replace(text, @"(?<![\w./\\-])(?:(?i:[A-Z]:\\(?:Users\\[^\\\s]+\\AppData\\Local\\)?(?:tmp|temp)\\)|/(?:tmp|var/tmp)/)", "<temporary-root>/");
        text = Regex.Replace(text, @"(?<![\w./\\-])(?:(?i:[A-Z]:\\a\\_work\\\d+\\s\\)|/mnt/vss/_work/\d+/s/)", "<workspace>/");
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        return $"{observation.Text("kind")}|{observation.Text("name")}|{text}";
    }

    public static bool MissingFailureEvidence(JsonNode build) =>
        build.Text("result").ToLowerInvariant() is "failed" or "canceled" &&
        !build.Rows("observations").Objects().Any(o => o.Text("outcome") is "Failed" or "Unknown");

    public static IEnumerable<string> IncompleteTestRuns(JsonNode build)
    {
        foreach (JsonObject run in (build["testRuns"] is null ? new JsonArray() : build.Rows("testRuns")).Objects())
        {
            if (!string.Equals(run.Text("state"), "Completed", StringComparison.OrdinalIgnoreCase) ||
                run["incompleteTests"] is null || run.Number("incompleteTests") != 0)
            {
                yield return $"Incomplete test run {run.Text("id")} for build {build.Text("id")}: " +
                    $"state='{run.Text("state")}', incompleteTests='{run.Text("incompleteTests")}'.";
            }
        }
    }

    public static IEnumerable<string> TestResultCoverageGaps(JsonNode build)
    {
        bool includePassed = build["testResultsIncludePassed"] is null || build.Flag("testResultsIncludePassed");
        if (!includePassed)
        {
            yield return $"Passing test results omitted for build {build.Text("id")}; no complete result coverage is supported.";
        }
        ILookup<string, JsonObject> results = build.Rows("observations").Objects()
            .Where(o => o.Text("kind") is "test" or "test-group" && o["parentResultId"] is null)
            .ToLookup(o => o.Text("id").Split(':', 3) is ["test", string runId, _] ? runId : "", StringComparer.Ordinal);
        foreach (JsonObject run in (build["testRuns"] is null ? new JsonArray() : build.Rows("testRuns")).Objects())
        {
            string context = $"Test result coverage for run {run.Text("id")} in build {build.Text("id")}";
            JsonObject[] acquired = results[run.Text("id")].ToArray();
            JsonObject[] distinct = acquired.DistinctBy(o => o.Text("id"), StringComparer.Ordinal).ToArray();
            if (distinct.Length != acquired.Length)
            {
                yield return $"{context}: duplicate result IDs.";
            }
            foreach ((string counter, long count, bool exact) in new (string, long, bool)[]
            {
                ("totalTests", distinct.Length, includePassed),
                ("failedTests", distinct.LongCount(o => o.Text("outcome") == "Failed"), false),
                ("passedTests", distinct.LongCount(o => o.Text("outcome") == "Passed"), includePassed)
            })
            {
                if (!includePassed && counter is "totalTests" or "passedTests")
                {
                    continue;
                }
                if (run[counter] is null)
                {
                    yield return $"{context}: missing {counter} counter.";
                    continue;
                }
                long expected = run.Number(counter);
                // Failed observations also include error/timeout outcomes, so the counter is a lower bound.
                if (expected < 0 || (exact ? count != expected : count < expected))
                {
                    yield return $"{context}: {counter} advertises {expected}, but acquired {count} distinct results.";
                }
            }
        }
    }

    private static IEnumerable<string> ScenarioIdentityLimitations(JsonNode observation)
    {
        string name = observation.Text("name");
        if (string.IsNullOrWhiteSpace(name) || observation.Text("kind") is "test" or "test-group" &&
            name.Split(" | ", StringSplitOptions.None).All(string.IsNullOrWhiteSpace))
        {
            yield return "Missing scenario name";
        }
        if (observation.Text("kind") is "test" or "test-group" && !observation.Flag("hierarchyVerified"))
        {
            yield return "Missing test hierarchy provenance";
        }
        if (observation.Text("kind") == "work-item" && observation["submissionContexts"] is not JsonArray { Count: > 0 })
        {
            yield return "Missing Helix submission context";
        }
        if (observation.Text("kind") == "work-item" && string.IsNullOrWhiteSpace(observation.Text("queue")))
        {
            yield return "Missing Helix queue";
        }
        if (observation.Text("kind") is "job" or "task" && !observation.Flag("executionVerified"))
        {
            yield return "Unverified timeline execution";
        }
        string lane = observation.Text("lane");
        if (string.IsNullOrWhiteSpace(lane) || observation.Text("kind") is "job" or "task" &&
            lane.Split(" > ", StringSplitOptions.None).Any(string.IsNullOrWhiteSpace))
        {
            yield return "Missing lane";
        }
    }

    public static IEnumerable<string> ScenarioIdentityGaps(JsonNode build)
    {
        foreach (JsonObject observation in build.Rows("observations").Objects())
        {
            string[] limitations = ScenarioIdentityLimitations(observation).ToArray();
            if (limitations.Length > 0)
            {
                yield return $"Unverified scenario identity for build {build.Text("id")}, observation {observation.Text("id")}: " +
                    string.Join("; ", limitations);
            }
        }
    }

    public static IEnumerable<string> CoverageGaps(JsonNode build) =>
        IncompleteTestRuns(build).Concat(TestResultCoverageGaps(build))
            .Concat(ScenarioIdentityGaps(build));

    private static (string Kind, string Name, string Lane) ScenarioKey(JsonNode observation) =>
        (observation.Text("kind"), observation.Text("name"), observation.Text("lane"));

    public static ILookup<(string Kind, string Name, string Lane), JsonObject> IndexExecutions(JsonNode build) =>
        build.Rows("observations").Objects().Where(o => !ScenarioIdentityLimitations(o).Any()).ToLookup(ScenarioKey);

    public static Compatibility Compatible(JsonNode build, JsonNode other, JsonNode observation,
        ILookup<(string Kind, string Name, string Lane), JsonObject> executionIndex)
    {
        List<string> differences = [];
        List<string> limitations = ScenarioIdentityLimitations(observation).ToList();
        bool knownIdentity = limitations.Count == 0;
        foreach (string key in new[] { "repository", "organization", "project", "definitionId" })
        {
            if (string.IsNullOrWhiteSpace(build.Text(key)) || string.IsNullOrWhiteSpace(other.Text(key)))
            {
                limitations.Add($"Missing {key}");
                knownIdentity = false;
            }
            else if (build.Text(key) != other.Text(key))
            {
                differences.Add($"Different {key}");
            }
        }
        JsonNode[] executions = executionIndex[ScenarioKey(observation)].ToArray();
        bool executed = executions.Any(e => e.Text("outcome") is "Passed" or "Failed");
        if (!executed)
        {
            limitations.Add("Verified scenario execution not found in this lane");
        }
        foreach (string key in new[] { "parameters", "templateParameters", "resources" })
        {
            if (build[key] is null || other[key] is null)
            {
                limitations.Add($"Unverified {key}");
            }
            else if (!JsonNode.DeepEquals(build[key], other[key]))
            {
                limitations.Add($"Changed {key}");
            }
        }
        limitations.Add("Queue, toolchain and platform require source/log verification");
        if (string.IsNullOrWhiteSpace(other.Text("branch")) || string.IsNullOrWhiteSpace(other.Text("reason")))
        {
            limitations.Add("Unverified trigger provenance");
        }
        return new(knownIdentity && differences.Count == 0 && executed, differences.ToArray(), limitations.ToArray(), executions);
    }

    public static JsonObject FindObservation(ILookup<string, JsonObject> observations, string id)
    {
        JsonObject[] matches = observations[id].Take(2).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidDataException($"Comparison observation {id} is absent from its build."),
            _ => throw new InvalidDataException($"Comparison observation {id} is ambiguous in its build.")
        };
    }

    private static void RequireFields(JsonObject obj, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (!obj.ContainsKey(key))
            {
                throw new InvalidDataException($"Evidence contract missing '{key}'.");
            }
        }
    }

    private static void Validate(JsonObject dossier)
    {
        if (dossier.Number("schemaVersion") != 1)
        {
            throw new InvalidDataException("Unsupported evidence contract: expected schemaVersion 1.");
        }
        RequireFields(dossier, "mode", "repository", "selectedBuildIds", "builds", "acquisitions", "checks", "changeWindows");
        if (dossier.Text("mode") is not ("pr" or "build") || dossier.Text("repository") != "dotnet/sdk")
        {
            throw new InvalidDataException("Evidence must describe dotnet/sdk in pr or build mode.");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonNode? node in dossier.Rows("builds"))
        {
            JsonObject build = node as JsonObject ?? throw new InvalidDataException("Invalid build object.");
            RequireFields(build, "id", "url", "repository", "organization", "project", "definitionId", "branch",
                "commit", "queuedAt", "observations", "complete", "reason", "parameters", "resources");
            if (build["coverageGaps"] is not null and not JsonArray)
            {
                throw new InvalidDataException($"Invalid coverage gaps for build {build.Text("id")}.");
            }
            if (!ids.Add(build.Text("id")))
            {
                throw new InvalidDataException($"Duplicate build ID {build.Text("id")}.");
            }
            foreach (JsonNode? entry in build.Rows("observations"))
            {
                JsonObject observation = entry as JsonObject ?? throw new InvalidDataException("Invalid observation object.");
                RequireFields(observation, "id", "kind", "name", "lane", "outcome", "mechanism", "url", "contextOnly");
                if (observation.Text("outcome") is not ("Passed" or "Failed" or "Skipped" or "Unknown"))
                {
                    throw new InvalidDataException($"Unknown observation outcome: {observation.Text("outcome")}");
                }
            }
        }
        if (dossier.Rows("selectedBuildIds").Count == 0)
        {
            throw new InvalidDataException("No selected build IDs.");
        }
        foreach (JsonNode? id in dossier.Rows("selectedBuildIds"))
        {
            if (!ids.Contains(JsonData.Scalar(id)))
            {
                throw new InvalidDataException($"Selected build {JsonData.Scalar(id)} is absent.");
            }
        }
    }

    public static JsonObject Compare(JsonObject dossier)
    {
        Validate(dossier);
        JsonObject[] builds = dossier.Rows("builds").Objects().ToArray();
        foreach (JsonObject build in builds)
        {
            build["parameters"] = JsonData.BuildParameters(build["parameters"]);
        }
        Dictionary<string, ILookup<(string Kind, string Name, string Lane), JsonObject>> executionIndexes =
            builds.ToDictionary(b => b.Text("id"), IndexExecutions, StringComparer.Ordinal);
        JsonArray comparisons = [];
        List<string> gaps = [];
        if (dossier["error"] is not null)
        {
            gaps.Add($"Collection failed: {dossier.Text("error")}");
        }
        foreach (JsonNode acquisition in dossier.Rows("acquisitions").Objects())
        {
            if (!acquisition.Flag("ok") || !acquisition.Flag("complete"))
            {
                gaps.Add(acquisition.Text("description"));
            }
        }
        gaps.AddRange(dossier.Rows("checks").Objects().Where(c => c.Text("coverage") != "assessed")
            .Select(c => $"Unassessed check: {c.Text("name")}"));
        gaps.AddRange(dossier.Rows("changeWindows").Objects().Where(w => !w.Flag("complete"))
            .Select(w => $"Unverified change window for build {w.Text("buildId")}"));
        foreach (JsonObject build in builds)
        {
            gaps.AddRange(build["coverageGaps"] is JsonArray stored
                ? stored.Select(JsonData.Scalar)
                : CoverageGaps(build));
        }
        HashSet<string> selected = dossier.Rows("selectedBuildIds").Select(JsonData.Scalar).ToHashSet(StringComparer.Ordinal);
        foreach (JsonObject build in dossier.Rows("builds").Objects().Where(b => selected.Contains(b.Text("id"))))
        {
            if (!build.Flag("complete"))
            {
                gaps.Add($"Incomplete selected build {build.Text("id")}");
            }
            if (MissingFailureEvidence(build) &&
                !dossier.Rows("acquisitions").Objects().Any(a =>
                    a.Text("description").StartsWith($"Missing failure evidence for build {build.Text("id")};", StringComparison.Ordinal)))
            {
                gaps.Add($"Missing failure evidence for build {build.Text("id")}");
            }
            JsonObject[] prior = dossier.Rows("builds").Objects().Where(b => b.Text("id") != build.Text("id") &&
                b.Text("queuedAt").Length > 0 && build.Text("queuedAt").Length > 0 && JsonData.Time(b) < JsonData.Time(build))
                .OrderByDescending(JsonData.Time).ToArray();
            foreach (JsonObject failure in build.Rows("observations").Objects()
                .Where(f => f.Text("outcome") is "Failed" or "Unknown"))
            {
                if (failure.Text("outcome") == "Unknown")
                {
                    gaps.Add($"Unknown execution outcome: {failure.Text("name")}");
                }
                string? signature = Signature(failure);
                JsonObject[] controls = prior.Select(other =>
                {
                    Compatibility compatibility = Compatible(build, other, failure, executionIndexes[other.Text("id")]);
                    return JsonData.Object(new
                    {
                        buildId = other.Text("id"), url = other["url"], branch = other["branch"], commit = other["commit"],
                        reason = other["reason"], complete = other.Flag("complete"),
                        sameSource = other.Text("commit").Length > 0 && other.Text("commit") == build.Text("commit"),
                        samePrHead = build.Text("prHeadSha").Length > 0 &&
                            (other.Text("prHeadSha") == build.Text("prHeadSha") || other.Text("commit") == build.Text("prHeadSha")),
                        independentProvenance = KnownNonPrExecution(other),
                        eligible = compatibility.Eligible, limitations = compatibility.Limitations, differences = compatibility.Differences,
                        outcomes = compatibility.Executions.Select(e => e.Text("outcome")).ToArray(),
                        matchingFailure = compatibility.Executions.Any(e =>
                            e.Text("outcome") == "Failed" && signature is not null && Signature(e) == signature)
                    });
                }).ToArray();
                JsonObject[] passing = controls.Where(c => c.Flag("eligible") && c.Rows("outcomes").Count > 0 &&
                    c.Rows("outcomes").All(o => JsonData.Scalar(o) == "Passed")).ToArray();
                JsonObject[] matching = controls.Where(c => c.Flag("eligible") && c.Flag("matchingFailure")).ToArray();
                comparisons.Add(JsonData.Object(new
                {
                    buildId = build["id"], observationId = failure["id"], kind = failure["kind"], name = failure["name"],
                    hierarchyVerified = failure.Flag("hierarchyVerified"), executionVerified = failure.Flag("executionVerified"),
                    submissionContexts = failure["submissionContexts"], queue = failure["queue"],
                    lane = failure["lane"], contextOnly = failure.Flag("contextOnly"), currentOutcome = failure["outcome"],
                    mechanism = failure["mechanism"], signature, url = failure["url"], controls,
                    precedingExecution = controls.FirstOrDefault(c => c.Flag("eligible") && c.Text("branch") == build.Text("branch")),
                    mostRecentPassingExecution = passing.FirstOrDefault(), matchingOccurrences = matching.Length,
                    independentMatchingOccurrences = matching.Count(c => c.Text("commit").Length > 0 && build.Text("commit").Length > 0 &&
                        !c.Flag("sameSource") && !c.Flag("samePrHead") &&
                        c.Flag("independentProvenance")),
                    observedMixedOutcomes = passing.Length > 0 && matching.Length > 0
                }));
            }
        }
        return JsonData.Object(new
        {
            schemaVersion = 1, mode = dossier["mode"], repository = dossier["repository"],
            selectedBuildIds = dossier["selectedBuildIds"], changeWindows = dossier["changeWindows"],
            coverageGaps = gaps, comparisons,
            interpretation = "Comparison facts only. Eligible controls still require verification of their listed limitations."
        });
    }

    private static bool KnownNonPrExecution(JsonNode build) =>
        (build.Text("branch").StartsWith("refs/heads/", StringComparison.Ordinal) ||
         build.Text("branch").StartsWith("refs/tags/", StringComparison.Ordinal)) &&
        build.Text("reason").ToLowerInvariant() is "manual" or "individualci" or "batchedci" or
            "schedule" or "scheduleforced" or "buildcompletion" or "resourcetrigger";

    public static void Write(JsonObject comparison, string outputDirectory)
    {
        string jsonPath = Path.Combine(outputDirectory, "comparison.json");
        JsonData.Write(comparison, jsonPath);
        List<string> lines =
        [
            "# CI comparison facts", "",
            "These are observations, not causal verdicts. Read comparison.json for control limitations and full coverage.", "",
            "| Build / failure | Prior matches | Passing control |", "| --- | --- | --- |"
        ];
        foreach (JsonNode row in comparison.Rows("comparisons").Objects())
        {
            string name = Regex.Replace(row.Text("name"), "[|\\r\\n]", " ").Replace("<", "&lt;", StringComparison.Ordinal);
            string control = row["mostRecentPassingExecution"]?.Text("buildId") ?? "Not established";
            lines.Add($"| {row.Text("buildId")} / {name} | {row.Text("matchingOccurrences")} | {control} |");
        }
        if (comparison.Rows("coverageGaps").Count > 0)
        {
            lines.AddRange(["", "## Coverage gaps"]);
            lines.AddRange(comparison.Rows("coverageGaps").Select(g => "- " + Regex.Replace(JsonData.Scalar(g), "[\\r\\n]", " ")));
        }
        File.WriteAllLines(Path.Combine(outputDirectory, "comparison.md"), lines, new UTF8Encoding(false));
        Console.WriteLine(jsonPath);
    }
}

sealed record BuildIdentity(string Id, string Organization, string Project, string Url)
{
    public static BuildIdentity Resolve(string value)
    {
        if (Regex.IsMatch(value, @"^\d+$"))
        {
            return new(value, "dnceng-public", "public", $"https://dev.azure.com/dnceng-public/public/_build/results?buildId={value}");
        }
        return TryResolveUrl(value, out BuildIdentity? identity) ? identity :
            throw new ArgumentException("Use an Azure build ID or https://dev.azure.com/ORG/PROJECT/_build/results?buildId=ID URL.");
    }

    public static bool TryResolveUrl(string value, [NotNullWhen(true)] out BuildIdentity? identity)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == "https" && uri.Host == "dev.azure.com" &&
            uri.UserInfo.Length == 0 && Regex.IsMatch(uri.AbsolutePath, @"^/[^/]+/[^/]+/_build/results/?$") &&
            Regex.Match(uri.Query, @"(?:^\?|&)buildId=(\d+)(?:&|$)") is { Success: true } match)
        {
            string[] parts = uri.AbsolutePath.Split('/');
            string id = match.Groups[1].Value;
            identity = new(id, Uri.UnescapeDataString(parts[1]), Uri.UnescapeDataString(parts[2]),
                $"https://dev.azure.com/{parts[1]}/{parts[2]}/_build/results?buildId={id}");
            return true;
        }
        identity = null;
        return false;
    }
}

sealed class Collector
{
    private readonly Options _options;
    private readonly string _outputDirectory;
    private readonly int _historyLimit;
    private readonly int _lookbackDays;
    private readonly int _testResultLimit;
    private readonly Reader _reader;
    private readonly JsonObject _dossier;
    private readonly Dictionary<string, JsonObject> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Acquisition> _cache = new(StringComparer.Ordinal);

    public Collector(Options options)
    {
        _options = options;
        _outputDirectory = Path.GetFullPath(options.Required("output-directory"));
        _historyLimit = options.Number("history-limit", 20, 100);
        _lookbackDays = options.Number("lookback-days", 7, 365);
        _testResultLimit = options.Number("test-result-limit", 100000, 1000000);
        _reader = new(Path.Combine(_outputDirectory, "snapshots"), options["replay-directory"],
            options["gh-path"] ?? "gh", options["hlx-path"] ?? "hlx", options.Number("timeout-seconds", 120, 3600),
            options["az-path"] ?? "az");
        _dossier = JsonData.Object(new
        {
            schemaVersion = 1, mode = options["pull-request"] is null ? "build" : "pr", repository = "dotnet/sdk",
            selectedBuildIds = new JsonArray(), pr = (JsonNode?)null, checks = new JsonArray(), builds = new JsonArray(),
            acquisitions = new JsonArray(), changeWindows = new JsonArray(), buildAnalysis = new JsonArray(),
            issueSearches = new JsonArray(), history = new { limit = _historyLimit, lookbackDays = _lookbackDays, queries = new JsonArray() },
            generatedAt = DateTimeOffset.UtcNow.ToString("o"), replayed = options["replay-directory"] is not null
        });
    }

    private async Task<Acquisition> ReadAsync(string tool, string[] arguments, bool text = false, bool list = false)
    {
        string key = Reader.Key(tool, arguments) + $":{text}:{list}";
        if (_cache.TryGetValue(key, out Acquisition? cached))
        {
            return cached;
        }
        Acquisition result;
        try
        {
            result = await _reader.ReadAsync(tool, arguments, text, list);
        }
        catch (Exception ex)
        {
            _dossier.Rows("acquisitions").Add(JsonData.Object(new
            {
                description = $"{tool} {string.Join(' ', arguments)}", snapshot = (string?)null, exitCode = (int?)null,
                ok = false, complete = false, error = ex.Message,
                replayed = _options["replay-directory"] is not null
            }));
            throw;
        }
        _dossier.Rows("acquisitions").Add(result.Summary());
        _cache.Add(key, result);
        if (!result.Complete)
        {
            Console.Error.WriteLine($"WARNING: {result.Description}: {result.Error}");
        }
        return result;
    }

    private void Gap(JsonObject record, string description, bool ok = false)
    {
        record["complete"] = false;
        _dossier.Rows("acquisitions").Add(JsonData.Object(new { description, ok, complete = false }));
    }

    private async Task<JsonArray> PagesAsync(string endpoint, string property = "")
    {
        Acquisition response = await ReadAsync("gh", ["api", "--method", "GET", "--paginate", "--slurp", endpoint]);
        if (!response.Complete)
        {
            return [];
        }
        JsonArray pages = JsonData.Rows(response.Data);
        List<JsonNode?> entries = [];
        foreach (JsonNode? page in pages)
        {
            entries.AddRange(JsonData.Rows(property.Length > 0 ? page?[property] : page));
        }
        return JsonData.Array(entries);
    }

    private Task<Acquisition> AzureAsync(JsonObject record, string path, string query = "")
        => AzureAsync(record.Text("organization"), record.Text("project"), path, query);

    private Task<Acquisition> AzureAsync(BuildIdentity identity, string path, string query = "")
        => AzureAsync(identity.Organization, identity.Project, path, query);

    private Task<Acquisition> AzureAsync(string organization, string project, string path, string query = "")
    {
        string endpoint = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/" +
            $"{Uri.EscapeDataString(project)}/_apis/{path}?api-version=7.1" +
            (query.Length > 0 ? "&" + query : "");
        return ReadAsync("az", ["rest", "--method", "GET", "--url", endpoint,
            "--resource", "499b84ac-1321-427f-aa17-267ca6975798", "--output", "json", "--only-show-errors"]);
    }

    private async Task<JsonArray> AzureRowsAsync(JsonObject record, string path, string query)
    {
        JsonArray entries = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        const int PageSize = 200;
        while (true)
        {
            Acquisition response = await AzureAsync(record, path, $"{query}&$skip={entries.Count}&$top={PageSize}");
            if (!response.Complete || response.Data?["value"] is not JsonArray page ||
                response.Data["count"] is null || response.Data.Number("count") != page.Count || page.Count > PageSize)
            {
                Gap(record, $"Incomplete raw Azure list for {path}; inspect acquisition snapshots.");
                return entries;
            }
            foreach (JsonObject row in page.Objects())
            {
                entries.Add(row.DeepClone());
                if (row.Text("id").Length == 0 || !ids.Add(row.Text("id")))
                {
                    Gap(record, $"Raw Azure list for {path} has duplicate result IDs or a missing ID.");
                    return entries;
                }
            }
            if (page.Count < PageSize)
            {
                return entries;
            }
        }
    }

    private async Task<JsonArray> ChecksAsync(string sha)
    {
        JsonArray checks = await PagesAsync($"repos/dotnet/sdk/commits/{sha}/check-runs?per_page=100", "check_runs");
        JsonArray statuses = await PagesAsync($"repos/dotnet/sdk/commits/{sha}/status?per_page=100", "statuses");
        JsonArray inventory = [];
        foreach (JsonNode check in checks.Objects())
        {
            if (check.Text("name") == "Build Analysis")
            {
                _dossier.Rows("buildAnalysis").Add(JsonData.Object(new { sha, report = check, coverage = "unverified-build-association" }));
            }
            inventory.Add(JsonData.Object(new
            {
                name = check["name"], sha, url = check["details_url"] ?? check["html_url"], status = check["status"],
                conclusion = check["conclusion"], coverage = "unassessed"
            }));
        }
        foreach (JsonNode status in statuses.Objects())
        {
            inventory.Add(JsonData.Object(new
            {
                name = status["context"], sha, url = status["target_url"], status = status["state"],
                conclusion = status["state"], coverage = "unassessed"
            }));
        }
        return inventory;
    }

    private async Task<JsonObject> BuildAsync(BuildIdentity identity)
    {
        if (_byId.TryGetValue(identity.Id, out JsonObject? existing))
        {
            if (existing.Text("organization") != identity.Organization || existing.Text("project") != identity.Project)
            {
                throw new InvalidDataException($"Build ID {identity.Id} occurs in different projects; collect these contexts in separate invocations.");
            }
            return existing;
        }
        Acquisition response = await AzureAsync(identity, $"build/builds/{identity.Id}");
        if (!response.Complete || response.Data is not JsonObject detail ||
            detail.Text("id") != identity.Id || detail["repository"]?.Text("id") != "dotnet/sdk")
        {
            throw new InvalidDataException($"Cannot identify selected/control build {identity.Id}. See acquisition snapshots.");
        }
        if (!Regex.IsMatch(detail.Text("sourceVersion"), "^[0-9a-f]{40}$", RegexOptions.IgnoreCase))
        {
            throw new InvalidDataException("Expected a full tested GitHub source SHA.");
        }
        string definitionId = detail["definition"].Text("id");
        JsonObject record = JsonData.Object(new
        {
            id = identity.Id, url = identity.Url, repository = "dotnet/sdk", organization = identity.Organization,
            project = identity.Project, definitionId, definitionName = detail["definition"]?["name"],
            branch = detail["sourceBranch"], commit = detail["sourceVersion"],
            prHeadSha = detail["triggerInfo"]?["pr.sourceSha"], prNumber = detail["triggerInfo"]?["pr.number"],
            targetSha = (JsonNode?)null, targetBranchHint = (JsonNode?)null, reason = detail["reason"],
            queuedAt = detail["queueTime"], status = detail["status"], result = detail["result"],
            parameters = JsonData.BuildParameters(detail["parameters"]), templateParameters = (JsonNode?)null,
            resources = (JsonNode?)null, complete = detail.Text("status") == "completed",
            observations = new JsonArray(), artifacts = new JsonArray(), testRuns = new JsonArray()
        });
        _byId.Add(identity.Id, record);
        _dossier.Rows("builds").Add(record);
        if (record.Text("definitionId").Length > 0)
        {
            Acquisition pipelineInputs = await AzureAsync(record, $"pipelines/{record.Text("definitionId")}/runs/{identity.Id}");
            if (pipelineInputs.Complete && pipelineInputs.Data is JsonObject pipeline &&
                pipeline.Text("id") == identity.Id && pipeline["pipeline"]?.Text("id") == record.Text("definitionId"))
            {
                record["resources"] = pipeline["resources"]?.DeepClone();
                record["templateParameters"] = pipeline["templateParameters"]?.DeepClone() ?? record["templateParameters"]?.DeepClone();
            }
            else
            {
                Gap(record, $"Raw pipeline inputs unavailable or mismatched for build {identity.Id}.");
            }
        }
        else
        {
            Gap(record, $"Raw pipeline inputs unavailable for build {identity.Id}: missing pipeline identity.");
        }
        if (record.Text("branch").StartsWith("refs/pull/", StringComparison.Ordinal) && record.Text("prHeadSha").Length > 0)
        {
            Acquisition merge = await ReadAsync("gh", ["api", "--method", "GET", $"repos/dotnet/sdk/git/commits/{record.Text("commit")}"]);
            if (merge.Complete && merge.Data?["parents"] is JsonArray { Count: 2 } parents &&
                parents[1].Text("sha") == record.Text("prHeadSha") &&
                Regex.IsMatch(parents[0].Text("sha"), "^[0-9a-f]{40}$", RegexOptions.IgnoreCase))
            {
                record["targetSha"] = parents[0]!["sha"]!.DeepClone();
            }
        }
        return record;
    }

    private static string Outcome(string result) => result.ToLowerInvariant() switch
    {
        "passed" or "succeeded" => "Passed",
        "failed" or "error" or "timeout" or "aborted" or "canceled" or "abandoned" => "Failed",
        "skipped" or "notexecuted" or "notapplicable" or "notimpacted" => "Skipped",
        _ => "Unknown"
    };

    private const string RunAttemptPattern =
        @"(?i)[ \t]+\(attempt[ \t]+(?<attempt>[1-9][0-9]*)\)(?=[ \t]*(?:\[HelixJob(?::[0-9a-f-]+)?\])?[ \t]*$)";

    private static string RunLane(string name) => Regex.Replace(
        Regex.Replace(name, @"(?i)\[HelixJob:[0-9a-f-]+\]", "[HelixJob]"), RunAttemptPattern, "").Trim();

    private static void FindHelix(string text, Dictionary<string, HashSet<string>> jobs, string context)
    {
        string pattern = $@"(?i)(?:HelixJob:\s*|jobs/)(?<job>{JsonData.GuidPattern})|{JsonData.MonitorJobPattern}";
        foreach (Match match in Regex.Matches(text, pattern))
        {
            string job = match.Groups["job"].Value;
            if (!jobs.TryGetValue(job, out HashSet<string>? contexts))
            {
                contexts = new(StringComparer.Ordinal);
                jobs.Add(job, contexts);
            }
            if (!string.IsNullOrWhiteSpace(context))
            {
                contexts.Add(context);
            }
        }
    }

    private async Task ObservationsAsync(JsonObject record)
    {
        string url = record.Text("url");
        Dictionary<string, HashSet<string>> helixJobs = new(StringComparer.OrdinalIgnoreCase);
        Acquisition timeline = await ReadAsync("hlx", ["azdo", "timeline", url, "--filter", "all", "--json"]);
        if (!timeline.Complete || timeline.Data?["records"] is not JsonArray records)
        {
            record["complete"] = false;
        }
        else
        {
            Dictionary<string, JsonObject> byId = records.Objects().ToDictionary(t => t.Text("id"), StringComparer.Ordinal);
            foreach (JsonObject task in records.Objects().Where(t => t.Text("type") is "Job" or "Task"))
            {
                string mechanism = string.Join('\n', (task["issues"] as JsonArray ?? []).Objects().Select(i => i.Text("message")));
                string signatureMechanism = mechanism;
                string? discoveryLog = null;
                string outcome = Outcome(task.Text("result"));
                bool canceled = task.Text("result").ToLowerInvariant() is "canceled" or "abandoned";
                bool executionVerified = task.Text("state").ToLowerInvariant() is not ("pending" or "notstarted") &&
                    (!canceled || DateTimeOffset.TryParse(task.Text("startTime"), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTimeOffset started) && started > DateTimeOffset.MinValue);
                if (!executionVerified)
                {
                    outcome = "Unknown";
                }
                if (outcome == "Unknown")
                {
                    record["complete"] = false;
                }
                bool discoveryTask = task.Text("type") == "Task" &&
                    Regex.IsMatch(task.Text("name"), @"(?i)\bhelix\b|\bqueue tests\b");
                if ((outcome == "Failed" || discoveryTask) && task["log"] is JsonObject logInfo)
                {
                    string[] arguments = discoveryTask
                        ? ["azdo", "log", url, logInfo.Text("id"), "--full", "--json"]
                        : ["azdo", "log", url, logInfo.Text("id"), "--tail-lines", "120", "--json"];
                    Acquisition log = await ReadAsync("hlx", arguments);
                    if (log.Complete)
                    {
                        if (log.Data is JsonValue value && value.TryGetValue(out string? excerpt))
                        {
                            discoveryLog = excerpt;
                            if (outcome == "Failed")
                            {
                                string diagnostic = discoveryTask ? string.Join('\n', excerpt.Split('\n').TakeLast(120)) : excerpt;
                                mechanism = mechanism.Length == 0 ? diagnostic : mechanism + "\n" + diagnostic;
                                string normalized = JsonData.NormalizeLogTimestamps(diagnostic);
                                signatureMechanism = signatureMechanism.Length == 0 ? normalized : signatureMechanism + "\n" + normalized;
                            }
                        }
                        else if (discoveryTask && executionVerified && outcome is "Passed" or "Failed")
                        {
                            Gap(record, $"Helix discovery log unavailable for task {task.Text("id")} in build {record.Text("id")}.");
                        }
                        else
                        {
                            Gap(record, $"Task log for build {record.Text("id")}, task {task.Text("id")} is not a JSON string.");
                        }
                    }
                    else
                    {
                        record["complete"] = false;
                    }
                }
                JsonObject parent = task;
                HashSet<string> visited = new(StringComparer.Ordinal);
                while (parent.Text("type") != "Job" && byId.TryGetValue(parent.Text("parentId"), out JsonObject? next) &&
                    visited.Add(parent.Text("id")))
                {
                    parent = next;
                }
                string submission = parent.Text("type") == "Job" &&
                    !string.IsNullOrWhiteSpace(parent.Text("name")) ? $"job:{parent.Text("name")}" : "";
                FindHelix(mechanism, helixJobs, submission);
                if (discoveryLog is not null)
                {
                    FindHelix(discoveryLog, helixJobs, submission);
                }
                record.Rows("observations").Add(JsonData.Object(new
                {
                    id = $"timeline:{task.Text("id")}", kind = task.Text("type").ToLowerInvariant(), name = task["name"],
                    lane = parent.Text("type") == "Job" ? $"{parent.Text("name")} > {task.Text("name")}" : "",
                    outcome, mechanism, signatureMechanism,
                    url = $"{url}&view=logs&j={task.Text("parentId")}&t={task.Text("id")}", contextOnly = task.Text("type") == "Job",
                    parentId = task["parentId"], attempt = task["attempt"], startedAt = task["startTime"],
                    producerOutcome = task["result"], executionVerified
                }));
            }
        }
        Acquisition runs = await ReadAsync("hlx", ["azdo", "test-runs", url, "--all", "--json"], list: true);
        if (!runs.Complete)
        {
            record["complete"] = false;
        }
        JsonArray runRows = [];
        record["testRuns"] = runRows;
        foreach (JsonObject listedRun in (runs.Data?["results"] as JsonArray ?? []).Objects())
        {
            Acquisition rawRun = await AzureAsync(record, $"test/Runs/{listedRun.Text("id")}");
            if (rawRun.Complete && rawRun.Data is JsonObject detail && detail.Text("id") == listedRun.Text("id") &&
                (detail["build"]?["id"] is null || detail["build"].Text("id") == record.Text("id")))
            {
                runRows.Add(detail.DeepClone());
            }
            else
            {
                Gap(record, $"Raw test-run metadata unavailable or mismatched for run {listedRun.Text("id")}.");
                runRows.Add(JsonData.Object(new { id = listedRun["id"], name = listedRun["name"] }));
            }
        }
        bool totalsKnown = runRows.Objects().All(r => r["totalTests"] is JsonValue value &&
            value.TryGetValue(out long count) && count >= 0);
        long total = totalsKnown ? runRows.Objects().Sum(r => r.Number("totalTests")) : 0;
        bool fullResults = totalsKnown && total <= _testResultLimit;
        record["testResultsIncludePassed"] = fullResults;
        if (!fullResults)
        {
            Gap(record, totalsKnown
                ? $"Build {record.Text("id")} exceeds TestResultLimit {_testResultLimit}; passed controls omitted"
                : $"Build {record.Text("id")} has an unknown raw test count; passed controls omitted", ok: true);
        }
        foreach (JsonObject run in runRows.Objects())
        {
            FindHelix(run.Text("name"), helixJobs,
                string.IsNullOrWhiteSpace(run.Text("name")) ? "" : $"run:{RunLane(run.Text("name"))}");
            string query = "detailsToInclude=SubResults";
            if (!fullResults)
            {
                query += "&outcomes=Unspecified,None,Failed,Inconclusive,Timeout,Aborted,Blocked,NotExecuted,Warning,Error,NotApplicable,Paused,InProgress,NotImpacted";
            }
            JsonArray results = await AzureRowsAsync(record, $"test/Runs/{run.Text("id")}/results",
                query);
            foreach (JsonObject test in results.Objects())
            {
                AddTestResult(record, run, test, $"test:{run.Text("id")}:{test.Text("id")}",
                    $"{test.Text("automatedTestName")} | {test.Text("testCaseTitle")}", null);
            }
        }
        Acquisition artifacts = await ReadAsync("hlx", ["azdo", "artifacts", url, "--all", "--json"], list: true);
        if (artifacts.Complete)
        {
            record["artifacts"] = artifacts.Data!["results"]!.DeepClone();
        }
        else
        {
            record["complete"] = false;
        }
        foreach ((string job, HashSet<string> contexts) in helixJobs)
        {
            await HelixAsync(record, job, contexts);
        }
        JsonArray coverageGaps = new(Comparison.CoverageGaps(record).Select(g => (JsonNode?)JsonValue.Create(g)).ToArray());
        record["coverageGaps"] = coverageGaps;
        if (coverageGaps.Count > 0)
        {
            record["complete"] = false;
        }
        if (Comparison.MissingFailureEvidence(record))
        {
            Gap(record, $"Missing failure evidence for build {record.Text("id")}; investigate pipeline validation/startup diagnostics.");
        }
    }

    private void AddTestResult(JsonObject record, JsonObject run, JsonObject test, string id, string name, string? parentResultId)
    {
        JsonArray children = test["subResults"] as JsonArray ?? [];
        bool group = children.Count > 0 || test.Text("resultGroupType").Length > 0 &&
            !test.Text("resultGroupType").Equals("none", StringComparison.OrdinalIgnoreCase);
        string outcome = Outcome(test.Text("outcome"));
        if (outcome == "Unknown")
        {
            record["complete"] = false;
        }
        record.Rows("observations").Add(JsonData.Object(new
        {
            id, kind = group ? "test-group" : "test", name, parentResultId, hierarchyVerified = true,
            resultGroupType = test["resultGroupType"],
            runName = run["name"],
            runAttempt = int.TryParse(Regex.Match(run.Text("name"), RunAttemptPattern).Groups["attempt"].Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out int attempt) ? attempt : (int?)null,
            lane = RunLane(run.Text("name")),
            outcome, producerOutcome = test["outcome"],
            mechanism = string.Join('\n', new[] { test.Text("errorMessage"), test.Text("stackTrace") }.Where(s => s.Length > 0)),
            url = $"{record.Text("url")}&view=ms.vss-test-web.build-test-results-tab&runId={run.Text("id")}&resultId={id.Split(':')[2].Split('/')[0]}",
            contextOnly = group || test.Text("testCaseTitle").EndsWith(" Work Item", StringComparison.OrdinalIgnoreCase)
        }));
        if (group && children.Count == 0)
        {
            Gap(record, $"Unverified test hierarchy for {id}: grouped result has no acquired children.");
        }
        foreach (JsonObject child in children.Objects())
        {
            AddTestResult(record, run, child, $"{id}/{child.Text("id")}",
                string.IsNullOrWhiteSpace(child.Text("displayName")) || name.Split(" | ").All(string.IsNullOrWhiteSpace)
                    ? "" : $"{name} > {child.Text("displayName")}", id);
        }
    }

    private async Task HelixAsync(JsonObject record, string job, HashSet<string> contexts)
    {
        Acquisition status = await ReadAsync("hlx", ["status", job, "all", "--json"]);
        if (!status.Complete)
        {
            record["complete"] = false;
            return;
        }
        if (status.Data is not JsonObject data || data["failed"] is not JsonArray failed || data["passed"] is not JsonArray passed ||
            data["job"] is not JsonObject jobData || data["inProgressCount"] is null)
        {
            Gap(record, $"Helix job {job} has an unsupported status contract");
            return;
        }
        if (data.Number("inProgressCount") > 0)
        {
            record["complete"] = false;
        }
        bool queueKnown = !string.IsNullOrWhiteSpace(jobData.Text("QueueId"));
        if (!queueKnown)
        {
            Gap(record, $"Helix job {job} has a missing queue; work-item evidence has unknown lane identity.");
        }
        string lane = contexts.Count == 0 || !queueKnown ? "" :
            $"{jobData.Text("QueueId")} > {string.Join(" | ", contexts.Order(StringComparer.Ordinal))}";
        foreach (JsonObject item in passed.Objects())
        {
            record.Rows("observations").Add(JsonData.Object(new
            {
                id = $"helix:{job}:{item.Text("Name")}", kind = "work-item", name = item["Name"], lane,
                queue = jobData["QueueId"], submissionContexts = contexts.Order(StringComparer.Ordinal).ToArray(),
                outcome = "Passed", mechanism = "", url = item["ConsoleLogUrl"], contextOnly = false
            }));
        }
        foreach (JsonObject item in failed.Objects())
        {
            Acquisition files = await ReadAsync("hlx", ["files", job, item.Text("Name"), "--json"]);
            Acquisition log = await ReadAsync("hlx", ["logs", job, item.Text("Name")], text: true);
            string mechanism = $"State={item.Text("State")}; exit code={item.Text("ExitCode")}";
            string signatureMechanism = mechanism;
            if (log.Complete)
            {
                string excerptPath = Path.ChangeExtension(log.Snapshot, ".console.json");
                JsonObject? excerpt = null;
                if (log.Replayed)
                {
                    string replayPath = Path.ChangeExtension(log.ReplaySnapshot!, ".console.json");
                    if (File.Exists(replayPath))
                    {
                        excerpt = JsonData.Read(replayPath);
                        if (excerpt.Number("schemaVersion") != 1 || excerpt.Text("sourceUrl") != item.Text("ConsoleLogUrl") ||
                            excerpt["text"] is not JsonValue text || !text.TryGetValue<string>(out _))
                        {
                            throw new InvalidDataException("Invalid Helix console excerpt snapshot.");
                        }
                    }
                }
                else
                {
                    string logPath = JsonData.Scalar(log.Data).Trim();
                    if (File.Exists(logPath))
                    {
                        excerpt = JsonData.Object(new
                        {
                            schemaVersion = 1, sourceUrl = item["ConsoleLogUrl"],
                            text = string.Join('\n', File.ReadLines(logPath).TakeLast(120))
                        });
                    }
                }
                if (excerpt is not null)
                {
                    JsonData.Write(excerpt, excerptPath);
                    mechanism += "\n" + excerpt.Text("text");
                    signatureMechanism += "\n" + JsonData.NormalizeLogTimestamps(excerpt.Text("text"));
                }
                else
                {
                    Gap(record, $"Helix console excerpt unavailable for {job}/{item.Text("Name")}");
                    mechanism += "\nConsole content not available in this command snapshot; inspect the source URL.";
                    signatureMechanism = mechanism;
                }
            }
            else
            {
                record["complete"] = false;
            }
            if (!files.Complete)
            {
                record["complete"] = false;
            }
            record.Rows("observations").Add(JsonData.Object(new
            {
                id = $"helix:{job}:{item.Text("Name")}", kind = "work-item", name = item["Name"], lane,
                queue = jobData["QueueId"], submissionContexts = contexts.Order(StringComparer.Ordinal).ToArray(),
                outcome = "Failed", mechanism, signatureMechanism, url = item["ConsoleLogUrl"], contextOnly = false, artifacts = files.Data
            }));
        }
    }

    private async Task WindowAsync(JsonObject record, JsonObject? baseline, string observationId)
    {
        string baseSha = _dossier.Text("mode") == "pr" ? record.Text("targetSha") : baseline.Text("commit");
        JsonObject window = JsonData.Object(new
        {
            buildId = record["id"], observationId, baselineBuildId = baseline?["id"],
            baseSha = baseSha.Length > 0 ? baseSha : null, headSha = record["commit"], ancestry = "unknown",
            commits = new JsonArray(), complete = false, diffPath = (string?)null, files = new JsonArray()
        });
        _dossier.Rows("changeWindows").Add(window);
        if (baseSha.Length == 0)
        {
            return;
        }
        string endpoint = $"repos/dotnet/sdk/compare/{baseSha}...{record.Text("commit")}";
        Acquisition response = await ReadAsync("gh", ["api", "--method", "GET", "--paginate", "--slurp", endpoint + "?per_page=100"]);
        if (!response.Complete || response.Data is not JsonArray { Count: > 0 } pages)
        {
            return;
        }
        JsonArray commits = JsonData.Array(pages.SelectMany(p => JsonData.Rows(p?["commits"])));
        window["ancestry"] = pages[0]!["status"]!.DeepClone();
        window["commits"] = commits;
        window["files"] = pages[0]!["files"]?.DeepClone() ?? new JsonArray();
        window["complete"] = window.Text("ancestry") is "ahead" or "identical" && commits.Count == pages[0]!.Number("total_commits");
        if (window.Rows("files").Count >= 300)
        {
            window["complete"] = false;
            window["limitation"] = "GitHub comparison file list reached its 300-file cap; do not claim exhaustive changed-file coverage.";
        }
        Acquisition diff = await ReadAsync("gh", ["api", "--method", "GET", "-H", "Accept: application/vnd.github.diff", endpoint], text: true);
        if (diff.Complete)
        {
            string path = Path.Combine(_outputDirectory, $"changes-{record.Text("id")}-{baseSha}.diff");
            Directory.CreateDirectory(_outputDirectory);
            File.WriteAllText(path, JsonData.Scalar(diff.Data) + "\n", new UTF8Encoding(false));
            window["diffPath"] = path;
        }
        else
        {
            window["complete"] = false;
        }
        Acquisition changes = await ReadAsync("hlx", ["azdo", "changes", record.Text("url"), "--all", "--json"], list: true);
        window["azureChanges"] = changes.Data?.DeepClone();
    }

    public async Task CollectAsync()
    {
        string evidencePath = Path.Combine(_outputDirectory, "evidence.json");
        try
        {
            await CollectCoreAsync();
            JsonData.Write(_dossier, evidencePath);
            Console.WriteLine(evidencePath);
            if (_dossier.Rows("acquisitions").Any(a => !a.Flag("complete")) ||
                _dossier.Rows("builds").Any(b => !b.Flag("complete")) ||
                _dossier.Rows("changeWindows").Any(w => !w.Flag("complete")))
            {
                throw new InvalidDataException("Evidence is incomplete; partial dossier saved. Inspect acquisition/coverage gaps before attribution.");
            }
        }
        catch (Exception ex)
        {
            _dossier["error"] = ex.Message;
            HashSet<string> selected = _dossier.Rows("selectedBuildIds").Select(JsonData.Scalar).ToHashSet(StringComparer.Ordinal);
            foreach (JsonObject record in _byId.Values.Where(b => selected.Contains(b.Text("id"))))
            {
                record["complete"] = false;
            }
            JsonData.Write(_dossier, evidencePath);
            throw;
        }
    }

    private async Task CollectCoreAsync()
    {
        Acquisition version = await ReadAsync("hlx", ["--version"], text: true);
        if (!version.Complete || !Regex.IsMatch(JsonData.Scalar(version.Data).Trim(), @"^0\.11\."))
        {
            throw new InvalidDataException("This adapter supports hlx 0.11.x JSON contracts. Use a compatible version or update and validate the adapter.");
        }
        List<BuildIdentity> selected = [];
        string? number = null;
        if (_options["pull-request"] is string pullRequest)
        {
            number = Regex.IsMatch(pullRequest, @"^\d+$") ? pullRequest :
                Regex.Match(pullRequest, @"^https://github\.com/dotnet/sdk/pull/(\d+)/?$") is { Success: true } match
                    ? match.Groups[1].Value : throw new ArgumentException("Use a dotnet/sdk PR number or URL.");
            Acquisition pr = await ReadAsync("gh", ["api", "--method", "GET", $"repos/dotnet/sdk/pulls/{number}"]);
            if (!pr.Complete || pr.Data is not JsonObject)
            {
                throw new InvalidDataException("PR metadata unavailable.");
            }
            _dossier["pr"] = pr.Data.DeepClone();
            _dossier["prFiles"] = await PagesAsync($"repos/dotnet/sdk/pulls/{number}/files?per_page=100");
            _dossier["prComments"] = await PagesAsync($"repos/dotnet/sdk/issues/{number}/comments?per_page=100");
            _dossier["prReviews"] = await PagesAsync($"repos/dotnet/sdk/pulls/{number}/reviews?per_page=100");
            _dossier["prReviewComments"] = await PagesAsync($"repos/dotnet/sdk/pulls/{number}/comments?per_page=100");
            if (_options["build"] is null)
            {
                _dossier["checks"] = await ChecksAsync(_dossier["pr"]!["head"].Text("sha"));
                selected.AddRange(_dossier.Rows("checks").Objects()
                    .Select(c => BuildIdentity.TryResolveUrl(c.Text("url"), out BuildIdentity? identity) ? identity : null)
                    .OfType<BuildIdentity>()
                    .Distinct().OrderBy(b => b.Id, StringComparer.Ordinal));
            }
        }
        if (_options["build"] is string build)
        {
            selected = [BuildIdentity.Resolve(build)];
        }
        if (selected.Count == 0)
        {
            throw new InvalidDataException("No Azure builds located. Supply an exact --build URL/ID; other checks remain unassessed.");
        }
        foreach (BuildIdentity identity in selected)
        {
            JsonObject record = await BuildAsync(identity);
            if (number is not null && (record.Text("branch") != $"refs/pull/{number}/merge" || record.Text("prNumber") != number ||
                _options["build"] is null && record.Text("prHeadSha") != _dossier["pr"]!["head"].Text("sha")))
            {
                throw new InvalidDataException("The selected build does not validate the requested PR/revision. Supply its matching PR and exact build.");
            }
            _dossier.Rows("selectedBuildIds").Add(record.Text("id"));
            if (number is not null && record.Text("prHeadSha").Length > 0 && _options["build"] is not null)
            {
                _dossier["checks"] = await ChecksAsync(record.Text("prHeadSha"));
            }
            else if (number is null)
            {
                JsonArray reports = await PagesAsync($"repos/dotnet/sdk/commits/{record.Text("commit")}/check-runs?per_page=100", "check_runs");
                foreach (JsonObject report in reports.Objects().Where(r => r.Text("name") == "Build Analysis"))
                {
                    _dossier.Rows("buildAnalysis").Add(JsonData.Object(new { sha = record["commit"], report, coverage = "unverified-build-association" }));
                }
            }
            List<string> branches = [record.Text("branch")];
            if (number is not null)
            {
                string hint = $"refs/heads/{_dossier["pr"]!["base"].Text("ref")}";
                record["targetBranchHint"] = hint;
                branches.Add(hint);
            }
            foreach (string branch in branches.Distinct(StringComparer.Ordinal))
            {
                DateTimeOffset time = JsonData.Time(record);
                string minimum = time.AddDays(-_lookbackDays).ToString("o");
                string maximum = time.AddTicks(-1).ToString("o");
                Acquisition history = await ReadAsync("hlx", ["azdo", "builds", "--org", record.Text("organization"), "--project", record.Text("project"),
                    "--definition-id", record.Text("definitionId"), "--branch", branch, "--min-time", minimum, "--max-time", maximum,
                    "--top", _historyLimit.ToString(CultureInfo.InvariantCulture), "--query-order", "queueTimeDescending", "--json"]);
                _dossier["history"]!.Rows("queries").Add(JsonData.Object(new
                {
                    selectedBuildId = record["id"], branch, minTime = minimum, maxTime = maximum,
                    reachedLimit = history.Data is JsonArray rows && rows.Count >= _historyLimit
                }));
                if (!history.Complete || history.Data is not JsonArray historyRows)
                {
                    record["complete"] = false;
                    continue;
                }
                foreach (JsonObject raw in historyRows.Objects().Where(r => r.Text("status") == "completed"))
                {
                    string url = $"https://dev.azure.com/{record.Text("organization")}/{record.Text("project")}/_build/results?buildId={raw.Text("id")}";
                    await BuildAsync(BuildIdentity.Resolve(url));
                }
            }
        }
        JsonObject? explicitBaseline = _options["baseline-build"] is string baselineValue ? await BuildAsync(BuildIdentity.Resolve(baselineValue)) : null;
        foreach (JsonObject record in _byId.Values)
        {
            await ObservationsAsync(record);
        }
        foreach (JsonObject check in _dossier.Rows("checks").Objects())
        {
            if (BuildIdentity.TryResolveUrl(check.Text("url"), out BuildIdentity? checkBuild) &&
                selected.Any(b => b.Id == checkBuild.Id && b.Organization == checkBuild.Organization && b.Project == checkBuild.Project) &&
                check.Text("status") is "completed" or "success" or "failure" or "error")
            {
                check["coverage"] = "assessed";
            }
        }
        JsonObject comparison = Comparison.Compare(_dossier);
        Dictionary<string, ILookup<(string Kind, string Name, string Lane), JsonObject>> baselineIndexes = new(StringComparer.Ordinal);
        foreach (BuildIdentity identity in selected)
        {
            JsonObject record = _byId[identity.Id];
            if (number is not null)
            {
                await WindowAsync(record, null, "");
                continue;
            }
            ILookup<string, JsonObject> observations = record.Rows("observations").Objects()
                .ToLookup(o => o.Text("id"), StringComparer.Ordinal);
            foreach (JsonObject row in comparison.Rows("comparisons").Objects().Where(r => r.Text("buildId") == identity.Id))
            {
                JsonObject? baseline = explicitBaseline;
                if (baseline is not null)
                {
                    if (!baselineIndexes.TryGetValue(baseline.Text("id"), out ILookup<(string Kind, string Name, string Lane), JsonObject>? index))
                    {
                        index = Comparison.IndexExecutions(baseline);
                        baselineIndexes.Add(baseline.Text("id"), index);
                    }
                    JsonObject observation = Comparison.FindObservation(observations, row.Text("observationId"));
                    if (!Comparison.Compatible(record, baseline, observation, index).Eligible ||
                        baseline.Text("branch") != record.Text("branch") ||
                        JsonData.Time(baseline) >= JsonData.Time(record))
                    {
                        throw new InvalidDataException("Explicit baseline must be an earlier same-branch/pipeline execution of the scenario.");
                    }
                }
                else if (row["precedingExecution"] is JsonNode preceding)
                {
                    baseline = _byId[preceding.Text("buildId")];
                }
                await WindowAsync(record, baseline, row.Text("observationId"));
            }
        }
        HashSet<string> searches = new(StringComparer.Ordinal);
        foreach (JsonObject row in comparison.Rows("comparisons").Objects().Where(r => !r.Flag("contextOnly")))
        {
            if (searches.Count >= 6)
            {
                break;
            }
            Match diagnostic = Regex.Match(row.Text("mechanism"), @"\b(?:NETSDK|MSB|CS|NU|CONTAINER)\d+\b");
            string term = Regex.Replace(diagnostic.Success ? diagnostic.Value : row.Text("name").Split(" | ").Last(), @"[^\w. -]", " ").Trim();
            if (term.Length == 0)
            {
                continue;
            }
            foreach (string state in new[] { "open", "closed" })
            {
                if (searches.Count >= 6)
                {
                    break;
                }
                string query = $"{term} repo:dotnet/sdk is:issue is:{state}";
                if (!searches.Add(query))
                {
                    continue;
                }
                Acquisition response = await ReadAsync("gh", ["api", "--method", "GET", $"search/issues?q={Uri.EscapeDataString(query)}&per_page=10"]);
                _dossier.Rows("issueSearches").Add(JsonData.Object(new { query, response = response.Data, coverage = "bounded-candidates-not-verified-matches" }));
            }
        }
    }
}
