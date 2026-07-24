import { MAX_PROCESSED_BUILD_IDS } from "./constants.mjs";
import { buildConsumptionKey, isFailedBuild } from "./evidence-utils.mjs";

export function stateKey(pipeline, branch) {
  return `${pipeline.organization}/${pipeline.project}/${pipeline.definitionId}:${branch}`;
}

export function auditConsumptionKey(build, monitoringCategory, contextIdentity) {
  return `${buildConsumptionKey(build)}|${monitoringCategory}:${contextIdentity}`;
}

function auditStateKey(pipeline) {
  return stateKey(pipeline, "audit-contexts");
}

export function isAuditConsumed(state, pipeline, auditKey) {
  return (state.pipelines[auditStateKey(pipeline)]?.consumedAuditKeys ?? []).includes(auditKey);
}

export function consumeAudit(state, pipeline, auditKey) {
  const key = auditStateKey(pipeline);
  const previous = state.pipelines[key] ?? {};
  state.pipelines[key] = {
    ...previous,
    consumedAuditKeys: [...new Set([auditKey, ...(previous.consumedAuditKeys ?? [])])]
      .slice(0, MAX_PROCESSED_BUILD_IDS),
    lastCheckedAt: new Date().toISOString()
  };
}

export function updateState(state, key, history) {
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
  const failures = unprocessed.filter(isFailedBuild);
  return { bootstrap: !previous, failures: previous ? failures : failures.slice(0, 1) };
}
