import { AzureDevOpsClient } from "./azure/client.mjs";
import {
  createPipelineObservation,
  createTaskObservations,
  getTimelineFailuresFromRecords as parseTimelineFailures
} from "./azure/observations.mjs";
import {
  MAX_RELATED_BUILDS,
  MAX_RELATED_HELIX_REFERENCES,
  MAX_TASK_LOGS,
  PIPELINE_HEARTBEAT_AGE_MS
} from "./constants.mjs";
import {
  buildConsumptionKey,
  createFailureSignature,
  isFailedBuild,
  normalizeBuild,
  sanitizeText
} from "./evidence-utils.mjs";
import { getGitHubBranchHead } from "./github/client.mjs";
import { HelixClient } from "./helix/client.mjs";
import { parseHelixWorkItemReferences } from "./helix/parsing.mjs";
import { selectUnprocessedFailures, stateKey, updateState } from "./state.mjs";

/** @typedef {import("./types.d.ts").CandidateSelection} CandidateSelection */
/** @typedef {import("./types.d.ts").CollectionDossier} CollectionDossier */
/** @typedef {import("./types.d.ts").Pipeline} Pipeline */

export function createHeartbeatObservation(pipeline, branch, head, builds, now = Date.now()) {
  const committedAt = Date.parse(head.committedAt);
  if (!Number.isFinite(committedAt) || now - committedAt < PIPELINE_HEARTBEAT_AGE_MS) return null;
  const covered = builds.some(build => build.sourceVersion === head.sha || Date.parse(build.queueTime) >= committedAt);
  if (covered) return null;
  const mechanism = `No ${pipeline.definitionId} build was queued for branch head ${head.sha} within 90 minutes.`;
  return {
    kind: "pipeline-heartbeat",
    category: "pipeline-not-triggered",
    component: `${pipeline.repository}:${branch}`,
    mechanism,
    signature: createFailureSignature("pipeline-not-triggered", pipeline.definitionId, branch),
    actionable: false,
    branch,
    branchHead: head,
    latestBuild: builds[0] ? normalizeBuild(builds[0]) : null
  };
}

function updateHeartbeatState(state, key, observation) {
  const previousMisses = state.pipelines[key]?.heartbeatMisses ?? 0;
  const heartbeatMisses = observation ? previousMisses + 1 : 0;
  state.pipelines[key] = { ...state.pipelines[key], heartbeatMisses };
  if (!observation) return null;
  return { ...observation, missedChecks: heartbeatMisses, actionable: heartbeatMisses >= 2 };
}

function matchesPipeline(build, pipeline) {
  return build.definition?.id === pipeline.definitionId
    && build.repository?.id?.toLowerCase() === pipeline.repository.toLowerCase();
}

function isRegisteredBuild(build, pipeline) {
  return matchesPipeline(build, pipeline)
    && pipeline.branches.includes(build.sourceBranch)
    && build.reason?.toLowerCase() !== "pullrequest";
}

function isPullRequestBuild(build, pipeline) {
  return matchesPipeline(build, pipeline)
    && build.reason?.toLowerCase() === "pullrequest"
    && /^refs\/pull\/\d+\/merge$/.test(build.sourceBranch);
}

export function getTimelineFailuresFromRecords(records = []) {
  return parseTimelineFailures(records, parseHelixWorkItemReferences);
}

