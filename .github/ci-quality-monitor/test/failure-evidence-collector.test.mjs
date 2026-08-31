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

test("related observations are compacted after recurrence analysis", async () =>
{
    let collection = 0;
    const azure = {
        getTimeline: async () => ({records: []}),
        getTestFailures: async () => []
    };
    const current = {
        kind: "test", phase: "test-execution", failureType: "test-assertion",
        component: "Sdk.Tests.Flaky", mechanism: "current", fingerprint: "current",
        mechanismFingerprint: "shared", actionable: true,
        kbe: {eligible: true, validation: {valid: true}}
    };
    const related = Array.from({length: 20}, (_, index) => ({
        kind: "test", phase: "test-execution", failureType: "test-assertion",
        component: index === 19 ? current.component : `Other.${index}`,
        mechanism: "m".repeat(5_000), fingerprint: `related-${index}`,
        mechanismFingerprint: index === 19 ? current.mechanismFingerprint : `other-${index}`,
        actionable: true, stackTrace: "s".repeat(80_000)
    }));
    const helix = {
        collectObservations: async () => collection++ === 0 ? [current] : related
    };
    const collector = new FailureEvidenceCollector(() => azure, helix);

    const result = await collector.collectFailureEvidence(
        {repository: "dotnet/sdk"},
        {id: 2, result: "failed", sourceVersion: "current", validationResults: []},
        [{id: 2, result: "failed", sourceVersion: "current"},
         {id: 1, result: "failed", sourceVersion: "previous"}]);

    const compact = result.relatedFailureSummaries[0].observations;
    assert.equal(compact.length, 10);
    assert.equal(compact[0].component, current.component);
    assert.equal(compact[0].mechanism.length, 1_000);
    assert.equal("stackTrace" in compact[0], false);
    assert.ok(JSON.stringify(result).length < 100_000);
});
