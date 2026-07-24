import { appendFile, mkdir, readFile, writeFile } from "node:fs/promises";
import { spawnSync } from "node:child_process";
import path from "node:path";
import process from "node:process";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createTestKbeCandidate } from "./known-build-error.mjs";

const API_VERSION = "7.1";
const DEFAULT_BUILD_LIMIT = 20;
const MAX_FAILURES = 10;
const MAX_LOG_CHARACTERS = 4_000;
const MAX_CONSOLE_CHARACTERS = 16_000;
const MAX_PROCESSED_BUILD_IDS = 100;
const MAX_TEST_FAILURES = 20;
const MAX_TIMELINE_FAILURES = 100;
const MODULE_DIRECTORY = path.dirname(fileURLToPath(import.meta.url));
const PIPELINE_HEARTBEAT_AGE_MS = 90 * 60 * 1000;

export function parseHelixWorkItemReferences(messages) {
  const pattern = /Work item '([^']+)' in job '(.+) \(([0-9a-f-]{36})\)' failed \([^,]+, exit code (-?\d+)\)\./i;
  return messages.flatMap(message => {
    const match = `${message}`.match(pattern);
    return match ? [{
      workItem: match[1],
      queue: match[2],
      jobId: match[3],
      exitCode: Number.parseInt(match[4], 10)
    }] : [];
  });
}

export function classifyWorkItem(exitCode, consoleText, testFailures = []) {
  if (testFailures.length > 0) return "test-failure";
  const text = `${consoleText ?? ""}`;
  if (/test run completed|detected test end tag/i.test(text)
      && /app_crash|timed_out|exit(?:ed)? with (?:80|143)/i.test(text)) {
    return "post-test-harness-failure";
  }
  if (/workload timed out|run timed out|timed_out|timeout|timed out/i.test(text)
      || exitCode === 130 || exitCode === 143) {
    return "timeout";
  }
  if (/segmentation fault|stack overflow|core dump(?:ed)?|assert failed|app_crash|created crash dump/i.test(text)
      || [133, 134, 139].includes(exitCode)) {
    return "crash";
  }
  if (/device_not_found|infrastructure error|agent connection|machine is not available/i.test(text)
      || [-4, 71, 81].includes(exitCode)) {
    return "infrastructure";
  }
  return "work-item-failure";
}

export function classifyTaskFailure(name, issues = []) {
  const text = `${name}\n${issues.join("\n")}`;
  if (/artifact (?:was )?not found|download previous build|missing artifact/i.test(text)) return "cascade";
  if (/yaml|pipeline validation|unexpected value|mapping was not expected|template expression/i.test(text)) return "pipeline-configuration";
  if (/monitor helix jobs|send to helix|testbuild tests/i.test(text)) return "helix";
  if (/checkout|initialize container|install|acquire|setup/i.test(text)) return "setup";
  if (/restore|nuget|feed/i.test(text)) return "restore";
  if (/\b(?:MSB\d{4}|NETSDK\d{4}|CS\d{4})\b|\bbuild\b|compile/i.test(text)) return "build";
  if (/test/i.test(text)) return "test";
  return "pipeline-task";
}

export function normalizeSignaturePart(value) {
  return sanitizeText(value)
    .toLowerCase()
    .replace(/https?:\/\/[^\s]+/g, "<url>")
    .replace(/\b\d+\b/g, "<n>")
    .replace(/[^a-z0-9<>._-]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 180);
}

export function createFailureSignature(category, component, mechanism) {
  return [category, component, mechanism].map(normalizeSignaturePart).join("|");
}

export function parseArguments(argumentsList) {
  const options = {};
  for (let index = 0; index < argumentsList.length; index += 2) {
    const key = argumentsList[index];
    if (!key?.startsWith("--") || index + 1 >= argumentsList.length) {
      throw new Error(`Invalid argument near '${key ?? "end of arguments"}'.`);
    }
    options[key.slice(2)] = argumentsList[index + 1];
  }
  if (!options.registry || !options.output) {
    throw new Error("--registry and --output are required.");
  }
  return options;
}