export function applyKbeRecurrence(observations, relatedFailureSummaries) {
  const relatedTests = relatedFailureSummaries.flatMap(summary =>
    (summary.observations ?? []).filter(observation => observation.kind === "test")
      .map(observation => ({ observation, build: summary.build })));
  return observations.map(observation => {
    if (observation.kind !== "test" || !observation.kbe) return observation;
    const matches = relatedTests.filter(candidate => candidate.observation.component === observation.component
      && candidate.observation.mechanismSignature === observation.mechanismSignature);
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

export class EvidenceCollector {
  /**
   * @param {{ pipelines: Pipeline[] }} registry
   * @param {{ schemaVersion: number, pipelines: Record<string, object> }} state
   * @param {typeof fetch} [fetchImplementation]
   */
  constructor(registry, state, fetchImplementation = fetch) {
    this.registry = registry;
    this.state = state;
    this.fetch = fetchImplementation;
    this.azureClients = new Map();
    this.helix = new HelixClient(fetchImplementation);
  }

  azure(pipeline) {
    if (!this.azureClients.has(pipeline)) {
      this.azureClients.set(pipeline, new AzureDevOpsClient(pipeline, this.fetch));
    }
    return this.azureClients.get(pipeline);
  }

  async getRelatedFailureSummaries(pipeline, buildId, history) {
    const related = [];
    const failedBuilds = history
      .filter(build => build.id !== buildId && isFailedBuild(build))
      .slice(0, MAX_RELATED_BUILDS);
    for (const build of failedBuilds) {
      try {
        const timeline = await this.azure(pipeline).getTimeline(build.id);
        const timelineFailures = getTimelineFailuresFromRecords(timeline.records);
        related.push({
          build: normalizeBuild(build),
          timelineFailures,
          observations: await this.helix.collectObservations(timelineFailures, MAX_RELATED_HELIX_REFERENCES)
        });
      } catch (error) {
        related.push({ build: normalizeBuild(build), unavailable: sanitizeText(error.message) });
      }
    }
    return related;
  }

  async collectFailureEvidence(pipeline, build, history) {
    const azure = this.azure(pipeline);
    const detailedBuild = build.validationResults ? build : await azure.getBuild(build.id);
    const timeline = await azure.getTimeline(build.id);
    const timelineFailures = getTimelineFailuresFromRecords(timeline.records);
    const pipelineObservation = createPipelineObservation(detailedBuild, timeline.records ?? []);
    const helixObservations = await this.helix.collectObservations(timelineFailures);
    const relatedFailureSummaries = await this.getRelatedFailureSummaries(pipeline, build.id, history);
    const logFailures = await this.collectFailureLogs(azure, build.id, timelineFailures);
    const taskObservations = createTaskObservations(
      timelineFailures,
      new Map(logFailures.filter(failure => failure.text).map(failure => [failure.logId, failure.text])));
    let testFailures = [];
    try {
      testFailures = await azure.getTestFailures(build.id);
    } catch (error) {
      testFailures = [{ unavailable: sanitizeText(error.message) }];
    }
    return {
      pipeline,
      build: normalizeBuild(build),
      recentBuilds: history.map(normalizeBuild),
      observations: applyKbeRecurrence(
        [pipelineObservation, ...taskObservations, ...helixObservations].filter(Boolean),
        relatedFailureSummaries),
      timelineFailures,
      relatedFailureSummaries,
      testFailures,
      logFailures
    };
  }

  async collectFailureLogs(azure, buildId, timelineFailures) {
    const logs = [];
    const failedTasks = [...new Map(
      timelineFailures.filter(candidate => candidate.type === "Task" && candidate.logId)
        .map(candidate => [candidate.logId, candidate])).values()].slice(0, MAX_TASK_LOGS);
    for (const failure of failedTasks) {
      try {
        logs.push({
          name: failure.name,
          logId: failure.logId,
          text: await azure.getFailureLog(buildId, failure.logId, failure.logUrl)
        });
      } catch (error) {
        logs.push({ name: failure.name, unavailable: sanitizeText(error.message) });
      }
    }
    return logs;
  }

  async selectManualBuild(buildId) {
    for (const pipeline of this.registry.pipelines) {
      try {
        const build = await this.azure(pipeline).getBuild(buildId);
        if (matchesPipeline(build, pipeline)) return { pipeline, build };
      } catch (error) {
        if (!error.message.includes("returned 404")) throw error;
      }
    }
    throw new Error(`Build ${buildId} is not from a pipeline and repository in the registry.`);
  }

  async collectEventCandidate(buildId) {
    const selected = await this.selectManualBuild(buildId);
    if (!isPullRequestBuild(selected.build, selected.pipeline)) return emptySelection();
    const history = (await this.azure(selected.pipeline).listCompletedBuilds(selected.build.sourceBranch))
      .filter(build => isPullRequestBuild(build, selected.pipeline));
    const key = stateKey(selected.pipeline, selected.build.sourceBranch);
    const consumedKeys = new Set(this.state.pipelines[key]?.consumedBuildKeys ?? []);
    const candidate = !consumedKeys.has(buildConsumptionKey(selected.build)) && isFailedBuild(selected.build)
      ? selected.build
      : null;
    updateState(this.state, key, history);
    return {
      candidates: candidate ? [{ pipeline: selected.pipeline, build: candidate, history }] : [],
      bootstrap: false,
      pipelineHealth: []
    };
  }

  async collectEventCandidateByHead(headSha) {
    for (const pipeline of this.registry.pipelines) {
      const build = await this.azure(pipeline).findPullRequestBuildByHead(headSha);
      if (build) return this.collectEventCandidate(`${build.id}`);
    }
    return emptySelection();
  }

  /** @returns {Promise<CandidateSelection>} */
  async collectCandidates(buildId, eventBuildId, eventHeadSha) {
    if (buildId) {
      const selected = await this.selectManualBuild(buildId);
      if (selected.build.status?.toLowerCase() !== "completed") return emptySelection();
      const history = await this.azure(selected.pipeline).listCompletedBuilds(selected.build.sourceBranch);
      return { candidates: [{ ...selected, history }], bootstrap: false, pipelineHealth: [] };
    }
    if (eventBuildId) return this.collectEventCandidate(eventBuildId);
    if (eventHeadSha) return this.collectEventCandidateByHead(eventHeadSha);
    return this.collectScheduledCandidates();
  }

  /** @returns {Promise<CandidateSelection>} */
  async collectScheduledCandidates() {
    const candidates = [];
    const pipelineHealth = [];
    let bootstrap = false;
    for (const pipeline of this.registry.pipelines) {
      for (const branch of pipeline.branches) {
        const azure = this.azure(pipeline);
        const history = (await azure.listCompletedBuilds(branch)).filter(build => isRegisteredBuild(build, pipeline));
        const key = stateKey(pipeline, branch);
        const selected = selectUnprocessedFailures(this.state, key, history);
        bootstrap ||= selected.bootstrap;
        candidates.push(...selected.failures.map(build => ({ pipeline, build, history })));
        await this.collectHeartbeat(pipeline, branch, azure, key, pipelineHealth);
        updateState(this.state, key, history);
      }
    }
    return { candidates, bootstrap, pipelineHealth };
  }

  async collectHeartbeat(pipeline, branch, azure, key, pipelineHealth) {
    try {
      const [head, recentBuilds] = await Promise.all([
        getGitHubBranchHead(pipeline, branch, this.fetch),
        azure.listRecentBuilds(branch)
      ]);
      const heartbeat = createHeartbeatObservation(
        pipeline,
        branch,
        head,
        recentBuilds.filter(build => isRegisteredBuild(build, pipeline)));
      const trackedHeartbeat = updateHeartbeatState(this.state, key, heartbeat);
      if (trackedHeartbeat) pipelineHealth.push(trackedHeartbeat);
    } catch (error) {
      pipelineHealth.push({
        kind: "pipeline-heartbeat",
        category: "heartbeat-unavailable",
        component: `${pipeline.repository}:${branch}`,
        mechanism: sanitizeText(error.message),
        actionable: false
      });
    }
  }

  /** @returns {Promise<CollectionDossier>} */
  async collect(buildId, eventBuildId = null, eventHeadSha = null) {
    const selected = await this.collectCandidates(buildId, eventBuildId, eventHeadSha);
    const failures = [];
    for (const candidate of selected.candidates) {
      failures.push(await this.collectFailureEvidence(candidate.pipeline, candidate.build, candidate.history));
    }
    return {
      schemaVersion: 1,
      generatedAt: new Date().toISOString(),
      manualBuildId: buildId || null,
      eventBuildId: eventBuildId || null,
      eventHeadSha: eventHeadSha || null,
      bootstrap: selected.bootstrap,
      pipelineHealth: selected.pipelineHealth,
      failures
    };
  }
}

function emptySelection() {
  return { candidates: [], bootstrap: false, pipelineHealth: [] };
}

export async function collectEvidence(registry, buildId, state, fetchImplementation = fetch, eventBuildId = null, eventHeadSha = null) {
  return new EvidenceCollector(registry, state, fetchImplementation).collect(buildId, eventBuildId, eventHeadSha);
}
