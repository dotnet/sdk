import { MAX_PROCESSED_BUILD_IDS } from "./constants.mjs";
import { createBuildProcessingKey, isFailedBuild } from "./evidence-utils.mjs";

export function stateKey(pipeline, branch) {
  return `${pipeline.organization}/${pipeline.project}/${pipeline.definitionId}:${branch}`;
}

export function createAuditProcessingKey(build, monitoringCategory, contextIdentity) {
  return `${createBuildProcessingKey(build)}|${monitoringCategory}:${contextIdentity}`;
}

function auditStateKey(pipeline) {
  return stateKey(pipeline, "audit-contexts");
}

export function isAuditProcessed(state, pipeline, auditKey) {
  return (state.pipelines[auditStateKey(pipeline)]?.processedAuditKeys ?? []).includes(auditKey);
}

export function markAuditProcessed(state, pipeline, auditKey) {
  const key = auditStateKey(pipeline);
  const previous = state.pipelines[key] ?? {};
  state.pipelines[key] = {
    ...previous,
    processedAuditKeys: [...new Set([auditKey, ...(previous.processedAuditKeys ?? [])])]
      .slice(0, MAX_PROCESSED_BUILD_IDS),
    lastCheckedAt: new Date().toISOString()
  };
}

export function recordProcessedBuilds(state, key, history) {
  const previous = state.pipelines[key] ?? {};
  const previousKeys = previous.processedBuildKeys ?? [];
  const processedBuildKeys = [...new Set([...history.map(createBuildProcessingKey), ...previousKeys])]
    .slice(0, MAX_PROCESSED_BUILD_IDS);
  const { processedBuildIds: _legacyProcessedBuildIds, ...existing } = previous;
  state.pipelines[key] = { ...existing, processedBuildKeys, lastCheckedAt: new Date().toISOString() };
}

export function selectUnprocessedFailures(state, key, history) {
  const previous = state.pipelines[key];
  const processedKeys = new Set(previous?.processedBuildKeys ?? []);
  const legacyProcessedIds = new Set(previous?.processedBuildIds ?? []);
  const unprocessed = previous ? history.filter(build => processedKeys.size > 0
    ? !processedKeys.has(createBuildProcessingKey(build))
    : !legacyProcessedIds.has(build.id)) : history;
  const failures = unprocessed.filter(isFailedBuild);
  return { bootstrap: !previous, failures: previous ? failures : failures.slice(0, 1) };
}
