import assert from "node:assert/strict";
import {readFile} from "node:fs/promises";
import test from "node:test";

const workflowUrl = new URL("../../workflows/ci-quality-monitor.md", import.meta.url);

test("stable HIGH incidents may request live-build-incident", async () =>
{
    const workflow = await readFile(workflowUrl, "utf8");

    assert.match(workflow, /allowed-labels: \[[^\]]*live-build-incident[^\]]*\]/);
    assert.match(workflow, /Request `Test Debt` and `live-build-incident` only when the dossier marks the failure as `monitoringScope: stable-branch` and `priority: HIGH`\./);
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
