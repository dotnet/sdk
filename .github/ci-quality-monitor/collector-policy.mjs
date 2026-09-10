import {getTimelineFailuresFromRecords as parseTimelineFailures} from "./azure/observations.mjs";
import {PIPELINE_HEARTBEAT_AGE_MS} from "./constants.mjs";
import {createBuildSummary, createFailureFingerprint} from "./evidence-utils.mjs";
import {parseHelixWorkItemReferences} from "./helix/parsing.mjs";

export function createHeartbeatObservation(pipeline, branch, head, builds, now = Date.now())
{
  const committedAt = Date.parse(head.committedAt);
  if (!Number.isFinite(committedAt) || now - committedAt < PIPELINE_HEARTBEAT_AGE_MS) return null;
  const covered = builds.some(build => build.sourceVersion === head.sha || Date.parse(build.queueTime) >= committedAt);
  if (covered) return null;
  const mechanism = `No ${pipeline.definitionId} build was queued for branch head ${head.sha}; the head was at least 90 minutes old when checked.`;
  return {
    kind: "pipeline-heartbeat",
    phase: "pipeline-scheduling",
    failureType: "missing-execution",
    evidenceSources: ["github-branch", "azure-build-history"],
    component: `${pipeline.repository}:${branch}`,
    mechanism,
    fingerprint: createFailureFingerprint({
      phase: "pipeline-scheduling",
      failureType: "missing-execution",
      component: pipeline.definitionId,
      mechanism: branch
    }),
    actionable: false,
    branch,
    branchHead: head,
    latestBuild: builds[0] ? createBuildSummary(builds[0]) : null
  };
}

export function recordHeartbeatCheck(state, key, observation)
{
  const previousMisses = state.pipelines[key]?.heartbeatMisses ?? 0;
  const heartbeatMisses = observation ? previousMisses + 1 : 0;
  state.pipelines[key] = {...state.pipelines[key], heartbeatMisses};
  if (!observation) return null;
  return {...observation, missedChecks: heartbeatMisses, actionable: heartbeatMisses >= 2};
}

export function matchesPipeline(build, pipeline)
{
  return build.definition?.id === pipeline.definitionId
    && build.repository?.id?.toLowerCase() === pipeline.repository.toLowerCase();
}

export function isRegisteredBuild(build, pipeline)
{
  return matchesPipeline(build, pipeline)
    && pipeline.branches.includes(build.sourceBranch)
    && build.reason?.toLowerCase() !== "pullrequest";
}

export function isPullRequestBuild(build, pipeline)
{
  return matchesPipeline(build, pipeline)
    && build.reason?.toLowerCase() === "pullrequest"
    && /^refs\/pull\/\d+\/merge$/.test(build.sourceBranch);
}

export function isStableBranchBuild(build, pipeline)
{
  return matchesPipeline(build, pipeline)
    && (pipeline.stableBranches ?? []).includes(build.sourceBranch)
    && build.reason?.toLowerCase() !== "pullrequest";
}

export function getTimelineFailuresFromRecords(records = [])
{
  return parseTimelineFailures(records, parseHelixWorkItemReferences);
}

export function applyKbeRecurrence(observations, relatedFailureSummaries, currentBuild = null)
{
  const currentPullRequest = /^refs\/pull\/\d+\/merge$/.test(currentBuild?.branch ?? "")
    ? currentBuild.branch
    : null;
  const relatedTests = relatedFailureSummaries.flatMap(summary =>
    (summary.build?.commit === currentBuild?.commit || summary.build?.branch === currentPullRequest
      ? []
      : summary.observations ?? [])
      .filter(observation => observation.kind === "test")
      .map(observation => ({observation, build: summary.build})));
  return observations.map(observation =>
  {
    if (observation.kind !== "test" || !observation.kbe) return observation;
    const matches = relatedTests.filter(candidate => candidate.observation.component === observation.component
      && candidate.observation.mechanismFingerprint === observation.mechanismFingerprint);
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
