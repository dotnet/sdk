import assert from "node:assert/strict";
import test from "node:test";

import {
  createErrorMessageBlock,
  createTestKbeCandidate,
  validateErrorMessagePattern
} from "./known-build-error.mjs";

const failure = {
  fullyQualifiedName: "Sdk.Tests.PackageTest.UpdatesPackages",
  errorMessage: `Test method Sdk.Tests.PackageTest.UpdatesPackages threw exception:
FluentAssertions.Execution.AssertionFailedException: Expected command to pass.
Unhandled exception: Response status code does not indicate success: 503 (Service Unavailable).`
};

test("test KBE candidate uses distinct ordered lines and validates", () => {
  const candidate = createTestKbeCandidate(failure, "test|package|503");

  assert.equal(candidate.eligible, true);
  assert.deepEqual(candidate.errorMessage, [
    "Sdk.Tests.PackageTest.UpdatesPackages",
    "FluentAssertions.Execution.AssertionFailedException",
    "Response status code does not indicate success: 503 (Service Unavailable)."
  ]);
  assert.equal(candidate.buildRetry, true);
  assert.equal(candidate.validation.valid, true);
});

test("ordered validation rejects two values on one line", () => {
  const validation = validateErrorMessagePattern(["PackageTest", "503"], "PackageTest failed with 503");

  assert.equal(validation.valid, false);
  assert.equal(validation.missing, "503");
});

test("KBE block contains only Build Analysis fields", () => {
  const block = createErrorMessageBlock(createTestKbeCandidate(failure, "test|package|503"));

  assert.match(block, /^## Error Message/);
  assert.match(block, /"ErrorMessage"/);
  assert.match(block, /"BuildRetry": true/);
  assert.match(block, /"ExcludeConsoleLog": false/);
  assert.doesNotMatch(block, /ErrorPattern/);
});