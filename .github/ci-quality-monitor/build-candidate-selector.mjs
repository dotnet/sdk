import { isFailedBuild } from "./evidence-utils.mjs";
import {
  isPullRequestBuild,
  isRegisteredBuild,
  isStableBranchBuild,
  matchesPipeline
} from "./collector-policy.mjs";
import {
  createAuditProcessingKey,
  isAuditProcessed,
  markAuditProcessed,
  recordProcessedBuilds,
  selectUnprocessedFailures,
  stateKey
} from "./state.mjs";

/** @typedef {import("./types.d.ts").CandidateSelection} CandidateSelection */

function emptySelection() {
  return { candidates: [], bootstrap: false, pipelineHealth: [] };
}

export class BuildCandidateSelector {
  constructor(registry, state, getAzureClient, pipelineHealthMonitor) {
    this.registry = registry;
    this.state = state;
    this.getAzureClient = getAzureClient;
    this.pipelineHealthMonitor = pipelineHealthMonitor;
  }

  async selectManualBuild(buildId) {
    for (const pipeline of this.registry.pipelines) {
      try {
        const build = await this.getAzureClient(pipeline).getBuild(buildId);
        if (matchesPipeline(build, pipeline)) return { pipeline, build };
      } catch (error) {
        if (!error.message.includes("returned 404")) throw error;
      }
    }
    throw new Error(`Build ${buildId} is not from a pipeline and repository in the registry.`);
  }

  selectHighCandidate(pipeline, build, history, auditContext, mergedPullRequest = null) {
    const auditKey = createAuditProcessingKey(build, "stable-branch", auditContext);
    const candidate = !isAuditProcessed(this.state, pipeline, auditKey) && isFailedBuild(build) ? build : null;
    markAuditProcessed(this.state, pipeline, auditKey);
    return {
      candidates: candidate ? [{
        pipeline, build: candidate, history, monitoringCategory: "stable-branch", priority: "HIGH",
        auditContext, mergedPullRequest
      }] : [],
      bootstrap: false,
      pipelineHealth: []
    };
  }

  async selectEventBuild(buildId, mergedPullRequest = null) {
    const selected = await this.selectManualBuild(buildId);
    if (isStableBranchBuild(selected.build, selected.pipeline)) {
      const history = (await this.getAzureClient(selected.pipeline).listCompletedBuilds(selected.build.sourceBranch))
        .filter(build => isStableBranchBuild(build, selected.pipeline));
      return this.selectHighCandidate(
        selected.pipeline, selected.build, history, `stable-direct:${selected.build.sourceBranch}`);
    }
    if (!mergedPullRequest?.number || !mergedPullRequest.baseRef || !mergedPullRequest.mergeCommitSha
      || !isPullRequestBuild(selected.build, selected.pipeline)) return emptySelection();
    const stableTarget = `refs/heads/${mergedPullRequest.baseRef}`;
    if (!(selected.pipeline.stableBranches ?? []).includes(stableTarget)
      || `${selected.build.triggerInfo?.["pr.number"]}` !== `${mergedPullRequest.number}`) {
      return emptySelection();
    }
    const history = (await this.getAzureClient(selected.pipeline).listCompletedBuilds(selected.build.sourceBranch))
      .filter(build => isPullRequestBuild(build, selected.pipeline));
    return this.selectHighCandidate(
      selected.pipeline, selected.build, history,
      `stable-merge:${mergedPullRequest.number}:${mergedPullRequest.mergeCommitSha}`, mergedPullRequest);
  }

  async selectEventBuildByHead(headSha, mergedPullRequest = null) {
    for (const pipeline of this.registry.pipelines) {
      const build = await this.getAzureClient(pipeline).findPullRequestBuildByHead(headSha);
      if (build) return this.selectEventBuild(`${build.id}`, mergedPullRequest);
    }
    return emptySelection();
  }

  /** @returns {Promise<CandidateSelection>} */
  async select(buildId, eventBuildId, eventHeadSha, mergedPullRequest) {
    if (buildId) {
      const selected = await this.selectManualBuild(buildId);
      if (selected.build.status?.toLowerCase() !== "completed") return emptySelection();
      const history = await this.getAzureClient(selected.pipeline).listCompletedBuilds(selected.build.sourceBranch);
      return { candidates: [{ ...selected, history }], bootstrap: false, pipelineHealth: [] };
    }
    if (eventBuildId) return this.selectEventBuild(eventBuildId, mergedPullRequest);
    if (eventHeadSha) return this.selectEventBuildByHead(eventHeadSha, mergedPullRequest);
    return this.selectScheduledBuilds();
  }

  /** @returns {Promise<CandidateSelection>} */
  async selectScheduledBuilds() {
    const candidates = [];
    const pipelineHealth = [];
    let bootstrap = false;
    for (const pipeline of this.registry.pipelines) {
      for (const branch of pipeline.branches) {
        const azure = this.getAzureClient(pipeline);
        const history = (await azure.listCompletedBuilds(branch)).filter(build => isRegisteredBuild(build, pipeline));
        const key = stateKey(pipeline, branch);
        const selected = selectUnprocessedFailures(this.state, key, history);
        bootstrap ||= selected.bootstrap;
        for (const build of selected.failures) {
          if ((pipeline.stableBranches ?? []).includes(branch)) {
            const auditContext = `stable-direct:${branch}`;
            const auditKey = createAuditProcessingKey(build, "stable-branch", auditContext);
            if (!isAuditProcessed(this.state, pipeline, auditKey)) {
              markAuditProcessed(this.state, pipeline, auditKey);
              candidates.push({
                pipeline, build, history, monitoringCategory: "stable-branch", priority: "HIGH", auditContext
              });
            }
          }
        }
        await this.pipelineHealthMonitor.collect(pipeline, branch, azure, key, pipelineHealth);
        recordProcessedBuilds(this.state, key, history);
      }
    }
    return { candidates, bootstrap, pipelineHealth };
  }

}