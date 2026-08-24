import assert from "node:assert/strict";
import test from "node:test";

import {MAX_TEST_DIAGNOSTIC_CHARACTERS, MAX_TEST_FAILURES} from "../constants.mjs";
import {parseTestResultXml} from "../test-results.mjs";

test("TRX parser bounds failure count and diagnostic text", () =>
{
    const oversizedMessage = "m".repeat(MAX_TEST_DIAGNOSTIC_CHARACTERS + 100);
    const oversizedStack = "s".repeat(MAX_TEST_DIAGNOSTIC_CHARACTERS + 100);
    const failures = Array.from({length: MAX_TEST_FAILURES + 5}, (_, index) => `
    <UnitTestResult testId="test-${index}" testName="Test${index}" outcome="Failed">
      <Output><ErrorInfo><Message>${oversizedMessage}</Message><StackTrace>${oversizedStack}</StackTrace></ErrorInfo></Output>
    </UnitTestResult>`).join("");

    const results = parseTestResultXml(`
    <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
      <Results>${failures}</Results>
    </TestRun>`);

    assert.equal(results.failures.length, MAX_TEST_FAILURES);
    assert.ok(results.failures.every(failure => failure.errorMessage.length === MAX_TEST_DIAGNOSTIC_CHARACTERS));
    assert.ok(results.failures.every(failure => failure.stackTrace.length === MAX_TEST_DIAGNOSTIC_CHARACTERS));
});
