import assert from "node:assert/strict";
import test from "node:test";

import {
  createPipelineObservation,
  createTaskObservations,
  classifyTaskFailure,
  classifyWorkItem,
  createFailureSignature,
  createHeartbeatObservation,
  normalizeBuild,
  parseArguments,
  parseHelixWorkItemReferences,
  parseTestResultXml,
  sanitizeText,
  selectUnprocessedFailures
} from "./collect-ci-evidence.mjs";

test("parseArguments requires registry and output", () => {
  assert.deepEqual(
    parseArguments(["--registry", "pipelines.json", "--output", "dossier.json"]),
    { registry: "pipelines.json", output: "dossier.json" });
  assert.throws(() => parseArguments(["--registry", "pipelines.json"]), /required/);
  assert.throws(() => parseArguments(["registry", "pipelines.json"]), /Invalid argument/);
});

test("sanitizeText removes volatile values and bounds output", () => {
  const input = "2026-07-24T17:25:31.817Z job 123e4567-e89b-12d3-a456-426614174000 ";
  const sanitized = sanitizeText(input + "x".repeat(5_000));

  assert.match(sanitized, /^<timestamp> job <guid>/);
  assert.equal(sanitized.length, 4_000);
});

test("normalizeBuild retains only evidence fields", () => {
  const normalized = normalizeBuild({
    id: 42,
    buildNumber: "20260724.1",
    result: "failed",
    reason: "batchedCI",
    sourceBranch: "refs/heads/main",
    sourceVersion: "abc",
    definition: { id: 101, name: "dotnet-sdk-public-ci" },
    repository: { id: "dotnet/sdk" },
    _links: { web: { href: "https://example.test/build/42" } },
    untrustedExtraField: "excluded"
  });

  assert.equal(normalized.id, 42);
  assert.equal(normalized.url, "https://example.test/build/42");
  assert.equal("untrustedExtraField" in normalized, false);
});

test("bootstrap selects at most one historical failure", () => {
  const history = [
    { id: 4, result: "failed" },
    { id: 3, result: "succeeded" },
    { id: 2, result: "failed" }
  ];

  const selected = selectUnprocessedFailures({ pipelines: {} }, "pipeline:main", history);

  assert.equal(selected.bootstrap, true);
  assert.deepEqual(selected.failures.map(build => build.id), [4]);
});

test("subsequent polls select only unseen failures", () => {
  const history = [
    { id: 4, result: "failed" },
    { id: 3, result: "succeeded" },
    { id: 2, result: "failed" }
  ];
  const state = { pipelines: { "pipeline:main": { processedBuildIds: [3, 2] } } };

  const selected = selectUnprocessedFailures(state, "pipeline:main", history);

  assert.equal(selected.bootstrap, false);
  assert.deepEqual(selected.failures.map(build => build.id), [4]);
});

test("parseHelixWorkItemReferences extracts SDK timeline warnings", () => {
  const references = parseHelixWorkItemReferences([
    "Work item 'dotnet.Tests.dll.16' in job 'Windows x64 - windows.amd64.open (851425f3-3af7-4195-adf3-3851bbbcf57f)' failed (Finished, exit code 2).",
    "Bash exited with code '1'."
  ]);

  assert.deepEqual(references, [{
    workItem: "dotnet.Tests.dll.16",
    queue: "Windows x64 - windows.amd64.open",
    jobId: "851425f3-3af7-4195-adf3-3851bbbcf57f",
    exitCode: 2
  }]);
});

test("classifyWorkItem distinguishes tests, timeout, crash, and infrastructure", () => {
  assert.equal(classifyWorkItem(2, "", [{ test: "Example" }]), "test-failure");
  assert.equal(classifyWorkItem(143, "WORKLOAD TIMED OUT"), "timeout");
  assert.equal(classifyWorkItem(139, "Segmentation fault (core dumped)"), "crash");
  assert.equal(classifyWorkItem(81, "DEVICE_NOT_FOUND"), "infrastructure");
  assert.equal(classifyWorkItem(80, "Test run completed\nAPP_CRASH"), "post-test-harness-failure");
  assert.equal(classifyWorkItem(2, "Copy all crash dumps to upload directory"), "work-item-failure");
});

