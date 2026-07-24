const assert = require("node:assert/strict");
const test = require("node:test");

const { applyIssue, prepareIssues } = require("./apply-issue-output.cjs");

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

const validBody = `## Build Information
Build failed in CI.

## Failure History
One observed failure.

## Error Details
Specific error.

## Root Cause Analysis
**Observed:** The task emitted the specific error.

**Assessment:** The proximate cause is established; the underlying cause is not.

**Confidence:** Medium

**Alternatives / Unknowns:** Target-branch behavior is unknown.

## Suggested Investigation
Compare the target-branch build.`;

function output(issueKind, body = validBody) {
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

test("trusted title prefix is applied exactly once", () => {
  const agentOutput = output("ordinary");
  agentOutput.items[0].title = "[AI discovered CI] [AI discovered CI] Investigate failure";

  const issue = prepareIssues(agentOutput, { failures: [{ observations: [validatedTest] }] })[0];

  assert.equal(issue.title, "[AI discovered CI] Investigate failure");
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
    () => prepareIssues(output("ordinary", `${validBody}\n\n## Error Message\n\`\`\`json\n{}\n\`\`\``), { failures: [{ observations: [validatedTest] }] }),
    /must not contain/);
});

test("issues require a bounded root cause analysis", () => {
  assert.throws(
    () => prepareIssues(output("ordinary", "## Build Information\nBuild failed."), { failures: [{ observations: [validatedTest] }] }),
    /missing required sections/);
  assert.throws(
    () => prepareIssues(output("ordinary", validBody.replace("**Confidence:** Medium", "**Confidence:** Maybe")), { failures: [{ observations: [validatedTest] }] }),
    /confidence must be High, Medium, or Low/);
});

test("live evaluation applies requested fork labels without changing production defaults", () => {
  process.env.CI_QUALITY_LIVE_EVALUATION = "true";
  try {
    const issue = prepareIssues(output("ordinary"), { failures: [{ observations: [validatedTest] }] })[0];
    assert.deepEqual(issue.labels, ["agentic-workflows", "cookie", "Test Debt"]);
    assert.match(issue.body, /^> Fork-only CI monitor evaluation; not a production tracking issue\./);

    const nonRecurring = { ...validatedTest, kbe: { ...validatedTest.kbe, recurring: false } };
    const kbe = prepareIssues(output("test-kbe"), { failures: [{ observations: [nonRecurring] }] })[0];
    assert.deepEqual(kbe.labels, ["agentic-workflows", "Known Build Error", "cookie", "Test Debt"]);
    assert.match(kbe.body, /^> Fork-only CI monitor evaluation; not a production tracking issue\./);
  } finally {
    delete process.env.CI_QUALITY_LIVE_EVALUATION;
  }
});

test("HIGH stable-branch labels are derived from trusted dossier metadata", () => {
  const issue = prepareIssues(output("ordinary"), {
    failures: [{ monitoringCategory: "stable-branch", priority: "HIGH", observations: [validatedTest] }]
  })[0];

  assert.deepEqual(issue.labels, ["agentic-workflows", "Test Debt", "live-build-incident"]);
});

test("ordinary issues require an actionable collector signature", () => {
  assert.throws(
    () => prepareIssues(output("ordinary"), { failures: [{ observations: [{ ...validatedTest, actionable: false }] }] }),
    /not an actionable collector observation/);
});

test("an existing issue receives missing HIGH promotion labels", async () => {
  const added = [];
  const github = { rest: {
    search: { issuesAndPullRequests: async () => ({ data: { total_count: 1, items: [{ number: 42, labels: ["agentic-workflows"] }] } }) },
    issues: { addLabels: async options => added.push(options) }
  } };
  const issue = prepareIssues(output("ordinary"), {
    failures: [{ monitoringCategory: "stable-branch", priority: "HIGH", observations: [validatedTest] }]
  })[0];

  await applyIssue(issue, github, { repo: { owner: "dotnet", repo: "sdk" } }, { info() {} }, false);

  assert.deepEqual(added, [{
    owner: "dotnet", repo: "sdk", issue_number: 42,
    labels: ["Test Debt", "live-build-incident"]
  }]);
});

test("the trusted applicator rejects more than three issue writes", () => {
  const agentOutput = output("ordinary");
  agentOutput.items = Array.from({ length: 4 }, () => ({ ...agentOutput.items[0] }));

  assert.throws(
    () => prepareIssues(agentOutput, { failures: [{ observations: [validatedTest] }] }),
    /At most 3/);
});

test("issue application fails closed without a trusted signature marker", async () => {
  await assert.rejects(
    () => applyIssue({ body: "unsigned", labels: [] }, {}, {}, {}, false),
    /missing its trusted signature marker/);
});