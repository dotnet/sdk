const assert = require("node:assert/strict");
const test = require("node:test");

const { prepareIssues } = require("./apply-issue-output.cjs");

const validatedTest = {
  kind: "test",
  signature: "test|sdk.test|503",
  kbe: {
    eligible: true,
    recurring: true,
    errorMessage: ["Sdk.Test", "HttpRequestException", "503 (Service Unavailable)"],
    buildRetry: true,
    excludeConsoleLog: false,
    validation: { valid: true }
  }
};

function output(issueKind, body = "## Build Information\nBuild failed in CI.") {
  return { items: [{
    type: "create_ci_quality_issue",
    issue_kind: issueKind,
    title: "Investigate failure",
    body,
    signature: validatedTest.signature
  }] };
}

test("ordinary issues never receive KBE label or block", () => {
  const issue = prepareIssues(output("ordinary"), { failures: [{ observations: [validatedTest] }] })[0];

  assert.deepEqual(issue.labels, ["agentic-workflows"]);
  assert.doesNotMatch(issue.body, /^## Error Message$/m);
  assert.match(issue.body, /<!-- ci-quality-signature-sha256: [a-f0-9]{64} -->/);
});

test("validated named test KBE receives fixed label and generated block", () => {
  const issue = prepareIssues(output("test-kbe"), { failures: [{ observations: [validatedTest] }] })[0];

  assert.deepEqual(issue.labels, ["agentic-workflows", "Known Build Error"]);
  assert.match(issue.body, /^## Error Message$/m);
  assert.match(issue.body, /"ErrorMessage"/);
  assert.match(issue.body, /"BuildRetry": true/);
});

test("non-test and unvalidated signatures cannot become KBEs", () => {
  assert.throws(
    () => prepareIssues(output("test-kbe"), { failures: [{ observations: [{ kind: "build", signature: validatedTest.signature }] }] }),
    /not a recurring, collector-validated named test/);
});

  test("validated but non-recurring tests cannot become KBEs", () => {
    const nonRecurring = { ...validatedTest, kbe: { ...validatedTest.kbe, recurring: false } };
    assert.throws(
    () => prepareIssues(output("test-kbe"), { failures: [{ observations: [nonRecurring] }] }),
    /not a recurring, collector-validated named test/);
  });

test("ordinary issues cannot smuggle a KBE block", () => {
  assert.throws(
    () => prepareIssues(output("ordinary", "## Error Message\n```json\n{}\n```"), { failures: [] }),
    /must not contain/);
});

test("live evaluation applies requested fork labels without changing production defaults", () => {
  process.env.CI_QUALITY_LIVE_EVALUATION = "true";
  try {
    const issue = prepareIssues(output("ordinary"), { failures: [{ observations: [validatedTest] }] })[0];
    assert.deepEqual(issue.labels, ["agentic-workflows", "cookie", "Test Debt"]);

    const nonRecurring = { ...validatedTest, kbe: { ...validatedTest.kbe, recurring: false } };
    const kbe = prepareIssues(output("test-kbe"), { failures: [{ observations: [nonRecurring] }] })[0];
    assert.deepEqual(kbe.labels, ["agentic-workflows", "Known Build Error", "cookie", "Test Debt"]);
  } finally {
    delete process.env.CI_QUALITY_LIVE_EVALUATION;
  }
});