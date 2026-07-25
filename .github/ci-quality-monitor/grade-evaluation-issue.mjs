import process from "node:process";

const [issuePath, scenarioPath] = process.argv.slice(2);
if (!issuePath || !scenarioPath) throw new Error("Usage: node grade-evaluation-issue.mjs <issue.json> <scenario.json>");

const { readFile } = await import("node:fs/promises");
const issue = JSON.parse(await readFile(issuePath, "utf8"));
const scenario = JSON.parse(await readFile(scenarioPath, "utf8"));
const body = issue.body ?? "";
const requiredSections = ["Build Information", "Failure History", "Error Details", "Root Cause Analysis", "Suggested Investigation"];
const requiredLabels = ["agentic-workflows", "cookie", "Test Debt"];
const labeledValue = label => body.match(new RegExp(`^[-*] \\*\\*${label}:\\*\\*\\s*(.+)$`, "im"))?.[1]
  ?.replaceAll("`", "").trim();
const result = {
  title: issue.title,
  hasBuild: body.includes(`${scenario.buildId}`),
  hasPhase: scenario.expectedPhases.includes(labeledValue("Phase")),
  hasFailureType: scenario.expectedFailureTypes.includes(labeledValue("Failure type")),
  hasEvidenceSources: /\*\*Evidence sources:\*\*/i.test(body),
  hasFingerprint: /\*\*Failure fingerprint:\*\*\s*`[^`]+`/.test(body),
  hasRequiredSections: requiredSections.every(section => new RegExp(`^## ${section}$`, "m").test(body)),
  hasRcaLabels: ["Observed", "Assessment", "Confidence", "Alternatives / Unknowns"]
    .every(label => body.includes(`**${label}:**`)),
  hasRequiredLabels: requiredLabels.every(label => issue.labels?.some(candidate => candidate.name === label)),
  hasExpectedMechanism: (scenario.expectedMechanismIncludes ?? [])
    .every(term => body.toLowerCase().includes(term.toLowerCase()))
};
result.passed = Object.entries(result).filter(([key]) => key.startsWith("has")).every(([, value]) => value);
console.log(JSON.stringify(result, null, 2));
if (!result.passed) process.exitCode = 1;
