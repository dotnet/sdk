import assert from "node:assert/strict";
import test from "node:test";
import { BuildCandidateSelector } from "../build-candidate-selector.mjs";

import {
  applyKbeRecurrence,
  createBuildAttemptKey,
  createPipelineObservation,
  createTaskObservations,
  classifyTaskFailure,
  classifyWorkItem,
  createFailureFingerprint,
  createHeartbeatObservation,
  collectCiEvidence,
  CiEvidenceCollector,
  getTimelineFailuresFromRecords,
  getRecentlyTrackedFingerprints,
  getArtifactEvidenceSources,
  createBuildSummary,
  parseArguments,
  parseHelixWorkItemReferences,
  parseTestResultXml,
  normalizeEvidenceText,
  summarizeHelixConsole,
  summarizeSharedTestMechanism,
  shouldRunAgent,
  suppressTrackedIssueCandidates,
  selectUnprocessedFailures
} from "../collect-ci-evidence.mjs";

test("CiEvidenceCollector owns one Azure client per registered pipeline", () => {
  const pipeline = {
    organization: "dnceng-public", project: "public", definitionId: 101,
    repository: "dotnet/sdk", branches: ["refs/heads/main"]
  };
  const collector = new CiEvidenceCollector(
    { pipelines: [pipeline] },
    { schemaVersion: 1, pipelines: {} },
    async () => { throw new Error("not called"); });

  assert.equal(collector.getAzureClient(pipeline), collector.getAzureClient(pipeline));
});

test("manual build lookup continues after a pipeline returns 404", async () => {
  const pipelines = [
    { definitionId: 1, repository: "dotnet/sdk" },
    { definitionId: 2, repository: "dotnet/sdk" }
  ];
  const build = { definition: { id: 2 }, repository: { id: "dotnet/sdk" } };
  const selector = new BuildCandidateSelector(
    { pipelines },
    { schemaVersion: 1, pipelines: {} },
    pipeline => ({
      getBuild: async () => {
        if (pipeline === pipelines[0]) throw Object.assign(new Error("not found"), { status: 404 });
        return build;
      }
    }),
    null);

  assert.deepEqual(await selector.selectManualBuild("42"), { pipeline: pipelines[1], build });
});

test("parseArguments requires registry and output", () => {
  assert.deepEqual(
    parseArguments(["--registry", "pipelines.json", "--output", "dossier.json"]),
    { registry: "pipelines.json", output: "dossier.json" });
  assert.throws(() => parseArguments(["--registry", "pipelines.json"]), /required/);
  assert.throws(() => parseArguments(["registry", "pipelines.json"]), /Invalid argument/);
});

test("normalizeEvidenceText removes volatile values and bounds output", () => {
  const input = "2026-07-24T17:25:31.817Z job 123e4567-e89b-12d3-a456-426614174000 ";
  const normalizedEvidence = normalizeEvidenceText(input + "x".repeat(5_000));

  assert.match(normalizedEvidence, /^<timestamp> job <guid>/);
  assert.equal(normalizedEvidence.length, 4_000);
});

