import assert from "node:assert/strict";
import test from "node:test";

import {
  normalizeBuild,
  parseArguments,
  sanitizeText,
  selectUnprocessedFailures
} from "./collect-ci-evidence.mjs";

test("parseArguments requires registry and output", () => {
  assert.deepEqual(
    parseArguments(["--registry", "pipelines.json", "--output", "dossier.json"]),
    { registry: "pipelines.json", output: "dossier.json" });
  assert.throws(() => parseArguments(["--registry", "pipelines.json"]), /required/);
  assert.throws(() => parseArguments(["registry", "pipelines.json"]), /Invalid argument/);
});

test("sanitizeText removes volatile values and bounds output", () => {
  const input = "2026-07-24T17:25:31.817Z job 123e4567-e89b-12d3-a456-426614174000 ";
  const sanitized = sanitizeText(input + "x".repeat(5_000));

  assert.match(sanitized, /^<timestamp> job <guid>/);
  assert.equal(sanitized.length, 4_000);
});

test("normalizeBuild retains only evidence fields", () => {
  const normalized = normalizeBuild({
    id: 42,
    buildNumber: "20260724.1",
    result: "failed",
    reason: "batchedCI",
    sourceBranch: "refs/heads/main",
    sourceVersion: "abc",
    definition: { id: 101, name: "dotnet-sdk-public-ci" },
    repository: { id: "dotnet/sdk" },
    _links: { web: { href: "https://example.test/build/42" } },
    untrustedExtraField: "excluded"
  });

  assert.equal(normalized.id, 42);
  assert.equal(normalized.url, "https://example.test/build/42");
  assert.equal("untrustedExtraField" in normalized, false);
});

test("bootstrap selects at most one historical failure", () => {
  const history = [
    { id: 4, result: "failed" },
    { id: 3, result: "succeeded" },
    { id: 2, result: "failed" }
  ];

  const selected = selectUnprocessedFailures({ pipelines: {} }, "pipeline:main", history);

  assert.equal(selected.bootstrap, true);
  assert.deepEqual(selected.failures.map(build => build.id), [4]);
});

test("subsequent polls select only unseen failures", () => {
  const history = [
    { id: 4, result: "failed" },
    { id: 3, result: "succeeded" },
    { id: 2, result: "failed" }
  ];
  const state = { pipelines: { "pipeline:main": { processedBuildIds: [3, 2] } } };

  const selected = selectUnprocessedFailures(state, "pipeline:main", history);

  assert.equal(selected.bootstrap, false);
  assert.deepEqual(selected.failures.map(build => build.id), [4]);
});