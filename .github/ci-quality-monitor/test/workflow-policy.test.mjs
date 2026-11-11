import assert from "node:assert/strict";
import {readFile} from "node:fs/promises";
import test from "node:test";

const workflowUrl = new URL("../../workflows/ci-quality-monitor.md", import.meta.url);

function getJob(workflow, name)
{
    const job = workflow.match(new RegExp(`^  ${name}:\\r?\\n(?:(?: {4}[^\\r\\n]*|)\\r?\\n)*`, "m"))?.[0];
    assert.ok(job, `Missing ${name} job`);
    return job;
}

test("every created issue receives live-build-incident", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /labels: \[[^\]]*cookie[^\]]*live-build-incident[^\]]*\]/);
    assert.doesNotMatch(workflow, /allowed-labels: \[[^\]]*live-build-incident[^\]]*\]/);
    assert.match(workflow, /`live-build-incident` is applied automatically to every created issue\./);
});

test("created issues are dispatched directly to Issue Monster", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /actions: write/);
    assert.match(workflow, /needs\.safe_outputs\.outputs\.created_issue_number/);
    assert.match(workflow, /name: safe-outputs-items/);
    assert.match(workflow, /CREATED_ISSUE_MAP_FILE: \.ci-quality-monitor\/safe-outputs\/temporary-id-map\.json/);
    assert.match(workflow, /github-token: \$\{\{ secrets\.GH_AW_GITHUB_TOKEN \|\| secrets\.GITHUB_TOKEN \}\}/);
    assert.match(workflow, /dispatchCreatedIssues/);
    assert.match(getJob(workflow, "dispatch-issues"), /^      issues: read$/m);
    const dispatch = workflow.match(/      - name: Dispatch Issue Monster for created issues\r?\n([\s\S]*?)(?=^\S)/m)?.[1];
    assert.ok(dispatch, "Expected the dispatch step in its separate downstream job.");
    assert.match(dispatch, /^              context,$/m);
    assert.doesNotMatch(dispatch, /if: needs\.safe_outputs\.outputs\.created_issue_number != ''/);
});

test("named test assertions require the recurring KBE gate", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /A named test assertion .* may create an issue only through the validated recurring Known Build Error gate/s);
    assert.match(workflow, /If a named test assertion does not satisfy every KBE requirement, do not create an ordinary issue for it\./);
});

test("same pull request attempts are not independent recurrence", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /Attempts of the same pull request are not independent recurrence, even across different commits\./);
});

test("closed incidents provide context but do not suppress resurfaced failures", async () =>
{
        const workflow = await readFile(workflowUrl, "utf8");

        assert.match(workflow, /Recently closed issues are historical context only and must not block filing a resurfaced failure\./);
        assert.match(workflow, /only when it is open and its observable failure and mechanism materially match/);
        assert.match(workflow, /do not create a duplicate when an existing open issue already tracks either one/);
});

test("failed Azure check suites trigger build ID resolution", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /check_suite:\s*\n\s*types: \[completed\]/);
    assert.match(workflow, /github\.event\.check_suite\.app\.slug == 'azure-pipelines'/);
    assert.match(workflow, /github\.event\.check_suite\.conclusion != 'success'/);
    assert.match(workflow, /resolveAzureBuildId\(checks\)/);
});

test("merged pull requests pass stable-target metadata to the collector", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /pull_request_target:\s*\n\s*types: \[closed\]/);
    assert.doesNotMatch(workflow, /^\s*pull_request:\s*$/m);
    assert.match(workflow, /Never check out or execute PR code\./);
    assert.match(workflow, /^checkout: false$/m);
    assert.match(workflow, /job-discriminator: \$\{\{ github\.run_id \}\}/);
    assert.match(workflow, /github\.event\.pull_request\.merged == true/);
    assert.match(workflow, /MERGED_PR_NUMBER: \$\{\{ github\.event\.pull_request\.number \}\}/);
    assert.match(workflow, /MERGED_PR_BASE_REF: \$\{\{ github\.event\.pull_request\.base\.ref \}\}/);
    assert.match(workflow, /MERGED_PR_COMMIT_SHA: \$\{\{ github\.event\.pull_request\.merge_commit_sha \}\}/);
});

