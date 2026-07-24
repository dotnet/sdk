export interface Pipeline {
  organization: string;
  project: string;
  definitionId: number;
  repository: string;
  branches: string[];
}

export interface Observation {
  kind: string;
  category: string;
  component: string;
  mechanism: string;
  signature?: string;
  actionable: boolean;
  [detail: string]: unknown;
}

export interface BuildCandidate {
  pipeline: Pipeline;
  build: Record<string, unknown>;
  history: Array<Record<string, unknown>>;
}

export interface CandidateSelection {
  candidates: BuildCandidate[];
  bootstrap: boolean;
  pipelineHealth: Observation[];
}

export interface CollectionDossier {
  schemaVersion: 1;
  generatedAt: string;
  manualBuildId: string | null;
  eventBuildId: string | null;
  eventHeadSha: string | null;
  bootstrap: boolean;
  pipelineHealth: Observation[];
  failures: Array<Record<string, unknown>>;
}
