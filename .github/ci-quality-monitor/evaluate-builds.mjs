import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";

import { collectCiEvidence } from "./collect-ci-evidence.mjs";

function parseArguments(argumentsList) {
  const options = { catalog: ".github/ci-quality-monitor/evaluation-builds.json", output: "artifacts/tmp/ci-quality-monitor/evaluations" };
  for (let index = 0; index < argumentsList.length; index += 2) {
    const key = argumentsList[index];
    if (!key?.startsWith("--") || index + 1 >= argumentsList.length) throw new Error(`Invalid argument '${key}'.`);
    options[key.slice(2)] = argumentsList[index + 1];
  }
  return options;
}

function evaluateExample(example, dossier) {
  const candidates = dossier.failures.flatMap(failure => failure.issueCandidates ?? []);
  const phases = [...new Set(candidates.map(candidate => candidate.phase))];
  const failureTypes = [...new Set(candidates.map(candidate => candidate.failureType))];
  const missingPhases = example.expectedPhases.filter(phase => !phases.includes(phase));
  const missingFailureTypes = example.expectedFailureTypes
    .filter(failureType => !failureTypes.includes(failureType));
  const minimum = example.minimumObservations ?? 1;
  const minimumTotalCandidates = example.minimumTotalCandidates ?? 1;
  const taxonomyMatches = candidates.filter(candidate => example.expectedPhases.includes(candidate.phase)
    && example.expectedFailureTypes.includes(candidate.failureType));
  const mechanismTerms = example.expectedMechanismIncludes ?? [];
  const componentTerm = example.expectedComponentIncludes?.toLowerCase();
  const exactMatches = taxonomyMatches.filter(candidate =>
    mechanismTerms.every(term => candidate.mechanism?.toLowerCase().includes(term.toLowerCase()))
    && (!componentTerm || candidate.component?.toLowerCase().includes(componentTerm)));
  const sharedMechanismTerms = example.expectedMatchingMechanismIncludes ?? [];
  const matchingCandidates = candidates.filter(candidate =>
    sharedMechanismTerms.every(term => candidate.mechanism?.toLowerCase().includes(term.toLowerCase())));
  const minimumMatchingCandidates = example.minimumMatchingCandidates ?? 0;
  const passed = missingPhases.length === 0
    && missingFailureTypes.length === 0
    && candidates.length >= minimumTotalCandidates
    && taxonomyMatches.length >= minimum
    && exactMatches.length > 0
    && matchingCandidates.length >= minimumMatchingCandidates;
  return {
    passed,
    phases,
    failureTypes,
    taxonomyMatchCount: taxonomyMatches.length,
    totalCandidateCount: candidates.length,
    exactMatchCount: exactMatches.length,
    matchingCandidateCount: matchingCandidates.length,
    missingPhases,
    missingFailureTypes
  };
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const catalog = JSON.parse(await readFile(options.catalog, "utf8"));
  const registry = JSON.parse(await readFile(".github/ci-quality-monitor/pipelines.json", "utf8"));
  await mkdir(options.output, { recursive: true });
  const results = [];
  for (const example of catalog.examples) {
    const dossier = await collectCiEvidence(registry, `${example.buildId}`, { schemaVersion: 1, pipelines: {} });
    const result = evaluateExample(example, dossier);
    results.push({ ...example, ...result });
    await writeFile(path.join(options.output, `${example.buildId}.json`), `${JSON.stringify(dossier, null, 2)}\n`);
    console.log(`${result.passed ? "PASS" : "FAIL"} ${example.buildId} ${example.name}: ${result.phases.join(", ")} / ${result.failureTypes.join(", ")}`);
  }
  await writeFile(path.join(options.output, "summary.json"), `${JSON.stringify(results, null, 2)}\n`);
  if (results.some(result => !result.passed)) process.exitCode = 1;
}

main().catch(error => {
  console.error(error.stack ?? error.message);
  process.exitCode = 1;
});