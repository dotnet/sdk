import { appendFile, mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { pathToFileURL } from "node:url";
import { collectEvidence } from "./collector.mjs";

export {
  buildConsumptionKey,
  createFailureSignature,
  normalizeBuild,
  normalizeSignaturePart,
  sanitizeText
} from "./evidence-utils.mjs";
export {
  classifyTaskFailure,
  createPipelineObservation,
  createTaskObservations
} from "./azure/observations.mjs";
export { getBuild, listCompletedBuilds } from "./azure/client.mjs";
export {
  applyKbeRecurrence,
  collectEvidence,
  createHeartbeatObservation,
  EvidenceCollector,
  getTimelineFailuresFromRecords
} from "./collector.mjs";
export {
  classifyWorkItem,
  parseHelixWorkItemReferences,
  sharedTestMechanism,
  summarizeHelixConsole
} from "./helix/parsing.mjs";
export { selectUnprocessedFailures } from "./state.mjs";
export { parseTestResultXml } from "./test-results.mjs";

export function parseArguments(argumentsList) {
  const options = {};
  for (let index = 0; index < argumentsList.length; index += 2) {
    const key = argumentsList[index];
    if (!key?.startsWith("--") || index + 1 >= argumentsList.length) {
      throw new Error(`Invalid argument near '${key ?? "end of arguments"}'.`);
    }
    options[key.slice(2)] = argumentsList[index + 1];
  }
  if (!options.registry || !options.output) {
    throw new Error("--registry and --output are required.");
  }
  return options;
}

async function readState(statePath) {
  if (!statePath) return { schemaVersion: 1, pipelines: {} };
  try {
    const state = JSON.parse(await readFile(statePath, "utf8"));
    if (state.schemaVersion !== 1 || typeof state.pipelines !== "object") {
      throw new Error("Unsupported CI quality monitor state format.");
    }
    return state;
  } catch (error) {
    if (error.code === "ENOENT") return { schemaVersion: 1, pipelines: {} };
    throw error;
  }
}

export function shouldRunAgent(dossier) {
  if (dossier.bootstrap) return false;
  const actionableHealth = dossier.pipelineHealth.filter(observation => observation.actionable).length;
  return dossier.failures.length + actionableHealth > 0;
}

async function writeGitHubOutputs(outputPath, dossier) {
  if (!outputPath) return;
  const delimiter = `CI_QUALITY_${Date.now()}`;
  const compactDossier = JSON.stringify(dossier);
  const actionableHealth = dossier.pipelineHealth.filter(observation => observation.actionable).length;
  await appendFile(outputPath, `should_run=${shouldRunAgent(dossier)}\n`);
  await appendFile(outputPath, `failure_count=${dossier.failures.length + actionableHealth}\n`);
  await appendFile(outputPath, `dossier<<${delimiter}\n${compactDossier}\n${delimiter}\n`);
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const registry = JSON.parse(await readFile(options.registry, "utf8"));
  const state = await readState(options.state);
  const dossier = await collectEvidence(
    registry,
    options["build-id"],
    state,
    fetch,
    options["event-build-id"],
    options["event-head-sha"]);
  await mkdir(path.dirname(options.output), { recursive: true });
  await writeFile(options.output, `${JSON.stringify(dossier, null, 2)}\n`);
  if (options["state-output"]) {
    await mkdir(path.dirname(options["state-output"]), { recursive: true });
    await writeFile(options["state-output"], `${JSON.stringify(state, null, 2)}\n`);
  }
  await writeGitHubOutputs(options["github-output"], dossier);
  console.log(`Collected ${dossier.failures.length} failed build dossier(s) in ${options.output}.`);
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(error => {
    console.error(error.stack ?? error.message);
    process.exitCode = 1;
  });
}