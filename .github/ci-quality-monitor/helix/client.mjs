import {MAX_CONSOLE_CHARACTERS, MAX_TEST_FAILURES} from "../constants.mjs";
import {
  createFailureFingerprint,
  isAuthenticationFailure,
  isNetworkFailure,
  normalizeEvidenceText
} from "../evidence-utils.mjs";
import {HttpClient} from "../http-client.mjs";
import {createTestKbeCandidate} from "../known-build-error.mjs";
import {parseTestResultXml} from "../test-results.mjs";
import
{
  classifyWorkItem,
  summarizeHelixConsole,
  summarizeSharedTestMechanism,
  summarizeTestMechanism
} from "./parsing.mjs";

function getUniqueHelixReferences(timelineFailures)
{
  const references = timelineFailures.flatMap(failure => failure.helixReferences ?? []);
  return [...new Map(references.map(reference => [`${reference.jobId}:${reference.workItem}`, reference])).values()];
}

function helixWorkItemUrl(reference)
{
  return `https://helix.dot.net/api/2019-06-17/jobs/${encodeURIComponent(reference.jobId)}/workitems/${encodeURIComponent(reference.workItem)}`;
}

function selectArtifactLinks(files = [])
{
  return files
    .filter(file => /\.(?:trx|xml|binlog|dmp|core|crash|log)$/i.test(file.FileName))
    .slice(0, 10)
    .map(file => ({name: file.FileName, url: file.Uri}));
}

export function getArtifactEvidenceSources(files = [])
{
  const sources = [];
  if (files.some(file => /\.(?:trx|xml)$/i.test(file.FileName))) sources.push("helix-trx");
  if (files.some(file => /\.(?:dmp|core|crash)$/i.test(file.FileName))) sources.push("helix-dump");
  return sources;
}

function createTestObservation(reference, test, testSummary)
{
  const component = test.fullyQualifiedName || test.testName;
  const mechanism = summarizeTestMechanism(test.errorMessage, test.outcome);
  const sharedMechanism = summarizeSharedTestMechanism(test.errorMessage, test.outcome);
  const phase = "test-execution";
  const failureType = classifyTestFailureType(test.errorMessage, test.outcome);
  const fingerprint = createFailureFingerprint({phase, failureType, component, mechanism});
  return {
    kind: "test",
    phase,
    failureType,
    evidenceSources: ["helix-trx"],
    component,
    mechanism,
    fingerprint,
    mechanismFingerprint: createFailureFingerprint({
      phase, failureType, component: "shared", mechanism: sharedMechanism
    }),
    actionable: true,
    workItem: reference.workItem,
    jobId: reference.jobId,
    queue: reference.queue,
    outcome: test.outcome,
    duration: test.duration,
    testSummary,
    stackTrace: normalizeEvidenceText(test.stackTrace),
    kbe: createTestKbeCandidate(test, fingerprint)
  };
}

function classifyTestFailureType(errorMessage, outcome)
{
  const text = `${errorMessage ?? ""}`;
  if (`${outcome}`.toLowerCase() === "timeout") return "timeout";
  if (`${outcome}`.toLowerCase() === "aborted") return "process-termination";
  if (isAuthenticationFailure(text)) return "authentication-failure";
  if (isNetworkFailure(text)) return "network-failure";
  if (/timed? ?out|timeout/i.test(text)) return "timeout";
  if (/segmentation fault|stack overflow|core dump|app_crash/i.test(text)) return "process-crash";
  if (/\bCS\d{4}\b/i.test(text)) return "compiler-error";
  return "test-assertion";
}

