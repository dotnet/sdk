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