test("createBuildSummary retains only evidence fields", () => {
  const summary = createBuildSummary({
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

  assert.equal(summary.id, 42);
  assert.equal(summary.url, "https://example.test/build/42");
  assert.equal("untrustedExtraField" in summary, false);
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
    { id: 4, result: "failed", finishTime: "2026-07-24T14:00:00Z" },
    { id: 3, result: "succeeded", finishTime: "2026-07-24T13:00:00Z" },
    { id: 2, result: "failed", finishTime: "2026-07-24T12:00:00Z" }
  ];
  const state = { pipelines: { "pipeline:main": { processedBuildKeys: history.slice(1).map(createBuildAttemptKey) } } };

  const selected = selectUnprocessedFailures(state, "pipeline:main", history);

  assert.equal(selected.bootstrap, false);
  assert.deepEqual(selected.failures.map(build => build.id), [4]);
});

test("bootstrapping a new branch does not suppress failures on initialized branches", async () => {
  const pipeline = {
    organization: "dnceng-public", project: "public", definitionId: 101, repository: "dotnet/sdk",
    branches: ["refs/heads/main", "refs/heads/release/new"],
    stableBranches: ["refs/heads/main", "refs/heads/release/new"]
  };
  const mainFailure = {
    id: 2, result: "failed", sourceBranch: "refs/heads/main", finishTime: "2026-08-14T12:00:00Z",
    definition: {id: 101}, repository: {id: "dotnet/sdk"}
  };
  const releaseFailure = {
    id: 3, result: "failed", sourceBranch: "refs/heads/release/new", finishTime: "2026-08-14T12:00:00Z",
    definition: {id: 101}, repository: {id: "dotnet/sdk"}
  };
  const mainKey = "dnceng-public/public/101:refs/heads/main";
  const state = {schemaVersion: 1, pipelines: {[mainKey]: {processedBuildKeys: []}}};
  const azure = {
    listCompletedBuilds: async branch => branch === "refs/heads/main" ? [mainFailure] : [releaseFailure]
  };
  const selector = new BuildCandidateSelector(
    {pipelines: [pipeline]}, state, () => azure, {checkPipeline: async () => null});

  const selected = await selector.selectScheduledCandidates();

  assert.equal(selected.bootstrap, false);
  assert.deepEqual(selected.candidates.map(candidate => candidate.build.id), [mainFailure.id]);
});

test("same build ID is reconsidered when its completed attempt changes", () => {
  const original = { id: 4, result: "failed", finishTime: "2026-07-24T14:00:00Z" };
  const retried = { id: 4, result: "failed", finishTime: "2026-07-24T15:00:00Z" };
  const state = { pipelines: { "pipeline:main": { processedBuildKeys: [createBuildAttemptKey(original)] } } };

  const selected = selectUnprocessedFailures(state, "pipeline:main", [retried]);

  assert.deepEqual(selected.failures, [retried]);
});

test("open PR check-suite builds do not run in the HIGH-only milestone", async () => {
  const build = {
    id: 42,
    result: "failed",
    reason: "pullRequest",
    sourceBranch: "refs/pull/123/merge",
    sourceVersion: "abc",
    finishTime: "2026-07-24T14:00:00Z",
    definition: { id: 101, name: "dotnet-sdk-public-ci" },
    repository: { id: "dotnet/sdk" },
    validationResults: []
  };
  const responses = new Map([
    ["/build/builds/42?", build],
    ["/build/builds?", { value: [build] }],
    ["/build/builds/42/timeline?", { records: [] }],
    ["/runs?", { value: [] }]
  ]);
  const fetchImplementation = async url => {
    const match = [...responses].find(([fragment]) => url.includes(fragment));
    assert.ok(match, `Unexpected URL: ${url}`);
    return { ok: true, status: 200, json: async () => match[1] };
  };
  const registry = { pipelines: [{ organization: "dnceng-public", project: "public", definitionId: 101, repository: "dotnet/sdk", branches: ["refs/heads/main"] }] };
  const state = { schemaVersion: 1, pipelines: {} };

  const first = await collectCiEvidence(registry, null, state, fetchImplementation, "42");
  const second = await collectCiEvidence(registry, null, state, fetchImplementation, "42");

  assert.equal(first.failures.length, 0);
  assert.equal(second.failures.length, 0);
});

test("manual build IDs skip incomplete attempts without recording them", async () => {
  const build = {
    id: 44, status: "inProgress", result: null, reason: "pullRequest", sourceBranch: "refs/pull/123/merge",
    sourceVersion: "abc", finishTime: null,
    definition: { id: 101, name: "dotnet-sdk-public-ci" }, repository: { id: "dotnet/sdk" }
  };
  const fetchImplementation = async url => {
    assert.match(url, /\/build\/builds\/44\?/);
    return { ok: true, status: 200, json: async () => build };
  };
  const state = { schemaVersion: 1, pipelines: {} };
  const registry = { pipelines: [{
    organization: "dnceng-public", project: "public", definitionId: 101,
    repository: "dotnet/sdk", branches: ["refs/heads/main"]
  }] };

  const dossier = await collectCiEvidence(registry, "44", state, fetchImplementation);

  assert.equal(dossier.failures.length, 0);
  assert.equal(shouldRunAgent(dossier), false);
  assert.deepEqual(state.pipelines, {});
});

test("direct stable-branch check-suite builds audit once at HIGH", async () => {
  const build = {
    id: 43,
    result: "failed",
    reason: "batchedCI",
    sourceBranch: "refs/heads/main",
    definition: { id: 101 },
    repository: { id: "dotnet/sdk" }
  };
  const fetchImplementation = async () => ({ ok: true, json: async () => build });
  const registry = { pipelines: [{
    organization: "dnceng-public", project: "public", definitionId: 101, repository: "dotnet/sdk",
    branches: ["refs/heads/main"], stableBranches: ["refs/heads/main"]
  }] };
  const state = { schemaVersion: 1, pipelines: {} };

  const first = await collectCiEvidence(registry, null, state, fetchImplementation, "43");
  const second = await collectCiEvidence(registry, null, state, fetchImplementation, "43");

  assert.equal(first.failures.length, 1);
  assert.equal(first.failures[0].priority, "HIGH");
  assert.equal(first.failures[0].auditContext, "stable-direct:refs/heads/main");
  assert.equal(second.failures.length, 0);
});

test("merged stable-target PR failures promote the same Azure attempt once", async () => {
  const requestedUrls = [];
  const build = {
    id: 44,
    result: "failed",
    reason: "pullRequest",
    sourceBranch: "refs/pull/124/merge",
    sourceVersion: "merge-sha",
    finishTime: "2026-07-24T15:00:00Z",
    triggerInfo: { "pr.sourceSha": "head-sha", "pr.number": "124" },
    definition: { id: 101, name: "dotnet-sdk-public-ci" },
    repository: { id: "dotnet/sdk" },
    validationResults: [{ result: "error", message: "Unexpected parameter 'example'" }]
  };
  const fetchImplementation = async url => {
    requestedUrls.push(url);
    if (url.includes("/build/builds/44?")) return { ok: true, json: async () => build };
    if (url.includes("/build/builds/44/timeline?")) return { ok: true, status: 204, json: async () => ({ records: [] }) };
    if (url.includes("/runs?")) return { ok: true, json: async () => ({ value: [] }) };
    if (url.includes("/build/builds?")) return { ok: true, json: async () => ({ value: [build] }) };
    assert.fail(`Unexpected URL: ${url}`);
  };
  const registry = { pipelines: [{
    organization: "dnceng-public", project: "public", definitionId: 101, repository: "dotnet/sdk",
    branches: ["refs/heads/main"], stableBranches: ["refs/heads/main"]
  }] };
  const state = { schemaVersion: 1, pipelines: {} };
  const mergedPullRequest = { number: 124, baseRef: "main", mergeCommitSha: "landed-sha" };

  const openPr = await collectCiEvidence(registry, null, state, fetchImplementation, null, "head-sha");
  const promoted = await collectCiEvidence(registry, null, state, fetchImplementation, null, "head-sha", mergedPullRequest);
  const redelivery = await collectCiEvidence(registry, null, state, fetchImplementation, null, "head-sha", mergedPullRequest);

  assert.equal(openPr.failures.length, 0);
  assert.equal(promoted.failures.length, 1);
  assert.equal(promoted.failures[0].issueCandidates[0].phase, "pipeline-validation");
  assert.equal(promoted.failures[0].issueCandidates[0].failureType, "configuration-error");
  assert.equal(promoted.failures[0].priority, "HIGH");
  assert.equal(promoted.failures[0].auditContext, "stable-merge:124:landed-sha");
  assert.equal(redelivery.failures.length, 0);
  assert.ok(requestedUrls.some(url => url.includes("branchName=refs%2Fpull%2F124%2Fmerge")));
});

test("merged PR audits require a landed commit identity", async () => {
  const build = {
    id: 44, result: "failed", reason: "pullRequest", sourceBranch: "refs/pull/124/merge",
    sourceVersion: "head-sha", finishTime: "2026-07-24T14:00:00Z",
    definition: { id: 101 }, repository: { id: "dotnet/sdk" },
    triggerInfo: { "pr.number": "124" }
  };
  const fetchImplementation = async url => {
    if (url.includes("builds/44?")) return new Response(JSON.stringify(build), { status: 200 });
    if (url.includes("builds?")) return new Response(JSON.stringify({ value: [build] }), { status: 200 });
    throw new Error(`Unexpected URL ${url}`);
  };
  const registry = { pipelines: [{
    organization: "dnceng-public", project: "public", definitionId: 101, repository: "dotnet/sdk",
    branches: ["refs/heads/main"], stableBranches: ["refs/heads/main"]
  }] };

  const dossier = await collectCiEvidence(
    registry, null, { schemaVersion: 1, pipelines: {} }, fetchImplementation, "44", null,
    { number: 124, baseRef: "main", mergeCommitSha: "" });

  assert.equal(dossier.failures.length, 0);
});

test("agent gating skips bootstrap and previously processed windows", () => {
  assert.equal(shouldRunAgent({ bootstrap: true, pipelineHealth: [], failures: [{ build: { id: 4 } }] }), false);
  assert.equal(shouldRunAgent({ bootstrap: false, pipelineHealth: [], failures: [] }), false);
  assert.equal(shouldRunAgent({
    bootstrap: false,
    pipelineHealth: [],
    failures: [{ issueCandidates: [{ fingerprint: "build|compiler|cs0114" }] }]
  }), true);
  assert.equal(shouldRunAgent({
    bootstrap: false,
    pipelineHealth: [{ actionable: false }, { actionable: true }],
    failures: []
  }), true);
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
  assert.deepEqual(classifyWorkItem(2, "", [{ test: "Example" }]), {
    phase: "test-execution", failureType: "test-assertion", evidenceSources: ["helix-trx"]
  });
  assert.equal(classifyWorkItem(143, "WORKLOAD TIMED OUT").failureType, "timeout");
  assert.equal(classifyWorkItem(139, "Segmentation fault (core dumped)").failureType, "process-crash");
  assert.equal(classifyWorkItem(81, "DEVICE_NOT_FOUND").failureType, "infrastructure-unavailable");
  assert.equal(classifyWorkItem(80, "Test run completed\nAPP_CRASH").phase, "test-post-processing");
  assert.equal(classifyWorkItem(2, "Copy all crash dumps to upload directory").failureType, "unknown-error");
});

test("classifyTaskFailure identifies roots and artifact cascades", () => {
  assert.deepEqual(classifyTaskFailure("Download Previous Build", ["Artifact not found"]), {
    phase: "artifact-transfer", failureType: "artifact-missing", diagnosticCode: null
  });
  assert.equal(classifyTaskFailure("Initialize containers", []).phase, "environment-setup");
  assert.equal(classifyTaskFailure("Build", ["error NETSDK1005"]).failureType, "build-task-error");
  assert.equal(classifyTaskFailure("Validate pipeline", ["Unexpected value 'jobs'"]).phase, "pipeline-validation");
  assert.deepEqual(classifyTaskFailure("Build", ["ResolvePackageAssets.cs: error CS1061: missing member"]), {
    phase: "compilation", failureType: "compiler-error", diagnosticCode: "CS1061"
  });
  assert.equal(classifyTaskFailure("Build", ["C:\\src\\Program.cs(401,1): error CS1002: ; expected"]).failureType, "compiler-error");
  assert.equal(classifyTaskFailure("Build", ["C:\\src\\Program.cs(429,1): error CS1002: ; expected"]).failureType, "compiler-error");
  assert.deepEqual(classifyTaskFailure("Checkout dotnet/sdk", ["401 Unauthorized"]), {
    phase: "source-checkout", failureType: "authentication-failure", diagnosticCode: null
  });
  assert.deepEqual(classifyTaskFailure("Checkout dotnet/sdk", ["Response status code does not indicate success: 503"]), {
    phase: "source-checkout", failureType: "network-failure", diagnosticCode: null
  });
  assert.equal(classifyTaskFailure("Restore", ["Response status code does not indicate success: 503"]).failureType, "network-failure");
  assert.equal(classifyTaskFailure("Build", ["NuGet.targets error 503 Service Unavailable"]).failureType, "network-failure");
  assert.equal(classifyTaskFailure("Restore", ["401 Unauthorized from package source"]).failureType, "authentication-failure");
  assert.equal(classifyTaskFailure("Restore", ["error NU1902: known vulnerability"]).failureType, "package-policy-error");
});

test("createFailureFingerprint removes volatile numeric values", () => {
  assert.equal(
    createFailureFingerprint({
      phase: "test-execution", failureType: "network-failure",
      component: "ItCanUpdatePackages", mechanism: "HTTP 503 on port 443"
    }),
    "test-execution|network-failure|itcanupdatepackages|http-<n>-on-port-<n>");
});

test("parseTestResultXml returns recovered totals and independent named failures", () => {
  const results = parseTestResultXml(`<?xml version="1.0" encoding="UTF-8"?>
    <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
      <TestDefinitions><UnitTest id="test-1" name="ShortName"><TestMethod className="Sdk.Tests.Example" name="Fails" /></UnitTest></TestDefinitions>
      <Results><UnitTestResult testId="test-1" testName="ShortName" outcome="Failed" duration="00:00:01">
        <Output><ErrorInfo><Message>Expected zero but found one.</Message><StackTrace>at Sdk.Tests.Example.Fails()</StackTrace></ErrorInfo></Output>
      </UnitTestResult><UnitTestResult testId="test-2" testName="Passes" outcome="Passed" duration="00:00:01" /></Results>
      <ResultSummary outcome="Failed"><Counters total="2" executed="2" passed="1" failed="1" error="0" timeout="0" aborted="0" /></ResultSummary>
    </TestRun>`);

  assert.deepEqual(results.summary, {
    total: 2, executed: 2, passed: 1, failed: 1, error: 0, timeout: 0, aborted: 0
  });
  assert.deepEqual(results.failures, [{
    testName: "ShortName",
    fullyQualifiedName: "Sdk.Tests.Example.Fails",
    outcome: "Failed",
    duration: "00:00:01",
    errorMessage: "Expected zero but found one.",
    stackTrace: "at Sdk.Tests.Example.Fails()"
  }]);
});

test("summarizeHelixConsole preserves hang, host exit, and dump failures", () => {
  const summary = summarizeHelixConsole(`
    The following tests were still running when dump was taken (format: [<time-elapsed-since-start>] <name>):
    [50:03] Microsoft.DotNet.Watcher.Tools.Tests.BrowserTests.BrowserDiagnostics
    Hang timeout expired. Capturing process tree and hang dumps.
    Failed to collect dump for dotnet-watch-test-browser: Permission denied.
    Test host crashed.
    Test application process didn't exit gracefully, exit code is '137'.`);

  assert.match(summary.activeTest, /BrowserDiagnostics/);
  assert.equal(summary.hostExitCode, 137);
  assert.match(summary.hangEvidence.join("\n"), /Hang timeout expired/);
  assert.match(summary.dumpFailures.join("\n"), /Permission denied/);
});

test("createTaskObservations preserves roots and suppresses cascades", () => {
  const observations = createTaskObservations([
    { type: "Task", name: "Build", issues: ["error NETSDK1005"], path: ["Build", "Windows", "Build"], logId: 1 },
    { type: "Task", name: "Download Previous Build", issues: ["Artifact not found"], path: ["Validate", "Download Previous Build"], logId: 2 }
  ]);

  assert.equal(observations[0].phase, "compilation");
  assert.equal(observations[0].failureType, "build-task-error");
  assert.equal(observations[0].actionable, true);
  assert.deepEqual(observations[0].path, ["Build", "Windows", "Build"]);
  assert.equal(observations[1].phase, "artifact-transfer");
  assert.equal(observations[1].failureType, "artifact-missing");
  assert.equal(observations[1].actionable, false);
});

test("timeline failures preserve every issue message", () => {
  const failures = getTimelineFailuresFromRecords([{
    id: "task",
    type: "Task",
    name: "Checkout",
    result: "failed",
    issues: [{ message: "Git fetch failed" }, { message: "Git fetch failed with exit code 128" }]
  }]);

  assert.deepEqual(failures[0].issues, ["Git fetch failed", "Git fetch failed with exit code 128"]);
});

test("createTaskObservations classifies restore failures from task logs", () => {
  const observations = createTaskObservations([{
    type: "Task", name: "Build", issues: [], path: ["Build", "Linux", "Build"], logId: 28
  }], new Map([[28, "error: Unable to load the service index for NuGet source.\nResponse status code does not indicate success: 503 (Service Unavailable)."]]));

  assert.equal(observations[0].phase, "dependency-restore");
  assert.equal(observations[0].failureType, "network-failure");
  assert.deepEqual(observations[0].evidenceSources, ["azure-timeline", "azure-task-log"]);
  assert.match(observations[0].mechanism, /NuGet source/);
  assert.equal(observations[0].actionable, true);
});

test("normal task log text does not become an actionable failure", () => {
  const observations = createTaskObservations([{
    type: "Task", name: "Build", issues: [], path: ["Build", "Linux", "Build"], logId: 29
  }], new Map([[29, "All projects are up-to-date for restore."]]));

  assert.equal(observations[0].failureType, "build-task-error");
  assert.deepEqual(observations[0].logExcerpt, []);
  assert.equal(observations[0].actionable, false);
});

test("checkout observations use the stable fatal cause instead of randomized retry delays", () => {
  const failures = [3.784, 9.696].map((delay, index) => ({
    type: "Task",
    name: "Checkout dotnet/sdk",
    issues: [`Git fetch failed with exit code 128, back off ${delay} seconds before retry.`],
    path: ["Build", `Leg ${index}`, "Checkout dotnet/sdk"],
    logId: index
  }));
  const logs = new Map(failures.map(failure => [failure.logId,
    "fatal: couldn't find remote ref refs/pull/55429/merge\nGit fetch failed with exit code: 128"]));

  const observations = createTaskObservations(failures, logs);

  assert.equal(observations[0].mechanism, "fatal: couldn't find remote ref refs/pull/55429/merge");
  assert.equal(observations[0].fingerprint, observations[1].fingerprint);
});

test("createPipelineObservation represents YAML rejection and empty execution", () => {
  const rejected = createPipelineObservation({
    definition: { name: "sdk-ci" },
    validationResults: [{ result: "error", message: "Unexpected value 'jobs'" }]
  }, []);
  const empty = createPipelineObservation({ definition: { name: "sdk-ci" } }, []);

  assert.equal(rejected.phase, "pipeline-validation");
  assert.equal(rejected.failureType, "configuration-error");
  assert.deepEqual(rejected.evidenceSources, ["azure-validation"]);
  assert.equal(rejected.actionable, true);
  assert.equal(empty.phase, "pipeline-startup");
  assert.equal(empty.failureType, "missing-execution");
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
  assert.equal(missed.phase, "pipeline-scheduling");
  assert.equal(missed.failureType, "missing-execution");
  assert.equal(missed.actionable, false);
});

test("summarizeSharedTestMechanism removes test identity and keeps a shared service failure", () => {
  const first = summarizeSharedTestMechanism(
    "Test method Sdk.Tests.First threw exception:\nAssertionException: command failed\nResponse status code does not indicate success: 503 (Service Unavailable).\nResponse status code does not indicate success: 503 (Service Unavailable).",
    "Failed");
  const second = summarizeSharedTestMechanism(
    "Test method Sdk.Tests.Second threw exception:\nHttpRequestException: request failed\nUnhandled exception: Response status code does not indicate success: 503 (Service Unavailable).",
    "Failed");

  assert.match(first, /503 \(Service Unavailable\)/);
  assert.match(second, /503 \(Service Unavailable\)/);
  assert.equal(first, second);
  assert.doesNotMatch(first, /Sdk.Tests.First/);
  assert.doesNotMatch(second, /Sdk.Tests.Second/);
});

test("applyKbeRecurrence requires the same test and mechanism", () => {
  const current = {
    kind: "test",
    component: "Sdk.Tests.Flaky",
    mechanismFingerprint: "test-mechanism|shared|timeout",
    kbe: { eligible: true }
  };
  const related = [{
    build: { id: 41, commit: "related-commit" },
    observations: [
      { kind: "test", component: "Sdk.Tests.Flaky", mechanismFingerprint: "test-mechanism|shared|timeout" },
      { kind: "test", component: "Sdk.Tests.Other", mechanismFingerprint: "test-mechanism|shared|timeout" }
    ]
  }];

  const result = applyKbeRecurrence([current], related, { id: 42, commit: "current-commit" })[0];
  assert.equal(result.kbe.recurring, true);
  assert.deepEqual(result.kbe.matchingBuilds, [{ id: 41, commit: "related-commit" }]);

  const different = applyKbeRecurrence([current], [{
    build: { id: 40 },
    observations: [{ kind: "test", component: "Sdk.Tests.Flaky", mechanismFingerprint: "different" }]
  }], { id: 42, commit: "current-commit" })[0];
  assert.equal(different.kbe.recurring, false);
});

test("issue candidates contain only actionable observations from the selected build", async () => {
  const build = {
    id: 45, status: "completed", result: "failed", reason: "manual",
    sourceBranch: "refs/heads/main", finishTime: "2026-07-24T14:00:00Z",
    definition: { id: 101 }, repository: { id: "dotnet/sdk" },
    validationResults: [{ result: "error", message: "Current YAML failure" }]
  };
  const related = {
    ...build, id: 44, finishTime: "2026-07-24T13:00:00Z", validationResults: undefined
  };
  const fetchImplementation = async url => {
    if (url.includes("builds/45?")) return new Response(JSON.stringify(build), { status: 200 });
    if (url.includes("builds?") && url.includes("branchName")) {
      return new Response(JSON.stringify({ value: [build, related] }), { status: 200 });
    }
    if (url.includes("builds/45/timeline")) return new Response(JSON.stringify({ records: [] }), { status: 200 });
    if (url.includes("builds/44/timeline")) {
      return new Response(JSON.stringify({ records: [{
        type: "Task", name: "Related Build", result: "failed",
        issues: [{ message: "CS1000 related failure" }]
      }] }), { status: 200 });
    }
    if (url.includes("vstmr.dev.azure.com")) return new Response("not found", { status: 404 });
    throw new Error(`Unexpected URL ${url}`);
  };
  const registry = { pipelines: [{
    organization: "dnceng-public", project: "public", definitionId: 101,
    repository: "dotnet/sdk", branches: ["refs/heads/main"], stableBranches: ["refs/heads/main"]
  }] };

  const dossier = await collectCiEvidence(
    registry, "45", { schemaVersion: 1, pipelines: {} }, fetchImplementation);

  assert.deepEqual(dossier.failures[0].issueCandidates.map(candidate => candidate.mechanism), ["Current YAML failure"]);
  assert.match(dossier.failures[0].relatedFailureSummaries[0].taskFailures[0].issues[0], /related failure/);
});

test("applyKbeRecurrence ignores retries of the current commit", () => {
  const current = {
    kind: "test", component: "Sdk.Tests.Flaky", mechanismFingerprint: "test-mechanism|shared|timeout",
    kbe: { eligible: true, validation: { valid: true } }
  };
  const result = applyKbeRecurrence([current], [{
    build: { id: 41, commit: "same-commit" },
    observations: [{ kind: "test", component: current.component, mechanismFingerprint: current.mechanismFingerprint }]
  }], { id: 42, commit: "same-commit" });

  assert.equal(result[0].kbe.recurring, false);
  assert.deepEqual(result[0].kbe.matchingBuilds, []);
});

test("applyKbeRecurrence ignores attempts of the same pull request", () => {
  const current = {
    kind: "test", component: "Sdk.Tests.Flaky", mechanismFingerprint: "test-mechanism|shared|timeout",
    kbe: { eligible: true, validation: { valid: true } }
  };
  const result = applyKbeRecurrence([current], [{
    build: { id: 41, commit: "previous-commit", branch: "refs/pull/123/merge" },
    observations: [{ kind: "test", component: current.component, mechanismFingerprint: current.mechanismFingerprint }]
  }], { id: 42, commit: "current-commit", branch: "refs/pull/123/merge" });

  assert.equal(result[0].kbe.recurring, false);
  assert.deepEqual(result[0].kbe.matchingBuilds, []);
});

test("open issue fingerprints suppress candidates before agent activation", async () => {
  const fingerprint = "restore|build|nuget-503";
  const fetchImplementation = async (url, options) => {
    assert.match(url, /repos\/nagilson\/sdk\/issues/);
    assert.match(url, /state=open/);
    assert.equal(options.headers.Authorization, "Bearer token");
    return new Response(JSON.stringify([{
      body: `## Build Information\n\n- **Failure fingerprint:** \`${fingerprint}\``
    }]), { status: 200 });
  };
  const dossier = {
    bootstrap: false,
    pipelineHealth: [],
    failures: [{ issueCandidates: [{ fingerprint }, { fingerprint: "build|compiler|cs0114" }] }]
  };

  const tracked = await getRecentlyTrackedFingerprints("nagilson/sdk", "token", fetchImplementation);
  suppressTrackedIssueCandidates(dossier, tracked);

  assert.deepEqual(dossier.failures[0].issueCandidates, [{ fingerprint: "build|compiler|cs0114" }]);
  dossier.failures[0].issueCandidates = [];
  assert.equal(shouldRunAgent(dossier), false);
});

test("closed issue fingerprints remain eligible for a new issue", async () => {
  const fingerprint = "restore|build|nuget-503";
  const fetchImplementation = async url => {
    assert.match(url, /state=open/);
    return new Response(JSON.stringify([]), { status: 200 });
  };
  const dossier = {
    failures: [{ issueCandidates: [{ fingerprint }] }]
  };

  const tracked = await getRecentlyTrackedFingerprints("nagilson/sdk", "token", fetchImplementation);
  suppressTrackedIssueCandidates(dossier, tracked);

  assert.deepEqual(dossier.failures[0].issueCandidates, [{ fingerprint }]);
});

test("Helix artifacts contribute TRX and dump evidence sources", () => {
  assert.deepEqual(getArtifactEvidenceSources([
    { FileName: "results.trx" },
    { FileName: "dotnet_hang.dmp" },
    { FileName: "console.log" }
  ]), ["helix-trx", "helix-dump"]);
});

test("test observations separate execution phase from wrapped network failures", () => {
  const classification = classifyWorkItem(2, "", [{ test: "Example" }]);
  assert.equal(classification.phase, "test-execution");
  assert.equal(classification.failureType, "test-assertion");
});

test("compiler fingerprints converge across CI workspace roots", () => {
  const mechanisms = [
    "D:\\a\\_work\\1\\s\\src\\Tasks\\Task.cs(12,3): error CS0114: member hides inherited member",
    "/Users/runner/work/1/s/src/Tasks/Task.cs(12,3): error CS0114: member hides inherited member",
    "/mnt/vss/_work/1/s/src/Tasks/Task.cs(12,3): error CS0114: member hides inherited member",
    "/__w/1/s/src/Tasks/Task.cs(12,3): error CS0114: member hides inherited member"
  ];
  const fingerprints = mechanisms.map(mechanism => createFailureFingerprint({
    phase: "compilation", failureType: "compiler-error", component: "Build", mechanism
  }));

  assert.equal(new Set(fingerprints).size, 1);
});

test("task fingerprints do not depend on evidence order", () => {
  const failures = [
    { type: "Task", name: "Build", issues: ["error CS0114: first", "error CS0115: second"], path: ["Build"] },
    { type: "Task", name: "Build", issues: ["error CS0115: second", "error CS0114: first"], path: ["Build"] }
  ];

  const observations = createTaskObservations(failures);

  assert.equal(observations[0].fingerprint, observations[1].fingerprint);
});