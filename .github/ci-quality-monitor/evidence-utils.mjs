import { MAX_LOG_CHARACTERS } from "./constants.mjs";

export function textLines(value) {
  return `${value ?? ""}`.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
}

export function sanitizeText(value, maxCharacters = MAX_LOG_CHARACTERS) {
  return `${value ?? ""}`
    .replace(/[0-9a-f]{8}-[0-9a-f-]{27,}/gi, "<guid>")
    .replace(/\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z\b/g, "<timestamp>")
    .replace(/[A-Za-z]:\\h\\w\\[^\r\n ]+/gi, "<helix-path>")
    .replace(/(?:[A-Za-z]:\\|\/)[^\r\n ]*(?:artifacts|tmp|temp)[^\r\n ]*/gi, "<temporary-path>")
    .slice(0, maxCharacters);
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

export function isFailedBuild(build) {
  return build.result === "failed" || build.result === "partiallySucceeded";
}