#!/usr/bin/env dotnet

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:property PublishAot=false

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

if (Environment.GetEnvironmentVariable("CI_EVIDENCE_TEST_REPLAY") is string fakeReplay)
{
    return await Fixtures.FakeToolAsync(fakeReplay, args);
}

try
{
    if (args is not ["--output-directory", _, ..] || args.Length != 2 &&
        (args is not [_, _, "--filter", string value] || string.IsNullOrWhiteSpace(value)))
    {
        Console.Error.WriteLine("Usage: dotnet TestCiEvidence.cs -- --output-directory PATH [--filter NAME_SUBSTRING]");
        return 1;
    }
    await new Checks(Path.GetFullPath(args[1]), args.Length == 4 ? args[3] : null).RunAsync();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class Checks(string outputDirectory, string? filter)
{
    private readonly string _source = Path.GetFullPath(Path.Combine(SourceDirectory(), "..", "scripts", "CiEvidence.cs"));
    private readonly string _dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
    private string _assembly = "";
    private int _invocation;
    private int _passed;

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    private static async Task<(int Code, string Output, string Error)> ProcessAsync(string executable, string[] arguments,
        Dictionary<string, string>? environment = null)
    {
        ProcessStartInfo start = new(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        foreach ((string key, string value) in environment ?? [])
        {
            start.Environment[key] = value;
        }
        using Process process = new() { StartInfo = start };
        process.Start();
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await output.WaitAsync(timeout.Token), await error.WaitAsync(timeout.Token));
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync();
            throw new TimeoutException("C# evidence check exceeded three minutes.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private async Task CaseAsync(string name, Func<Task> body)
    {
        if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        await body();
        _passed++;
        Console.WriteLine($"PASS {name}");
    }

    private string NextDirectory() => Path.Combine(outputDirectory, $"check-{++_invocation:D3}");
    private async Task<JsonObject> CompareAsync(JsonObject fixture, string? directory = null)
    {
        directory ??= NextDirectory();
        string evidence = Path.Combine(directory, "evidence.json");
        Fixtures.Write(fixture, evidence);
        (int code, string _, string error) = await ProcessAsync(_dotnet,
            [_assembly, "compare", "--evidence-path", evidence, "--output-directory", directory]);
        Assert(code == 0, $"Comparison failed: {error}");
        Assert(File.Exists(Path.Combine(directory, "comparison.md")), "Markdown comparison missing.");
        return Fixtures.Read(Path.Combine(directory, "comparison.json"));
    }

    private static JsonNode Row(JsonObject comparison) => comparison["comparisons"]![0]!;
    private static JsonNode Current(JsonObject fixture) => fixture["builds"]![0]!;
    private static JsonNode Previous(JsonObject fixture) => fixture["builds"]![1]!;
    private static JsonNode Observation(JsonNode build) => build["observations"]![0]!;
    private async Task<JsonObject> CollectAsync(string replay, string[]? extra = null, string? expectedError = null,
        Dictionary<string, string>? environment = null, string? executable = null)
    {
        string output = NextDirectory();
        List<string> arguments = [_assembly, "collect", "--build", "3", "--history-limit", "1", "--lookback-days", "2",
            "--output-directory", output, "--gh-path", executable ?? "never-executed", "--hlx-path", executable ?? "never-executed",
            "--az-path", executable ?? "never-executed"];
        if (environment is null)
        {
            arguments.AddRange(["--replay-directory", replay]);
        }
        arguments.AddRange(extra ?? []);
        (int code, string _, string error) = await ProcessAsync(_dotnet, arguments.ToArray(), environment);
        Assert(expectedError is null ? code == 0 : code != 0 && error.Contains(expectedError, StringComparison.Ordinal),
            $"Unexpected collection exit {code}: {error}");
        return Fixtures.Read(Path.Combine(output, "evidence.json"));
    }

    public async Task RunAsync()
    {
        Directory.CreateDirectory(outputDirectory);
        string binaryDirectory = Path.Combine(outputDirectory, "bin");
        (int code, string output, string error) = await ProcessAsync(_dotnet,
            ["build", _source, "--output", binaryDirectory, "--nologo", "--verbosity", "quiet"]);
        Assert(code == 0, $"Evidence app did not build: {output}\n{error}");
        _assembly = Path.Combine(binaryDirectory, "CiEvidence.dll");
        Assert(File.Exists(_assembly), "Built evidence assembly missing.");

        await CaseAsync("Preserve expected/actual values, codes, versions, endpoints and meaningful paths", async () =>
        {
            JsonObject fixture = Fixtures.New();
            string mechanism = @"MSB4019: pkg 8.0 C:\src\A.targets expected 42 actual 43 https://feed.example/v3";
            Observation(Current(fixture))["mechanism"] = mechanism;
            Observation(Previous(fixture))["outcome"] = "Failed";
            Observation(Previous(fixture))["mechanism"] = mechanism.Replace("8.0", "9.0", StringComparison.Ordinal);
            JsonNode row = Row(await CompareAsync(fixture));
            Assert(row["matchingOccurrences"]!.GetValue<int>() == 0, "Different package versions matched.");
            foreach (string term in new[] { "MSB4019", "8.0", "A.targets", "42", "43", "https://feed.example/v3" })
            {
                Assert(row["signature"]!.GetValue<string>().Contains(term, StringComparison.Ordinal), $"Lost {term}.");
            }
            Observation(Current(fixture))["mechanism"] = @"Load C:\Temp\pkg\8.0\A.dll failed";
            Observation(Previous(fixture))["mechanism"] = @"Load C:\Temp\pkg\9.0\A.dll failed";
            row = Row(await CompareAsync(fixture));
            Assert(row["matchingOccurrences"]!.GetValue<int>() == 0 && row["signature"]!.GetValue<string>().Contains(@"8.0\A.dll", StringComparison.Ordinal),
                "Meaningful temporary-path suffix erased.");
        });
        await CaseAsync("Temporary path normalization respects absolute roots and Unix casing", async () =>
        {
            foreach ((string first, string second) in new[]
            {
                ("/opt/pkg/tmp/A.targets", "/opt/pkg/var/tmp/A.targets"),
                ("/TMP/A.targets", "/tmp/A.targets"),
                ("/opt/pkg/mnt/vss/_work/1/s/A.targets", "/mnt/vss/_work/1/s/A.targets"),
                ("/MNT/vss/_work/1/s/A.targets", "/mnt/vss/_work/1/s/A.targets")
            })
            {
                JsonObject fixture = Fixtures.New("existing-flake");
                Observation(Current(fixture))["mechanism"] = "Missing " + first;
                Observation(Previous(fixture))["mechanism"] = "Missing " + second;
                JsonNode row = Row(await CompareAsync(fixture));
                Assert(row["matchingOccurrences"]!.GetValue<int>() == 0 &&
                    row["signature"]!.GetValue<string>().Contains(first, StringComparison.Ordinal),
                    "An interior path segment or case-sensitive Unix root was treated as a volatile directory.");
            }
            JsonObject roots = Fixtures.New("existing-flake");
            Observation(Current(roots))["mechanism"] = "Missing '/tmp/pkg/A.targets'";
            Observation(Previous(roots))["mechanism"] = "Missing '/var/tmp/pkg/A.targets'";
            Assert(Row(await CompareAsync(roots))["matchingOccurrences"]!.GetValue<int>() == 1,
                "Known absolute temporary roots stopped normalizing.");
        });
        await CaseAsync("Normalize volatile GUIDs without matching an empty mechanism", async () =>
        {
            JsonObject fixture = Fixtures.New();
            Observation(Current(fixture))["mechanism"] = "[2026-10-12T10:00:00Z] Failure HelixJob:01234567-0123-0123-0123-012345678901";
            Observation(Previous(fixture))["outcome"] = "Failed";
            Observation(Previous(fixture))["mechanism"] = "[2026-10-12T10:00:00Z] Failure HelixJob:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            Assert(Row(await CompareAsync(fixture))["matchingOccurrences"]!.GetValue<int>() == 1, "Volatile identifiers did not normalize.");
            foreach (string context in new[] { "CorrelationId=", "Activity-Id: ", "Request Id: ", "https://helix.dot.net/api/jobs/" })
            {
                Observation(Current(fixture))["mechanism"] = context + "01234567-0123-0123-0123-012345678901";
                Observation(Previous(fixture))["mechanism"] = context + "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
                Assert(Row(await CompareAsync(fixture))["matchingOccurrences"]!.GetValue<int>() == 1, $"Volatile {context} did not normalize.");
            }
            Observation(Current(fixture))["mechanism"] = "";
            Observation(Previous(fixture))["mechanism"] = "";
            JsonNode row = Row(await CompareAsync(fixture));
            Assert(row["signature"] is null && row["matchingOccurrences"]!.GetValue<int>() == 0, "Empty mechanism matched.");
        });
        await CaseAsync("Versioned Helix URLs and official monitor IDs normalize only volatile job identity", async () =>
        {
            const string first = "11111111-1111-1111-1111-111111111111";
            const string second = "22222222-2222-2222-2222-222222222222";
            foreach (string message in new[]
            {
                "See https://helix.dot.net/api/2019-06-17/jobs/{0}/workitems/Example.Test/console for MSB4036",
                "Work item 'Example.Test' in job 'Linux x64 ({0})' failed: MSB4036"
            })
            {
                JsonObject fixture = Fixtures.New("existing-flake");
                Observation(Current(fixture))["mechanism"] = message.Replace("{0}", first, StringComparison.Ordinal);
                Observation(Previous(fixture))["mechanism"] = message.Replace("{0}", second, StringComparison.Ordinal);
                JsonNode row = Row(await CompareAsync(fixture));
                Assert(row["matchingOccurrences"]!.GetValue<int>() == 1 &&
                    !row["signature"]!.GetValue<string>().Contains(first, StringComparison.Ordinal),
                    "A producer-supported volatile Helix job ID prevented the same mechanism from matching.");
                Observation(Previous(fixture))["mechanism"] =
                    Observation(Previous(fixture))["mechanism"]!.GetValue<string>().Replace("Example.Test", "Different.Test", StringComparison.Ordinal);
                Assert(Row(await CompareAsync(fixture))["matchingOccurrences"]!.GetValue<int>() == 0,
                    "Normalizing a job ID erased the meaningful work-item identity.");
            }
        });
        await CaseAsync("Stable GUIDs and expected timestamp values identify different mechanisms", async () =>
        {
            foreach ((string context, string first, string second, string suffix) in new[]
            {
                ("COMException: component CLSID {", "11111111-1111-1111-1111-111111111111",
                    "22222222-2222-2222-2222-222222222222", "} failed: 80040154 Class not registered."),
                ("Expected ", "11111111-1111-1111-1111-111111111111",
                    "22222222-2222-2222-2222-222222222222", "; actual value differs."),
                ("Expected ", "2026-10-12T10:00:00Z", "2026-10-11T10:00:00Z", "; actual value differs.")
            })
            {
                JsonObject fixture = Fixtures.New();
                Observation(Previous(fixture))["outcome"] = "Failed";
                Observation(Current(fixture))["mechanism"] = context + first + suffix;
                Observation(Previous(fixture))["mechanism"] = context + second + suffix;
                JsonNode row = Row(await CompareAsync(fixture));
                Assert(row["matchingOccurrences"]!.GetValue<int>() == 0 && row["independentMatchingOccurrences"]!.GetValue<int>() == 0 &&
                    row["signature"]!.GetValue<string>().Contains(first, StringComparison.Ordinal), "Material diagnostic identifiers were erased.");
            }
        });
        await CaseAsync("Multiline assertion timestamps are values rather than transport log prefixes", async () =>
        {
            foreach (string prefix in new[] { "Expected:\n", "Expected:\r\n", "Expected:\n  " })
            {
                JsonObject fixture = Fixtures.New("existing-flake");
                Observation(Current(fixture))["mechanism"] = prefix + "2026-10-12T10:00:00Z";
                Observation(Previous(fixture))["mechanism"] = prefix + "2026-10-11T10:00:00Z";
                Assert(Row(await CompareAsync(fixture))["matchingOccurrences"]!.GetValue<int>() == 0,
                    "A multiline expected timestamp was normalized into a matching failure.");
            }
        });
        await CaseAsync("Assertion whitespace is preserved while transport line endings normalize", async () =>
        {
            foreach ((string first, string second) in new[]
            {
                ("Expected: \"a b\"\nActual: \"a  b\"", "Expected: \"a  b\"\nActual: \"a b\""),
                ("Expected: \"a\tb\"", "Expected: \"a b\""),
                ("Expected: \"a\nb\"", "Expected: \"a b\""),
                ("Expected: value ", "Expected: value"),
                ("  Expected: value", "Expected: value")
            })
            {
                JsonObject fixture = Fixtures.New("existing-flake");
                Observation(Current(fixture))["mechanism"] = first;
                Observation(Previous(fixture))["mechanism"] = second;
                JsonNode row = Row(await CompareAsync(fixture));
                Assert(row["signature"]!.GetValue<string>().EndsWith(first, StringComparison.Ordinal) &&
                    row["matchingOccurrences"]!.GetValue<int>() == 0 && row["independentMatchingOccurrences"]!.GetValue<int>() == 0 &&
                    !row["observedMixedOutcomes"]!.GetValue<bool>(), "Meaningful whitespace produced a false prior match.");
            }
            JsonObject same = Fixtures.New("existing-flake");
            Observation(Current(same))["mechanism"] = "Expected: \"a b\"\r\nActual: \"a  b\"";
            Observation(Previous(same))["mechanism"] = "Expected: \"a b\"\nActual: \"a  b\"";
            Assert(Row(await CompareAsync(same))["matchingOccurrences"]!.GetValue<int>() == 1,
                "Transport line endings prevented matching otherwise identical assertions.");
        });
        await CaseAsync("Timestamp strings preserve timezone and fractional-second identity", async () =>
        {
            JsonObject fixture = Fixtures.New("scheduled-same-source");
            Current(fixture)["queuedAt"] = "2026-10-12T17:08:47.1994043+00:00";
            Previous(fixture)["queuedAt"] = "2026-10-12T17:08:47.1994042+00:00";
            Assert(Row(await CompareAsync(fixture))["controls"]!.AsArray().Count == 1, "Subsecond ordering was lost.");
        });
        await CaseAsync("Only verified executed scenarios establish controls and preceding boundaries", async () =>
        {
            JsonObject fixture = Fixtures.New("rolling-regression");
            JsonNode row = Row(await CompareAsync(fixture));
            Assert(row["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "2" && row["precedingExecution"] is not null,
                "Executed pass was missed.");
            foreach (string outcome in new[] { "Skipped", "Unknown" })
            {
                Observation(Previous(fixture))["outcome"] = outcome;
                row = Row(await CompareAsync(fixture));
                Assert(row["mostRecentPassingExecution"] is null && row["precedingExecution"] is null, $"{outcome} became an execution.");
            }
            Previous(fixture)["observations"] = new JsonArray();
            Assert(Row(await CompareAsync(fixture))["mostRecentPassingExecution"] is null, "Missing scenario became a pass.");
        });
        await CaseAsync("Lane and parameterized identities remain distinct", async () =>
        {
            JsonObject fixture = Fixtures.New();
            Observation(Previous(fixture))["lane"] = "Windows arm64 Release";
            Assert(!Row(await CompareAsync(fixture))["controls"]![0]!["eligible"]!.GetValue<bool>(), "Different lane accepted.");
            Observation(Previous(fixture))["lane"] = "Linux x64 Debug";
            Observation(Previous(fixture))["name"] = "Example.Test | Example.Test(another argument)";
            Assert(!Row(await CompareAsync(fixture))["controls"]![0]!["eligible"]!.GetValue<bool>(), "Different test row accepted.");
        });
        await CaseAsync("Missing scenario identities never establish controls in older dossiers", async () =>
        {
            foreach ((string kind, string? name, string? lane) in new (string, string?, string?)[]
            {
                ("test", null, "Linux"), ("test", "", "Linux"), ("test", " \t", "Linux"),
                ("test", " | ", "Linux"), ("test", " \t | \r\n ", "Linux"),
                ("test", "Example.Test | Example.Test", null), ("test", "Example.Test | Example.Test", " \t"),
                ("task", "Compile", " > Compile"), ("task", null, "Linux > "),
                ("work-item", "", "Linux"), ("work-item", "Example.WorkItem", " ")
            })
            {
                JsonObject fixture = Fixtures.New("existing-flake");
                foreach (JsonNode? build in fixture["builds"]!.AsArray())
                {
                    JsonNode observation = Observation(build!);
                    observation["kind"] = kind;
                    observation["name"] = name;
                    observation["lane"] = lane;
                }
                JsonObject comparison = await CompareAsync(fixture);
                JsonNode row = Row(comparison);
                Assert(row["controls"]!.AsArray().All(c => !c!["eligible"]!.GetValue<bool>()) &&
                    row["mostRecentPassingExecution"] is null && row["precedingExecution"] is null &&
                    row["matchingOccurrences"]!.GetValue<int>() == 0 && row["independentMatchingOccurrences"]!.GetValue<int>() == 0 &&
                    !row["observedMixedOutcomes"]!.GetValue<bool>(), "Unknown scenario identity established a control.");
                Assert(comparison["coverageGaps"]!.AsArray().Count(g =>
                    g!.GetValue<string>().Contains("Unverified scenario identity", StringComparison.Ordinal)) == 3,
                    "Missing selected or historical scenario identity was not an explicit coverage gap.");
            }
            foreach (string name in new[] { "Example.Test | ", " | Example.Test" })
            {
                JsonObject fixture = Fixtures.New("existing-flake");
                foreach (JsonNode? build in fixture["builds"]!.AsArray())
                {
                    Observation(build!)["name"] = name;
                }
                JsonObject comparison = await CompareAsync(fixture);
                Assert(Row(comparison)["mostRecentPassingExecution"] is not null &&
                    Row(comparison)["independentMatchingOccurrences"]!.GetValue<int>() == 1 &&
                    comparison["coverageGaps"]!.AsArray().Count == 0, "An available test-name component was rejected.");
            }
        });
        await CaseAsync("Collection preserves missing test names and lanes as unverified identities", async () =>
        {
            string replay = Path.Combine(outputDirectory, "missing-scenario-identity");
            foreach ((string field, string affected, string? value) in new (string, string, string?)[]
            {
                ("name", "both", null), ("name", "3", ""), ("name", "2", " \t"),
                ("lane", "both", null), ("lane", "3", ""), ("lane", "2", " \t")
            })
            {
                Fixtures.Collection(replay);
                Fixtures.TestRun(replay, "3");
                Fixtures.TestRun(replay, "2");
                foreach (string id in new[] { "3", "2" }.Where(id => affected == "both" || id == affected))
                {
                    Fixtures.Change(replay, s => field == "name" ? Fixtures.IsRawResults(s, id) :
                        Fixtures.IsTestAcquisition(s, id) && !Fixtures.IsRawResults(s, id), data =>
                    {
                        JsonObject entry = Fixtures.FirstResult(data);
                        foreach (string key in field == "name" ? new[] { "automatedTestName", "testCaseTitle" } : new[] { "name" })
                        {
                            if (value is null)
                            {
                                entry.Remove(key);
                            }
                            else
                            {
                                entry[key] = value;
                            }
                        }
                    });
                }
                JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
                foreach (JsonNode? build in dossier["builds"]!.AsArray().Where(b =>
                    affected == "both" || b!["id"]!.GetValue<string>() == affected))
                {
                    Assert(!build!["complete"]!.GetValue<bool>() &&
                        build["observations"]!.AsArray().Any(o => o!["kind"]!.GetValue<string>() == "test"),
                        "Missing identity was reported as complete or its acquired result was discarded.");
                }
                for (int pass = 0; pass < 2; pass++)
                {
                    JsonObject comparison = await CompareAsync(dossier);
                    JsonNode row = comparison["comparisons"]!.AsArray().Single(r => r!["kind"]!.GetValue<string>() == "test")!;
                    Assert(row["controls"]!.AsArray().All(c => !c!["eligible"]!.GetValue<bool>()) &&
                        row["mostRecentPassingExecution"] is null && row["precedingExecution"] is null &&
                        row["matchingOccurrences"]!.GetValue<int>() == 0 &&
                        comparison["coverageGaps"]!.AsArray().Any(g =>
                            g!.GetValue<string>().Contains("Unverified scenario identity", StringComparison.Ordinal)),
                        "Missing identity established a baseline or disappeared from offline coverage.");
                    dossier["error"] = null;
                    dossier["acquisitions"] = new JsonArray();
                    foreach (JsonNode? build in dossier["builds"]!.AsArray())
                    {
                        build!["complete"] = true;
                    }
                }
            }
        });
        await CaseAsync("Missing timeline names or job parents cannot establish controls", async () =>
        {
            string replay = Path.Combine(outputDirectory, "missing-timeline-identity");
            foreach (string type in new[] { "Job", "Task", "Parent" })
            {
                Fixtures.Collection(replay);
                Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                    s["arguments"]!.AsArray().Count > 1 && s["arguments"]![1]!.GetValue<string>() == "timeline", data =>
                {
                    foreach (JsonNode? record in data["records"]!.AsArray().Where(r =>
                        r!["type"]!.GetValue<string>() == (type == "Parent" ? "Task" : type)))
                    {
                        record![type == "Parent" ? "parentId" : "name"] = type == "Parent" ? "missing-job" : null;
                    }
                });
                JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
                JsonObject comparison = await CompareAsync(dossier);
                JsonNode row = comparison["comparisons"]!.AsArray().Single(r => r!["kind"]!.GetValue<string>() == "task")!;
                Assert(row["mostRecentPassingExecution"] is null && row["precedingExecution"] is null &&
                    row["controls"]!.AsArray().All(c => !c!["eligible"]!.GetValue<bool>()) &&
                    comparison["coverageGaps"]!.AsArray().Any(g =>
                        g!.GetValue<string>().Contains("Unverified scenario identity", StringComparison.Ordinal)),
                    "A generated timeline lane hid the missing job/task identity or parent.");
            }
        });
        await CaseAsync("Controls require known matching repository, project and pipeline identity", async () =>
        {
            foreach (string key in new[] { "repository", "organization", "project", "definitionId" })
            {
                foreach (string? value in new string?[] { null, "", " ", "different" })
                {
                    foreach (bool selected in new[] { true, false })
                    {
                        JsonObject fixture = Fixtures.New("existing-flake");
                        foreach (JsonNode? build in fixture["builds"]!.AsArray().Where(b => (b == Current(fixture)) == selected))
                        {
                            build![key] = value == "different" && key == "definitionId" ? JsonValue.Create(202) : JsonValue.Create(value);
                        }
                        JsonNode row = Row(await CompareAsync(fixture));
                        Assert(row["controls"]!.AsArray().All(c => !c!["eligible"]!.GetValue<bool>()) &&
                            row["mostRecentPassingExecution"] is null && row["precedingExecution"] is null &&
                            row["matchingOccurrences"]!.GetValue<int>() == 0 && row["independentMatchingOccurrences"]!.GetValue<int>() == 0,
                            $"Unverified or mismatched {key} established a control.");
                        Assert(row["controls"]!.AsArray().All(c =>
                            c![value == "different" ? "differences" : "limitations"]!.AsArray().Any(l =>
                                l!.GetValue<string>() == $"{(value == "different" ? "Different" : "Missing")} {key}")),
                            $"Unverified or mismatched {key} was not explained.");
                    }
                }
            }
            JsonObject candidate = Fixtures.New();
            Previous(candidate)["parameters"] = null;
            Previous(candidate)["resources"] = null;
            Assert(Row(await CompareAsync(candidate))["mostRecentPassingExecution"] is not null,
                "Configuration limitations were incorrectly treated as missing identity.");
        });
        await CaseAsync("Different mechanism in the same test is not a match", async () =>
        {
            JsonObject fixture = Fixtures.New("existing-flake");
            Observation(Previous(fixture))["mechanism"] = "Expected 100; actual 101";
            Assert(Row(await CompareAsync(fixture))["matchingOccurrences"]!.GetValue<int>() == 0, "Different assertion matched.");
        });
        await CaseAsync("Independent matching history is separate from PR attempts in every mode", async () =>
        {
            foreach (string mode in new[] { "build", "pr" })
            {
                JsonObject independent = Fixtures.New("existing-flake");
                independent["mode"] = mode;
                JsonNode row = Row(await CompareAsync(independent));
                Assert(row["independentMatchingOccurrences"]!.GetValue<int>() == 1 && row["observedMixedOutcomes"]!.GetValue<bool>(),
                    "Independent history lost.");
                foreach (string identity in new[] { "branch", "reason", "pr-head", "same-source" })
                {
                    JsonObject fixture = independent.DeepClone().AsObject();
                    JsonNode previous = Previous(fixture);
                    switch (identity)
                    {
                        case "branch":
                            previous["branch"] = "refs/pull/12/merge";
                            break;
                        case "reason":
                            previous["reason"] = "pullRequest";
                            break;
                        case "pr-head":
                            previous["prHeadSha"] = Current(fixture)["prHeadSha"]!.DeepClone();
                            break;
                        case "same-source":
                            previous["commit"] = Current(fixture)["commit"]!.DeepClone();
                            break;
                    }
                    JsonObject comparison = await CompareAsync(fixture);
                    row = Row(comparison);
                    Assert(row["independentMatchingOccurrences"]!.GetValue<int>() == 0 &&
                        row["matchingOccurrences"]!.GetValue<int>() == 1 &&
                        row["controls"]![0]!["eligible"]!.GetValue<bool>() &&
                        row["observedMixedOutcomes"]!.GetValue<bool>() &&
                        comparison["coverageGaps"]!.AsArray().Count == 0,
                        $"{identity} became independent in {mode} mode or its observed history was discarded.");
                }
            }
        });
        await CaseAsync("Unknown trigger provenance and the tested PR head are not independent history", async () =>
        {
            foreach ((string key, string? value) in new (string, string?)[]
            {
                ("branch", null), ("branch", ""), ("branch", " \t"), ("reason", null), ("reason", ""), ("reason", "unknown")
            })
            {
                JsonObject fixture = Fixtures.New("existing-flake");
                Previous(fixture)[key] = value;
                JsonNode row = Row(await CompareAsync(fixture));
                Assert(row["matchingOccurrences"]!.GetValue<int>() == 1 &&
                    row["independentMatchingOccurrences"]!.GetValue<int>() == 0,
                    "A missing or unknown trigger established independent history or discarded ordinary matching evidence.");
            }
            JsonObject manual = Fixtures.New("existing-flake");
            Previous(manual)["commit"] = Current(manual)["prHeadSha"]!.DeepClone();
            Previous(manual)["reason"] = "manual";
            JsonNode head = Row(await CompareAsync(manual));
            Assert(head["matchingOccurrences"]!.GetValue<int>() == 1 && head["independentMatchingOccurrences"]!.GetValue<int>() == 0,
                "A manual execution of the tested PR head was called independent.");
        });
        await CaseAsync("Preceding failure and older passing control remain different facts", async () =>
        {
            JsonObject fixture = Fixtures.New("existing-flake");
            fixture["mode"] = "build";
            Current(fixture)["branch"] = "refs/heads/main";
            JsonNode row = Row(await CompareAsync(fixture));
            Assert(row["precedingExecution"]!["buildId"]!.GetValue<string>() == "2" &&
                row["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "1", "Distinct boundaries conflated.");
        });
        await CaseAsync("Same-source scheduled runs retain changed-resource limitations", async () =>
        {
            JsonObject comparison = await CompareAsync(Fixtures.New("scheduled-same-source"));
            JsonNode control = Row(comparison)["controls"]![0]!;
            Assert(control["sameSource"]!.GetValue<bool>() && control["limitations"]!.AsArray().Any(n => n!.GetValue<string>() == "Changed resources"),
                "Same-SHA/resource facts lost.");
            Assert(comparison["changeWindows"]![0]!["commits"]!.AsArray().Count == 0, "Invented commits.");
        });
        await CaseAsync("JSON key order does not imply input differences and key casing is preserved", async () =>
        {
            foreach (bool currentEncoded in new[] { false, true })
            {
                foreach (bool previousEncoded in new[] { false, true })
                {
                    JsonObject fixture = Fixtures.New();
                    string current = """{"configuration":"Debug","key":1,"Key":2}""";
                    string previous = """{ "Key": 2, "key": 1, "configuration": "Debug" }""";
                    Current(fixture)["parameters"] = currentEncoded ? JsonValue.Create(current) : JsonNode.Parse(current);
                    Previous(fixture)["parameters"] = previousEncoded ? JsonValue.Create(previous) : JsonNode.Parse(previous);
                    JsonNode control = Row(await CompareAsync(fixture))["controls"]![0]!;
                    Assert(!control["limitations"]!.AsArray().Any(n => n!.GetValue<string>() == "Changed parameters"),
                        "Serialization order or representation became an input change.");
                    previous = """{"Key":3,"key":1,"configuration":"Debug"}""";
                    Previous(fixture)["parameters"] = previousEncoded ? JsonValue.Create(previous) : JsonNode.Parse(previous);
                    control = Row(await CompareAsync(fixture))["controls"]![0]!;
                    Assert(control["limitations"]!.AsArray().Any(n => n!.GetValue<string>() == "Changed parameters"), "Case-distinct keys conflated.");
                }
            }
        });
        await CaseAsync("Scenario indexes preserve matching executions across large result sets", async () =>
        {
            JsonObject fixture = Fixtures.New("existing-flake");
            fixture["mode"] = "build";
            fixture["pr"] = null;
            int buildIndex = 0;
            foreach (JsonNode? build in fixture["builds"]!.AsArray())
            {
                JsonNode template = Observation(build!).DeepClone();
                JsonArray observations = new();
                build!["branch"] = "refs/heads/main";
                for (int i = 0; i < 10000; i++)
                {
                    JsonNode observation = template.DeepClone();
                    bool failed = buildIndex != 2 && i < 1000;
                    observation["id"] = $"test:1:{i}";
                    observation["name"] = $"Example.Test{i} | Example.Test{i}";
                    observation["outcome"] = failed ? "Failed" : "Passed";
                    observation["mechanism"] = failed ? "Expected 42; actual 43" : "";
                    observations.Add(observation);
                    if (buildIndex == 2 && i < 1000)
                    {
                        JsonNode otherLane = observation.DeepClone();
                        otherLane["id"] = $"test:2:{i}";
                        otherLane["lane"] = "Windows x64 Debug";
                        otherLane["outcome"] = "Failed";
                        otherLane["mechanism"] = "Expected 42; actual 43";
                        observations.Add(otherLane);
                    }
                }
                if (buildIndex == 2)
                {
                    JsonNode duplicateExecution = observations[0]!.DeepClone();
                    duplicateExecution["id"] = "test:3:0";
                    observations.Add(duplicateExecution);
                    JsonNode otherKind = duplicateExecution.DeepClone();
                    otherKind["id"] = "helix:example:0";
                    otherKind["kind"] = "work-item";
                    otherKind["submissionContexts"] = new JsonArray("job:Linux");
                    otherKind["queue"] = "Linux x64 Debug";
                    otherKind["outcome"] = "Failed";
                    otherKind["mechanism"] = "Expected 42; actual 43";
                    observations.Add(otherKind);
                }
                build["observations"] = observations;
                buildIndex++;
            }
            Stopwatch elapsed = Stopwatch.StartNew();
            JsonObject comparison = await CompareAsync(fixture);
            Assert(elapsed.Elapsed < TimeSpan.FromSeconds(10),
                $"Indexed comparison of 1000 failures against two large controls took {elapsed.Elapsed.TotalSeconds:F1} seconds.");
            Assert(comparison["comparisons"]!.AsArray().Count == 1000 && comparison["coverageGaps"]!.AsArray().Count == 0 &&
                comparison["comparisons"]!.AsArray().All(row =>
                    row!["matchingOccurrences"]!.GetValue<int>() == 1 &&
                    row["independentMatchingOccurrences"]!.GetValue<int>() == 1 &&
                    row["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "1" &&
                    row["precedingExecution"]!["buildId"]!.GetValue<string>() == "2") &&
                Row(comparison)["controls"]!.AsArray().Last()!["outcomes"]!.AsArray().Count == 2,
                "The index conflated kinds/lanes, lost duplicate executions, or changed comparison facts.");
        });
        await CaseAsync("Retain whole rolling ranges without script-generated causality or confidence", async () =>
        {
            JsonObject comparison = await CompareAsync(Fixtures.New("rolling-regression"));
            Assert(comparison["changeWindows"]![0]!["commits"]!.AsArray().Count == 2, "Batch narrowed.");
            Assert(!Row(comparison).AsObject().ContainsKey("attribution") && !Row(comparison).AsObject().ContainsKey("confidence"),
                "Script generated a causal verdict.");
        });
        await CaseAsync("Non-ancestor change windows remain coverage gaps", async () =>
        {
            JsonObject fixture = Fixtures.New("rolling-regression");
            fixture["changeWindows"]![0]!["ancestry"] = "diverged";
            fixture["changeWindows"]![0]!["complete"] = false;
            Assert((await CompareAsync(fixture))["coverageGaps"]!.AsArray().Count == 1, "Unverified ancestry hidden.");
        });
        await CaseAsync("Every mixed failure and incomplete/unsupported input stays visible", async () =>
        {
            JsonObject comparison = await CompareAsync(Fixtures.New("mixed-insufficient"));
            Assert(comparison["comparisons"]!.AsArray().Count == 3 && comparison["coverageGaps"]!.AsArray().Count == 3,
                "Mixed failures or gaps omitted.");
        });
        await CaseAsync("Unknown outcomes remain unassessed observations", async () =>
        {
            JsonObject fixture = Fixtures.New();
            Observation(Current(fixture))["outcome"] = "Unknown";
            JsonObject comparison = await CompareAsync(fixture);
            Assert(Row(comparison)["currentOutcome"]!.GetValue<string>() == "Unknown" && comparison["coverageGaps"]!.AsArray().Count == 1,
                "Unknown state omitted.");
        });
        await CaseAsync("Invalid contracts, duplicate identities and absent builds fail explicitly", async () =>
        {
            Action<JsonObject>[] mutations =
            [
                f => f["schemaVersion"] = 2, f => f["selectedBuildIds"] = new JsonArray("absent"),
                f => f["builds"]!.AsArray().Add(Current(f).DeepClone()), f => Observation(Current(f))["outcome"] = "Maybe",
                f => f["builds"] = new JsonObject(), f => f["selectedBuildIds"] = new JsonArray(),
                f => f["checks"]!.AsArray().Add((JsonNode?)null),
                f => f["acquisitions"]!.AsArray().Add(42),
                f => f["changeWindows"]!.AsArray().Add("invalid"),
                f => Current(f)["testRuns"] = new JsonObject()
            ];
            foreach (Action<JsonObject> mutate in mutations)
            {
                JsonObject fixture = Fixtures.New();
                mutate(fixture);
                string directory = NextDirectory();
                string path = Path.Combine(directory, "evidence.json");
                Fixtures.Write(fixture, path);
                (int result, string _, string diagnostic) = await ProcessAsync(_dotnet,
                    [_assembly, "compare", "--evidence-path", path, "--output-directory", directory]);
                Assert(result != 0 && diagnostic.Length > 0, "Invalid contract silently accepted.");
            }
        });
        await CaseAsync("Credential redaction retains endpoints, versions and diagnostic context", async () =>
        {
            JsonObject fixture = Fixtures.New();
            Observation(Current(fixture))["mechanism"] = "MSB4036: https://feed.example/v3?access_token=private&sig=secret Bearer test-secret";
            fixture["changeWindows"] = new JsonArray(new JsonObject
            {
                ["buildId"] = "3", ["complete"] = false, ["authorization"] = "private",
                ["nested"] = new JsonObject { ["password"] = "password-value" }
            });
            string json = (await CompareAsync(fixture)).ToJsonString(Fixtures.Json);
            Assert(!json.Contains("private", StringComparison.Ordinal) && !json.Contains("test-secret", StringComparison.Ordinal) &&
                !json.Contains("password-value", StringComparison.Ordinal) && json.Contains("/v3", StringComparison.Ordinal) &&
                json.Contains("MSB4036", StringComparison.Ordinal), "Credentials leaked or context erased.");
        });
        await CaseAsync("Raw build inputs expose parameter template and resolved resource changes", async () =>
        {
            string replay = Path.Combine(outputDirectory, "raw-build-inputs");
            Fixtures.Collection(replay);
            Fixtures.Save(replay, "az", Fixtures.BuildInputArguments("3"), new
            {
                id = 3, sourceVersion = "3".PadLeft(40, '0'), repository = new { id = "dotnet/sdk" },
                parameters = """{"configuration":"Release"}"""
            });
            Fixtures.Save(replay, "az", Fixtures.BuildInputArguments("3", pipeline: true), new
            {
                id = 3, pipeline = new { id = 101 }, templateParameters = new { configuration = "Release" },
                resources = new { toolchain = "fixed-2" }, finalYaml = "CI_EVIDENCE_UNNEEDED_SECRET",
                variables = new { credential = new { isSecret = true, value = "CI_EVIDENCE_UNNEEDED_SECRET" } }
            });
            Fixtures.Save(replay, "az", Fixtures.BuildInputArguments("2"), new
            {
                id = 2, sourceVersion = "2".PadLeft(40, '0'), repository = new { id = "dotnet/sdk" },
                parameters = """{"configuration":"Debug"}"""
            });
            JsonObject dossier = await CollectAsync(replay);
            JsonNode control = Row(await CompareAsync(dossier))["controls"]![0]!;
            Assert(Current(dossier)["parameters"]!["configuration"]!.GetValue<string>() == "Release" &&
                Current(dossier)["templateParameters"]!["configuration"]!.GetValue<string>() == "Release" &&
                new[] { "parameters", "templateParameters", "resources" }.All(key =>
                    control["limitations"]!.AsArray().Any(l => l!.GetValue<string>() == "Changed " + key)) &&
                dossier["acquisitions"]!.AsArray().All(a => a!["snapshot"] is null ||
                    !File.ReadAllText(a["snapshot"]!.GetValue<string>()).Contains("CI_EVIDENCE_UNNEEDED_SECRET", StringComparison.Ordinal)),
                "Typed producer omissions hid raw input changes, or unrelated secret-bearing metadata was persisted.");
        });
        await CaseAsync("Encoded build parameters are canonicalized and scrubbed before persistence", async () =>
        {
            string replay = Path.Combine(outputDirectory, "encoded-parameters");
            const string Canary = "CI_EVIDENCE_SYNTHETIC_PARAMETER_CREDENTIAL";
            foreach (bool encoded in new[] { false, true })
            {
                Fixtures.Collection(replay);
                Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                    s["arguments"]!.AsArray().Count > 1 && s["arguments"]![1]!.GetValue<string>() == "builds" ||
                    s["tool"]!.GetValue<string>() == "az" && s["arguments"]![4]!.GetValue<string>().Contains("/build/builds/", StringComparison.Ordinal), data =>
                {
                    IEnumerable<JsonNode?> builds = data is JsonArray array ? array : [data];
                    foreach (JsonNode? build in builds)
                    {
                        string parameters = build!["id"]!.GetValue<int>() == 3
                            ? $$$"""{"configuration":"Debug","key":1,"Key":2,"password":"{{{Canary}}}","nested":{"token":"{{{Canary}}}","value":42}}"""
                            : $$$"""{ "nested": { "value": 42, "token": "{{{Canary}}}" }, "password": "{{{Canary}}}", "Key": 2, "key": 1, "configuration": "Debug" }""";
                        build["parameters"] = encoded ? JsonValue.Create(parameters) : JsonNode.Parse(parameters);
                    }
                });
                JsonObject dossier = await CollectAsync(replay);
                JsonObject comparison = await CompareAsync(dossier);
                Assert(dossier["builds"]!.AsArray().All(build =>
                    build!["parameters"] is JsonObject parameters &&
                    parameters["password"]!.GetValue<string>() == "<redacted>" &&
                    parameters["nested"]!["token"]!.GetValue<string>() == "<redacted>" &&
                    parameters["nested"]!["value"]!.GetValue<int>() == 42 &&
                    parameters["key"]!.GetValue<int>() == 1 && parameters["Key"]!.GetValue<int>() == 2) &&
                    !dossier.ToJsonString(Fixtures.Json).Contains(Canary, StringComparison.Ordinal) &&
                    dossier["acquisitions"]!.AsArray().All(a =>
                        !File.ReadAllText(a!["snapshot"]!.GetValue<string>()).Contains(Canary, StringComparison.Ordinal)) &&
                    comparison["comparisons"]!.AsArray().All(row =>
                        !row!["controls"]![0]!["limitations"]!.AsArray().Any(n => n!.GetValue<string>() == "Changed parameters")) &&
                    comparison["coverageGaps"]!.AsArray().Count == 0,
                    "Encoded credentials were persisted, meaningful parameter values were lost, or equal inputs were reported changed.");
            }
        });
        await CaseAsync("Raw build inputs reject malformed parameters and mismatched identities", async () =>
        {
            string replay = Path.Combine(outputDirectory, "invalid-raw-build-inputs");
            const string Canary = "CI_EVIDENCE_INVALID_RAW_PARAMETER_SECRET";
            foreach (string mode in new[] { "parameters", "build", "pipeline" })
            {
                Fixtures.Collection(replay);
                bool pipeline = mode == "pipeline";
                Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "az" &&
                    s["arguments"]!.AsArray().Select(a => a!.GetValue<string>())
                        .SequenceEqual(Fixtures.BuildInputArguments("3", pipeline)), data =>
                {
                    if (mode == "parameters")
                    {
                        data["parameters"] = $$"""{"password":"{{Canary}}","unfinished":""";
                    }
                    else
                    {
                        data["id"] = 99;
                    }
                });
                JsonObject dossier = await CollectAsync(replay,
                    expectedError: mode == "parameters" ? "build parameters" : "Evidence is incomplete");
                Assert(!dossier.ToJsonString(Fixtures.Json).Contains(Canary, StringComparison.Ordinal) &&
                    dossier["acquisitions"]!.AsArray().Where(a => a!["snapshot"] is not null).All(a =>
                        !File.ReadAllText(a!["snapshot"]!.GetValue<string>()).Contains(Canary, StringComparison.Ordinal)) &&
                    (mode == "parameters"
                        ? dossier["acquisitions"]!.AsArray().Last()!["snapshot"] is null
                        : dossier["acquisitions"]!.AsArray().Any(a =>
                            a!["description"]!.GetValue<string>().Contains("mismatched", StringComparison.Ordinal))),
                    "Malformed raw parameters leaked, or input metadata from a different build/run was accepted.");
            }
        });
        await CaseAsync("Empty parameters stay unverified and malformed parameters fail without leaking metadata", async () =>
        {
            foreach (string? value in new string?[] { null, "", " \t", "null" })
            {
                JsonObject fixture = Fixtures.New();
                Current(fixture)["parameters"] = value;
                JsonNode control = Row(await CompareAsync(fixture))["controls"]![0]!;
                Assert(control["limitations"]!.AsArray().Any(n => n!.GetValue<string>() == "Unverified parameters"),
                    "Unavailable parameters became known empty inputs.");
            }
            string replay = Path.Combine(outputDirectory, "invalid-parameters");
            const string Canary = "CI_EVIDENCE_SYNTHETIC_INVALID_PARAMETER_CREDENTIAL";
            foreach (JsonNode invalid in new JsonNode[]
            {
                JsonValue.Create($$"""{"password":"{{Canary}}","unfinished":""")!,
                JsonValue.Create($$"""["{{Canary}}"]""")!,
                new JsonArray(Canary), JsonValue.Create(42)!
            })
            {
                Fixtures.Collection(replay);
                Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                    s["arguments"]!.AsArray().Count > 1 && s["arguments"]![1]!.GetValue<string>() == "builds", data =>
                {
                    foreach (JsonNode? build in data.AsArray())
                    {
                        build!["parameters"] = invalid.DeepClone();
                    }
                });
                JsonObject dossier = await CollectAsync(replay, expectedError: "build parameters");
                Assert(!dossier.ToJsonString(Fixtures.Json).Contains(Canary, StringComparison.Ordinal) &&
                    dossier["acquisitions"]!.AsArray().Last()!["snapshot"] is null &&
                    dossier["acquisitions"]!.AsArray().Where(a => a!["snapshot"] is not null).All(a =>
                        !File.ReadAllText(a!["snapshot"]!.GetValue<string>()).Contains(Canary, StringComparison.Ordinal)),
                    "Malformed parameter metadata leaked through a partial dossier or command snapshot.");
                JsonObject fixture = Fixtures.New();
                Current(fixture)["parameters"] = invalid.DeepClone();
                string directory = NextDirectory();
                string path = Path.Combine(directory, "evidence.json");
                Fixtures.Write(fixture, path);
                (int result, string _, string diagnostic) = await ProcessAsync(_dotnet,
                    [_assembly, "compare", "--evidence-path", path, "--output-directory", directory]);
                Assert(result != 0 && diagnostic.Contains("build parameters", StringComparison.Ordinal) &&
                    !diagnostic.Contains(Canary, StringComparison.Ordinal) && !File.Exists(Path.Combine(directory, "comparison.json")),
                    "Offline comparison accepted malformed parameters or exposed their contents.");
            }
        });
        await CaseAsync("Complete empty lists, truncation, errors and invalid JSON stay distinct", async () =>
        {
            string replay = Path.Combine(outputDirectory, "list-replay");
            Fixtures.Collection(replay);
            foreach ((object data, int exitCode, string? expectedError, bool text) in new (object, int, string?, bool)[]
            {
                (Fixtures.Envelope(), 0, null, false),
                (new { ok = true, results = new JsonArray(), complete = false, truncated = true }, 2, "Evidence is incomplete", false),
                (new { ok = false, error = "Forbidden" }, 1, "Evidence is incomplete", false),
                ("{invalid", 0, "Evidence is incomplete", true)
            })
            {
                Fixtures.Save(replay, "hlx", ["azdo", "test-runs", Fixtures.BuildUrl("3"), "--all", "--json"], data, exitCode, text);
                JsonObject dossier = await CollectAsync(replay, expectedError: expectedError);
                Assert((dossier["error"] is null) == (expectedError is null), "Partial evidence status lost.");
            }
        });
        await CaseAsync("Malformed list entries fail instead of becoming empty results", async () =>
        {
            string replay = Path.Combine(outputDirectory, "malformed-list");
            Fixtures.Collection(replay);
            Fixtures.Save(replay, "hlx", ["azdo", "test-runs", Fixtures.BuildUrl("3"), "--all", "--json"],
                new { ok = true, results = new object?[] { null }, complete = true, truncated = false });
            JsonObject dossier = await CollectAsync(replay, expectedError: "Expected a JSON object array entry.");
            Assert(dossier["error"] is not null, "Malformed list was reported as complete empty evidence.");
        });
        await CaseAsync("Azure JSON task logs decode before timestamp normalization", async () =>
        {
            string replay = Path.Combine(outputDirectory, "json-task-logs");
            Fixtures.Collection(replay);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" && s["arguments"]![0]!.GetValue<string>() == "azdo" &&
                s["arguments"]![1]!.GetValue<string>() == "build", data => data["Result"] = "failed");
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" && s["arguments"]![0]!.GetValue<string>() == "azdo" &&
                s["arguments"]![1]!.GetValue<string>() == "timeline", data =>
                {
                    foreach (JsonNode? task in data["records"]!.AsArray())
                    {
                        task!["result"] = "failed";
                        task["issues"] = new JsonArray();
                        if (task["type"]!.GetValue<string>() == "Task")
                        {
                            task["log"] = new JsonObject { ["id"] = 1 };
                        }
                    }
                });
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.Save(replay, "hlx", ["azdo", "log", Fixtures.BuildUrl(id), "1", "--tail-lines", "120", "--json"],
                    $"2026-10-0{id}T01:00:00Z MSB4036: SDKExampleTask task was not found.\r\n2026-10-0{id}T01:00:01Z at Target.Execute()");
            }
            JsonObject dossier = await CollectAsync(replay);
            JsonObject comparison = await CompareAsync(dossier);
            JsonNode row = comparison["comparisons"]!.AsArray().Single(r => r!["kind"]!.GetValue<string>() == "task")!;
            Assert(row["mechanism"]!.GetValue<string>().Contains("\r\n", StringComparison.Ordinal) &&
                row["signature"]!.GetValue<string>().Contains("<timestamp> MSB4036", StringComparison.Ordinal) &&
                row["matchingOccurrences"]!.GetValue<int>() == 1 && row["independentMatchingOccurrences"]!.GetValue<int>() == 1 &&
                comparison["coverageGaps"]!.AsArray().Count == 0, "Encoded task logs prevented matching the same diagnostic.");
        });
        await CaseAsync("Collected task and test diagnostics retain meaningful boundary whitespace", async () =>
        {
            string replay = Path.Combine(outputDirectory, "diagnostic-whitespace");
            Fixtures.Collection(replay);
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.TestRun(replay, id);
                string mechanism = id == "3" ? "  MSB4036: expected value  " : "MSB4036: expected value";
                Fixtures.Change(replay, s => Fixtures.IsTestAcquisition(s, id) || s["tool"]!.GetValue<string>() == "hlx" &&
                    s["arguments"]!.AsArray().Count > 2 && s["arguments"]![2]!.GetValue<string>() == Fixtures.BuildUrl(id), data =>
                {
                    if (data["records"] is JsonArray records)
                    {
                        foreach (JsonNode? task in records)
                        {
                            task!["result"] = "failed";
                            task["issues"] = new JsonArray();
                            if (task["type"]!.GetValue<string>() == "Task")
                            {
                                task["log"] = new JsonObject { ["id"] = 1 };
                            }
                        }
                    }
                    else if ((data["results"] ?? data["value"]) is JsonArray { Count: 1 } || data["totalTests"] is not null)
                    {
                        JsonObject entry = Fixtures.FirstResult(data);
                        if (entry.ContainsKey("totalTests"))
                        {
                            entry["failedTests"] = 1;
                            entry["passedTests"] = 0;
                        }
                        else
                        {
                            entry["outcome"] = "Failed";
                            entry["errorMessage"] = mechanism;
                        }
                    }
                    else if (data["Result"] is not null)
                    {
                        data["Result"] = "failed";
                    }
                });
                Fixtures.Save(replay, "hlx", ["azdo", "log", Fixtures.BuildUrl(id), "1", "--tail-lines", "120", "--json"], mechanism);
            }
            JsonObject dossier = await CollectAsync(replay);
            JsonObject comparison = await CompareAsync(dossier);
            Assert(Current(dossier)["observations"]!.AsArray().Where(o =>
                o!["kind"]!.GetValue<string>() is "task" or "test").All(o =>
                    o!["mechanism"]!.GetValue<string>() == "  MSB4036: expected value  ") &&
                comparison["comparisons"]!.AsArray().Where(r => r!["kind"]!.GetValue<string>() is "task" or "test").All(r =>
                    r!["matchingOccurrences"]!.GetValue<int>() == 0 && r["independentMatchingOccurrences"]!.GetValue<int>() == 0) &&
                comparison["coverageGaps"]!.AsArray().Count == 0, "Collection trimmed a meaningful assertion before signature comparison.");
        });
        await CaseAsync("Unsupported Azure log JSON shapes remain explicit gaps", async () =>
        {
            string replay = Path.Combine(outputDirectory, "invalid-task-log-shapes");
            Fixtures.Collection(replay);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" && s["arguments"]![0]!.GetValue<string>() == "azdo" &&
                s["arguments"]![1]!.GetValue<string>() == "timeline", data =>
                    data["records"]![1]!["log"] = new JsonObject { ["id"] = 1 });
            foreach (object data in new object[] { new JsonObject(), new JsonArray(), 42, JsonSerializer.Deserialize<JsonElement>("null") })
            {
                Fixtures.Save(replay, "hlx", ["azdo", "log", Fixtures.BuildUrl("3"), "1", "--tail-lines", "120", "--json"], data);
                JsonObject comparison = await CompareAsync(await CollectAsync(replay, expectedError: "Evidence is incomplete"));
                Assert(comparison["coverageGaps"]!.AsArray().Any(g => g!.GetValue<string>().Contains("is not a JSON string", StringComparison.Ordinal)),
                    "Unsupported log shape became valid text.");
            }
        });
        await CaseAsync("Fatal acquisition exceptions remain visible when comparing partial evidence", async () =>
        {
            string replay = Path.Combine(outputDirectory, "fatal-acquisition-replay");
            Fixtures.Collection(replay);
            string missing = Directory.EnumerateFiles(replay, "*.json").Single(file =>
            {
                JsonObject snapshot = Fixtures.Read(file);
                return snapshot["arguments"]!.AsArray().Any(a => a!.GetValue<string>() == "artifacts") &&
                    snapshot["arguments"]!.AsArray().Any(a => a!.GetValue<string>() == Fixtures.BuildUrl("3"));
            });
            File.Delete(missing);
            JsonObject dossier = await CollectAsync(replay, expectedError: "Missing replay command");
            JsonNode acquisition = dossier["acquisitions"]!.AsArray().Last()!;
            Assert(!acquisition["ok"]!.GetValue<bool>() && !acquisition["complete"]!.GetValue<bool>() &&
                acquisition["error"]!.GetValue<string>().Contains("Missing replay command", StringComparison.Ordinal) &&
                acquisition["exitCode"] is null && acquisition["snapshot"] is null &&
                !Current(dossier)["complete"]!.GetValue<bool>(), "Fatal acquisition or incomplete build was lost.");
            JsonObject comparison = await CompareAsync(dossier);
            Assert(comparison["coverageGaps"]!.AsArray().Any(g => g!.GetValue<string>().Contains("Missing replay command", StringComparison.Ordinal)),
                "Collector error disappeared from comparison coverage.");
            JsonObject legacy = Fixtures.New();
            legacy["error"] = dossier["error"]!.DeepClone();
            Assert((await CompareAsync(legacy))["coverageGaps"]!.AsArray().Any(g => g!.GetValue<string>().Contains("Missing replay command", StringComparison.Ordinal)),
                "Legacy partial evidence lost its fatal error.");
        });
        await CaseAsync("Failed builds without failure observations retain explicit coverage gaps", async () =>
        {
            string replay = Path.Combine(outputDirectory, "empty-failed-timeline");
            Fixtures.Collection(replay);
            Fixtures.Save(replay, "hlx", ["azdo", "timeline", Fixtures.BuildUrl("3"), "--filter", "all", "--json"], new { records = new JsonArray() });
            JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
            Assert(Current(dossier)["result"]!.GetValue<string>() == "failed" && !Current(dossier)["complete"]!.GetValue<bool>(),
                "Empty failed timeline became complete.");
            JsonObject comparison = await CompareAsync(dossier);
            Assert(comparison["coverageGaps"]!.AsArray().Any(g => g!.GetValue<string>().Contains("Missing failure evidence", StringComparison.Ordinal)),
                "Failure without jobs disappeared from coverage.");
            JsonObject legacy = Fixtures.New();
            Current(legacy)["observations"] = new JsonArray();
            Assert((await CompareAsync(legacy))["coverageGaps"]!.AsArray().Count == 1, "Legacy empty failed build was trusted.");
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" && s["arguments"]![0]!.GetValue<string>() == "azdo" &&
                s["arguments"]![1]!.GetValue<string>() == "build",
                detail => { if (detail["Id"]!.GetValue<int>() == 3) { detail["Result"] = "succeeded"; } });
            JsonObject successful = await CollectAsync(replay);
            Assert((await CompareAsync(successful))["coverageGaps"]!.AsArray().Count == 0, "Empty successful build became a failure.");
        });
        await CaseAsync("Structured test failures remain usable without timeline tasks", async () =>
        {
            string replay = Path.Combine(outputDirectory, "empty-timeline-with-tests");
            Fixtures.Collection(replay);
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.Save(replay, "hlx", ["azdo", "timeline", Fixtures.BuildUrl(id), "--filter", "all", "--json"], new { records = new JsonArray() });
                Fixtures.TestRun(replay, id);
            }
            JsonObject dossier = await CollectAsync(replay);
            JsonObject comparison = await CompareAsync(dossier);
            Assert(comparison["coverageGaps"]!.AsArray().Count == 0 &&
                Row(comparison)["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "2" &&
                Current(dossier)["testRuns"]![0]!["state"]!.GetValue<string>() == "Completed" &&
                Current(dossier)["testRuns"]![0]!["incompleteTests"]!.GetValue<int>() == 0,
                "Verified structured results were lost because timeline tasks were absent.");
        });
        await CaseAsync("Unfinished and unverified test runs retain metadata, results and coverage gaps", async () =>
        {
            string replay = Path.Combine(outputDirectory, "incomplete-test-runs");
            Fixtures.Collection(replay);
            foreach (string id in new[] { "3", "2" })
            {
                foreach ((string? state, int? incomplete) in new (string?, int?)[]
                {
                    ("InProgress", 1), ("NotStarted", 0), ("Waiting", 0), ("Aborted", 1),
                    ("Completed", 1), (null, 0), ("Completed", null)
                })
                {
                    Fixtures.TestRun(replay, "3");
                    Fixtures.TestRun(replay, "2");
                    Fixtures.TestRun(replay, id, state, incomplete);
                    JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
                    JsonNode build = dossier["builds"]!.AsArray().Single(b => b!["id"]!.GetValue<string>() == id)!;
                    JsonNode run = build["testRuns"]![0]!;
                    Assert(!build["complete"]!.GetValue<bool>() && run["state"]?.GetValue<string>() == state &&
                        run["incompleteTests"]?.GetValue<int>() == incomplete &&
                        build["observations"]!.AsArray().Any(o => o!["kind"]!.GetValue<string>() == "test"),
                        "Incomplete test-run metadata or known results were lost.");
                    JsonObject comparison = await CompareAsync(dossier);
                    string gap = $"Incomplete test run {id} for build {id}";
                    Assert(comparison["coverageGaps"]!.AsArray().Any(g => g!.GetValue<string>().Contains(gap, StringComparison.Ordinal)),
                        "Incomplete test run disappeared from collection coverage.");
                    dossier["error"] = null;
                    dossier["acquisitions"] = new JsonArray();
                    foreach (JsonNode? record in dossier["builds"]!.AsArray())
                    {
                        record!["complete"] = true;
                    }
                    Assert((await CompareAsync(dossier))["coverageGaps"]!.AsArray().Any(g =>
                        g!.GetValue<string>().Contains(gap, StringComparison.Ordinal)),
                        "Offline comparison trusted an incorrect complete flag.");
                }
            }
        });
        await CaseAsync("Retry attempt provenance is separate from structured test scenario lanes", async () =>
        {
            string replay = Path.Combine(outputDirectory, "retry-run-lanes");
            Fixtures.Collection(replay);
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.TestRun(replay, id);
                Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "az" &&
                    s["arguments"]![4]!.GetValue<string>() == Fixtures.RunArguments(id)[4], data =>
                    data["name"] = id == "3" ? "Linux (attempt 2)" : "Linux (attempt 1)");
            }
            JsonObject dossier = await CollectAsync(replay);
            JsonNode test = Current(dossier)["observations"]!.AsArray().Single(o => o!["kind"]!.GetValue<string>() == "test")!;
            JsonNode row = (await CompareAsync(dossier))["comparisons"]!.AsArray().Single(o => o!["kind"]!.GetValue<string>() == "test")!;
            Assert(test["lane"]!.GetValue<string>() == "Linux" && test["runAttempt"]!.GetValue<int>() == 2 &&
                test["runName"]!.GetValue<string>() == "Linux (attempt 2)" &&
                row["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "2",
                "Attempt annotations split the same scenario or retry provenance was discarded.");
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "az" &&
                s["arguments"]![4]!.GetValue<string>() == Fixtures.RunArguments("2")[4], data =>
                data["name"] = "Linux FullFramework (attempt 1)");
            row = (await CompareAsync(await CollectAsync(replay, expectedError: "Evidence is incomplete")))["comparisons"]!
                .AsArray().Single(o => o!["kind"]!.GetValue<string>() == "test")!;
            Assert(row["mostRecentPassingExecution"] is null, "Removing an attempt annotation also erased the framework stream.");
        });
        await CaseAsync("Raw run metadata preserves omitted counters rather than typed producer zeros", async () =>
        {
            string replay = Path.Combine(outputDirectory, "missing-raw-counters");
            Fixtures.Collection(replay);
            Fixtures.TestRun(replay, "3");
            Fixtures.Save(replay, "hlx", ["azdo", "test-runs", Fixtures.BuildUrl("3"), "--all", "--json"], new
            {
                ok = true, complete = true, truncated = false, results = new[]
                {
                    new { id = 3, name = "Linux", state = "Completed", incompleteTests = 0, totalTests = 0, failedTests = 0, passedTests = 0 }
                }
            });
            Fixtures.Save(replay, "az", Fixtures.RunArguments("3"), new { id = 3, name = "Linux", state = "Completed" });
            Fixtures.Save(replay, "az", Fixtures.ResultArguments("3", Fixtures.DiagnosticOutcomes),
                new { count = 0, value = new JsonArray() });
            JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
            JsonNode run = Current(dossier)["testRuns"]![0]!;
            Assert(run["incompleteTests"] is null && run["totalTests"] is null &&
                run["failedTests"] is null && run["passedTests"] is null &&
                (await CompareAsync(dossier))["coverageGaps"]!.AsArray().Any(g =>
                    g!.GetValue<string>().Contains("missing failedTests counter", StringComparison.Ordinal)),
                "Typed default zero counters replaced missing raw metadata or supported complete empty coverage.");
        });
        await CaseAsync("Run counters expose missing, contradictory and duplicate test results", async () =>
        {
            string replay = Path.Combine(outputDirectory, "test-result-counts");
            foreach (string id in new[] { "3", "2" })
            {
                foreach (string mutation in new[] { "empty", "total", "failed", "passed", "duplicate", "missing-counter" })
                {
                    Fixtures.Collection(replay);
                    Fixtures.TestRun(replay, "3");
                    Fixtures.TestRun(replay, "2");
                    Fixtures.Change(replay, s => Fixtures.IsTestAcquisition(s, id), data =>
                    {
                        JsonObject entry = Fixtures.FirstResult(data);
                        if (entry.ContainsKey("totalTests"))
                        {
                            switch (mutation)
                            {
                                case "total":
                                case "duplicate":
                                    entry["totalTests"] = 2;
                                    entry[id == "3" ? "failedTests" : "passedTests"] = 2;
                                    break;
                                case "failed":
                                    entry["failedTests"] = 1;
                                    entry["passedTests"] = 0;
                                    break;
                                case "passed":
                                    entry["failedTests"] = 0;
                                    entry["passedTests"] = 1;
                                    break;
                                case "missing-counter":
                                    entry.Remove("failedTests");
                                    break;
                            }
                        }
                        else
                        {
                            switch (mutation)
                            {
                                case "empty":
                                    Fixtures.ResultRows(data).Clear();
                                    break;
                                case "failed":
                                    entry["outcome"] = "Passed";
                                    break;
                                case "passed":
                                    entry["outcome"] = "Failed";
                                    break;
                                case "duplicate":
                                    Fixtures.ResultRows(data).Add(entry.DeepClone());
                                    break;
                            }
                        }
                    });
                    JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
                    JsonNode build = dossier["builds"]!.AsArray().Single(b => b!["id"]!.GetValue<string>() == id)!;
                    Assert(!build["complete"]!.GetValue<bool>() && build["testRuns"]!.AsArray().Count == 1,
                        "Inconsistent run counters were discarded or treated as complete.");
                    string gap = $"Test result coverage for run {id} in build {id}";
                    Assert((await CompareAsync(dossier))["coverageGaps"]!.AsArray().Any(g =>
                        g!.GetValue<string>().Contains(gap, StringComparison.Ordinal)), "A result-count mismatch disappeared from coverage.");
                    dossier["error"] = null;
                    dossier["acquisitions"] = new JsonArray();
                    foreach (JsonNode? record in dossier["builds"]!.AsArray())
                    {
                        record!["complete"] = true;
                        record.AsObject().Remove("testResultsIncludePassed");
                    }
                    Assert((await CompareAsync(dossier))["coverageGaps"]!.AsArray().Any(g =>
                        g!.GetValue<string>().Contains(gap, StringComparison.Ordinal)),
                        "Offline comparison trusted complete flags despite contradictory run counters.");
                }
            }
            Fixtures.Collection(replay);
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.TestRun(replay, id);
                Fixtures.Change(replay, s => Fixtures.IsTestAcquisition(s, id), data =>
                {
                    JsonObject entry = Fixtures.FirstResult(data);
                    if (entry.ContainsKey("totalTests"))
                    {
                        entry["totalTests"] = 0;
                        entry["failedTests"] = 0;
                        entry["passedTests"] = 0;
                    }
                    else
                    {
                        Fixtures.ResultRows(data).Clear();
                    }
                });
            }
            Assert((await CompareAsync(await CollectAsync(replay)))["coverageGaps"]!.AsArray().Count == 0,
                "A verified zero-test run became incomplete.");
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.TestRun(replay, id);
                Fixtures.Change(replay, s => Fixtures.IsTestAcquisition(s, id), data =>
                {
                    JsonObject entry = Fixtures.FirstResult(data);
                    if (entry.ContainsKey("totalTests"))
                    {
                        entry["failedTests"] = 0;
                        entry["passedTests"] = 0;
                    }
                    else
                    {
                        entry["outcome"] = id == "3" ? "NotApplicable" : "Error";
                    }
                });
            }
            Assert((await CompareAsync(await CollectAsync(replay)))["coverageGaps"]!.AsArray().Count == 0,
                "A normalized error or skipped result incorrectly contradicted run counters.");
        });
        await CaseAsync("Raw result hierarchies retain parameterized cases without inventing parent-pass controls", async () =>
        {
            string replay = Path.Combine(outputDirectory, "parameterized-results");
            Fixtures.Collection(replay);
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.TestRun(replay, id);
                Fixtures.Change(replay, s => Fixtures.IsRawResults(s, id), data =>
                {
                    JsonNode parent = Fixtures.ResultRows(data)[0]!;
                    parent["resultGroupType"] = "dataDriven";
                    parent["subResults"] = new JsonArray(new JsonObject
                    {
                        ["id"] = 10, ["displayName"] = "Case(1)", ["outcome"] = "Passed", ["resultGroupType"] = "none"
                    });
                    if (id == "3")
                    {
                        parent["subResults"]!.AsArray().Add(new JsonObject
                        {
                            ["id"] = 11, ["displayName"] = "Case(2)", ["outcome"] = "Failed", ["errorMessage"] = "MSB4036 Expected 42",
                            ["resultGroupType"] = "none"
                        });
                    }
                });
            }
            JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
            JsonObject comparison = await CompareAsync(dossier);
            JsonNode failure = comparison["comparisons"]!.AsArray().Single(r =>
                r!["name"]!.GetValue<string>().EndsWith(" > Case(2)", StringComparison.Ordinal))!;
            Assert(Current(dossier)["observations"]!.AsArray().Count(o => o!["kind"]!.GetValue<string>() == "test") == 2 &&
                failure["mostRecentPassingExecution"] is null && failure["precedingExecution"] is null &&
                !failure["controls"]![0]!["eligible"]!.GetValue<bool>() &&
                !comparison["coverageGaps"]!.AsArray().Any(g =>
                    g!.GetValue<string>().Contains("Test result coverage", StringComparison.Ordinal)),
                "An aggregate pass qualified as execution of an absent parameterized case, or children inflated run counters.");
            Fixtures.Change(replay, s => Fixtures.IsRawResults(s, "2"), data =>
                Fixtures.ResultRows(data)[0]!["subResults"] = new JsonArray());
            comparison = await CompareAsync(await CollectAsync(replay, expectedError: "Evidence is incomplete"));
            Assert(comparison["coverageGaps"]!.AsArray().Any(g =>
                g!.GetValue<string>().Contains("Unverified test hierarchy", StringComparison.Ordinal)),
                "A grouped parent without acquired children became a complete named assertion.");
            JsonObject legacy = Fixtures.New();
            Observation(Previous(legacy)).AsObject().Remove("hierarchyVerified");
            Assert(Row(await CompareAsync(legacy))["mostRecentPassingExecution"] is null,
                "An older projected parent with unknown hierarchy established a case-level passing control.");
        });
        await CaseAsync("Raw result acquisition retains every official Azure outcome without inventing passes", async () =>
        {
            string replay = Path.Combine(outputDirectory, "all-result-outcomes");
            string[] outcomes =
            [
                "Unspecified", "None", "Passed", "Failed", "Inconclusive", "Timeout", "Aborted", "Blocked",
                "NotExecuted", "Warning", "Error", "NotApplicable", "Paused", "InProgress", "NotImpacted"
            ];
            Fixtures.Collection(replay);
            Fixtures.TestRun(replay, "3");
            foreach (string outcome in outcomes)
            {
                Fixtures.IssueSearch(replay, "Example." + outcome);
            }
            Fixtures.Save(replay, "az", Fixtures.RunArguments("3"), new
            {
                id = 3, name = "Linux", state = "Completed", incompleteTests = 0,
                totalTests = outcomes.Length, failedTests = 1, passedTests = 1
            });
            foreach (bool full in new[] { true, false })
            {
                var results = outcomes.Where(o => full || o != "Passed").Select((outcome, index) => new
                {
                    id = index + 1, automatedTestName = "Example." + outcome, testCaseTitle = "Example." + outcome,
                    outcome, errorMessage = "", stackTrace = ""
                }).ToArray();
                Fixtures.Save(replay, "az", Fixtures.ResultArguments("3", full ? "" : Fixtures.DiagnosticOutcomes),
                    new { count = results.Length, value = results });
                JsonObject dossier = await CollectAsync(replay, full ? null : ["--test-result-limit", "1"], "Evidence is incomplete");
                JsonObject comparison = await CompareAsync(dossier);
                JsonNode[] tests = Current(dossier)["observations"]!.AsArray()
                    .Where(o => o!["kind"]!.GetValue<string>() == "test").Select(o => o!).ToArray();
                Assert(tests.Length == results.Length && tests.All(t =>
                    t["outcome"]!.GetValue<string>() == (t["producerOutcome"]!.GetValue<string>() switch
                    {
                        "Passed" => "Passed",
                        "Failed" or "Error" or "Timeout" or "Aborted" => "Failed",
                        "NotExecuted" or "NotApplicable" or "NotImpacted" => "Skipped",
                        _ => "Unknown"
                    })) && tests.Any(t => t["producerOutcome"]!.GetValue<string>() == "None") &&
                    !comparison["coverageGaps"]!.AsArray().Any(g =>
                        g!.GetValue<string>().Contains("Test result coverage for run", StringComparison.Ordinal)) &&
                    dossier["acquisitions"]!.AsArray().Where(a =>
                        a!["description"]!.GetValue<string>().Contains("/test/Runs/3/results?", StringComparison.Ordinal))
                        .All(a => a!["description"]!.GetValue<string>().Contains("&outcomes=", StringComparison.Ordinal) != full),
                    "Outcome filtering lost valid states, created false passes, or caused artificial counter mismatches.");
            }
        });
        await CaseAsync("Diagnostic-only results reconcile failures without requiring omitted passes", async () =>
        {
            string replay = Path.Combine(outputDirectory, "diagnostic-result-counts");
            Fixtures.Collection(replay);
            foreach (string id in new[] { "3", "2" })
            {
                Fixtures.TestRun(replay, id);
                Fixtures.Change(replay, s => Fixtures.IsTestAcquisition(s, id) && !Fixtures.IsRawResults(s, id), data =>
                {
                    Fixtures.FirstResult(data)["totalTests"] = 2;
                    Fixtures.FirstResult(data)["passedTests"] = id == "3" ? 1 : 2;
                });
                Fixtures.Save(replay, "az", Fixtures.ResultArguments(id, Fixtures.DiagnosticOutcomes), id == "3" ? new
                    {
                        count = 1, value = new[]
                        {
                            new { id = 1, automatedTestName = "Example.Test", testCaseTitle = "Example.Test",
                                outcome = "Failed", errorMessage = "MSB4036: SDKExampleTask task was not found.", stackTrace = "" }
                        }
                    } : new { count = 0, value = new JsonArray() });
            }
            JsonObject dossier = await CollectAsync(replay, ["--test-result-limit", "1"], "Evidence is incomplete");
            JsonObject comparison = await CompareAsync(dossier);
            Assert(dossier["builds"]!.AsArray().All(b => !b!["testResultsIncludePassed"]!.GetValue<bool>()) &&
                !comparison["coverageGaps"]!.AsArray().Any(g =>
                    g!.GetValue<string>().Contains("Test result coverage for run", StringComparison.Ordinal)) &&
                comparison["comparisons"]!.AsArray().Any(r => r!["kind"]!.GetValue<string>() == "test"),
                "Intentionally omitted passes caused a counter mismatch or discarded acquired failures.");
            Fixtures.Save(replay, "az", Fixtures.ResultArguments("3", Fixtures.DiagnosticOutcomes),
                new { count = 0, value = new JsonArray() });
            comparison = await CompareAsync(await CollectAsync(replay, ["--test-result-limit", "1"], "Evidence is incomplete"));
            Assert(comparison["coverageGaps"]!.AsArray().Any(g =>
                g!.GetValue<string>().Contains("Test result coverage for run 3 in build 3", StringComparison.Ordinal)),
                "Diagnostic-only collection hid missing advertised failures.");
        });
        await CaseAsync("Issue search budgets count unique queries rather than repeated failure observations", async () =>
        {
            string replay = Path.Combine(outputDirectory, "unique-issue-searches");
            Fixtures.Collection(replay);
            string[] diagnostics = ["NETSDK1045", "NETSDK1045", "NETSDK1045", "MSB4019", "CS1002", "NU1101"];
            foreach (string term in diagnostics.Distinct(StringComparer.Ordinal))
            {
                Fixtures.IssueSearch(replay, term);
            }
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                s["arguments"]!.AsArray().Count > 1 && s["arguments"]![1]!.GetValue<string>() == "timeline", data =>
            {
                JsonArray records = data["records"]!.AsArray();
                JsonNode template = records[1]!.DeepClone();
                records.RemoveAt(1);
                for (int i = 0; i < diagnostics.Length; i++)
                {
                    JsonNode task = template.DeepClone();
                    task["id"] = $"task-{i}";
                    task["name"] = $"Build step {i}";
                    task["issues"] = task["result"]!.GetValue<string>() == "failed"
                        ? new JsonArray(new JsonObject { ["message"] = diagnostics[i] + ": synthetic failure" }) : new JsonArray();
                    records.Add(task);
                }
            });
            JsonObject dossier = await CollectAsync(replay);
            string[] queries = dossier["issueSearches"]!.AsArray().Select(s => s!["query"]!.GetValue<string>()).ToArray();
            Assert(queries.Length == 6 && queries.Distinct(StringComparer.Ordinal).Count() == 6 &&
                new[] { "NETSDK1045", "MSB4019", "CS1002" }.All(term =>
                    new[] { "open", "closed" }.All(state =>
                        queries.Contains($"{term} repo:dotnet/sdk is:issue is:{state}", StringComparer.Ordinal))) &&
                !queries.Any(q => q.StartsWith("NU1101 ", StringComparison.Ordinal)) &&
                dossier["acquisitions"]!.AsArray().Count(a =>
                    a!["description"]!.GetValue<string>().StartsWith("gh api --method GET search/issues?", StringComparison.Ordinal)) == 6,
                "Duplicate queries consumed the budget, duplicate search rows were emitted, or the distinct-query cap was exceeded.");
        });
        await CaseAsync("CLI rejects write commands, unknown options, invalid ranges and missing replay entries", async () =>
        {
            foreach (string[] arguments in new[]
            {
                new[] { "pr", "comment", "1" },
                new[] { "collect", "--build", "3", "--output-directory", NextDirectory(), "--method", "POST" },
                new[] { "collect", "--build", "3", "--output-directory", NextDirectory(), "--history-limit", "0" },
                new[] { "collect", "--build", "3", "--output-directory", NextDirectory(), "--build", "2" }
            })
            {
                (int result, string _, string diagnostic) = await ProcessAsync(_dotnet, [_assembly, .. arguments]);
                Assert(result != 0 && diagnostic.Length > 0, "Invalid/write command accepted.");
            }
            string replay = Path.Combine(outputDirectory, "missing-replay");
            Directory.CreateDirectory(replay);
            await CollectAsync(replay, expectedError: "Missing replay command");
        });
        await CaseAsync("Offline entry point persists full JSON and Markdown", async () =>
        {
            string directory = Path.Combine(outputDirectory, "offline");
            JsonObject comparison = await CompareAsync(Fixtures.New("mixed-insufficient"), directory);
            Assert(comparison["comparisons"]!.AsArray().Count == 3 &&
                File.ReadAllText(Path.Combine(directory, "comparison.md")).Contains("Coverage gaps", StringComparison.Ordinal), "Persistent output differs.");
        });
        await CaseAsync("Never-started canceled timeline records cannot narrow an executed source window", async () =>
        {
            string replay = Path.Combine(outputDirectory, "never-started");
            Fixtures.Collection(replay);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                s["arguments"]!.AsArray().Count > 2 && s["arguments"]![1]!.GetValue<string>() == "timeline" &&
                s["arguments"]![2]!.GetValue<string>() == Fixtures.BuildUrl("2"), data =>
            {
                foreach (JsonNode? entry in data["records"]!.AsArray())
                {
                    entry!["result"] = "canceled";
                    entry["startTime"] = null;
                }
            });
            JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
            Assert(dossier["changeWindows"]!.AsArray().All(w => w!["baselineBuildId"] is null),
                "A never-started canceled build became the source boundary.");
            JsonNode earlier = Previous(dossier).DeepClone();
            earlier["id"] = "1";
            earlier["commit"] = "1".PadLeft(40, '0');
            earlier["queuedAt"] = "2026-10-01T00:00:00Z";
            earlier["complete"] = true;
            foreach (JsonNode? entry in earlier["observations"]!.AsArray())
            {
                entry!["outcome"] = "Passed";
                entry["executionVerified"] = true;
            }
            dossier["builds"]!.AsArray().Add(earlier);
            Assert((await CompareAsync(dossier))["comparisons"]!.AsArray()
                .Where(r => r!["buildId"]!.GetValue<string>() == "3")
                .All(r => r!["precedingExecution"]!["buildId"]!.GetValue<string>() == "1"),
                "Cancellation without execution hid the actual preceding scenario execution.");
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                s["arguments"]!.AsArray().Count > 2 && s["arguments"]![1]!.GetValue<string>() == "timeline" &&
                s["arguments"]![2]!.GetValue<string>() == Fixtures.BuildUrl("2"), data =>
            {
                foreach (JsonNode? entry in data["records"]!.AsArray())
                {
                    entry!["startTime"] = "2026-10-02T00:01:00Z";
                }
            });
            Assert((await CompareAsync(await CollectAsync(replay)))["comparisons"]!.AsArray()
                .All(r => r!["precedingExecution"]!["buildId"]!.GetValue<string>() == "2"),
                "A canceled execution with a real start time was incorrectly discarded.");
        });
        await CaseAsync("Full timelines preserve passing controls despite the producer's failure-only default", async () =>
        {
            string replay = Path.Combine(outputDirectory, "complete-timelines");
            Fixtures.Collection(replay);
            foreach (string file in Directory.EnumerateFiles(replay, "*.json").ToArray())
            {
                JsonObject snapshot = Fixtures.Read(file);
                JsonArray arguments = snapshot["arguments"]!.AsArray();
                if (snapshot["tool"]!.GetValue<string>() == "hlx" && arguments.Count > 1 &&
                    arguments[1]!.GetValue<string>() == "timeline")
                {
                    JsonNode data = JsonNode.Parse(snapshot["stdout"]!.GetValue<string>())!;
                    JsonArray failed = new(data["records"]!.AsArray()
                        .Where(r => r!["result"]!.GetValue<string>() != "succeeded" || r["issues"] is JsonArray { Count: > 0 })
                        .Select(r => r!.DeepClone()).ToArray());
                    Fixtures.Save(replay, "hlx", ["azdo", "timeline", arguments[2]!.GetValue<string>(), "--json"],
                        new { records = failed });
                }
            }
            JsonObject dossier = await CollectAsync(replay);
            JsonObject comparison = await CompareAsync(dossier);
            Assert(Previous(dossier)["observations"]!.AsArray().Count == 2 &&
                Previous(dossier)["observations"]!.AsArray().All(o => o!["outcome"]!.GetValue<string>() == "Passed") &&
                dossier["acquisitions"]!.AsArray().Where(a =>
                    a!["description"]!.GetValue<string>().StartsWith("hlx azdo timeline ", StringComparison.Ordinal))
                    .All(a => a!["description"]!.GetValue<string>().EndsWith("--filter all --json", StringComparison.Ordinal)) &&
                comparison["comparisons"]!.AsArray().Count == 2 &&
                comparison["comparisons"]!.AsArray().All(row =>
                    row!["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "2" &&
                    row["precedingExecution"]!["buildId"]!.GetValue<string>() == "2") &&
                dossier["changeWindows"]!.AsArray().Count == 2 &&
                dossier["changeWindows"]!.AsArray().All(w =>
                    w!["complete"]!.GetValue<bool>() && w["baselineBuildId"]!.GetValue<string>() == "2") &&
                comparison["coverageGaps"]!.AsArray().Count == 0,
                "The default failure-only view discarded executed passing controls or their source windows.");
        });
        await CaseAsync("Standalone, current PR and historical PR replay never invoke services", async () =>
        {
            foreach (string mode in new[] { "build", "historical-pr", "current-pr" })
            {
                string replay = Path.Combine(outputDirectory, mode + "-replay");
                Fixtures.Collection(replay, pr: mode != "build");
                JsonObject dossier;
                if (mode == "current-pr")
                {
                    string directory = NextDirectory();
                    (int result, string _, string diagnostic) = await ProcessAsync(_dotnet,
                        [_assembly, "collect", "--pull-request", "12", "--history-limit", "1", "--lookback-days", "2",
                            "--output-directory", directory, "--replay-directory", replay, "--gh-path", "never-executed", "--hlx-path", "never-executed"]);
                    Assert(result == 0, diagnostic);
                    dossier = Fixtures.Read(Path.Combine(directory, "evidence.json"));
                }
                else
                {
                    dossier = await CollectAsync(replay, mode == "build" ? null : ["--pull-request", "12"]);
                }
                Assert(dossier["replayed"]!.GetValue<bool>() && dossier["builds"]!.AsArray().Count == 2 &&
                    dossier["changeWindows"]![0]!["commits"]!.AsArray().Count == 2, "Collection mode, controls or range lost.");
                Assert(dossier["acquisitions"]!.AsArray().All(a => a!["complete"]!.GetValue<bool>()), "Complete replay became partial.");
            }
        });
        await CaseAsync("PR validations selected by build ID retain retries without claiming independence", async () =>
        {
            string replay = Path.Combine(outputDirectory, "pr-build-retries");
            Fixtures.Collection(replay, pr: true);
            string branch = "refs/pull/12/merge";
            string head = new('a', 40);
            string previousSha = "2".PadLeft(40, '0');
            DateTimeOffset queued = DateTimeOffset.Parse("2026-10-03T00:00:00Z", CultureInfo.InvariantCulture);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                s["arguments"]!.AsArray().Count > 1 && s["arguments"]![1]!.GetValue<string>() == "build", data =>
            {
                if (data["Id"]!.GetValue<int>() == 2)
                {
                    data["SourceBranch"] = branch;
                    data["Result"] = "failed";
                }
            });
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                s["arguments"]!.AsArray().Count > 2 && s["arguments"]![1]!.GetValue<string>() == "timeline" &&
                s["arguments"]![2]!.GetValue<string>() == Fixtures.BuildUrl("2"), data =>
            {
                foreach (JsonNode? task in data["records"]!.AsArray())
                {
                    task!["result"] = "failed";
                    if (task["type"]!.GetValue<string>() == "Task")
                    {
                        task["issues"] = new JsonArray(new JsonObject { ["message"] = "MSB4036: SDKExampleTask task was not found." });
                    }
                }
            });
            foreach (string historyBranch in new[] { branch, "refs/heads/main" })
            {
                Fixtures.Save(replay, "hlx", ["azdo", "builds", "--org", "dnceng-public", "--project", "public", "--definition-id", "101",
                    "--branch", historyBranch, "--min-time", queued.AddDays(-2).ToString("o"), "--max-time", queued.AddTicks(-1).ToString("o"),
                    "--top", "1", "--query-order", "queueTimeDescending", "--json"], historyBranch == branch ? new object[]
                    {
                        new
                        {
                            id = 2, repository = new { id = "dotnet/sdk" }, sourceBranch = branch, sourceVersion = previousSha,
                            status = "completed", reason = "pullRequest",
                            triggerInfo = new Dictionary<string, string> { ["pr.sourceSha"] = head, ["pr.number"] = "12" }
                        }
                    } : []);
            }
            Fixtures.Save(replay, "gh", ["api", "--method", "GET", $"repos/dotnet/sdk/git/commits/{previousSha}"],
                new { parents = new[] { new { sha = "1".PadLeft(40, '0') }, new { sha = head } } });
            Fixtures.Save(replay, "gh", ["api", "--method", "GET", "--paginate", "--slurp",
                $"repos/dotnet/sdk/commits/{"3".PadLeft(40, '0')}/check-runs?per_page=100"],
                new[] { new { check_runs = new JsonArray() } });
            foreach (bool includePr in new[] { false, true })
            {
                JsonObject dossier = await CollectAsync(replay, includePr ? ["--pull-request", "12"] : null);
                JsonObject comparison = await CompareAsync(dossier);
                JsonNode row = comparison["comparisons"]!.AsArray().Single(r => r!["kind"]!.GetValue<string>() == "task")!;
                Assert(dossier["mode"]!.GetValue<string>() == (includePr ? "pr" : "build") &&
                    row["matchingOccurrences"]!.GetValue<int>() == 1 &&
                    row["independentMatchingOccurrences"]!.GetValue<int>() == 0 &&
                    row["controls"]![0]!["samePrHead"]!.GetValue<bool>() &&
                    row["precedingExecution"]!["buildId"]!.GetValue<string>() == "2" &&
                    comparison["coverageGaps"]!.AsArray().Count == 0 &&
                    dossier["changeWindows"]!.AsArray().All(w => w!["complete"]!.GetValue<bool>()),
                    "Build-ID selection reclassified a PR retry or discarded its execution/source window.");
            }
        });
        await CaseAsync("Check coverage requires the selected organization, project and build ID", async () =>
        {
            string replay = Path.Combine(outputDirectory, "check-identity-replay");
            foreach (string url in new[]
            {
                "https://dev.azure.com/dnceng/internal/_build/results?buildId=3",
                "https://dev.azure.com/dnceng-public/another-project/_build/results?buildId=3",
                "https://dev.azure.com/dnceng-public/public/_build/results?buildId=4",
                "https://example.invalid/results?buildId=3",
                "http://dev.azure.com/dnceng-public/public/_build/results?buildId=3",
                "3"
            })
            {
                Fixtures.Collection(replay, pr: true);
                Fixtures.AddCheck(replay, "Same build", "https://dev.azure.com/dnceng-public/public/_build/results/?view=logs&buildId=3");
                Fixtures.AddCheck(replay, "Uncollected check", url);
                JsonObject dossier = await CollectAsync(replay, ["--pull-request", "12"]);
                Assert(dossier["checks"]!.AsArray().Count == 3 &&
                    dossier["checks"]!.AsArray().All(c => c!["coverage"]!.GetValue<string>() ==
                        (c["name"]!.GetValue<string>() == "Uncollected check" ? "unassessed" : "assessed")),
                    "A check was assessed using only its numeric build ID.");
                JsonObject comparison = await CompareAsync(dossier);
                Assert(comparison["coverageGaps"]!.AsArray().Count == 1 &&
                    comparison["coverageGaps"]![0]!.GetValue<string>() == "Unassessed check: Uncollected check",
                    "An uncollected check disappeared from coverage.");
            }
        });
        await CaseAsync("Current PR discovery retains unsupported checks without selecting their build IDs", async () =>
        {
            string replay = Path.Combine(outputDirectory, "unsupported-check-replay");
            Fixtures.Collection(replay, pr: true);
            Fixtures.AddCheck(replay, "Unsupported check", "https://example.invalid/results?buildId=3");
            Fixtures.AddCheck(replay, "Bare build ID", "3");
            string directory = NextDirectory();
            (int result, string _, string diagnostic) = await ProcessAsync(_dotnet,
                [_assembly, "collect", "--pull-request", "12", "--history-limit", "1", "--lookback-days", "2",
                    "--output-directory", directory, "--replay-directory", replay, "--gh-path", "never-executed", "--hlx-path", "never-executed"]);
            Assert(result == 0, diagnostic);
            JsonObject dossier = Fixtures.Read(Path.Combine(directory, "evidence.json"));
            Assert(dossier["selectedBuildIds"]!.AsArray().Count == 1 &&
                dossier["selectedBuildIds"]![0]!.GetValue<string>() == "3" &&
                (await CompareAsync(dossier))["coverageGaps"]!.AsArray().Count == 2,
                "Unsupported check URLs became build identities or lost their coverage gaps.");
        });
        await CaseAsync("Explicit baseline is honored and mismatched PR/build identity rejected", async () =>
        {
            string replay = Path.Combine(outputDirectory, "build-replay");
            Fixtures.Collection(replay);
            Fixtures.TestRun(replay, "3");
            Fixtures.TestRun(replay, "2");
            JsonObject dossier = await CollectAsync(replay, ["--baseline-build", "2"]);
            Assert(dossier["changeWindows"]!.AsArray().Count == 3 &&
                dossier["changeWindows"]!.AsArray().All(w => w!["baselineBuildId"]!.GetValue<string>() == "2"),
                "Explicit baseline ignored or execution provenance was lost between comparison and validation.");
            replay = Path.Combine(outputDirectory, "mismatched-pr");
            Fixtures.Collection(replay, pr: true);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" && s["arguments"]![0]!.GetValue<string>() == "azdo",
                data =>
                {
                    if (data is JsonObject detail && detail["Id"]?.GetValue<int>() == 3)
                    {
                        detail["SourceBranch"] = "refs/heads/main";
                    }
                    if (data is JsonArray { Count: 1 } rows && rows[0]!["id"]!.GetValue<int>() == 3)
                    {
                        rows[0]!["sourceBranch"] = "refs/heads/main";
                    }
                });
            await CollectAsync(replay, ["--pull-request", "12"], "does not validate the requested PR");
        });
        await CaseAsync("Missing pipeline identity cannot define an automatic or explicit source window", async () =>
        {
            string replay = Path.Combine(outputDirectory, "missing-control-pipeline");
            Fixtures.Collection(replay);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" && s["arguments"]![0]!.GetValue<string>() == "azdo" &&
                s["arguments"]![1]!.GetValue<string>() == "build", detail =>
                {
                    if (detail["Id"]!.GetValue<int>() == 2)
                    {
                        detail["DefinitionId"] = null;
                    }
                });
            JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
            Assert(dossier["changeWindows"]!.AsArray().Count == 2 &&
                dossier["changeWindows"]!.AsArray().All(w => w!["baselineBuildId"] is null && !w["complete"]!.GetValue<bool>()),
                "An unknown pipeline established a source window.");
            await CollectAsync(replay, ["--baseline-build", "2"], "Explicit baseline must be an earlier same-branch/pipeline execution");
        });
        await CaseAsync("Capped comparison file lists preserve useful but incomplete ranges", async () =>
        {
            string replay = Path.Combine(outputDirectory, "capped-files");
            Fixtures.Collection(replay);
            Fixtures.Save(replay, "gh", Fixtures.CompareArguments(), new[]
            {
                new { status = "ahead", total_commits = 2, commits = Fixtures.Commits(),
                    files = Enumerable.Range(1, 300).Select(n => new { filename = $"file-{n}.cs" }).ToArray() }
            });
            JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
            JsonNode window = dossier["changeWindows"]![0]!;
            Assert(!window["complete"]!.GetValue<bool>() && window["limitation"]!.GetValue<string>().Contains("300-file cap", StringComparison.Ordinal) &&
                window["commits"]!.AsArray().Count == 2 && File.Exists(window["diffPath"]!.GetValue<string>()), "Capped coverage lost.");
        });
        await CaseAsync("Helix replay retains console excerpts, artifact inventories and executed passes", async () =>
        {
            string replay = Path.Combine(outputDirectory, "helix-replay");
            Fixtures.Collection(replay, helix: true);
            JsonObject dossier = await CollectAsync(replay, ["--baseline-build", "2"]);
            JsonObject comparison = await CompareAsync(dossier);
            JsonNode row = comparison["comparisons"]!.AsArray().First(r => r!["kind"]!.GetValue<string>() == "work-item")!;
            Assert(row["mechanism"]!.GetValue<string>().Contains("MSB4036", StringComparison.Ordinal) &&
                row["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "2", "Console or executed control lost.");
            JsonNode item = Current(dossier)["observations"]!.AsArray().First(o => o!["kind"]!.GetValue<string>() == "work-item")!;
            Assert(item["artifacts"]!["binlogs"]!.AsArray().Count == 1, "Actual artifact inventory lost.");
            string missing = Path.Combine(outputDirectory, "helix-missing-console");
            Directory.CreateDirectory(missing);
            foreach (string file in Directory.EnumerateFiles(replay, "*.json").Where(f => !f.EndsWith(".console.json", StringComparison.Ordinal)))
            {
                File.Copy(file, Path.Combine(missing, Path.GetFileName(file)), overwrite: true);
            }
            await CollectAsync(missing, expectedError: "Evidence is incomplete");
        });
        await CaseAsync("Helix jobs without queues retain failures logs and artifacts but not controls", async () =>
        {
            string replay = Path.Combine(outputDirectory, "helix-missing-queue");
            foreach (string? queue in new string?[] { null, "", " \t" })
            {
                Fixtures.Collection(replay, helix: true);
                Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                    s["arguments"]![0]!.GetValue<string>() == "status", data => data["job"]!["QueueId"] = queue);
                JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
                JsonObject comparison = await CompareAsync(dossier);
                JsonNode row = comparison["comparisons"]!.AsArray().Single(r => r!["kind"]!.GetValue<string>() == "work-item")!;
                JsonNode item = Current(dossier)["observations"]!.AsArray().Single(o => o!["kind"]!.GetValue<string>() == "work-item")!;
                Assert(item["lane"]!.GetValue<string>() == "" &&
                    item["mechanism"]!.GetValue<string>().Contains("MSB4036", StringComparison.Ordinal) &&
                    item["artifacts"]!["binlogs"]!.AsArray().Count == 1 &&
                    Previous(dossier)["observations"]!.AsArray().Any(o =>
                        o!["kind"]!.GetValue<string>() == "work-item" && o["outcome"]!.GetValue<string>() == "Passed") &&
                    row["mostRecentPassingExecution"] is null && row["precedingExecution"] is null &&
                    !row["controls"]![0]!["eligible"]!.GetValue<bool>() &&
                    comparison["coverageGaps"]!.AsArray().Any(g => g!.GetValue<string>().Contains("missing queue", StringComparison.Ordinal)),
                    "An unknown queue discarded acquired failures/artifacts, invented a lane, or qualified as an executed control.");
            }
        });
        await CaseAsync("Official monitor failure messages discover Helix jobs without a URL", async () =>
        {
            string replay = Path.Combine(outputDirectory, "monitor-message-discovery");
            Fixtures.Collection(replay, helix: true);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                s["arguments"]!.AsArray().Count > 2 && s["arguments"]![1]!.GetValue<string>() == "timeline" &&
                s["arguments"]![2]!.GetValue<string>() == Fixtures.BuildUrl("3"), data =>
                    data["records"]![1]!["issues"] = new JsonArray(new JsonObject
                    {
                        ["message"] = "MSB4036: Work item 'Example.WorkItem' in job 'Linux x64 Debug (33333333-3333-3333-3333-333333333333)' failed"
                    }));
            JsonObject dossier = await CollectAsync(replay);
            Assert(Current(dossier)["observations"]!.AsArray().Any(o =>
                o!["kind"]!.GetValue<string>() == "work-item" && o["outcome"]!.GetValue<string>() == "Failed") &&
                dossier["acquisitions"]!.AsArray().Any(a =>
                    a!["description"]!.GetValue<string>() == "hlx status 33333333-3333-3333-3333-333333333333 all --json"),
                "A supported monitor message became only a parent failure without acquiring its Helix child evidence.");
        });
        await CaseAsync("Successful Helix monitor logs supply executed controls outside diagnostic tails", async () =>
        {
            string replay = Path.Combine(outputDirectory, "successful-helix-discovery");
            Fixtures.Collection(replay, helix: true);
            foreach (string id in new[] { "3", "2" })
            {
                string job = $"{new string(id[0], 8)}-{new string(id[0], 4)}-{new string(id[0], 4)}-{new string(id[0], 4)}-{new string(id[0], 12)}";
                Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                    s["arguments"]!.AsArray().Count > 2 && s["arguments"]![1]!.GetValue<string>() == "timeline" &&
                    s["arguments"]![2]!.GetValue<string>() == Fixtures.BuildUrl(id), data =>
                {
                    JsonNode task = data["records"]![1]!;
                    task["name"] = "Monitor Helix Jobs";
                    task["log"] = new JsonObject { ["id"] = 1 };
                    task["issues"] = id == "3"
                        ? new JsonArray(new JsonObject { ["message"] = "MSB4036: monitor failed" }) : new JsonArray();
                });
                string full = $"HelixJob:{job}\n" + string.Join('\n', Enumerable.Repeat("Monitoring completed", 250));
                Fixtures.Save(replay, "hlx", ["azdo", "log", Fixtures.BuildUrl(id), "1", "--full", "--json"], full);
                Fixtures.Save(replay, "hlx", ["azdo", "log", Fixtures.BuildUrl(id), "1", "--tail-lines", "120", "--json"],
                    string.Join('\n', Enumerable.Repeat("Monitoring completed", 120)));
            }
            JsonObject dossier = await CollectAsync(replay);
            JsonNode row = (await CompareAsync(dossier))["comparisons"]!.AsArray()
                .Single(r => r!["kind"]!.GetValue<string>() == "work-item")!;
            Assert(row["mostRecentPassingExecution"]!["buildId"]!.GetValue<string>() == "2" &&
                dossier["acquisitions"]!.AsArray().Count(a =>
                    a!["description"]!.GetValue<string>().Contains("--full --json", StringComparison.Ordinal)) == 2,
                "Successful monitor logs or IDs preceding their diagnostic tails were not acquired.");
        });
        await CaseAsync("Helix controls require the same submission stream as well as queue and work item", async () =>
        {
            string replay = Path.Combine(outputDirectory, "helix-streams");
            Fixtures.Collection(replay, helix: true);
            Fixtures.Change(replay, s => s["tool"]!.GetValue<string>() == "hlx" &&
                s["arguments"]!.AsArray().Count > 2 && s["arguments"]![1]!.GetValue<string>() == "timeline" &&
                s["arguments"]![2]!.GetValue<string>() == Fixtures.BuildUrl("2"), data =>
                    data["records"]![0]!["name"] = "FullFramework");
            JsonObject dossier = await CollectAsync(replay, expectedError: "Evidence is incomplete");
            JsonNode row = (await CompareAsync(dossier))["comparisons"]!.AsArray()
                .Single(r => r!["kind"]!.GetValue<string>() == "work-item")!;
            Assert(row["mostRecentPassingExecution"] is null && row["precedingExecution"] is null &&
                !row["controls"]![0]!["eligible"]!.GetValue<bool>(),
                "A same-queue work item in a different framework stream became a passing control.");
        });
        await CaseAsync("Native child processes use noninteractive credentials and safe argument boundaries", async () =>
        {
            string replay = Path.Combine(outputDirectory, "native-replay");
            Fixtures.Collection(replay);
            Fixtures.TestRun(replay, "3");
            Fixtures.TestRun(replay, "2");
            Dictionary<string, string> environment = new() { ["CI_EVIDENCE_TEST_REPLAY"] = replay };
            JsonObject dossier = await CollectAsync(replay, environment: environment, executable: Environment.ProcessPath!);
            Assert(!dossier["replayed"]!.GetValue<bool>() && dossier["acquisitions"]!.AsArray().All(a => a!["complete"]!.GetValue<bool>()),
                "Native mock did not receive noninteractive environment or exact arguments.");
        });
        await CaseAsync("Native timeouts and missing executables preserve explicit partial errors", async () =>
        {
            string replay = Path.Combine(outputDirectory, "native-replay");
            Fixtures.Collection(replay);
            Dictionary<string, string> environment = new()
            {
                ["CI_EVIDENCE_TEST_REPLAY"] = replay, ["CI_EVIDENCE_TEST_DELAY"] = "1"
            };
            await CollectAsync(replay, ["--timeout-seconds", "1"], "exceeded 1 seconds", environment, Environment.ProcessPath!);
            await CollectAsync(replay, expectedError: "never-executed", environment: new());
        });
        await CaseAsync("Acquisition deadlines bound inherited stdout and stderr after the producer exits", async () =>
        {
            string replay = Path.Combine(outputDirectory, "inherited-pipe-replay");
            Fixtures.Collection(replay);
            foreach (string pipe in new[] { "stdout", "stderr" })
            {
                string pidPath = Path.Combine(replay, $"inherited-{pipe}.pid");
                Dictionary<string, string> environment = new()
                {
                    ["CI_EVIDENCE_TEST_REPLAY"] = replay, ["CI_EVIDENCE_TEST_HOLD_PIPE"] = pipe
                };
                string directory = NextDirectory();
                ProcessStartInfo start = new(_dotnet)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
                };
                foreach (string argument in new[]
                {
                    _assembly, "collect", "--build", "3", "--timeout-seconds", "1", "--output-directory", directory,
                    "--gh-path", Environment.ProcessPath!, "--hlx-path", Environment.ProcessPath!
                })
                {
                    start.ArgumentList.Add(argument);
                }
                foreach ((string key, string value) in environment)
                {
                    start.Environment[key] = value;
                }
                using Process collector = Process.Start(start) ?? throw new InvalidOperationException("Could not start the collector.");
                Task<string> capturedOutput = collector.StandardOutput.ReadToEndAsync();
                Task<string> capturedError = collector.StandardError.ReadToEndAsync();
                using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
                Stopwatch elapsed = Stopwatch.StartNew();
                try
                {
                    // Measure process exit before fixture cleanup releases the outer capture pipes.
                    await collector.WaitForExitAsync(deadline.Token);
                    Assert(elapsed.Elapsed < TimeSpan.FromSeconds(5) && File.Exists(pidPath),
                        $"The acquisition waited for inherited {pipe} EOF or never created the fixture child.");
                    JsonObject dossier = Fixtures.Read(Path.Combine(directory, "evidence.json"));
                    JsonNode acquisition = dossier["acquisitions"]![0]!;
                    Assert(collector.ExitCode != 0 && dossier["error"]!.GetValue<string>().Contains("hlx acquisition exceeded 1 seconds.", StringComparison.Ordinal) &&
                        acquisition["description"]!.GetValue<string>() == "hlx --version" &&
                        !acquisition["ok"]!.GetValue<bool>() && !acquisition["complete"]!.GetValue<bool>(),
                        "Output draining timed out without preserving the failed acquisition.");
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested)
                {
                    if (!collector.HasExited)
                    {
                        collector.Kill(entireProcessTree: true);
                    }
                    await collector.WaitForExitAsync();
                    throw new TimeoutException($"Collector waited for the fixture's inherited {pipe} pipe.");
                }
                finally
                {
                    if (File.Exists(pidPath))
                    {
                        int pid = int.Parse(File.ReadAllText(pidPath), CultureInfo.InvariantCulture);
                        Process? holder;
                        try
                        {
                            holder = Process.GetProcessById(pid);
                        }
                        catch (ArgumentException)
                        {
                            Console.WriteLine($"Fixture process {pid} already exited.");
                            holder = null;
                        }
                        if (holder is not null)
                        {
                            using (holder)
                            {
                                if (!holder.HasExited)
                                {
                                    Assert(string.Equals(holder.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase),
                                        $"Refusing to stop an unrelated process with fixture PID {pid}.");
                                    holder.Kill(entireProcessTree: true);
                                }
                                using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(5));
                                await holder.WaitForExitAsync(cleanup.Token);
                            }
                        }
                        await Task.WhenAll(capturedOutput, capturedError).WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
            }
        });

        Assert(_passed > 0, "The filter selected no deterministic checks.");
        foreach (string scenario in new[] { "pr-regression", "existing-flake", "rolling-regression", "scheduled-same-source", "mixed-insufficient" })
        {
            await CompareAsync(Fixtures.New(scenario), Path.Combine(outputDirectory, scenario));
        }
        Console.WriteLine($"{_passed} deterministic checks passed.");
    }
}

