import { AzureDevOpsClient } from "./azure/client.mjs";
import { BuildCandidateSelector } from "./build-candidate-selector.mjs";
import { FailureEvidenceCollector } from "./failure-evidence-collector.mjs";
import { HelixEvidenceClient } from "./helix/client.mjs";
import { PipelineHealthMonitor } from "./pipeline-health-monitor.mjs";

/** @typedef {import("./types.d.ts").CollectionDossier} CollectionDossier */

export class CiEvidenceCollector {
  constructor(registry, state, fetchImplementation = fetch) {
    this.azureClients = new Map();
    const getAzureClient = pipeline => this.azure(pipeline, fetchImplementation);
    const pipelineHealthMonitor = new PipelineHealthMonitor(state, fetchImplementation);
    this.candidateSelector = new BuildCandidateSelector(registry, state, getAzureClient, pipelineHealthMonitor);
    this.failureCollector = new FailureEvidenceCollector(
      getAzureClient,
      new HelixEvidenceClient(fetchImplementation));
  }

  azure(pipeline, fetchImplementation) {
    if (!this.azureClients.has(pipeline)) {
      this.azureClients.set(pipeline, new AzureDevOpsClient(pipeline, fetchImplementation));
    }
    return this.azureClients.get(pipeline);
  }

  /** @returns {Promise<CollectionDossier>} */
  async collect(buildId, eventBuildId = null, eventHeadSha = null, mergedPullRequest = null) {
    const selected = await this.candidateSelector.select(buildId, eventBuildId, eventHeadSha, mergedPullRequest);
    const failures = [];
    for (const candidate of selected.candidates) {
      failures.push(await this.failureCollector.collect(
        candidate.pipeline,
        candidate.build,
        candidate.history,
        candidate));
    }
    return {
      schemaVersion: 1,
      generatedAt: new Date().toISOString(),
      manualBuildId: buildId || null,
      eventBuildId: eventBuildId || null,
      eventHeadSha: eventHeadSha || null,
      mergedPullRequest,
      bootstrap: selected.bootstrap,
      pipelineHealth: selected.pipelineHealth,
      failures
    };
  }
}

export async function collectEvidence(registry, buildId, state, fetchImplementation = fetch, eventBuildId = null, eventHeadSha = null, mergedPullRequest = null) {
  return new CiEvidenceCollector(registry, state, fetchImplementation)
    .collect(buildId, eventBuildId, eventHeadSha, mergedPullRequest);
}
