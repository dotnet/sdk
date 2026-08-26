import {AzureDevOpsClient} from "./azure/client.mjs";
import {BuildCandidateSelector} from "./build-candidate-selector.mjs";
import {FailureEvidenceCollector} from "./failure-evidence-collector.mjs";
import {HelixEvidenceClient} from "./helix/client.mjs";
import {PipelineHealthMonitor} from "./pipeline-health-monitor.mjs";

/** @typedef {import("./types.d.ts").CiEvidenceDossier} CiEvidenceDossier */

export class CiEvidenceCollector
{
    constructor(registry, state, fetchImplementation = fetch)
    {
        this.azureClients = new Map();
        const getAzureClient = pipeline => this.getAzureClient(pipeline, fetchImplementation);
        const pipelineHealthMonitor = new PipelineHealthMonitor(state, fetchImplementation);
        this.candidateSelector = new BuildCandidateSelector(registry, state, getAzureClient, pipelineHealthMonitor);
        this.failureCollector = new FailureEvidenceCollector(
            getAzureClient,
            new HelixEvidenceClient(fetchImplementation));
    }

    getAzureClient(pipeline, fetchImplementation)
    {
        if (!this.azureClients.has(pipeline))
        {
            this.azureClients.set(pipeline, new AzureDevOpsClient(pipeline, fetchImplementation));
        }
        return this.azureClients.get(pipeline);
    }

    /** @returns {Promise<CiEvidenceDossier>} */
    async collectDossier(buildId, eventBuildId = null, eventHeadSha = null, mergedPullRequest = null)
    {
        const selected = await this.candidateSelector.selectCandidates(
            buildId, eventBuildId, eventHeadSha, mergedPullRequest);
        const failures = [];
        for (const candidate of selected.candidates)
        {
            failures.push(await this.failureCollector.collectFailureEvidence(
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

export async function collectCiEvidence(registry, buildId, state, fetchImplementation = fetch, eventBuildId = null, eventHeadSha = null, mergedPullRequest = null)
{
    return new CiEvidenceCollector(registry, state, fetchImplementation)
        .collectDossier(buildId, eventBuildId, eventHeadSha, mergedPullRequest);
}
