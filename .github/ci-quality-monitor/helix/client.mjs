import { MAX_CONSOLE_CHARACTERS, MAX_TEST_FAILURES } from "../constants.mjs";
import { createFailureSignature, sanitizeText } from "../evidence-utils.mjs";
import { createTestKbeCandidate } from "../known-build-error.mjs";
import { parseTestResultXml } from "../test-results.mjs";
import { HttpClient } from "../http-client.mjs";
import {
  classifyWorkItem,
  sharedTestMechanism,
  summarizeHelixConsole,
  summarizeTestMechanism
} from "./parsing.mjs";

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

export class HelixEvidenceClient {
  constructor(fetchImplementation = fetch) {
    this.http = new HttpClient(fetchImplementation);
  }

  async getText(url) {
    const text = await (await this.http.response(url)).text();
    return sanitizeText(text.slice(-MAX_CONSOLE_CHARACTERS), MAX_CONSOLE_CHARACTERS);
  }

  async getTestFailures(workItem) {
    const testFile = (workItem.Files ?? []).find(file => /\.(?:trx|xml)$/i.test(file.FileName));
    if (!testFile) return { summary: null, failures: [] };
    const response = await this.http.response(testFile.Uri);
    const results = parseTestResultXml(Buffer.from(await response.arrayBuffer()));
    return { ...results, failures: results.failures.slice(0, MAX_TEST_FAILURES) };
  }

  async collectObservation(reference) {
    const url = helixWorkItemUrl(reference);
    const workItem = await this.http.json(url);
    let consoleText = "";
    let testResults = { summary: null, failures: [] };
    const unavailable = [];
    try {
      consoleText = await this.getText(`${url}/console`);
    } catch (error) {
      unavailable.push(sanitizeText(error.message));
    }
    try {
      testResults = await this.getTestFailures(workItem);
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

  async collectObservations(timelineFailures, maxReferences = Number.POSITIVE_INFINITY) {
    const observations = [];
    for (const reference of getHelixReferences(timelineFailures).slice(0, maxReferences)) {
      try {
        observations.push(...await this.collectObservation(reference));
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
}
