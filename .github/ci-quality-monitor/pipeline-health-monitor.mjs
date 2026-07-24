import { createHeartbeatObservation, isRegisteredBuild, updateHeartbeatState } from "./collector-policy.mjs";
import { sanitizeText } from "./evidence-utils.mjs";
import { getGitHubBranchHead } from "./github/client.mjs";

export class PipelineHealthMonitor {
  constructor(state, fetchImplementation = fetch) {
    this.state = state;
    this.fetch = fetchImplementation;
  }

  async collect(pipeline, branch, azure, stateKey, observations) {
    try {
      const [head, recentBuilds] = await Promise.all([
        getGitHubBranchHead(pipeline, branch, this.fetch),
        azure.listRecentBuilds(branch)
      ]);
      const heartbeat = createHeartbeatObservation(
        pipeline, branch, head, recentBuilds.filter(build => isRegisteredBuild(build, pipeline)));
      const trackedHeartbeat = updateHeartbeatState(this.state, stateKey, heartbeat);
      if (trackedHeartbeat) observations.push(trackedHeartbeat);
    } catch (error) {
      observations.push({
        kind: "pipeline-heartbeat",
        category: "heartbeat-unavailable",
        component: `${pipeline.repository}:${branch}`,
        mechanism: sanitizeText(error.message),
        actionable: false
      });
    }
  }
}