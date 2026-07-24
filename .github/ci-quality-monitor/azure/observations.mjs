import { MAX_TIMELINE_FAILURES } from "../constants.mjs";
import { createFailureFingerprint, normalizeEvidenceText, splitNonEmptyLines } from "../evidence-utils.mjs";

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

export function getTimelineFailuresFromRecords(records = [], parseHelixReferences = () => []) {
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
        helixReferences: parseHelixReferences(messages),
        issues: messages.map(message => normalizeEvidenceText(message))
      };
    });
}

function summarizeTaskLog(logText) {
  const lines = splitNonEmptyLines(logText);
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
      const mechanism = evidence.find(issue => /\b(?:fatal|error\b|MSB\d{4}|NETSDK\d{4}|CS\d{4})/i.test(issue)
        && !/back off .* before retry/i.test(issue))
        ?? evidence.find(issue => !/^Bash exited with code/i.test(issue) && !/back off .* before retry/i.test(issue))
        ?? failure.name;
      return {
        kind: "pipeline-task",
        category,
        component: failure.name,
        mechanism,
        fingerprint: createFailureFingerprint(category, failure.name, mechanism),
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
    .map(validation => normalizeEvidenceText(validation.message ?? validation.result));
  if (validations.length === 0 && timelineRecords.length > 0) return null;
  const category = validations.length > 0 ? "pipeline-configuration" : "pipeline-startup";
  const mechanism = validations.join("\n") || "Pipeline failed without creating stages, jobs, or tasks.";
  return {
    kind: "pipeline",
    category,
    component: build.definition?.name ?? "Azure DevOps pipeline",
    mechanism,
    fingerprint: createFailureFingerprint(category, build.definition?.name ?? "pipeline", mechanism),
    actionable: validations.length > 0,
    validationResults: validations
  };
}