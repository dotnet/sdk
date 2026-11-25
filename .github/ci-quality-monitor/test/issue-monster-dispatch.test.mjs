import assert from "node:assert/strict";
import {createRequire} from "node:module";
import test from "node:test";

const require = createRequire(import.meta.url);
const {
    dispatchCreatedIssues,
    getCreatedIssueNumbers,
    isCreatedByRun,
} = require("../issue-monster-dispatch.js");

test("collects all newly created issues in the current repository", () =>
{
    const issueNumbers = getCreatedIssueNumbers({
        temporaryIdMapInput: JSON.stringify({
            "issue-one": {repo: "dotnet/sdk", number: 101},
            "issue-two": {repo: "dotnet/sdk", number: 102},
            "other-repo": {repo: "dotnet/runtime", number: 103},
        }),
        createdIssueNumberInput: "101",
        repository: "dotnet/sdk",
    });

    assert.deepEqual(issueNumbers, [101, 102]);
});

test("uses the first-created issue output when the temporary ID map is empty", () =>
{
    const issueNumbers = getCreatedIssueNumbers({
        temporaryIdMapInput: "{}",
        createdIssueNumberInput: "123",
        repository: "dotnet/sdk",
    });

    assert.deepEqual(issueNumbers, [123]);
});

test("rejects an invalid temporary ID map", () =>
{
    assert.throws(
        () => getCreatedIssueNumbers({
            temporaryIdMapInput: "not-json",
            createdIssueNumberInput: "",
            repository: "dotnet/sdk",
        }),
        /Invalid safe-output temporary ID map/,
    );
});

function runMarker(runId, workflowId = "ci-quality-monitor")
{
    return `<!-- gh-aw-agentic-workflow: CI Quality Investigator, engine: copilot, id: ${runId}, workflow_id: ${workflowId}, run: https://github.com/dotnet/sdk/actions/runs/${runId} -->`;
}

test("identifies issues created by the current investigator run", () =>
{
    assert.equal(isCreatedByRun({body: `text\n${runMarker(42)}`}, 42), true);
    assert.equal(isCreatedByRun({body: runMarker(420)}, 42), false);
    assert.equal(isCreatedByRun({body: runMarker(42, "other-workflow")}, 42), false);
    assert.equal(isCreatedByRun({body: `> quoted ${runMarker(42)}`}, 42), false);
    assert.equal(isCreatedByRun({body: runMarker(42), pull_request: {}}, 42), false);
    assert.equal(isCreatedByRun({body: null}, 42), false);
});

function createGitHub(calls, issues)
{
    return {
        rest: {
            actions: {
                async createWorkflowDispatch(args)
                {
                    calls.push(args);
                },
            },
            issues: {
                async listForRepo(args)
                {
                    calls.push({list: args});
                    return {data: issues};
                },
            },
        },
    };
}

test("dispatches Issue Monster once for each issue created by the run", async () =>
{
    const calls = [];
    const logs = [];
    const issueNumbers = await dispatchCreatedIssues({
        github: createGitHub(calls, [
            {number: 103, body: runMarker(42)},
            {number: 102, body: runMarker(42)},
            {number: 99, body: runMarker(41)},
        ]),
        context: {repo: {owner: "dotnet", repo: "sdk"}, runId: 42},
        core: {info: (message) => logs.push(message)},
        temporaryIdMapInput: "",
        createdIssueNumberInput: "101",
        ref: "main",
    });

    assert.deepEqual(issueNumbers, [101, 102, 103]);
    assert.deepEqual(calls[0].list, {
        owner: "dotnet",
        repo: "sdk",
        labels: "live-build-incident",
        state: "open",
        sort: "created",
        direction: "desc",
        per_page: 100,
    });
    assert.deepEqual(calls.slice(1).map(call => call.inputs.issue_number), ["101", "102", "103"]);
    assert.ok(calls.slice(1).every(call => call.workflow_id === "issue-monster.lock.yml" && call.ref === "main"));
    assert.deepEqual(logs, [
        "Dispatched Issue Monster for issue #101",
        "Dispatched Issue Monster for issue #102",
        "Dispatched Issue Monster for issue #103",
    ]);
});

test("does not search for issues when the run created none", async () =>
{
    const calls = [];
    const issueNumbers = await dispatchCreatedIssues({
        github: createGitHub(calls, [{number: 102, body: runMarker(42)}]),
        context: {repo: {owner: "dotnet", repo: "sdk"}, runId: 42},
        core: {info() {}},
        temporaryIdMapInput: "",
        createdIssueNumberInput: "",
        ref: "main",
    });

    assert.deepEqual(issueNumbers, []);
    assert.deepEqual(calls, []);
});
