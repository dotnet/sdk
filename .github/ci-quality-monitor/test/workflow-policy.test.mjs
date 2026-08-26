import assert from "node:assert/strict";
import {readFile} from "node:fs/promises";
import test from "node:test";

const workflowUrl = new URL("../../workflows/ci-quality-monitor.md", import.meta.url);

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
    assert.match(workflow, /needs\.safe_outputs\.outputs\.process_safe_outputs_temporary_id_map/);
    assert.match(workflow, /github-token: \$\{\{ secrets\.GH_AW_GITHUB_TOKEN \|\| secrets\.GITHUB_TOKEN \}\}/);
    assert.match(workflow, /dispatchCreatedIssues/);
    assert.doesNotMatch(workflow, /if: needs\.safe_outputs\.outputs\.created_issue_number != ''/);
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
        assert.match(workflow, /do not create a duplicate when an existing open issue already tracks it/);
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

    assert.match(workflow, /pull_request:\s*\n\s*types: \[closed\]/);
    assert.match(workflow, /github\.event\.pull_request\.merged == true/);
    assert.match(workflow, /MERGED_PR_NUMBER: \$\{\{ github\.event\.pull_request\.number \}\}/);
    assert.match(workflow, /MERGED_PR_BASE_REF: \$\{\{ github\.event\.pull_request\.base\.ref \}\}/);
    assert.match(workflow, /MERGED_PR_COMMIT_SHA: \$\{\{ github\.event\.pull_request\.merge_commit_sha \}\}/);
});
