import { createPipelineObservation, createTaskObservations } from "./azure/observations.mjs";
import { MAX_HELIX_REFERENCES, MAX_RELATED_BUILDS, MAX_RELATED_HELIX_REFERENCES, MAX_TASK_LOGS } from "./constants.mjs";
import { createBuildSummary, isFailedBuild, normalizeEvidenceText } from "./evidence-utils.mjs";
import { applyKbeRecurrence, getTimelineFailuresFromRecords } from "./collector-policy.mjs";

export class FailureEvidenceCollector {
  constructor(getAzureClient, helixEvidenceClient) {
    this.getAzureClient = getAzureClient;
    this.helixEvidence = helixEvidenceClient;
  }

  async collectRelatedFailureEvidence(pipeline, buildId, history) {
    const related = [];
    const failedBuilds = history
      .filter(build => build.id !== buildId && isFailedBuild(build))
      .slice(0, MAX_RELATED_BUILDS);
    for (const build of failedBuilds) {
      try {
        const timeline = await this.getAzureClient(pipeline).getTimeline(build.id);
        const timelineFailures = getTimelineFailuresFromRecords(timeline.records);
        const observations = await this.helixEvidence.collectObservations(
          timelineFailures, MAX_RELATED_HELIX_REFERENCES);
        related.push({
          build: createBuildSummary(build),
          taskFailures: timelineFailures.map(failure => ({
            name: failure.name,
            path: failure.path,
            issues: failure.issues
          })),
          observations: deduplicateObservations(observations)
        });
      } catch (error) {
        related.push({ build: createBuildSummary(build), unavailable: normalizeEvidenceText(error.message) });
      }
    }
    return related;
  }

  async collectFailureEvidence(pipeline, build, history, candidate = {}) {
    const azure = this.getAzureClient(pipeline);
    const detailedBuild = build.validationResults ? build : await azure.getBuild(build.id);
    const timeline = await azure.getTimeline(build.id);
    const timelineFailures = getTimelineFailuresFromRecords(timeline.records);
    const pipelineObservation = createPipelineObservation(detailedBuild, timeline.records ?? []);
    const helixObservations = await this.helixEvidence.collectObservations(timelineFailures, MAX_HELIX_REFERENCES);
    const relatedFailureSummaries = await this.collectRelatedFailureEvidence(pipeline, build.id, history);
    const logFailures = await this.collectTaskLogs(azure, build.id, timelineFailures);
    const taskObservations = createTaskObservations(
      timelineFailures,
      new Map(logFailures.filter(failure => failure.text).map(failure => [failure.logId, failure.text])));
    const observations = deduplicateObservations(applyKbeRecurrence(
      [pipelineObservation, ...taskObservations, ...helixObservations].filter(Boolean),
      relatedFailureSummaries,
      createBuildSummary(build)));
    return {
      pipeline,
      build: createBuildSummary(build),
      monitoringScope: candidate.monitoringScope ?? null,
      priority: candidate.priority ?? null,
      auditContext: candidate.auditContext ?? null,
      mergedPullRequest: candidate.mergedPullRequest ?? null,
      recentBuilds: history.map(createBuildSummary),
      issueCandidates: observations.filter(observation => observation.actionable),
      contextObservations: observations.filter(observation => !observation.actionable),
      relatedFailureSummaries,
      testFailures: await this.collectAzureTestFailures(azure, build.id)
    };
  }

  async collectAzureTestFailures(azure, buildId) {
    try {
      return await azure.getTestFailures(buildId);
    } catch (error) {
      return [{ unavailable: normalizeEvidenceText(error.message) }];
    }
  }

  async collectTaskLogs(azure, buildId, timelineFailures) {
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
        logs.push({ name: failure.name, unavailable: normalizeEvidenceText(error.message) });
      }
    }
    return logs;
  }
}

function deduplicateObservations(observations) {
  return [...new Map(observations.map(observation => [
    observation.fingerprint ?? `${observation.kind}:${observation.component}:${observation.mechanism}`,
    observation
  ])).values()];
}