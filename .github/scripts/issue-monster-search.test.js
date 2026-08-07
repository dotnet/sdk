const assert = require("node:assert/strict");
const { test } = require("node:test");

const searchIssueMonsterCandidates = require("./issue-monster-search.js");

function createIssue(number, overrides = {}) {
  return {
    number,
    title: `Issue ${number}`,
    body: `Body ${number}`,
    state: "open",
    created_at: "2026-08-01T00:00:00Z",
    labels: [{ name: "cookie" }, { name: "bug" }],
    assignees: [],
    ...overrides,
  };
}

function createCore() {
  const outputs = {};
  const logs = { info: [], warning: [], error: [] };

  return {
    outputs,
    logs,
    setOutput(name, value) {
      outputs[name] = value;
    },
    info(message) {
      logs.info.push(message);
    },
    warning(message) {
      logs.warning.push(message);
    },
    error(message) {
      logs.error.push(message);
    },
  };
}

function createGithub({ candidates = [], issues = {}, recentPRs = [], rateLimitComments = {} } = {}) {
  const calls = [];
  const github = {
    rest: {
      search: {
        async issuesAndPullRequests(args) {
          const operation = args.q.startsWith("is:pr") ? "recent-pr-search" : "candidate-search";
          calls.push(operation);
          const items = operation === "recent-pr-search" ? recentPRs : candidates;
          return { data: { total_count: items.length, items } };
        },
      },
      issues: {
        async get({ issue_number: issueNumber }) {
          calls.push(`issue-get:${issueNumber}`);
          return { data: issues[issueNumber] };
        },
      },
    },
    async graphql(query, { number }) {
      if (query.includes("pullRequest(number")) {
        calls.push(`rate-limit-comments:${number}`);
        return {
          repository: { pullRequest: { timelineItems: { nodes: rateLimitComments[number] || [] } } },
        };
      }

      calls.push(`issue-details:${number}`);
      return {
        repository: {
          issue: {
            subIssues: { totalCount: 0 },
            timelineItems: { nodes: [] },
          },
        },
      };
    },
  };

  return { github, calls };
}

async function runScenario(options = {}, requestedIssueNumberInput = "") {
  const core = createCore();
  const { github, calls } = createGithub(options);

  await searchIssueMonsterCandidates({
    github,
    context: { repo: { owner: "dotnet", repo: "sdk" } },
    core,
    requestedIssueNumberInput,
  });

  return { core, calls };
}

test("returns candidates and logs the ranked summary", async () => {
  const candidate = createIssue(123);
  const { core, calls } = await runScenario({ candidates: [candidate], issues: { 123: candidate } });

  assert.equal(core.outputs.issue_count, 1);
  assert.equal(core.outputs.issue_numbers, "123");
  assert.equal(core.outputs.has_issues, "true");
  assert.match(core.outputs.issue_list, /^#123: Issue 123 \[cookie, bug\] \(score: \d+\.\d\)$/);
  assert.ok(core.logs.info.includes("Total candidate issues: 1"));
  assert.ok(core.logs.info.some((message) => message.startsWith("Top candidates:\n#123:")));
  assert.deepEqual(calls, ["recent-pr-search", "candidate-search", "issue-get:123", "issue-details:123"]);
});

test("returns empty outputs and logs when no candidates are available", async () => {
  const { core } = await runScenario();

  assert.deepEqual(core.outputs, {
    issue_count: 0,
    issue_numbers: "",
    issue_list: "",
    issue_context: "",
    has_issues: "false",
  });
  assert.ok(core.logs.info.includes("Total candidate issues: 0"));
  assert.ok(core.logs.info.includes("🍽️ No suitable candidate issues - the plate is empty!"));
  assert.ok(!core.logs.info.some((message) => message.startsWith("Top candidates:")));
});

test("stops before candidate search when recent comments indicate rate limiting", async () => {
  const { core, calls } = await runScenario({
    recentPRs: [{ number: 456 }],
    rateLimitComments: { 456: [{ body: "GitHub reported a secondary rate limit" }] },
  });

  assert.equal(core.outputs.issue_count, 0);
  assert.equal(core.outputs.has_issues, "false");
  assert.deepEqual(calls, ["recent-pr-search", "rate-limit-comments:456"]);
  assert.ok(core.logs.warning.includes("Rate limiting detected in recent PRs. Skipping issue assignment."));
  assert.ok(!core.logs.info.some((message) => message.startsWith("Total candidate issues:")));
});