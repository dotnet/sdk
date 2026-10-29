import {
    checkAudit, findAuditTrackers, isEligibleAlias, prepareContents, validateAuditRun, validateEvidence, validateInputs,
} from "./fix.mjs";

export const candidateHeading = "Automatic fix candidates (v1):";
export const fixWorkflow = "llm-detection-fix.lock.yml";

function requireCondition(condition, message) {
    if (!condition) {
        throw new Error(message);
    }
}

function exactKeys(value, keys) {
    return value !== null && typeof value === "object" && !Array.isArray(value) &&
        Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key));
}

export function parseCandidates(body, runId, attempt) {
    const blocks = [...body.matchAll(/^Automatic fix candidates \(v1\):\r?\n```json\r?\n([\s\S]*?)\r?\n```$/gm)];
    requireCondition(blocks.length === 1 && Buffer.byteLength(blocks[0][1]) <= 8192,
        "Include exactly one automatic-candidate JSON block, no larger than 8 KiB.");
    const report = JSON.parse(blocks[0][1]);
    requireCondition(exactKeys(report, ["version", "audit_run_id", "run_attempt", "candidates"]) &&
        report.version === 1 && report.audit_run_id === runId &&
        Number.isSafeInteger(report.run_attempt) && report.run_attempt >= 1 && report.run_attempt === attempt &&
        Array.isArray(report.candidates) && report.candidates.length <= 5,
    "The candidate block must identify this audit run and attempt and contain at most five candidates.");
    const seen = new Set();
    for (const candidate of report.candidates) {
        requireCondition(exactKeys(candidate, ["harness", "variable", "stable_release", "assignment", "propagation"]) &&
            typeof candidate.harness === "string" && typeof candidate.variable === "string" &&
            typeof candidate.stable_release === "string" && /^[^\s<>`]{1,80}$/.test(candidate.stable_release),
        "Each candidate requires only a harness, variable, stable release, and two pinned source links.");
        validateInputs(runId, candidate.harness, candidate.variable);
        requireCondition(candidate.variable.length > 0, "Automatic candidates require an explicit alias.");
        validateEvidence([candidate.assignment, candidate.propagation]);
        const key = `${candidate.harness}/${candidate.variable}`;
        requireCondition(!seen.has(key), "Do not repeat an automatic candidate.");
        seen.add(key);
    }
    return report.candidates;
}

export function fixRunTitle(harness, variable) {
    return `CLI agent detection fix: ${harness} / ${variable}`;
}

function isFixPull(pull, repo) {
    return pull.head.repo?.full_name === `${repo.owner}/${repo.repo}` &&
        pull.head.ref.startsWith("llm-detection-fix/");
}

export async function dispatchFix(github, context, detector, tests) {
    const repo = context.repo;
    const runId = String(context.payload.workflow_run.id);
    validateInputs(runId, "audit");
    const { data: run } = await github.rest.actions.getWorkflowRun({ ...repo, run_id: Number(runId) });
    validateAuditRun(run, repo);
    const trackers = await findAuditTrackers(github, repo);
    requireCondition(trackers.length === 1, "Resolve missing or duplicate audit trackers before automatic dispatch.");
    if (trackers[0].state !== "open") {
        return { reason: "The audit tracker is closed. Automatic fixes are suspended." };
    }
    const reportRuns = trackers[0].body.match(/https:\/\/github\.com\/[\w.-]+\/[\w.-]+\/actions\/runs\/[0-9]+/g) ?? [];
    if (!reportRuns.includes(run.html_url)) {
        return { reason: "The tracker no longer contains this audit. Automatic dispatch skipped the stale report." };
    }
    const pulls = await github.paginate(github.rest.pulls.list, { ...repo, state: "all", per_page: 100 });
    if (pulls.some(pull => pull.state === "open" && isFixPull(pull, repo))) {
        return { reason: "A detection-fix PR is already open. Review it before another automatic proposal." };
    }
    const candidates = parseCandidates(trackers[0].body, runId, run.run_attempt)
        .filter(candidate => isEligibleAlias(detector, candidate.harness, candidate.variable));
    if (candidates.length === 0) {
        return { reason: "This audit has no eligible additive aliases for the current SDK." };
    }
    const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
        ...repo, workflow_id: fixWorkflow, branch: "main", event: "workflow_dispatch", per_page: 100,
    });
    for (const candidate of candidates) {
        const { harness, variable } = candidate;
        const attempted = runs.some(previous => previous.head_repository?.full_name === `${repo.owner}/${repo.repo}` &&
            previous.head_branch === "main" && previous.event === "workflow_dispatch" &&
            previous.display_title === fixRunTitle(harness, variable));
        const handled = pulls.some(pull => isFixPull(pull, repo) && pull.state === "closed" &&
            (pull.head.ref === `llm-detection-fix/${harness}` || pull.head.ref.startsWith(`llm-detection-fix/${harness}-`)) &&
            new RegExp(`\\b${variable}\\b`).test(pull.body ?? ""));
        if (attempted || handled) {
            continue;
        }
        prepareContents(detector, tests, harness, variable);
        // Recheck publication state immediately before the only write operation.
        await checkAudit(github, context, runId, harness, variable);
        await github.rest.actions.createWorkflowDispatch({
            ...repo, workflow_id: fixWorkflow, ref: "main", inputs: { audit_run_id: runId, harness, variable },
        });
        return { reason: `Dispatched one draft-only investigation for ${harness}/${variable}.`, harness, variable };
    }
    return { reason: "Every eligible alias was already attempted or covered by a closed fix PR. Retry manually if needed." };
}
