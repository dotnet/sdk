import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { appendFileSync, lstatSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";

export const detectorPath = "src/Cli/dotnet/Telemetry/LLMEnvironmentDetectorForTelemetry.cs";
export const testsPath = "test/dotnet.Tests/TelemetryTests/TelemetryCommonPropertiesTests.cs";
export const marker = "Workflow marker: gh-aw-workflow-id: llm-detection-fix";
const auditMarker = "Workflow marker: gh-aw-workflow-id: llm-detection-audit";
const sourcePattern = /https:\/\/github\.com\/([a-zA-Z0-9_.-]+\/[a-zA-Z0-9_.-]+)\/blob\/([a-f0-9]{40})\/[a-zA-Z0-9_./-]+#L[1-9][0-9]*(?:-L[1-9][0-9]*)?/g;

function requireCondition(condition, message) {
    if (!condition) {
        throw new Error(message);
    }
}

export function validateInputs(runId, harness, variable = "") {
    requireCondition(/^[1-9][0-9]{0,15}$/.test(runId) && Number.isSafeInteger(Number(runId)),
        "Select a numeric audit run ID.");
    requireCondition(/^[a-z][a-z0-9-]{0,39}$/.test(harness), "Select one existing harness label, not free-form instructions.");
    if (variable !== "") {
        validateAlias(harness, variable);
    }
}

export function validateAuditRun(run, repo) {
    requireCondition(run.path === ".github/workflows/llm-detection-audit.lock.yml" &&
        run.repository?.full_name === `${repo.owner}/${repo.repo}` &&
        run.head_repository?.full_name === `${repo.owner}/${repo.repo}` &&
        run.head_branch === "main" && ["schedule", "workflow_dispatch"].includes(run.event) &&
        run.status === "completed" && run.conclusion === "success",
    "The selected run must be a successful main-branch detection audit in this repository.");
}

export function auditTrackers(issues) {
    return issues.filter(issue => !issue.pull_request &&
        issue.title === "[CLI agent detection] Evidence report" &&
        issue.user?.login === "github-actions[bot]" &&
        issue.labels.some(label => (typeof label === "string" ? label : label.name) === "agentic-workflows") &&
        issue.body?.split(/\r?\n/).includes(auditMarker));
}

export async function checkAudit(github, context, runId, harness, variable = "") {
    validateInputs(runId, harness, variable);
    const repo = context.repo;
    const { data: run } = await github.rest.actions.getWorkflowRun({ ...repo, run_id: Number(runId) });
    validateAuditRun(run, repo);
    const issues = await github.paginate(github.rest.issues.listForRepo, { ...repo, state: "all", per_page: 100 });
    const trackers = auditTrackers(issues);
    requireCondition(trackers.length === 1 && trackers[0].state === "open",
        "Exactly one open audit tracker is required. Resolve closed or duplicate trackers first.");
    const reportRuns = trackers[0].body.match(/https:\/\/github\.com\/[\w.-]+\/[\w.-]+\/actions\/runs\/[0-9]+/g) ?? [];
    requireCondition(reportRuns.includes(run.html_url),
        "The tracker no longer contains the selected audit. Select its current successful run.");
    await checkDuplicate(github, repo);
    return trackers[0].number;
}

export async function checkDuplicate(github, repo) {
    const pulls = await github.paginate(github.rest.pulls.list, { ...repo, state: "open", per_page: 100 });
    requireCondition(!pulls.some(pull => pull.head.repo?.full_name === `${repo.owner}/${repo.repo}` &&
        pull.head.ref.startsWith("llm-detection-fix/")),
    "An automated detection fix is already open. Review or close it before requesting another.");
}

export function selectArtifact(artifacts, startedAt) {
    const matches = artifacts.filter(artifact => artifact.name === "agent" && !artifact.expired &&
        Date.parse(artifact.created_at) >= Date.parse(startedAt));
    requireCondition(matches.length === 1,
        "Exactly one agent artifact from this run attempt is required. Dispatch a fresh run if an earlier attempt retained its artifact.");
    return matches[0].id;
}

function presenceRules(source) {
    return [...source.matchAll(/new EnvironmentDetectionRuleWithResult<string>\("([a-z0-9-]+)", new AnyPresentEnvironmentRule\(\s*getEnvironmentVariable, ((?:"[A-Z][A-Z0-9_]*"(?:,\s*)?)+)\)\)/g)];
}

function validateAlias(harness, variable) {
    requireCondition(/^[a-z][a-z0-9-]{0,39}$/.test(harness) &&
        /^[A-Z][A-Z0-9_]{1,63}$/.test(variable), "Invalid harness or environment variable identifier.");
    const prefix = harness.replaceAll("-", "_").toUpperCase();
    requireCondition(variable === prefix || variable.startsWith(`${prefix}_`),
        "Only harness-prefixed identity variables are eligible.");
}

export function isEligibleAlias(detector, harness, variable) {
    validateAlias(harness, variable);
    return !detector.includes(`"${variable}"`) && presenceRules(detector).filter(rule => rule[1] === harness).length === 1;
}

export function prepareContents(detector, tests, harness, variable) {
    requireCondition(isEligibleAlias(detector, harness, variable),
        "Select a missing variable for exactly one existing presence-based harness rule.");
    const rules = presenceRules(detector).filter(rule => rule[1] === harness);
    const rule = rules[0];
    const legacy = [...rule[2].matchAll(/"([^"]+)"/g)][0][1];
    const replacement = rule[0].replace(`${rule[2]}))`, `${rule[2]}, "${variable}"))`);
    const updatedDetector = detector.slice(0, rule.index) + replacement + detector.slice(rule.index + rule[0].length);
    const matrix = /public static IEnumerable<object\[\]> LLMTelemetryTestCases => new List<object\[\]>\(\)\r?\n    \{[\s\S]*?\r?\n    \};/;
    const matrices = [...tests.matchAll(new RegExp(matrix, "g"))];
    requireCondition(matrices.length === 1, "Cannot identify the existing telemetry test matrix. Update the trusted fixer first.");
    const newline = tests.includes("\r\n") ? "\r\n" : "\n";
    const rows = [
        `        new object[] { new Dictionary<string, string> { { "${variable}", "1" } }, "${harness}" },`,
        `        new object[] { new Dictionary<string, string> { { "${variable}", "" } }, null! },`,
        `        new object[] { new Dictionary<string, string> { { "${variable}", "false" } }, "${harness}" },`,
        `        new object[] { new Dictionary<string, string> { { "${legacy}", "1" }, { "${variable}", "1" } }, "${harness}" },`,
    ].join(newline);
    const updatedTests = tests.replace(matrix, match => match.replace(`${newline}    };`, `${newline}${rows}${newline}    };`));
    return { detector: updatedDetector, tests: updatedTests, variable };
}

export function validateContents(beforeDetector, beforeTests, afterDetector, afterTests, harness) {
    const oldRules = presenceRules(beforeDetector).filter(rule => rule[1] === harness);
    const newRules = presenceRules(afterDetector).filter(rule => rule[1] === harness);
    requireCondition(oldRules.length === 1 && newRules.length === 1, "Only an existing presence rule can change.");
    const oldVariables = [...oldRules[0][2].matchAll(/"([^"]+)"/g)].map(match => match[1]);
    const newVariables = [...newRules[0][2].matchAll(/"([^"]+)"/g)].map(match => match[1]);
    requireCondition(newVariables.length === oldVariables.length + 1, "Add exactly one alias without removing legacy variables.");
    const expected = prepareContents(beforeDetector, beforeTests, harness, newVariables.at(-1));
    requireCondition(afterDetector === expected.detector, "The detector patch exceeds the permitted alias addition.");
    requireCondition(afterTests === expected.tests, "Only the four generated regression rows may be added. Preserve every existing test.");
    return expected.variable;
}

export function validateEvidence(references) {
    const matches = references.map(reference => typeof reference === "string" &&
        reference.match(new RegExp(`^${sourcePattern.source}$`)));
    requireCondition(new Set(references).size >= 2 && matches.every(match => match &&
        match[1] === matches[0][1] && match[2] === matches[0][2]),
    "Provide distinct assignment and propagation permalinks at the same upstream revision.");
}

export function validateOutput(output, runId, harness, repo, variable = "") {
    validateInputs(runId, harness, variable);
    requireCondition(Array.isArray(output.items) && output.items.length === 1, "Exactly one terminal output is required.");
    const item = output.items[0];
    if (item.type === "noop") {
        return false;
    }
    requireCondition(item.type === "create_pull_request" &&
        Object.keys(item).every(key => ["type", "title", "body", "branch", "temporary_id"].includes(key)) &&
        item.branch === `llm-detection-fix/${harness}` &&
        typeof item.title === "string" && item.title.length > 0 &&
        typeof item.body === "string" && item.body.length < 20000,
    "Expected one bounded draft PR proposal, with no repository, base, transport, or policy overrides.");
    const lines = item.body.split(/\r?\n/);
    requireCondition(lines.includes(marker) &&
        lines.includes(`Audit run: https://github.com/${repo}/actions/runs/${runId}`),
    "The PR must identify this fixer and the approved audit run.");
    requireCondition(!variable || lines.includes(`Automatic candidate: ${harness}/${variable}`),
        "The PR must identify the automatically selected candidate.");
    requireCondition(!/\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+(?:#\d+|[\w.-]+\/[\w.-]+#\d+|https:\/\/github\.com\/[\w.-]+\/[\w.-]+\/issues\/\d+)/i.test(item.body),
        "Do not close the persistent audit tracker through PR closing keywords.");
    validateEvidence([...item.body.matchAll(sourcePattern)].map(match => match[0]));
    return true;
}

function git(root, ...args) {
    return execFileSync("git", ["--no-pager", "-c", "core.hooksPath=", ...args], { cwd: root, encoding: "utf8", maxBuffer: 1024 * 1024 });
}

export function validateArtifact(root, directory, runId, harness, repo, apply = false, expectedVariable = "") {
    const outputBytes = readFileSync(resolve(directory, "agent_output.json"));
    const output = JSON.parse(outputBytes.toString("utf8"));
    const hasProposal = validateOutput(output, runId, harness, repo, expectedVariable);
    const files = readdirSync(directory);
    const patches = files.filter(name => /^aw-.*\.patch$/.test(name));
    requireCondition(!files.some(name => name.endsWith(".bundle")), "Bundle transport is forbidden. Only the validated AM patch can be published.");
    if (!hasProposal) {
        requireCondition(patches.length === 0, "A no-op must not contain a patch.");
        return { hasProposal: false };
    }
    requireCondition(patches.length === 1, "Exactly one patch artifact is required.");
    const patchPath = resolve(directory, patches[0]);
    const stat = lstatSync(patchPath);
    requireCondition(stat.isFile() && stat.size > 0 && stat.size <= 16 * 1024, "The patch must be a regular file under 16 KiB.");
    const patchBytes = readFileSync(patchPath);
    const patch = patchBytes.toString("utf8");
    requireCondition((patch.match(/^From [a-f0-9]{40} Mon Sep 17 00:00:00 2001$/gm) ?? []).length === 1,
        "The patch must contain exactly one AM commit.");
    const baseHeaders = [...patch.matchAll(/^X-GH-AW-Base-Commit: (.*)$/gm)];
    requireCondition(baseHeaders.length <= 1 && baseHeaders.every(match => match[1] === git(root, "rev-parse", "HEAD").trim()),
        "Patch base metadata must match the validated checkout.");
    const beforeDetector = git(root, "show", `HEAD:${detectorPath}`);
    const beforeTests = git(root, "show", `HEAD:${testsPath}`);
    requireCondition(git(root, "status", "--porcelain").trim() === "", "Validate only in a clean trusted checkout.");
    git(root, "apply", "--check", "--index", patchPath);
    git(root, "apply", "--index", patchPath);
    const changes = git(root, "diff", "--cached", "--raw", "--no-abbrev").trim().split(/\r?\n/);
    requireCondition(changes.length === 2 && changes.every(line =>
        /^:100644 100644 [a-f0-9]{40} [a-f0-9]{40} M\t/.test(line)) &&
        [detectorPath, testsPath].every(path => changes.some(line => line.endsWith(`\t${path}`))),
    "Only the detector and telemetry test matrix may change, without mode changes, renames, additions, or deletions.");
    const variable = validateContents(beforeDetector, beforeTests,
        git(root, "show", `:${detectorPath}`), git(root, "show", `:${testsPath}`), harness);
    requireCondition(!expectedVariable || variable === expectedVariable,
        "The patch must use the automatically selected alias. Do not substitute another signal.");
    // Publication checks the same immutable artifact but never executes its code.
    if (!apply) {
        git(root, "apply", "--reverse", "--index", patchPath);
    }
    const payloadHash = createHash("sha256").update(outputBytes).update("\0").update(patchBytes).digest("hex");
    return { hasProposal: true, variable, payloadHash };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    const [command, ...args] = process.argv.slice(2);
    const root = process.cwd();
    if (command === "prepare") {
        const [harness, variable] = args;
        const contents = prepareContents(readFileSync(detectorPath, "utf8"), readFileSync(testsPath, "utf8"), harness, variable);
        writeFileSync(detectorPath, contents.detector);
        writeFileSync(testsPath, contents.tests);
    } else {
        requireCondition(command === "validate" || command === "check", "Use prepare, validate, or check.");
        const result = validateArtifact(root, args[0], process.env.AUDIT_RUN_ID, process.env.HARNESS,
            process.env.GITHUB_REPOSITORY, command === "validate", process.env.FIX_VARIABLE || "");
        if (command === "check") {
            requireCondition(result.hasProposal && /^[a-f0-9]{64}$/.test(process.env.VALIDATED_PAYLOAD_HASH) &&
                result.payloadHash === process.env.VALIDATED_PAYLOAD_HASH,
            "Publication payload differs from the patch and metadata that passed isolated validation.");
        }
        if (process.env.GITHUB_OUTPUT) {
            appendFileSync(process.env.GITHUB_OUTPUT, `has_proposal=${result.hasProposal}\n`);
            if (result.hasProposal) {
                appendFileSync(process.env.GITHUB_OUTPUT, `payload_hash=${result.payloadHash}\n`);
            }
        }
        console.log(result.hasProposal ? `Validated additive ${result.variable} alias and four regression cases.` : "Validated explicit no-op.");
    }
}