function createWorkItemObservation(reference, workItem, consoleText, testResults, unavailable)
{
  const classification = classifyWorkItem(workItem.ExitCode ?? reference.exitCode, consoleText);
  const consoleSummary = summarizeHelixConsole(consoleText);
  const causalConsoleLines = consoleSummary.hangEvidence.filter(line => line === consoleSummary.activeTest
    || /still running|hang timeout|timed? ?out|test host crashed|recovered \d+ test result|exit code/i.test(line));
  const mechanismLines = causalConsoleLines.length > 0
    ? causalConsoleLines
    : consoleText.split(/\r?\n/).filter(Boolean).slice(-8);
  const mechanism = mechanismLines.join("\n") || `Exit code ${workItem.ExitCode ?? reference.exitCode}`;
  const artifacts = selectArtifactLinks(workItem.Files);
  return {
    kind: "helix-work-item",
    ...classification,
    evidenceSources: [...new Set([...classification.evidenceSources, ...getArtifactEvidenceSources(workItem.Files)])],
    component: reference.workItem,
    mechanism,
    fingerprint: createFailureFingerprint({...classification, component: reference.workItem, mechanism}),
    actionable: classification.failureType !== "infrastructure-unavailable",
    jobId: reference.jobId,
    queue: reference.queue,
    exitCode: workItem.ExitCode ?? reference.exitCode,
    state: workItem.State,
    machine: workItem.MachineName,
    duration: workItem.Duration,
    testSummary: testResults.summary,
    consoleSummary,
    consoleUrl: workItem.ConsoleOutputUri,
    artifacts,
    unavailable
  };
}

export class HelixEvidenceClient
{
  constructor(fetchImplementation = fetch)
  {
    this.http = new HttpClient(fetchImplementation);
  }

  async getConsoleEvidence(url)
  {
    const text = await (await this.http.response(url)).text();
    return normalizeEvidenceText(text.slice(-MAX_CONSOLE_CHARACTERS), MAX_CONSOLE_CHARACTERS);
  }

  async getTestResults(workItem)
  {
    const files = workItem.Files ?? [];
    const testFile = files.find(file => /\.trx$/i.test(file.FileName))
      ?? files.find(file => /\.xml$/i.test(file.FileName));
    if (!testFile) return {summary: null, failures: []};
    const response = await this.http.response(testFile.Uri);
    const results = parseTestResultXml(Buffer.from(await response.arrayBuffer()));
    return {...results, failures: results.failures.slice(0, MAX_TEST_FAILURES)};
  }

  async collectWorkItemObservations(reference)
  {
    const url = helixWorkItemUrl(reference);
    const workItem = await this.http.json(url);
    let consoleText = "";
    let testResults = {summary: null, failures: []};
    const unavailable = [];
    try
    {
      consoleText = await this.getConsoleEvidence(`${url}/console`);
    } catch (error)
    {
      unavailable.push(normalizeEvidenceText(error.message));
    }
    try
    {
      testResults = await this.getTestResults(workItem);
    } catch (error)
    {
      unavailable.push(normalizeEvidenceText(error.message));
    }
    const testObservations = testResults.failures
      .map(test => createTestObservation(reference, test, testResults.summary));
    const workItemObservation = createWorkItemObservation(
      reference, workItem, consoleText, testResults, unavailable);
    if (testObservations.length === 0) return [workItemObservation];
    const independentlyClassified = workItemObservation.failureType !== "unknown-error"
      && !testObservations.some(observation => observation.failureType === workItemObservation.failureType);
    return independentlyClassified ? [...testObservations, workItemObservation] : testObservations;
  }

  async collectObservations(timelineFailures, maxReferences = Number.POSITIVE_INFINITY)
  {
    const observations = [];
    for (const reference of getUniqueHelixReferences(timelineFailures).slice(0, maxReferences))
    {
      try
      {
        observations.push(...await this.collectWorkItemObservations(reference));
      } catch (error)
      {
        observations.push({
          kind: "helix-work-item",
          phase: "test-execution",
          failureType: "evidence-unavailable",
          evidenceSources: ["helix-api"],
          component: reference.workItem,
          mechanism: normalizeEvidenceText(error.message),
          fingerprint: createFailureFingerprint({
            phase: "test-execution",
            failureType: "evidence-unavailable",
            component: reference.workItem,
            mechanism: error.message
          }),
          actionable: false,
          ...reference
        });
      }
    }
    return observations;
  }
}