test("classifyTaskFailure identifies roots and artifact cascades", () => {
  assert.equal(classifyTaskFailure("Download Previous Build", ["Artifact not found"]), "cascade");
  assert.equal(classifyTaskFailure("Initialize containers", []), "setup");
  assert.equal(classifyTaskFailure("Build", ["error NETSDK1005"]), "build");
  assert.equal(classifyTaskFailure("Validate pipeline", ["Unexpected value 'jobs'"]), "pipeline-configuration");
});

test("createFailureSignature removes volatile numeric values", () => {
  assert.equal(
    createFailureSignature("test", "ItCanUpdatePackages", "HTTP 503 on port 443"),
    "test|itcanupdatepackages|http-<n>-on-port-<n>");
});

test("parseTestResultXml returns independent named failures", () => {
  const results = parseTestResultXml(`<?xml version="1.0" encoding="UTF-8"?>
    <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
      <TestDefinitions><UnitTest id="test-1" name="ShortName"><TestMethod className="Sdk.Tests.Example" name="Fails" /></UnitTest></TestDefinitions>
      <Results><UnitTestResult testId="test-1" testName="ShortName" outcome="Failed" duration="00:00:01">
        <Output><ErrorInfo><Message>Expected zero but found one.</Message><StackTrace>at Sdk.Tests.Example.Fails()</StackTrace></ErrorInfo></Output>
      </UnitTestResult></Results>
    </TestRun>`);

  assert.deepEqual(results, [{
    testName: "ShortName",
    fullyQualifiedName: "Sdk.Tests.Example.Fails",
    outcome: "Failed",
    duration: "00:00:01",
    errorMessage: "Expected zero but found one.",
    stackTrace: "at Sdk.Tests.Example.Fails()"
  }]);
});

test("createTaskObservations preserves roots and suppresses cascades", () => {
  const observations = createTaskObservations([
    { type: "Task", name: "Build", issues: ["error NETSDK1005"], path: ["Build", "Windows", "Build"], logId: 1 },
    { type: "Task", name: "Download Previous Build", issues: ["Artifact not found"], path: ["Validate", "Download Previous Build"], logId: 2 }
  ]);

  assert.equal(observations[0].category, "build");
  assert.equal(observations[0].actionable, true);
  assert.deepEqual(observations[0].path, ["Build", "Windows", "Build"]);
  assert.equal(observations[1].category, "cascade");
  assert.equal(observations[1].actionable, false);
});

test("createPipelineObservation represents YAML rejection and empty execution", () => {
  const rejected = createPipelineObservation({
    definition: { name: "sdk-ci" },
    validationResults: [{ result: "error", message: "Unexpected value 'jobs'" }]
  }, []);
  const empty = createPipelineObservation({ definition: { name: "sdk-ci" } }, []);

  assert.equal(rejected.category, "pipeline-configuration");
  assert.equal(rejected.actionable, true);
  assert.equal(empty.category, "pipeline-startup");
  assert.equal(empty.actionable, false);
  assert.equal(createPipelineObservation({}, [{ id: "stage" }]), null);
});

test("createHeartbeatObservation tolerates batching and detects an unbuilt branch head", () => {
  const pipeline = { repository: "dotnet/sdk", definitionId: 101 };
  const branch = "refs/heads/main";
  const head = { sha: "head", committedAt: "2026-07-24T12:00:00Z", url: "https://example.test/head" };
  const now = Date.parse("2026-07-24T14:00:00Z");

  assert.equal(createHeartbeatObservation(pipeline, branch, head, [
    { sourceVersion: "batched-head", queueTime: "2026-07-24T12:05:00Z" }
  ], now), null);

  const missed = createHeartbeatObservation(pipeline, branch, head, [
    { sourceVersion: "old", queueTime: "2026-07-24T11:59:00Z", definition: { id: 101 } }
  ], now);
  assert.equal(missed.category, "pipeline-not-triggered");
  assert.equal(missed.actionable, false);
});