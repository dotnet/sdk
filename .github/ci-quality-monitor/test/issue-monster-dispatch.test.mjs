import assert from "node:assert/strict";
import {createRequire} from "node:module";
import test from "node:test";

const require = createRequire(import.meta.url);
const {
    dispatchCreatedIssues,
    getCreatedIssueNumbers,
} = require("../issue-monster-dispatch.js");

test("collects all created issues in the current repository", () =>
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

test("dispatches Issue Monster once for each created issue", async () =>
{
    const calls = [];
    const logs = [];
    const issueNumbers = await dispatchCreatedIssues({
        github: {
            rest: {
                actions: {
                    async createWorkflowDispatch(args)
                    {
                        calls.push(args);
                    },
                },
            },
        },
        context: {repo: {owner: "dotnet", repo: "sdk"}},
        core: {info: (message) => logs.push(message)},
        temporaryIdMapInput: JSON.stringify({
            "issue-one": {repo: "dotnet/sdk", number: 101},
            "issue-two": {repo: "dotnet/sdk", number: 102},
        }),
        createdIssueNumberInput: "101",
        ref: "main",
    });

    assert.deepEqual(issueNumbers, [101, 102]);
    assert.deepEqual(calls, [
        {
            owner: "dotnet",
            repo: "sdk",
            workflow_id: "issue-monster.lock.yml",
            ref: "main",
            inputs: {issue_number: "101"},
        },
        {
            owner: "dotnet",
            repo: "sdk",
            workflow_id: "issue-monster.lock.yml",
            ref: "main",
            inputs: {issue_number: "102"},
        },
    ]);
    assert.deepEqual(logs, [
        "Dispatched Issue Monster for issue #101",
        "Dispatched Issue Monster for issue #102",
    ]);
});
