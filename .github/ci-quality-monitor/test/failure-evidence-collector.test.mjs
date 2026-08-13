import assert from "node:assert/strict";
import test from "node:test";

import {MAX_HELIX_REFERENCES} from "../constants.mjs";
import {FailureEvidenceCollector} from "../failure-evidence-collector.mjs";

test("current build Helix collection is bounded", async () =>
{
    const observedLimits = [];
    const azure = {
        getTimeline: async () => ({records: []}),
        getTestFailures: async () => []
    };
    const helix = {
        collectObservations: async (_failures, maxReferences) =>
        {
            observedLimits.push(maxReferences);
            return [];
        }
    };
    const collector = new FailureEvidenceCollector(() => azure, helix);

    await collector.collectFailureEvidence(
        {repository: "dotnet/sdk"},
        {id: 1, validationResults: []},
        [{id: 1}]);

    assert.deepEqual(observedLimits, [MAX_HELIX_REFERENCES]);
});