test("monitor helpers and the agent source checkout use main", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /^checkout: false$/m);
    const checkouts = workflow.match(
        /(?<indent> +)- name: Check out monitor (?:configuration|dispatch helper|source)\r?\n(?:\k<indent> {2}[^\r\n]*\r?\n)+/g);
    assert.equal(checkouts?.length, 3);
    for (const checkout of checkouts)
    {
        assert.match(checkout, /^\s+repository: \$\{\{ github\.repository \}\}$/m);
        assert.match(checkout, /^\s+ref: main$/m);
    }
});

test("compiled monitor checkouts preserve the main pin", async () =>
{
    const workflow = await readFile(new URL("../../workflows/ci-quality-monitor.lock.yml", import.meta.url), "utf8");
    const checkouts = workflow.match(
        /      - name: Check out monitor (?:configuration|dispatch helper|source)\r?\n(?: {8}[^\r\n]*\r?\n)+/g);

    assert.equal(checkouts?.length, 3);
    for (const checkout of checkouts)
    {
        assert.match(checkout, /^\s+repository: \$\{\{ github\.repository \}\}$/m);
        assert.match(checkout, /^\s+ref: main$/m);
    }
    assert.doesNotMatch(workflow, /- name: Checkout PR branch/);
    assert.doesNotMatch(workflow, /- name: Checkout repository\r?\n/);
});

test("no-op collection skips protected environment admission", async () =>
{
    const source = await readFile(workflowUrl, "utf8");
    const compiled = await readFile(new URL("../../workflows/ci-quality-monitor.lock.yml", import.meta.url), "utf8");

    assert.match(source, /^  needs: \[collect, investigation_required\]$/m);
    for (const workflow of [source, compiled])
    {
        const admission = getJob(workflow, "investigation_required");
        assert.match(admission, /^    needs: collect$/m);
        assert.match(admission, /^    if: needs\.collect\.outputs\.should_run == 'true'$/m);
        assert.doesNotMatch(admission, /^    environment:/m);
    }

    const preActivation = getJob(compiled, "pre_activation");
    assert.match(preActivation, /^    needs:\r?\n +\- collect\r?\n +\- investigation_required$/m);
    assert.doesNotMatch(preActivation, /^    if:/m);
    assert.match(getJob(compiled, "pat_pool"), /^    needs: pre_activation$/m);
});

test("CI evidence uses a same-run artifact instead of job outputs", async () =>
{
    const source = await readFile(workflowUrl, "utf8");
    const compiled = await readFile(new URL("../../workflows/ci-quality-monitor.lock.yml", import.meta.url), "utf8");

    for (const workflow of [source, compiled])
    {
        assert.doesNotMatch(workflow, /needs\.collect\.outputs\.dossier|steps\.collect\.outputs\.dossier/);
        assert.match(workflow, /name: Upload public CI evidence[\s\S]*?name: ci-quality-evidence/);
        assert.match(workflow, /name: Download public CI evidence[\s\S]*?name: ci-quality-evidence/);
    }
    assert.match(source, /Read the supplied public CI evidence from `\.ci-quality-monitor\/dossier\.json`/);
});

test("only collection shares a concurrency queue", async () =>
{
    const compiled = await readFile(new URL("../../workflows/ci-quality-monitor.lock.yml", import.meta.url), "utf8");

    assert.match(compiled, /^concurrency:\r?\n  group: ci-quality-monitor-run-\$\{\{ github\.run_id \}\}\r?\n/m);
    const sharedGroups = [...compiled.matchAll(/^ +group: (.*)$/gm)]
        .map(match => match[1])
        .filter(group => !/github\.run_id \}\}"?$/.test(group) && !group.includes("'ci-quality-monitor-collect'"));
    assert.equal(sharedGroups.length, 0, `Unexpected shared groups: ${sharedGroups.join(", ")}`);

    const collect = getJob(compiled, "collect");
    assert.match(collect, /'ci-quality-monitor-collect'\) \|\| format\('ci-quality-monitor-skip-\{0\}', github\.run_id\)/);
    assert.match(collect, /^      cancel-in-progress: false$/m);
    assert.match(collect, /^      queue: max$/m);
    for (const job of ["pre_activation", "pat_pool", "agent", "detection", "safe_outputs"])
    {
        assert.doesNotMatch(getJob(compiled, job), /^    concurrency:/m);
    }
});

test("created issues expose cross-run deduplication keys", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /\*\*Failure fingerprint:\*\*/);
    assert.match(workflow, /\*\*Mechanism fingerprint:\*\*/);
    assert.match(workflow, /search for both exact visible values/);
});
