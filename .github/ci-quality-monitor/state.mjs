import {MAX_PROCESSED_BUILD_KEYS} from "./constants.mjs";
import {createBuildAttemptKey, isFailedBuild} from "./evidence-utils.mjs";

export function createPipelineStateKey(pipeline, branch)
{
  return `${pipeline.organization}/${pipeline.project}/${pipeline.definitionId}:${branch}`;
}

export function createAuditKey(build, monitoringScope, contextIdentity)
{
  return `${createBuildAttemptKey(build)}|${monitoringScope}:${contextIdentity}`;
}

function createAuditStateKey(pipeline)
{
  return createPipelineStateKey(pipeline, "audit-contexts");
}

export function isAuditProcessed(state, pipeline, auditKey)
{
  return (state.pipelines[createAuditStateKey(pipeline)]?.processedAuditKeys ?? []).includes(auditKey);
}

export function markAuditProcessed(state, pipeline, auditKey)
{
  const key = createAuditStateKey(pipeline);
  const previous = state.pipelines[key] ?? {};
  state.pipelines[key] = {
    ...previous,
    processedAuditKeys: [...new Set([auditKey, ...(previous.processedAuditKeys ?? [])])]
      .slice(0, MAX_PROCESSED_BUILD_KEYS),
    lastCheckedAt: new Date().toISOString()
  };
}

export function recordProcessedBuilds(state, key, history)
{
  const previous = state.pipelines[key] ?? {};
  const previousKeys = previous.processedBuildKeys ?? [];
  const processedBuildKeys = [...new Set([...history.map(createBuildAttemptKey), ...previousKeys])]
    .slice(0, MAX_PROCESSED_BUILD_KEYS);
  state.pipelines[key] = {...previous, processedBuildKeys, lastCheckedAt: new Date().toISOString()};
}

export function selectUnprocessedFailures(state, key, history)
{
  const previous = state.pipelines[key];
  const processedKeys = new Set(previous?.processedBuildKeys ?? []);
  const unprocessed = previous
    ? history.filter(build => !processedKeys.has(createBuildAttemptKey(build)))
    : history;
  const failures = unprocessed.filter(isFailedBuild);
  return {bootstrap: !previous, failures: previous ? failures : failures.slice(0, 1)};
}
