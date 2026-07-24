import { appendFile, mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { pathToFileURL } from "node:url";

const API_VERSION = "7.1";
const DEFAULT_BUILD_LIMIT = 20;
const MAX_FAILURES = 10;
const MAX_LOG_CHARACTERS = 4_000;
const MAX_PROCESSED_BUILD_IDS = 100;

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

export function sanitizeText(value) {
  return `${value ?? ""}`
    .replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/gi, "<guid>")
    .replace(/\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z\b/g, "<timestamp>")
    .replace(/(?:[A-Za-z]:\\|\/)[^\r\n ]*(?:artifacts|tmp|temp)[^\r\n ]*/gi, "<temporary-path>")
    .slice(0, MAX_LOG_CHARACTERS);
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

async function getTimelineFailures(pipeline, buildId, fetchImplementation = fetch) {
  const url = `${buildApiBase(pipeline)}/build/builds/${buildId}/timeline?api-version=${API_VERSION}`;
  const timeline = await fetchJson(url, fetchImplementation);
  return (timeline.records ?? [])
    .filter(record => record.result === "failed" || record.result === "partiallySucceeded")
    .filter(record => record.type === "Job" || record.type === "Task")
    .slice(0, MAX_FAILURES)
    .map(record => ({
      type: record.type,
      name: record.name,
      result: record.result,
      logId: record.log?.id,
      issues: (record.issues ?? []).map(issue => sanitizeText(issue.message))
    }));
}

async function getFailureLog(pipeline, buildId, logId, fetchImplementation = fetch) {
  if (!logId) return null;
  const url = `${buildApiBase(pipeline)}/build/builds/${buildId}/logs/${logId}?api-version=${API_VERSION}`;
  const response = await fetchResponse(url, fetchImplementation);
  return sanitizeText(await response.text());
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
      related.push({
        build: normalizeBuild(build),
        timelineFailures: await getTimelineFailures(pipeline, build.id, fetchImplementation)
      });
    } catch (error) {
      related.push({ build: normalizeBuild(build), unavailable: sanitizeText(error.message) });
    }
  }
  return related;
}

async function collectFailureEvidence(pipeline, build, history, fetchImplementation = fetch) {
  const timelineFailures = await getTimelineFailures(pipeline, build.id, fetchImplementation);
  const relatedFailureSummaries = await getRelatedFailureSummaries(
    pipeline,
    build.id,
    history,
    fetchImplementation);
  const logFailures = [];
  for (const failure of timelineFailures.filter(candidate => candidate.logId).slice(0, 3)) {
    try {
      logFailures.push({ name: failure.name, text: await getFailureLog(pipeline, build.id, failure.logId, fetchImplementation) });
    } catch (error) {
      logFailures.push({ name: failure.name, unavailable: sanitizeText(error.message) });
    }
  }
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
      if (isRegisteredBuild(build, pipeline)) return { pipeline, build };
    } catch (error) {
      if (!error.message.includes("returned 404")) throw error;
    }
  }
  throw new Error(`Build ${buildId} is not a non-PR build in the pipeline registry.`);
}

function stateKey(pipeline, branch) {
  return `${pipeline.organization}/${pipeline.project}/${pipeline.definitionId}:${branch}`;
}

function updateState(state, key, history) {
  const previousIds = state.pipelines[key]?.processedBuildIds ?? [];
  const processedBuildIds = [...new Set([...history.map(build => build.id), ...previousIds])]
    .slice(0, MAX_PROCESSED_BUILD_IDS);
  state.pipelines[key] = { processedBuildIds, lastCheckedAt: new Date().toISOString() };
}

function selectUnprocessedFailures(state, key, history) {
  const previous = state.pipelines[key];
  const processedIds = new Set(previous?.processedBuildIds ?? []);
  const unprocessed = previous ? history.filter(build => !processedIds.has(build.id)) : history;
  const failures = unprocessed.filter(build => build.result === "failed" || build.result === "partiallySucceeded");
  return { bootstrap: !previous, failures: previous ? failures : failures.slice(0, 1) };
}

async function collectCandidates(registry, buildId, state, fetchImplementation = fetch) {
  if (buildId) {
    const selected = await selectManualBuild(registry.pipelines, buildId, fetchImplementation);
    const history = await listCompletedBuilds(selected.pipeline, selected.build.sourceBranch, fetchImplementation);
    return { candidates: [{ ...selected, history }], bootstrap: false };
  }
  const candidates = [];
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
      updateState(state, key, history);
    }
  }
  return { candidates, bootstrap };
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

async function writeGitHubOutputs(outputPath, dossier) {
  if (!outputPath) return;
  const delimiter = `CI_QUALITY_${Date.now()}`;
  const compactDossier = JSON.stringify(dossier);
  await appendFile(outputPath, `should_run=${dossier.failures.length > 0}\n`);
  await appendFile(outputPath, `failure_count=${dossier.failures.length}\n`);
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