export function sanitizeText(value, maxCharacters = MAX_LOG_CHARACTERS) {
  return `${value ?? ""}`
    .replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/gi, "<guid>")
    .replace(/\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z\b/g, "<timestamp>")
    .replace(/[A-Za-z]:\\h\\w\\[^\r\n ]+/gi, "<helix-path>")
    .replace(/(?:[A-Za-z]:\\|\/)[^\r\n ]*(?:artifacts|tmp|temp)[^\r\n ]*/gi, "<temporary-path>")
    .slice(0, maxCharacters);
}

export function normalizeBuild(build) {
  return {
    id: build.id,
    number: build.buildNumber,
    result: build.result,
    reason: build.reason,
    branch: build.sourceBranch,
    commit: build.sourceVersion,
    definitionId: build.definition?.id,
    definitionName: build.definition?.name,
    repository: build.repository?.id,
    queuedAt: build.queueTime,
    startedAt: build.startTime,
    finishedAt: build.finishTime,
    url: build._links?.web?.href ?? build.url
  };
}

export function buildConsumptionKey(build) {
  return `${build.id}:${build.finishTime ?? ""}:${build.result ?? ""}`;
}

function buildApiBase(pipeline) {
  const organization = encodeURIComponent(pipeline.organization);
  const project = encodeURIComponent(pipeline.project);
  return `https://dev.azure.com/${organization}/${project}/_apis`;
}

function testApiBase(pipeline) {
  const organization = encodeURIComponent(pipeline.organization);
  const project = encodeURIComponent(pipeline.project);
  return `https://vstmr.dev.azure.com/${organization}/${project}/_apis/test`;
}

async function fetchResponse(url, fetchImplementation = fetch) {
  const response = await fetchImplementation(url, {
    headers: { Accept: "application/json", "User-Agent": "dotnet-sdk-ci-quality-monitor" }
  });
  if (!response.ok) {
    throw new Error(`GET ${url} returned ${response.status} ${response.statusText}.`);
  }
  return response;
}

async function fetchJson(url, fetchImplementation = fetch) {
  return (await fetchResponse(url, fetchImplementation)).json();
}

export function parseTestResultXml(xml, command = process.env.PYTHON || (process.platform === "win32" ? "python" : "python3")) {
  const parser = path.join(MODULE_DIRECTORY, "parse-test-results.py");
  const result = spawnSync(command, [parser], { input: xml, encoding: "utf8", maxBuffer: 2 * 1024 * 1024 });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`TRX parser failed: ${result.stderr.trim()}`);
  return JSON.parse(result.stdout);
}

export async function listCompletedBuilds(pipeline, branch, fetchImplementation = fetch) {
  const query = new URLSearchParams({
    definitions: `${pipeline.definitionId}`,
    branchName: branch,
    statusFilter: "completed",
    queryOrder: "finishTimeDescending",
    "$top": `${DEFAULT_BUILD_LIMIT}`,
    "api-version": API_VERSION
  });
  const result = await fetchJson(`${buildApiBase(pipeline)}/build/builds?${query}`, fetchImplementation);
  return result.value ?? [];
}

async function listRecentBuilds(pipeline, branch, fetchImplementation = fetch) {
  const query = new URLSearchParams({
    definitions: `${pipeline.definitionId}`,
    branchName: branch,
    queryOrder: "queueTimeDescending",
    "$top": `${DEFAULT_BUILD_LIMIT}`,
    "api-version": API_VERSION
  });
  const result = await fetchJson(`${buildApiBase(pipeline)}/build/builds?${query}`, fetchImplementation);
  return result.value ?? [];
}

