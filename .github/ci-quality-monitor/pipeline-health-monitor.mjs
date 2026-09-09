import {createHeartbeatObservation, isRegisteredBuild, recordHeartbeatCheck} from "./collector-policy.mjs";
import {normalizeEvidenceText} from "./evidence-utils.mjs";
import {getGitHubBranchHead} from "./github/client.mjs";

export class PipelineHealthMonitor
{
  constructor(state, fetchImplementation = fetch)
  {
    this.state = state;
    this.fetch = fetchImplementation;
  }

  async checkPipeline(pipeline, branch, azure, stateKey)
  {
    try
    {
      const [head, recentBuilds] = await Promise.all([
        getGitHubBranchHead(pipeline, branch, this.fetch),
        azure.listRecentBuilds(branch)
      ]);
      const heartbeat = createHeartbeatObservation(
        pipeline, branch, head, recentBuilds.filter(build => isRegisteredBuild(build, pipeline)));
      const trackedHeartbeat = recordHeartbeatCheck(this.state, stateKey, heartbeat);
      return trackedHeartbeat && (pipeline.stableBranches ?? []).includes(branch)
        ? {...trackedHeartbeat, monitoringScope: "stable-branch", priority: "HIGH"}
        : trackedHeartbeat;
    } catch (error)
    {
      return {
        kind: "pipeline-heartbeat",
        phase: "pipeline-scheduling",
        failureType: "evidence-unavailable",
        evidenceSources: ["github-api", "azure-build-history"],
        component: `${pipeline.repository}:${branch}`,
        mechanism: normalizeEvidenceText(error.message),
        actionable: false
      };
    }
  }
}