static class Fixtures
{
    public static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 100 };
    private const string Failure = "MSB4036: SDKExampleTask task was not found.";
    private static string Sha(string id) => id.PadLeft(40, '0');
    public static string BuildUrl(string id) => $"https://dev.azure.com/dnceng-public/public/_build/results?buildId={id}";
    private static JsonObject Node(object value) => JsonSerializer.SerializeToNode(value, Json)!.AsObject();
    public static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path), documentOptions: new() { MaxDepth = 100 })!.AsObject();
    public static void Write(JsonNode node, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, node.ToJsonString(Json) + "\n");
    }
    private static JsonObject Observation(string outcome = "Failed", string mechanism = "Expected 42; actual 43", string name = "Example.Test | Example.Test") =>
        Node(new { id = name, kind = "test", name, lane = "Linux x64 Debug", outcome, mechanism,
            url = $"fixture://results/{name}", contextOnly = false, hierarchyVerified = true });
    private static JsonObject Build(string id, JsonObject observation) => Node(new
    {
        id, url = $"fixture://builds/{id}", repository = "dotnet/sdk", organization = "dnceng-public", project = "public",
        definitionId = 101, branch = "refs/heads/main", commit = Sha(id), prHeadSha = (string?)null, targetSha = (string?)null,
        queuedAt = $"2026-10-{id.PadLeft(2, '0')}T00:00:00Z", reason = "batchedCI", status = "completed", result = "failed",
        complete = true, parameters = new { configuration = "Debug" }, templateParameters = new { },
        resources = new { toolchain = "fixed-1" }, observations = new[] { observation }
    });
    public static object[] Commits() => [new { sha = "change-a" }, new { sha = "change-b" }];

    public static JsonObject New(string scenario = "pr-regression")
    {
        JsonObject current = Build("3", Observation());
        JsonObject previous = Build("2", Observation("Passed", ""));
        current["branch"] = "refs/pull/12/merge";
        current["prHeadSha"] = new string('a', 40);
        current["targetSha"] = Sha("2");
        JsonObject dossier = Node(new
        {
            schemaVersion = 1, mode = "pr", repository = "dotnet/sdk", pr = new { number = 12 }, selectedBuildIds = new[] { "3" },
            builds = new[] { current, previous }, acquisitions = new JsonArray(), checks = new JsonArray(), changeWindows = new JsonArray(),
            buildAnalysis = new JsonArray(), issueSearches = new JsonArray(), fixture = scenario, sourceEvidence = new JsonArray()
        });
        current = dossier["builds"]![0]!.AsObject();
        previous = dossier["builds"]![1]!.AsObject();
        void Evidence(string url, string observed) => dossier["sourceEvidence"]!.AsArray().Add((JsonNode)Node(new { url, observed }));
        switch (scenario)
        {
            case "pr-regression":
                current["observations"]![0]!["mechanism"] = "NETSDK1013: TargetFramework value is empty.";
                Evidence("fixture://source/target-framework.diff", "The PR deletes the only TargetFramework assignment in the exercised asset. The tested failing project evaluates TargetFramework to empty; the baseline evaluates it to net11.0.");
                Evidence("fixture://controls/platform.json", "The same test asset, Linux x64 Debug job, toolchain and dependencies executed in both runs. The diagnostic follows the changed assignment.");
                break;
            case "existing-flake":
                previous["observations"] = new JsonArray(Observation());
                dossier["builds"]!.AsArray().Add((JsonNode)Build("1", Observation("Passed", "")));
                Evidence("fixture://source/docs-only.diff", "The PR changes documentation only. No executable, test, CI, dependency or generated build inputs changed.");
                Evidence("fixture://history/verified-controls.json", "The same assertion and race occurred independently on the target branch before the PR. Both older runs executed the identical scenario and fixed inputs; one passed and one failed. The unchanged source contains an independently diagnosed environment-variable race.");
                break;
            case "rolling-regression":
            case "scheduled-same-source":
                dossier["mode"] = "build";
                dossier["pr"] = null;
                current["branch"] = "refs/heads/main";
                current["prHeadSha"] = null;
                bool scheduled = scenario == "scheduled-same-source";
                if (scheduled)
                {
                    current["reason"] = "schedule";
                    current["commit"] = Sha("2");
                    current["resources"] = Node(new { toolchain = "new-2" });
                    current["observations"]![0]!["mechanism"] = "Watchdog terminated a hung test host.";
                    Evidence("fixture://runtime/metadata.json", "The source SHA is unchanged; a resolved resource changed. No stack trace, dump or source-level hang mechanism is available. The watchdog event is verified but does not establish why the host hung.");
                }
                else
                {
                    current["observations"]![0]!["mechanism"] = Failure;
                    Evidence("fixture://commits/change-a.diff", "First commit in the batch deletes the only UsingTask registration for SDKExampleTask. The failing target still invokes it; the diagnostic identifies exactly that unregistered task.");
                    Evidence("fixture://commits/change-b.diff", "The final commit changes documentation only. The batch contains both commits and both builds executed the same target and fixed toolchain.");
                }
                dossier["changeWindows"]!.AsArray().Add((JsonNode)Node(new
                {
                    buildId = "3", baselineBuildId = "2", baseSha = Sha("2"), headSha = current["commit"],
                    ancestry = scheduled ? "identical" : "ahead", complete = true, commits = scheduled ? [] : Commits()
                }));
                break;
            case "mixed-insufficient":
                previous["observations"] = new JsonArray(Observation());
                current["observations"]!.AsArray().Add((JsonNode)Observation(mechanism: Failure, name: "Compile.Asset | Compile.Asset"));
                current["observations"]!.AsArray().Add((JsonNode)Observation(mechanism: "", name: "Host.Hang | Host.Hang"));
                current["complete"] = false;
                dossier["checks"] = new JsonArray(Node(new { name = "Unsupported check", coverage = "unassessed" }));
                dossier["acquisitions"] = new JsonArray(Node(new { description = "Hang log unavailable", ok = false, complete = false }));
                Evidence("fixture://history/race.json", "The first test matches an independently verified pre-existing race under the same inputs.");
                Evidence("fixture://source/task.diff", "The PR removes the only SDKExampleTask registration and the new compile assertion identifies this exact task. The hang has no diagnostic evidence. The known-issue check was manually overridden green but still reports the compile failure unmatched.");
                break;
            default:
                throw new ArgumentException($"Unknown fixture {scenario}.");
        }
        return dossier;
    }

    private static string Key(string tool, string[] arguments) => Convert.ToHexStringLower(SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { tool }.Concat(arguments), Json))));
    public static void Save(string directory, string tool, string[] arguments, object data, int exitCode = 0,
        bool text = false, string? console = null, string? consoleUrl = null)
    {
        string key = Key(tool, arguments);
        Write(Node(new { tool, arguments, exitCode, stdout = text ? (string)data : JsonSerializer.Serialize(data, Json), stderr = "" }),
            Path.Combine(directory, key + ".json"));
        if (console is not null)
        {
            Write(Node(new { schemaVersion = 1, sourceUrl = consoleUrl, text = console }), Path.Combine(directory, key + ".console.json"));
        }
    }
    public static object Envelope() => new { ok = true, results = new JsonArray(), complete = true, truncated = false };
    public const string DiagnosticOutcomes =
        "Unspecified,None,Failed,Inconclusive,Timeout,Aborted,Blocked,NotExecuted,Warning,Error,NotApplicable,Paused,InProgress,NotImpacted";
    public static string[] ResultArguments(string id, string outcomes = "", int skip = 0) =>
        ["rest", "--method", "GET", "--url",
            $"https://dev.azure.com/dnceng-public/public/_apis/test/Runs/{id}/results?api-version=7.1&detailsToInclude=SubResults{(outcomes.Length == 0 ? "" : "&outcomes=" + outcomes)}&$skip={skip}&$top=200",
            "--resource", "499b84ac-1321-427f-aa17-267ca6975798", "--output", "json", "--only-show-errors"];
    public static bool IsRawResults(JsonObject snapshot, string id) => snapshot["tool"]!.GetValue<string>() == "az" &&
        snapshot["arguments"]![4]!.GetValue<string>().Contains($"/test/Runs/{id}/results?", StringComparison.Ordinal);
    public static bool IsTestAcquisition(JsonObject snapshot, string id) => IsRawResults(snapshot, id) ||
        snapshot["tool"]!.GetValue<string>() == "az" &&
        snapshot["arguments"]![4]!.GetValue<string>().Contains($"/test/Runs/{id}?", StringComparison.Ordinal) ||
        snapshot["tool"]!.GetValue<string>() == "hlx" && snapshot["arguments"]!.AsArray().Count > 2 &&
        snapshot["arguments"]![1]!.GetValue<string>() == "test-runs" &&
        snapshot["arguments"]![2]!.GetValue<string>() == BuildUrl(id);
    public static JsonArray ResultRows(JsonNode data) => (data["results"] ?? data["value"])!.AsArray();
    public static JsonObject FirstResult(JsonNode data) =>
        (data["results"] ?? data["value"]) is JsonArray rows ? rows[0]!.AsObject() : data.AsObject();
    public static string[] RunArguments(string id) =>
        ["rest", "--method", "GET", "--url", $"https://dev.azure.com/dnceng-public/public/_apis/test/Runs/{id}?api-version=7.1",
            "--resource", "499b84ac-1321-427f-aa17-267ca6975798", "--output", "json", "--only-show-errors"];
    public static string[] BuildInputArguments(string id, bool pipeline = false) =>
        ["rest", "--method", "GET", "--url",
            $"https://dev.azure.com/dnceng-public/public/_apis/{(pipeline ? $"pipelines/101/runs/{id}" : $"build/builds/{id}")}?api-version=7.1",
            "--resource", "499b84ac-1321-427f-aa17-267ca6975798", "--output", "json", "--only-show-errors"];
    public static string[] CompareArguments() =>
        ["api", "--method", "GET", "--paginate", "--slurp", $"repos/dotnet/sdk/compare/{Sha("2")}...{Sha("3")}?per_page=100"];

    public static void TestRun(string directory, string id, string? state = "Completed", int? incompleteTests = 0)
    {
        object run = new { id = int.Parse(id, CultureInfo.InvariantCulture), name = "Linux", state,
            totalTests = 1 + incompleteTests.GetValueOrDefault(), incompleteTests,
            failedTests = id == "3" ? 1 : 0, passedTests = id == "2" ? 1 : 0 };
        Save(directory, "hlx", ["azdo", "test-runs", BuildUrl(id), "--all", "--json"], new
        {
            ok = true, complete = true, truncated = false, results = new[] { run }
        });
        Save(directory, "az", RunArguments(id), run);
        Save(directory, "az", ResultArguments(id), new
        {
            count = 1, value = new[]
            {
                new { id = 1, automatedTestName = "Example.Test", testCaseTitle = "Example.Test",
                    outcome = id == "3" ? "Failed" : "Passed", errorMessage = id == "3" ? Failure : "", stackTrace = "" }
            }
        });
    }

    public static void Collection(string directory, bool pr = false, bool helix = false)
    {
        string baseSha = Sha("2");
        string head = Sha("3");
        string prHead = new('a', 40);
        DateTimeOffset queued = DateTimeOffset.Parse("2026-10-03T00:00:00Z", CultureInfo.InvariantCulture);
        string branch = pr ? "refs/pull/12/merge" : "refs/heads/main";
        Save(directory, "hlx", ["--version"], "0.11.0", text: true);
        foreach (string id in new[] { "3", "2" })
        {
            string url = BuildUrl(id);
            DateTimeOffset date = id == "3" ? queued : queued.AddDays(-1);
            Save(directory, "hlx", ["azdo", "build", url, "--json"], new
            {
                Id = int.Parse(id, CultureInfo.InvariantCulture), SourceVersion = id == "3" ? head : baseSha, DefinitionId = 101,
                DefinitionName = "fixture-pipeline", QueueTime = date.ToString("o"), Status = "completed",
                Result = id == "3" ? "failed" : "succeeded", SourceBranch = id == "3" ? branch : "refs/heads/main"
            });
            Save(directory, "az", BuildInputArguments(id), new
            {
                id = int.Parse(id, CultureInfo.InvariantCulture), sourceVersion = id == "3" ? head : baseSha,
                repository = new { id = "dotnet/sdk" }, parameters = (string?)null
            });
            Save(directory, "az", BuildInputArguments(id, pipeline: true), new
            {
                id = int.Parse(id, CultureInfo.InvariantCulture), pipeline = new { id = 101 },
                templateParameters = new { }, resources = new { toolchain = "fixed-1" }
            });
            List<object> issues = id == "3" ? [new { message = Failure }] : [];
            if (helix)
            {
                string job = $"{new string(id[0], 8)}-{new string(id[0], 4)}-{new string(id[0], 4)}-{new string(id[0], 4)}-{new string(id[0], 12)}";
                issues.Add(new { message = $"HelixJob:{job}" });
                object item = new { Name = "Example.WorkItem", ExitCode = id == "3" ? 1 : 0, State = "Finished", ConsoleLogUrl = $"fixture://helix/{job}/console.log" };
                Save(directory, "hlx", ["status", job, "all", "--json"], new
                {
                    job = new { jobId = job, QueueId = "Linux x64 Debug" }, inProgressCount = 0,
                    failed = id == "3" ? new[] { item } : [], passed = id == "2" ? new[] { item } : []
                });
                if (id == "3")
                {
                    Save(directory, "hlx", ["files", job, "Example.WorkItem", "--json"], new
                    {
                        binlogs = new[] { new { Name = "build.binlog", Uri = "fixture://artifacts/build.binlog" } },
                        testResults = new JsonArray(), other = new JsonArray()
                    });
                    Save(directory, "hlx", ["logs", job, "Example.WorkItem"], "Replay must not read this local path.",
                        text: true, console: Failure, consoleUrl: $"fixture://helix/{job}/console.log");
                }
            }
            Save(directory, "hlx", ["azdo", "timeline", url, "--filter", "all", "--json"], new
            {
                records = new object[]
                {
                    new { id = "job", type = "Job", name = "Linux", parentId = (string?)null, result = id == "3" ? "failed" : "succeeded", issues = (object?)null, log = (object?)null, attempt = 1 },
                    new { id = "task", type = "Task", name = "Compile", parentId = "job", result = id == "3" ? "failed" : "succeeded", issues, log = (object?)null, attempt = 1 }
                }
            });
            foreach (string command in new[] { "test-runs", "artifacts" })
            {
                Save(directory, "hlx", ["azdo", command, url, "--all", "--json"], Envelope());
            }
        }
        object rawSelected = new
        {
            id = 3, repository = new { id = "dotnet/sdk" }, sourceBranch = branch, sourceVersion = head,
            status = "completed", reason = pr ? "pullRequest" : "batchedCI",
            triggerInfo = new Dictionary<string, string?> { ["pr.sourceSha"] = pr ? prHead : null, ["pr.number"] = pr ? "12" : null }
        };
        object rawControl = new
        {
            id = 2, repository = new { id = "dotnet/sdk" }, sourceBranch = "refs/heads/main", sourceVersion = baseSha,
            status = "completed", reason = "batchedCI", triggerInfo = new JsonObject()
        };
        Save(directory, "hlx", ["azdo", "builds", "--org", "dnceng-public", "--project", "public", "--definition-id", "101",
            "--top", "20", "--min-time", queued.AddSeconds(-1).ToString("o"), "--max-time", queued.AddSeconds(1).ToString("o"),
            "--query-order", "queueTimeDescending", "--json"], new[] { rawSelected });
        foreach (string historyBranch in new[] { branch, "refs/heads/main" }.Distinct(StringComparer.Ordinal))
        {
            Save(directory, "hlx", ["azdo", "builds", "--org", "dnceng-public", "--project", "public", "--definition-id", "101",
                "--branch", historyBranch, "--min-time", queued.AddDays(-2).ToString("o"), "--max-time", queued.AddTicks(-1).ToString("o"),
                "--top", "1", "--query-order", "queueTimeDescending", "--json"], historyBranch == "refs/heads/main" ? new[] { rawControl } : []);
        }
        if (pr)
        {
            Save(directory, "gh", ["api", "--method", "GET", "repos/dotnet/sdk/pulls/12"],
                new { number = 12, head = new { sha = prHead }, @base = new { @ref = "main" } });
            foreach (string endpoint in new[] { "pulls/12/files", "issues/12/comments", "pulls/12/reviews", "pulls/12/comments" })
            {
                Save(directory, "gh", ["api", "--method", "GET", "--paginate", "--slurp", $"repos/dotnet/sdk/{endpoint}?per_page=100"], new[] { new JsonArray() });
            }
            Save(directory, "gh", ["api", "--method", "GET", $"repos/dotnet/sdk/git/commits/{head}"],
                new { parents = new[] { new { sha = baseSha }, new { sha = prHead } } });
            Save(directory, "gh", ["api", "--method", "GET", "--paginate", "--slurp", $"repos/dotnet/sdk/commits/{prHead}/status?per_page=100"],
                new[] { new { statuses = new JsonArray() } });
        }
        object[] checks = pr ? [new { name = "azure-pipelines", details_url = BuildUrl("3"), status = "completed", conclusion = "failure" }] : [];
        Save(directory, "gh", ["api", "--method", "GET", "--paginate", "--slurp", $"repos/dotnet/sdk/commits/{(pr ? prHead : head)}/check-runs?per_page=100"],
            new[] { new { check_runs = checks } });
        Save(directory, "gh", CompareArguments(), new[] { new { status = "ahead", total_commits = 2, commits = Commits(), files = new JsonArray() } });
        Save(directory, "gh", ["api", "--method", "GET", "-H", "Accept: application/vnd.github.diff", $"repos/dotnet/sdk/compare/{baseSha}...{head}"],
            "synthetic fixture diff", text: true);
        Save(directory, "hlx", ["azdo", "changes", BuildUrl("3"), "--all", "--json"], Envelope());
        foreach (string term in helix ? new[] { "MSB4036", "Example.WorkItem" } : new[] { "MSB4036" })
        {
            IssueSearch(directory, term);
        }
    }

    public static void IssueSearch(string directory, string term)
    {
        foreach (string state in new[] { "open", "closed" })
        {
            string query = Uri.EscapeDataString($"{term} repo:dotnet/sdk is:issue is:{state}");
            Save(directory, "gh", ["api", "--method", "GET", $"search/issues?q={query}&per_page=10"],
                new { total_count = 0, incomplete_results = false, items = new JsonArray() });
        }
    }

    public static void Change(string directory, Func<JsonObject, bool> predicate, Action<JsonNode> mutate)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
        {
            JsonObject snapshot = Read(file);
            if (snapshot["tool"] is not null && predicate(snapshot))
            {
                JsonNode data = JsonNode.Parse(snapshot["stdout"]!.GetValue<string>())!;
                mutate(data);
                if (snapshot["tool"]!.GetValue<string>() == "az" && data["value"] is JsonArray values)
                {
                    data["count"] = values.Count;
                }
                snapshot["stdout"] = data.ToJsonString(Json);
                Write(snapshot, file);
            }
        }
    }

    public static void AddCheck(string directory, string name, string url)
    {
        Change(directory, s => s["tool"]!.GetValue<string>() == "gh" &&
            s["arguments"]!.AsArray().Any(a => a!.GetValue<string>().Contains("/check-runs?", StringComparison.Ordinal)),
            data => data[0]!["check_runs"]!.AsArray().Add(Node(new
            {
                name, details_url = url, status = "completed", conclusion = "failure"
            })));
    }

    public static async Task<int> FakeToolAsync(string replay, string[] arguments)
    {
        if (arguments is ["--hold-output"])
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            return 0;
        }
        if (Environment.GetEnvironmentVariable("CI_EVIDENCE_TEST_DELAY") is not null)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
        if (Environment.GetEnvironmentVariable("GIT_TERMINAL_PROMPT") != "0" ||
            Environment.GetEnvironmentVariable("GCM_INTERACTIVE") != "never")
        {
            Console.Error.WriteLine("Interactive credential prompts were not disabled.");
            return 1;
        }
        if (arguments is ["--version"] && Environment.GetEnvironmentVariable("CI_EVIDENCE_TEST_HOLD_PIPE") is string pipe)
        {
            using Process holder = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false, RedirectStandardOutput = pipe == "stderr", RedirectStandardError = pipe == "stdout",
                ArgumentList = { "--hold-output" }
            }) ?? throw new InvalidOperationException("Could not start the inherited-pipe fixture child.");
            File.WriteAllText(Path.Combine(replay, $"inherited-{pipe}.pid"), holder.Id.ToString(CultureInfo.InvariantCulture));
        }
        string tool = arguments[0] switch { "api" => "gh", "rest" => "az", _ => "hlx" };
        string path = Path.Combine(replay, Key(tool, arguments) + ".json");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Unexpected native acquisition: {tool} {string.Join(' ', arguments)}");
            return 1;
        }
        JsonObject snapshot = Read(path);
        Console.Write(snapshot["stdout"]!.GetValue<string>());
        Console.Error.Write(snapshot["stderr"]!.GetValue<string>());
        return snapshot["exitCode"]!.GetValue<int>();
    }
}
