import {MAX_TIMELINE_FAILURES} from "../constants.mjs";
import {
  createFailureFingerprint,
  isAuthenticationFailure,
  isNetworkFailure,
  normalizeEvidenceText,
  splitNonEmptyLines
} from "../evidence-utils.mjs";

export function classifyTaskFailure(name, issues = [])
{
  const text = `${name}\n${issues.join("\n")}`;
  const diagnosticCode = text.match(/\b(?:MSB|NETSDK|CS|NU)\d{4}\b/i)?.[0]?.toUpperCase() ?? null;
  if (/artifact (?:was )?not found|download previous build|missing artifact/i.test(text))
  {
    return {phase: "artifact-transfer", failureType: "artifact-missing", diagnosticCode};
  }
  if (/yaml|pipeline validation|unexpected value|mapping was not expected|template expression/i.test(text))
  {
    return {phase: "pipeline-validation", failureType: "configuration-error", diagnosticCode};
  }
  if (/monitor helix jobs|send to helix|testbuild tests/i.test(text))
  {
    return {phase: "test-orchestration", failureType: "downstream-failure", diagnosticCode};
  }
  if (isAuthenticationFailure(text))
  {
    return {phase: inferTaskPhase(name, text), failureType: "authentication-failure", diagnosticCode};
  }
  if (isNetworkFailure(text))
  {
    return {phase: inferTaskPhase(name, text), failureType: "network-failure", diagnosticCode};
  }
  if (/checkout|couldn't find remote ref|repository not found/i.test(text))
  {
    return {phase: "source-checkout", failureType: "source-unavailable", diagnosticCode};
  }
  if (diagnosticCode?.startsWith("CS"))
  {
    return {phase: "compilation", failureType: "compiler-error", diagnosticCode};
  }
  if (/exec format error|cannot execute binary|signing failed|signtool|sn\.exe/i.test(text))
  {
    return {phase: "signing", failureType: "tool-execution-error", diagnosticCode};
  }
  if (/\bNU19\d{2}\b|known (?:low|moderate|high|critical) severity vulnerability/i.test(text))
  {
    return {phase: "dependency-restore", failureType: "package-policy-error", diagnosticCode};
  }
  if (diagnosticCode?.startsWith("NU") || /restore|nuget|feed/i.test(text))
  {
    return {phase: "dependency-restore", failureType: "package-resolution-error", diagnosticCode};
  }
  if (/initialize container|install|acquire|setup/i.test(text))
  {
    return {phase: "environment-setup", failureType: "tool-execution-error", diagnosticCode};
  }
  if (/\b(?:MSB\d{4}|NETSDK\d{4})\b|\bbuild\b|compile/i.test(text))
  {
    return {phase: "compilation", failureType: "build-task-error", diagnosticCode};
  }
  if (/test/i.test(text)) return {phase: "test-execution", failureType: "unknown-error", diagnosticCode};
  return {phase: "unknown", failureType: "unknown-error", diagnosticCode};
}

function inferTaskPhase(name, text)
{
  if (/checkout/i.test(name)) return "source-checkout";
  if (/restore|nuget|feed|NuGet\.targets/i.test(text)) return "dependency-restore";
  if (/sign|sn\.exe/i.test(text)) return "signing";
  if (/test|helix/i.test(name)) return "test-execution";
  if (/build|compile/i.test(name)) return "compilation";
  if (/install|acquire|setup|initialize/i.test(name)) return "environment-setup";
  return "unknown";
}

function timelinePath(record, recordsById)
{
  const names = [record.name];
  let parentId = record.parentId;
  while (parentId && recordsById.has(parentId))
  {
    const parent = recordsById.get(parentId);
    names.unshift(parent.name);
    parentId = parent.parentId;
  }
  return names;
}

export function getTimelineFailuresFromRecords(records = [], parseHelixReferences = () => [])
{
  const recordsById = new Map(records.map(record => [record.id, record]));
  return records
    .filter(record => record.result === "failed" || record.result === "partiallySucceeded")
    .filter(record => record.type === "Job" || record.type === "Task")
    .slice(0, MAX_TIMELINE_FAILURES)
    .map(record =>
    {
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

function summarizeTaskLog(logText)
{
  const lines = splitNonEmptyLines(logText);
  const diagnostics = lines.filter(line => /\b(?:MSB\d{4}|NETSDK\d{4}|CS\d{4})\b|response status|unable to|service unavailable|timed? ?out|connection refused|exec format error/i.test(line));
  const fallback = lines.filter(line => /\b(?:error|fatal|exception|failed)\b/i.test(line) && !/\bat\s+\S+\(/i.test(line));
  return [...new Set((diagnostics.length > 0 ? diagnostics : fallback).slice(-8))];
}

function selectTaskMechanism(evidence, taskName)
{
  const usable = evidence
    .filter(line => !/back off .* before retry/i.test(line))
    .filter(line => !/^Bash exited with code/i.test(line));
  const ranked = [
    usable.filter(line => /\b(?:MSB|NETSDK|CS|NU)\d{4}\b/i.test(line)),
    usable.filter(line => /\b(?:fatal|error|exception)\b/i.test(line)),
    usable
  ];
  const candidates = ranked.find(group => group.length > 0) ?? [];
  return candidates
    .map(line => normalizeEvidenceText(line))
    .sort((left, right) => left.localeCompare(right))[0]
    ?? taskName;
}

export function createTaskObservations(timelineFailures, logsById = new Map())
{
  return timelineFailures
    .filter(failure => failure.type === "Task")
    .map(failure =>
    {
      const logExcerpt = summarizeTaskLog(logsById.get(failure.logId));
      const evidence = [...failure.issues, ...logExcerpt];
      const classification = classifyTaskFailure(failure.name, evidence);
      const mechanism = selectTaskMechanism(evidence, failure.name);
      return {
        kind: "pipeline-task",
        ...classification,
        evidenceSources: ["azure-timeline", ...(logExcerpt.length > 0 ? ["azure-task-log"] : [])],
        component: failure.name,
        mechanism,
        fingerprint: createFailureFingerprint({
          ...classification, component: failure.name, mechanism
        }),
        actionable: evidence.length > 0
          && classification.failureType !== "artifact-missing"
          && classification.failureType !== "downstream-failure"
          && classification.failureType !== "unknown-error",
        path: failure.path,
        issues: failure.issues,
        logExcerpt,
        logId: failure.logId,
        logUrl: failure.logUrl
      };
    });
}

export function createPipelineObservation(build, timelineRecords = [])
{
  const validations = (build.validationResults ?? [])
    .filter(validation => `${validation.result ?? ""}`.toLowerCase() !== "ok")
    .map(validation => normalizeEvidenceText(validation.message ?? validation.result));
  if (validations.length === 0 && timelineRecords.length > 0) return null;
  const mechanism = validations.join("\n") || "Pipeline failed without creating stages, jobs, or tasks.";
  const phase = validations.length > 0 ? "pipeline-validation" : "pipeline-startup";
  const failureType = validations.length > 0 ? "configuration-error" : "missing-execution";
  return {
    kind: "pipeline",
    phase,
    failureType,
    evidenceSources: [validations.length > 0 ? "azure-validation" : "azure-timeline"],
    component: build.definition?.name ?? "Azure DevOps pipeline",
    mechanism,
    fingerprint: createFailureFingerprint({
      phase, failureType, component: build.definition?.name ?? "pipeline", mechanism
    }),
    actionable: validations.length > 0,
    validationResults: validations
  };
}
