export interface Pipeline
{
  organization: string;
  project: string;
  definitionId: number;
  repository: string;
  branches: string[];
  stableBranches: string[];
}

export interface Observation
{
  kind: string;
  phase: string;
  failureType: string;
  evidenceSources: string[];
  component: string;
  mechanism: string;
  fingerprint?: string;
  diagnosticCode?: string | null;
  actionable: boolean;
  [detail: string]: unknown;
}

export interface BuildCandidate
{
  pipeline: Pipeline;
  build: Record<string, unknown>;
  history: Array<Record<string, unknown>>;
  monitoringScope?: "stable-branch";
  priority?: "HIGH";
  auditContext?: string;
  mergedPullRequest?: Record<string, unknown>;
}

export interface CandidateSelection
{
  candidates: BuildCandidate[];
  bootstrap: boolean;
  pipelineHealth: Observation[];
}

export interface CiEvidenceDossier
{
  schemaVersion: 1;
  generatedAt: string;
  manualBuildId: string | null;
  eventBuildId: string | null;
  eventHeadSha: string | null;
  mergedPullRequest: Record<string, unknown> | null;
  bootstrap: boolean;
  pipelineHealth: Observation[];
  failures: Array<{
    issueCandidates: Observation[];
    contextObservations: Observation[];
    [detail: string]: unknown;
  }>;
}
