import assert from "node:assert/strict";
import test from "node:test";

import {PipelineHealthMonitor} from "../pipeline-health-monitor.mjs";

test("second missing main build is an actionable HIGH stable-branch incident", async () =>
{
    const state = {schemaVersion: 1, pipelines: {}};
    const pipeline = {
        definitionId: 101,
        repository: "dotnet/sdk",
        stableBranches: ["refs/heads/main"]
    };
    const branch = "refs/heads/main";
    const fetchImplementation = async () => new Response(JSON.stringify({
        sha: "main-head",
        commit: {committer: {date: "2026-08-13T12:00:00Z"}},
        html_url: "https://github.com/dotnet/sdk/commit/main-head"
    }), {status: 200});
    const azure = {listRecentBuilds: async () => []};
    const monitor = new PipelineHealthMonitor(state, fetchImplementation);
    const realDateNow = Date.now;
    Date.now = () => Date.parse("2026-08-13T14:00:00Z");

    try
    {
        const first = await monitor.checkPipeline(pipeline, branch, azure, "main-key");
        const second = await monitor.checkPipeline(pipeline, branch, azure, "main-key");

        assert.equal(first.actionable, false);
        assert.equal(second.actionable, true);
        assert.equal(second.monitoringScope, "stable-branch");
        assert.equal(second.priority, "HIGH");
    } finally
    {
        Date.now = realDateNow;
    }
});