async function getGitHubBranchHead(pipeline, branch, fetchImplementation = fetch) {
  const branchName = branch.replace(/^refs\/heads\//, "");
  const url = `https://api.github.com/repos/${pipeline.repository}/commits/${encodeURIComponent(branchName)}`;
  const commit = await fetchJson(url, fetchImplementation);
  return {
    sha: commit.sha,
    committedAt: commit.commit?.committer?.date ?? commit.commit?.author?.date,
    url: commit.html_url
  };
}

export function createHeartbeatObservation(pipeline, branch, head, builds, now = Date.now()) {
  const committedAt = Date.parse(head.committedAt);
  if (!Number.isFinite(committedAt) || now - committedAt < PIPELINE_HEARTBEAT_AGE_MS) return null;
  const covered = builds.some(build => build.sourceVersion === head.sha
    || Date.parse(build.queueTime) >= committedAt);
  if (covered) return null;
  const mechanism = `No ${pipeline.definitionId} build was queued for branch head ${head.sha} within 90 minutes.`;
  return {
    kind: "pipeline-heartbeat",
    category: "pipeline-not-triggered",
    component: `${pipeline.repository}:${branch}`,
    mechanism,
    signature: createFailureSignature("pipeline-not-triggered", pipeline.definitionId, branch),
    actionable: false,
    branch,
    branchHead: head,
    latestBuild: builds[0] ? normalizeBuild(builds[0]) : null
  };
}

function updateHeartbeatState(state, key, observation) {
  const previousMisses = state.pipelines[key]?.heartbeatMisses ?? 0;
  const heartbeatMisses = observation ? previousMisses + 1 : 0;
  state.pipelines[key] = { ...state.pipelines[key], heartbeatMisses };
  if (!observation) return null;
  return { ...observation, missedChecks: heartbeatMisses, actionable: heartbeatMisses >= 2 };
}

export async function getBuild(pipeline, buildId, fetchImplementation = fetch) {
  const url = `${buildApiBase(pipeline)}/build/builds/${encodeURIComponent(buildId)}?api-version=${API_VERSION}`;
  return fetchJson(url, fetchImplementation);
}

function isRegisteredBuild(build, pipeline) {
  return build.definition?.id === pipeline.definitionId
    && pipeline.branches.includes(build.sourceBranch)
    && build.repository?.id?.toLowerCase() === pipeline.repository.toLowerCase()
    && build.reason?.toLowerCase() !== "pullrequest";
}

async function getTimeline(pipeline, buildId, fetchImplementation = fetch) {
  const url = `${buildApiBase(pipeline)}/build/builds/${buildId}/timeline?api-version=${API_VERSION}`;
  const response = await fetchResponse(url, fetchImplementation);
  return response.status === 204 ? { records: [] } : response.json();
}

function timelinePath(record, recordsById) {
  const names = [record.name];
  let parentId = record.parentId;
  while (parentId && recordsById.has(parentId)) {
    const parent = recordsById.get(parentId);
    names.unshift(parent.name);
    parentId = parent.parentId;
  }
  return names;
}

export function getTimelineFailuresFromRecords(records = []) {
  const recordsById = new Map(records.map(record => [record.id, record]));
  return records
    .filter(record => record.result === "failed" || record.result === "partiallySucceeded")
    .filter(record => record.type === "Job" || record.type === "Task")
    .slice(0, MAX_TIMELINE_FAILURES)
    .map(record => {
      const messages = (record.issues ?? []).map(issue => issue.message);
      return {
        type: record.type,
        name: record.name,
        result: record.result,
        logId: record.log?.id,
        logUrl: record.log?.url,
        path: timelinePath(record, recordsById),
        startedAt: record.startTime,
        finishedAt: record.finishTime,
        helixReferences: parseHelixWorkItemReferences(messages),
        issues: messages.map(message => sanitizeText(message))
      };
    });
}

async function getTimelineFailures(pipeline, buildId, fetchImplementation = fetch) {
  const timeline = await getTimeline(pipeline, buildId, fetchImplementation);
  return getTimelineFailuresFromRecords(timeline.records);
}

function summarizeTaskLog(logText) {
  const lines = `${logText ?? ""}`.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
  const diagnostics = lines.filter(line => /\b(?:MSB\d{4}|NETSDK\d{4}|CS\d{4})\b|response status|unable to|service unavailable|timed? ?out|connection refused|exec format error/i.test(line));
  const fallback = lines.filter(line => /\b(?:error|fatal|exception|failed)\b/i.test(line) && !/\bat\s+\S+\(/i.test(line));
  return [...new Set((diagnostics.length > 0 ? diagnostics : fallback.length > 0 ? fallback : lines).slice(-8))];
}

export function createTaskObservations(timelineFailures, logsById = new Map()) {
  return timelineFailures
    .filter(failure => failure.type === "Task")
    .map(failure => {
      const logExcerpt = summarizeTaskLog(logsById.get(failure.logId));
      const evidence = [...failure.issues, ...logExcerpt];
      const category = classifyTaskFailure(failure.name, evidence);
      const mechanism = evidence.find(issue => !/^Bash exited with code/i.test(issue)) ?? failure.name;
      return {
        kind: "pipeline-task",
        category,
        component: failure.name,
        mechanism,
        signature: createFailureSignature(category, failure.name, mechanism),
        actionable: category !== "cascade" && category !== "helix",
        path: failure.path,
        issues: failure.issues,
        logExcerpt,
        logId: failure.logId,
        logUrl: failure.logUrl
      };
    });
}

export function createPipelineObservation(build, timelineRecords = []) {
  const validations = (build.validationResults ?? [])
    .filter(validation => `${validation.result ?? ""}`.toLowerCase() !== "ok")
    .map(validation => sanitizeText(validation.message ?? validation.result));
  if (validations.length === 0 && timelineRecords.length > 0) return null;
  const category = validations.length > 0 ? "pipeline-configuration" : "pipeline-startup";
  const mechanism = validations.join("\n") || "Pipeline failed without creating stages, jobs, or tasks.";
  return {
    kind: "pipeline",
    category,
    component: build.definition?.name ?? "Azure DevOps pipeline",
    mechanism,
    signature: createFailureSignature(category, build.definition?.name ?? "pipeline", mechanism),
    actionable: validations.length > 0,
    validationResults: validations
  };
}

function getHelixReferences(timelineFailures) {
  const references = timelineFailures.flatMap(failure => failure.helixReferences ?? []);
  return [...new Map(references.map(reference => [`${reference.jobId}:${reference.workItem}`, reference])).values()];
}

function helixWorkItemUrl(reference) {
  return `https://helix.dot.net/api/2019-06-17/jobs/${encodeURIComponent(reference.jobId)}/workitems/${encodeURIComponent(reference.workItem)}`;
}

function selectArtifactLinks(files = []) {
  return files
    .filter(file => /\.(?:trx|xml|binlog|dmp|core|crash|log)$/i.test(file.FileName))
    .slice(0, 10)
    .map(file => ({ name: file.FileName, url: file.Uri }));
}

async function getHelixText(url, fetchImplementation = fetch) {
  const text = await (await fetchResponse(url, fetchImplementation)).text();
  return sanitizeText(text.slice(-MAX_CONSOLE_CHARACTERS), MAX_CONSOLE_CHARACTERS);
}

async function getHelixTestFailures(workItem, fetchImplementation = fetch) {
  const testFile = (workItem.Files ?? []).find(file => /\.(?:trx|xml)$/i.test(file.FileName));
  if (!testFile) return { summary: null, failures: [] };
  const response = await fetchResponse(testFile.Uri, fetchImplementation);
  const results = parseTestResultXml(Buffer.from(await response.arrayBuffer()));
  return { ...results, failures: results.failures.slice(0, MAX_TEST_FAILURES) };
}

export function summarizeHelixConsole(consoleText) {
  const lines = `${consoleText ?? ""}`.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
  const runningTestsMarker = lines.findIndex(line => /tests were still running when dump was taken/i.test(line));
  const markedActiveTest = runningTestsMarker >= 0
    ? lines.slice(runningTestsMarker + 1).find(line => /^\[[\d:.]+\]\s+\S/.test(line))
    : null;
  const relevant = lines
    .filter(line => /hang|timed? ?out|active test|currently running|process tree|test host crashed|exit code|dump|permission denied|diagnostics IPC/i.test(line))
    .filter(line => !/^[-*]?\s*(?:\/|[A-Za-z]:\\)/.test(line));
  const hostExitCode = [...lines].reverse().map(line => line.match(/exit code(?: is)?\s*['"]?(-?\d+)/i)?.[1])
    .find(Boolean);
  const activeTest = markedActiveTest
    ?? [...relevant].reverse().find(line => /active test|currently running|has been running/i.test(line));
  if (activeTest && !relevant.includes(activeTest)) relevant.push(activeTest);
  const dumpFailures = relevant.filter(line => /dump.*(?:fail|error)|permission denied|diagnostics IPC/i.test(line)).slice(-4);
  return {
    activeTest: activeTest ? sanitizeText(activeTest) : null,
    hostExitCode: hostExitCode ? Number(hostExitCode) : null,
    hangEvidence: [...new Set(relevant.slice(-12).map(line => sanitizeText(line)))],
    dumpFailures: [...new Set(dumpFailures.map(line => sanitizeText(line)))]
  };
}

function summarizeTestMechanism(errorMessage, outcome) {
  const lines = `${errorMessage ?? ""}`.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
  const salient = lines.filter(line => /exception|error|expected|actual|exit code|status code|timed? ?out|failed/i.test(line));
  return sanitizeText((salient.length > 0 ? salient : lines).slice(0, 8).join("\n") || `${outcome} test result`);
}

export function sharedTestMechanism(errorMessage, outcome) {
  const lines = `${errorMessage ?? ""}`.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
  const diagnosticLines = lines.filter(line => /\b(?:MSB\d{4}|NETSDK\d{4}|CS\d{4})\b/i.test(line));
  const responseLines = lines.filter(line => /response status code/i.test(line))
    .map(line => line.slice(line.search(/response status code/i)));
  const operationalLines = responseLines.length > 0 ? responseLines
    : lines.filter(line => /service unavailable|timed? ?out|connection|refused|not found|access denied/i.test(line));
  const exceptionLines = lines.filter(line => !/^Test method .+ threw exception:?$/i.test(line))
    .filter(line => /(?:system\.)?\w+exception/i.test(line));
  const rootCauseLines = diagnosticLines.length > 0 ? diagnosticLines
    : operationalLines.length > 0 ? operationalLines
      : exceptionLines;
  const distinctLines = [...new Set(rootCauseLines.length > 0 ? rootCauseLines : lines.slice(-3))];
  return sanitizeText(distinctLines.slice(0, 4).join("\n") || `${outcome} test result`);
}

function createTestObservation(reference, test, testSummary) {
  const component = test.fullyQualifiedName || test.testName;
  const mechanism = summarizeTestMechanism(test.errorMessage, test.outcome);
  const sharedMechanism = sharedTestMechanism(test.errorMessage, test.outcome);
  const signature = createFailureSignature("test", component, mechanism);
  return {
    kind: "test",
    category: "test-failure",
    component,
    mechanism,
    signature,
    mechanismSignature: createFailureSignature("test-mechanism", "shared", sharedMechanism),
    actionable: true,
    workItem: reference.workItem,
    jobId: reference.jobId,
    queue: reference.queue,
    outcome: test.outcome,
    duration: test.duration,
    testSummary,
    stackTrace: sanitizeText(test.stackTrace),
    kbe: createTestKbeCandidate(test, signature)
  };
}

async function collectHelixObservation(reference, fetchImplementation = fetch) {
  const url = helixWorkItemUrl(reference);
  const workItem = await fetchJson(url, fetchImplementation);
  let consoleText = "";
  let testResults = { summary: null, failures: [] };
  const unavailable = [];
  try {
    consoleText = await getHelixText(`${url}/console`, fetchImplementation);
  } catch (error) {
    unavailable.push(sanitizeText(error.message));
  }
  try {
    testResults = await getHelixTestFailures(workItem, fetchImplementation);
  } catch (error) {
    unavailable.push(sanitizeText(error.message));
  }
  if (testResults.failures.length > 0) {
    return testResults.failures.map(test => createTestObservation(reference, test, testResults.summary));
  }
  const category = classifyWorkItem(workItem.ExitCode ?? reference.exitCode, consoleText);
  const consoleSummary = summarizeHelixConsole(consoleText);
  const causalConsoleLines = consoleSummary.hangEvidence.filter(line => line === consoleSummary.activeTest
    || /still running|hang timeout|timed? ?out|test host crashed|recovered \d+ test result|exit code/i.test(line));
  const mechanismLines = causalConsoleLines.length > 0
    ? causalConsoleLines
    : consoleText.split(/\r?\n/).filter(Boolean).slice(-8);
  const mechanism = mechanismLines.join("\n") || `Exit code ${workItem.ExitCode ?? reference.exitCode}`;
  return [{
    kind: "helix-work-item",
    category,
    component: reference.workItem,
    mechanism,
    signature: createFailureSignature(category, reference.workItem, mechanism),
    actionable: category !== "infrastructure",
    jobId: reference.jobId,
    queue: reference.queue,
    exitCode: workItem.ExitCode ?? reference.exitCode,
    state: workItem.State,
    machine: workItem.MachineName,
    duration: workItem.Duration,
    testSummary: testResults.summary,
    consoleSummary,
    consoleUrl: workItem.ConsoleOutputUri,
    artifacts: selectArtifactLinks(workItem.Files),
    unavailable
  }];
}

async function collectHelixObservations(timelineFailures, fetchImplementation = fetch, maxReferences = Number.POSITIVE_INFINITY) {
  const observations = [];
  for (const reference of getHelixReferences(timelineFailures).slice(0, maxReferences)) {
    try {
      observations.push(...await collectHelixObservation(reference, fetchImplementation));
    } catch (error) {
      observations.push({
        kind: "helix-work-item",
        category: "work-item-failure",
        component: reference.workItem,
        mechanism: sanitizeText(error.message),
        signature: createFailureSignature("work-item-failure", reference.workItem, error.message),
        actionable: false,
        ...reference
      });
    }
  }
  return observations;
}

async function getFailureLog(pipeline, buildId, logId, logUrl, fetchImplementation = fetch) {
  if (!logId) return null;
  const url = logUrl ?? `${buildApiBase(pipeline)}/build/builds/${buildId}/logs/${logId}?api-version=${API_VERSION}`;
  const response = await fetchImplementation(url, {
    headers: { Accept: "text/plain", "User-Agent": "dotnet-sdk-ci-quality-monitor" }
  });
  if (!response.ok) throw new Error(`GET ${url} returned ${response.status} ${response.statusText}.`);
  const text = await response.text();
  return sanitizeText(text.slice(-MAX_LOG_CHARACTERS));
}

async function getTestFailures(pipeline, buildId, fetchImplementation = fetch) {
  const runsUrl = `${testApiBase(pipeline)}/runs?buildIds=${buildId}&api-version=${API_VERSION}`;
  const runs = (await fetchJson(runsUrl, fetchImplementation)).value ?? [];
  const failures = [];
  for (const run of runs.filter(candidate => candidate.totalTests > candidate.passedTests).slice(0, MAX_FAILURES)) {
    const query = new URLSearchParams({
      outcomes: "Failed,Aborted,Timeout",
      "$top": `${MAX_FAILURES}`,
      "api-version": API_VERSION
    });
    const url = `${testApiBase(pipeline)}/runs/${run.id}/results?${query}`;
    const results = (await fetchJson(url, fetchImplementation)).value ?? [];
    failures.push(...results.slice(0, MAX_FAILURES - failures.length).map(result => ({
      runId: run.id,
      runName: run.name,
      test: result.testCaseTitle,
      outcome: result.outcome,
      error: sanitizeText(result.errorMessage),
      stackTrace: sanitizeText(result.stackTrace)
    })));
    if (failures.length >= MAX_FAILURES) break;
  }
  return failures;
}

async function getRelatedFailureSummaries(pipeline, buildId, history, fetchImplementation = fetch) {
  const related = [];
  const failedBuilds = history
    .filter(build => build.id !== buildId)
    .filter(build => build.result === "failed" || build.result === "partiallySucceeded")
    .slice(0, 5);
  for (const build of failedBuilds) {
    try {
      const timelineFailures = await getTimelineFailures(pipeline, build.id, fetchImplementation);
      related.push({
        build: normalizeBuild(build),
        timelineFailures,
        observations: await collectHelixObservations(timelineFailures, fetchImplementation, 10)
      });
    } catch (error) {
      related.push({ build: normalizeBuild(build), unavailable: sanitizeText(error.message) });
    }
  }
  return related;
}

export function applyKbeRecurrence(observations, relatedFailureSummaries) {
  const relatedTests = relatedFailureSummaries.flatMap(summary =>
    (summary.observations ?? []).filter(observation => observation.kind === "test")
      .map(observation => ({ observation, build: summary.build })));
  return observations.map(observation => {
    if (observation.kind !== "test" || !observation.kbe) return observation;
    const matches = relatedTests.filter(candidate => candidate.observation.component === observation.component
      && candidate.observation.mechanismSignature === observation.mechanismSignature);
    return {
      ...observation,
      kbe: {
        ...observation.kbe,
        recurring: matches.length > 0,
        matchingBuilds: matches.map(match => match.build)
      }
    };
  });
}

async function collectFailureEvidence(pipeline, build, history, fetchImplementation = fetch) {
  const detailedBuild = build.validationResults ? build : await getBuild(pipeline, build.id, fetchImplementation);
  const timeline = await getTimeline(pipeline, build.id, fetchImplementation);
  const timelineFailures = getTimelineFailuresFromRecords(timeline.records);
  const pipelineObservation = createPipelineObservation(detailedBuild, timeline.records ?? []);
  const helixObservations = await collectHelixObservations(timelineFailures, fetchImplementation);
  const relatedFailureSummaries = await getRelatedFailureSummaries(
    pipeline,
    build.id,
    history,
    fetchImplementation);
  const logFailures = [];
  const failedTasks = [...new Map(
    timelineFailures.filter(candidate => candidate.type === "Task" && candidate.logId)
      .map(candidate => [candidate.logId, candidate])).values()].slice(0, 10);
  for (const failure of failedTasks) {
    try {
      logFailures.push({
        name: failure.name,
        logId: failure.logId,
        text: await getFailureLog(pipeline, build.id, failure.logId, failure.logUrl, fetchImplementation)
      });
    } catch (error) {
      logFailures.push({ name: failure.name, unavailable: sanitizeText(error.message) });
    }
  }
  const taskObservations = createTaskObservations(
    timelineFailures,
    new Map(logFailures.filter(failure => failure.text).map(failure => [failure.logId, failure.text])));
  let testFailures = [];
  try {
    testFailures = await getTestFailures(pipeline, build.id, fetchImplementation);
  } catch (error) {
    testFailures = [{ unavailable: sanitizeText(error.message) }];
  }
  return {
    pipeline,
    build: normalizeBuild(build),
    recentBuilds: history.map(normalizeBuild),
    observations: applyKbeRecurrence(
      [pipelineObservation, ...taskObservations, ...helixObservations].filter(Boolean),
      relatedFailureSummaries),
    timelineFailures,
    relatedFailureSummaries,
    testFailures,
    logFailures
  };
}

async function selectManualBuild(pipelines, buildId, fetchImplementation = fetch) {
  for (const pipeline of pipelines) {
    try {
      const build = await getBuild(pipeline, buildId, fetchImplementation);
      const matchesPipeline = build.definition?.id === pipeline.definitionId
        && build.repository?.id?.toLowerCase() === pipeline.repository.toLowerCase();
      if (matchesPipeline) return { pipeline, build };
    } catch (error) {
      if (!error.message.includes("returned 404")) throw error;
    }
  }
  throw new Error(`Build ${buildId} is not from a pipeline and repository in the registry.`);
}

function stateKey(pipeline, branch) {
  return `${pipeline.organization}/${pipeline.project}/${pipeline.definitionId}:${branch}`;
}

function updateState(state, key, history) {
  const previous = state.pipelines[key] ?? {};
  const previousKeys = previous.consumedBuildKeys ?? [];
  const consumedBuildKeys = [...new Set([...history.map(buildConsumptionKey), ...previousKeys])]
    .slice(0, MAX_PROCESSED_BUILD_IDS);
  const { processedBuildIds: _legacyProcessedBuildIds, ...existing } = previous;
  state.pipelines[key] = { ...existing, consumedBuildKeys, lastCheckedAt: new Date().toISOString() };
}

export function selectUnprocessedFailures(state, key, history) {
  const previous = state.pipelines[key];
  const consumedKeys = new Set(previous?.consumedBuildKeys ?? []);
  const legacyProcessedIds = new Set(previous?.processedBuildIds ?? []);
  const unprocessed = previous ? history.filter(build => consumedKeys.size > 0
    ? !consumedKeys.has(buildConsumptionKey(build))
    : !legacyProcessedIds.has(build.id)) : history;
  const failures = unprocessed.filter(build => build.result === "failed" || build.result === "partiallySucceeded");
  return { bootstrap: !previous, failures: previous ? failures : failures.slice(0, 1) };
}

async function collectCandidates(registry, buildId, state, fetchImplementation = fetch) {
  if (buildId) {
    const selected = await selectManualBuild(registry.pipelines, buildId, fetchImplementation);
    const history = await listCompletedBuilds(selected.pipeline, selected.build.sourceBranch, fetchImplementation);
    return { candidates: [{ ...selected, history }], bootstrap: false, pipelineHealth: [] };
  }
  const candidates = [];
  const pipelineHealth = [];
  let bootstrap = false;
  for (const pipeline of registry.pipelines) {
    for (const branch of pipeline.branches) {
      const history = (await listCompletedBuilds(pipeline, branch, fetchImplementation))
        .filter(build => isRegisteredBuild(build, pipeline));
      const key = stateKey(pipeline, branch);
      const selected = selectUnprocessedFailures(state, key, history);
      bootstrap ||= selected.bootstrap;
      for (const build of selected.failures) {
        candidates.push({ pipeline, build, history });
      }
      try {
        const [head, recentBuilds] = await Promise.all([
          getGitHubBranchHead(pipeline, branch, fetchImplementation),
          listRecentBuilds(pipeline, branch, fetchImplementation)
        ]);
        const heartbeat = createHeartbeatObservation(
          pipeline,
          branch,
          head,
          recentBuilds.filter(build => isRegisteredBuild(build, pipeline)));
        const trackedHeartbeat = updateHeartbeatState(state, key, heartbeat);
        if (trackedHeartbeat) pipelineHealth.push(trackedHeartbeat);
      } catch (error) {
        pipelineHealth.push({
          kind: "pipeline-heartbeat",
          category: "heartbeat-unavailable",
          component: `${pipeline.repository}:${branch}`,
          mechanism: sanitizeText(error.message),
          actionable: false
        });
      }
      updateState(state, key, history);
    }
  }
  return { candidates, bootstrap, pipelineHealth };
}

export async function collectEvidence(registry, buildId, state, fetchImplementation = fetch) {
  const selected = await collectCandidates(registry, buildId, state, fetchImplementation);
  const failures = [];
  for (const candidate of selected.candidates) {
    failures.push(await collectFailureEvidence(
      candidate.pipeline,
      candidate.build,
      candidate.history,
      fetchImplementation));
  }
  return {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    manualBuildId: buildId || null,
    bootstrap: selected.bootstrap,
    pipelineHealth: selected.pipelineHealth,
    failures
  };
}

async function readState(statePath) {
  if (!statePath) return { schemaVersion: 1, pipelines: {} };
  try {
    const state = JSON.parse(await readFile(statePath, "utf8"));
    if (state.schemaVersion !== 1 || typeof state.pipelines !== "object") {
      throw new Error("Unsupported CI quality monitor state format.");
    }
    return state;
  } catch (error) {
    if (error.code === "ENOENT") return { schemaVersion: 1, pipelines: {} };
    throw error;
  }
}

export function shouldRunAgent(dossier) {
  if (dossier.bootstrap) return false;
  const actionableHealth = dossier.pipelineHealth.filter(observation => observation.actionable).length;
  return dossier.failures.length + actionableHealth > 0;
}

async function writeGitHubOutputs(outputPath, dossier) {
  if (!outputPath) return;
  const delimiter = `CI_QUALITY_${Date.now()}`;
  const compactDossier = JSON.stringify(dossier);
  const actionableHealth = dossier.pipelineHealth.filter(observation => observation.actionable).length;
  await appendFile(outputPath, `should_run=${shouldRunAgent(dossier)}\n`);
  await appendFile(outputPath, `failure_count=${dossier.failures.length + actionableHealth}\n`);
  await appendFile(outputPath, `dossier<<${delimiter}\n${compactDossier}\n${delimiter}\n`);
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const registry = JSON.parse(await readFile(options.registry, "utf8"));
  const state = await readState(options.state);
  const dossier = await collectEvidence(registry, options["build-id"], state);
  await mkdir(path.dirname(options.output), { recursive: true });
  await writeFile(options.output, `${JSON.stringify(dossier, null, 2)}\n`);
  if (options["state-output"]) {
    await mkdir(path.dirname(options["state-output"]), { recursive: true });
    await writeFile(options["state-output"], `${JSON.stringify(state, null, 2)}\n`);
  }
  await writeGitHubOutputs(options["github-output"], dossier);
  console.log(`Collected ${dossier.failures.length} failed build dossier(s) in ${options.output}.`);
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(error => {
    console.error(error.stack ?? error.message);
    process.exitCode = 1;
  });
}