const assert = require("node:assert/strict");
const {readFileSync} = require("node:fs");
const path = require("node:path");
const {test} = require("node:test");

const workflows = path.join(__dirname, "..", "workflows");
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;

function readWorkflow(name)
{
    return readFileSync(path.join(workflows, name), "utf8");
}

function getStep(source, name)
{
    const match = source.match(new RegExp(`(?<indent> +)- name: ${name}\\r?\\n(?<body>(?:\\k<indent> {2}[^\\r\\n]*\\r?\\n)+)`));
    assert.ok(match, `Missing step: ${name}`);
    return match.groups.body;
}

function getScript(step)
{
    const match = step.match(/(?<indent> +)script: \|\r?\n(?<body>(?:\k<indent> {2}[^\r\n]*\r?\n)+)/);
    assert.ok(match, "Missing github-script body");
    return match.groups.body.split(/\r?\n/).map((line) => line.slice(match.groups.indent.length + 2)).join("\n");
}

const assignment = readWorkflow("issue-monster-assigner.md");
const assignmentStep = getStep(assignment, "Validate assignment credential");
const validateAssignment = new AsyncFunction("process", "getOctokit", "context", getScript(assignmentStep));
const triage = readWorkflow("issue-triage.md");
const unlockStep = getStep(triage, "Unlock triaged issue with retries");
const unlockIssue = new AsyncFunction("github", "context", getScript(unlockStep));
const context = {repo: {owner: "dotnet", repo: "sdk"}, issue: {number: 56547}};

test("assignment authentication gates activation using only the separate write credential", () =>
{
    assert.match(assignment, /needs: \[assignment-auth\]/);
    assert.match(assignment, /assignment-auth:\s+runs-on: ubuntu-slim\s+environment: issue-monster\s+permissions: \{\}/);
    assert.match(assignmentStep, /ISSUE_MONSTER_ASSIGNMENT_TOKEN: \$\{\{ secrets\.ISSUE_MONSTER_ASSIGNMENT_TOKEN \}\}/);
    assert.doesNotMatch(assignmentStep, /COPILOT_PAT_|GITHUB_TOKEN \|\|/);
    assert.match(assignment, /needs\.safe_outputs\.outputs\.assign_to_agent_assigned/);
});

test("missing assignment credential fails before contacting GitHub", async () =>
{
    await assert.rejects(
        validateAssignment({env: {}}, () => assert.fail("Must not contact GitHub"), context),
        /Assignment credential is missing.*Renew ISSUE_MONSTER_ASSIGNMENT_TOKEN/);
});

test("assignment authentication uses the expected token and repository", async () =>
{
    let calls = 0;
    await validateAssignment({env: {ISSUE_MONSTER_ASSIGNMENT_TOKEN: "fixture-token"}}, (token) =>
    {
        assert.equal(token, "fixture-token");
        return {rest: {repos: {async get(params)
        {
            calls++;
            assert.deepEqual(params, context.repo);
        }}}};
    }, context);
    assert.equal(calls, 1);
});

for (const status of [401, 403, 500])
{
    test(`assignment HTTP ${status} fails explicitly without exposing credentials`, async () =>
    {
        await assert.rejects(
            validateAssignment({env: {ISSUE_MONSTER_ASSIGNMENT_TOKEN: "fixture-token"}}, () =>
                ({rest: {repos: {async get() { throw Object.assign(new Error("fixture-token"), {status}); }}}}), context),
            (error) =>
            {
                assert.match(error.message, new RegExp(`HTTP ${status}`));
                assert.match(error.message, /Do not use an inference-pool PAT/);
                assert.doesNotMatch(error.message, /fixture-token/);
                return true;
            });
    });
}

test("unlock retries transient failures without dropping the activation lock guard", () =>
{
    assert.match(triage, /lock-for-agent: true/);
    assert.match(unlockStep, /needs\.activation\.outputs\.issue_locked == 'true'/);
    assert.match(unlockStep, /github\.event_name == 'issues' \|\| github\.event_name == 'issue_comment'/);
    assert.match(unlockStep, /retries: 3/);
    assert.match(unlockStep, /retry-exempt-status-codes: 400,401,403,404,422/);
});

test("unlock addresses only the issue in event context", async () =>
{
    const calls = [];
    await unlockIssue({rest: {issues: {
        async get(params) { calls.push(["get", params]); return {data: {locked: true}}; },
        async unlock(params) { calls.push(["unlock", params]); },
    }}}, context);
    const params = {...context.repo, issue_number: context.issue.number};
    assert.deepEqual(calls, [["get", params], ["unlock", params]]);
});

for (const issue of [{locked: false}, {locked: true, pull_request: {}}])
{
    test(`unlock skips ${issue.pull_request ? "pull requests" : "already unlocked issues"}`, async () =>
    {
        await unlockIssue({rest: {issues: {
            async get() { return {data: issue}; },
            async unlock() { assert.fail("Must not unlock"); },
        }}}, context);
    });
}

test("unlock failures are not silently converted to success", async () =>
{
    await assert.rejects(unlockIssue({rest: {issues: {
        async get() { return {data: {locked: true}}; },
        async unlock() { throw new Error("GitHub unavailable"); },
    }}}, context), /GitHub unavailable/);
});

test("build-failure detection is gated by real outputs or a patch in both entry points", () =>
{
    const shared = readWorkflow(path.join("shared", "build-failure-analysis-shared.md"));
    assert.match(shared, /detection:\s+(?:#[^\r\n]*\r?\n\s+)*if: needs\.agent\.outputs\.output_types != '' \|\| needs\.agent\.outputs\.has_patch == 'true'/);
    for (const name of ["build-failure-analysis.md", "build-failure-analysis-command.md"])
    {
        assert.match(readWorkflow(name), /shared\/build-failure-analysis-shared\.md/);
    }
});

test("affected workflows default to the compatible model without disabling detection", () =>
{
    const monster = readWorkflow("issue-monster.md");
    const shared = readWorkflow(path.join("shared", "build-failure-analysis-shared.md"));
    assert.match(monster, /threat-detection:[\s\S]*model: claude-sonnet-5/);
    assert.match(shared, /model: \$\{\{ vars\.GH_AW_MODEL_AGENT_COPILOT \|\| vars\.GH_AW_DEFAULT_MODEL_COPILOT \|\| 'claude-sonnet-5' \}\}/);
    assert.doesNotMatch(monster, /threat-detection: false/);
});

function getJob(source, name)
{
    const match = source.match(new RegExp(`^  ${name}:\\r?\\n(?<body>[\\s\\S]*?)(?=^  \\S|$(?![\\s\\S]))`, "m"));
    assert.ok(match, `Missing compiled job: ${name}`);
    return match.groups.body;
}

test("compiled assignment gate precedes activation and agent execution", () =>
{
    const compiled = readWorkflow("issue-monster-assigner.lock.yml");
    for (const name of ["pre_activation", "activation", "agent"])
    {
        assert.match(getJob(compiled, name), /needs:[\s\S]*assignment-auth/);
    }
    const auth = getJob(compiled, "assignment-auth");
    assert.match(auth, /environment: issue-monster/);
    assert.match(auth, /permissions: \{\}/);
    assert.doesNotMatch(auth, /^\s+needs:/m);
    assert.equal(getScript(getStep(compiled, "Validate assignment credential")), getScript(assignmentStep));
});

test("compiled unlock retries before the framework unlock in its existing privileged job", () =>
{
    const compiled = readWorkflow("issue-triage.lock.yml");
    const job = getJob(compiled, "unlock");
    assert.match(job, /issues: write/);
    assert.ok(job.indexOf("Unlock triaged issue with retries") < job.indexOf("Unlock issue after agentic workflow"));
    const step = getStep(job, "Unlock triaged issue with retries");
    assert.match(step, /retries: 3/);
    assert.match(step, /retry-exempt-status-codes: 400,401,403,404,422/);
    assert.equal(getScript(step), getScript(unlockStep));
});

test("both compiled analyzers retain normal detection for outputs and patches", () =>
{
    for (const name of ["build-failure-analysis.lock.yml", "build-failure-analysis-command.lock.yml"])
    {
        const compiled = readWorkflow(name);
        const job = getJob(compiled, "detection");
        assert.match(job, /needs\.agent\.outputs\.output_types != '' \|\| needs\.agent\.outputs\.has_patch == 'true'/);
        assert.match(job, /needs\.agent\.result != 'skipped'/);
        const condition = job.match(/if: >\r?\n\s+([^\r\n]+)/);
        assert.ok(condition, "Missing compiled detection condition");
        const shouldDetect = new Function("needs", "always", `return ${condition[1]};`);
        for (const [result, outputTypes, hasPatch, expected] of [
            ["success", "", "false", false],
            ["failure", "", "false", false],
            ["success", "noop", "false", true],
            ["failure", "add_comment", "false", true],
            ["success", "", "true", true],
            ["skipped", "add_comment", "true", false],
        ])
        {
            assert.equal(shouldDetect({agent: {result, outputs: {output_types: outputTypes, has_patch: hasPatch}}}, () => true),
                expected, `${name}: ${result}, output_types=${outputTypes}, has_patch=${hasPatch}`);
        }
        assert.match(job, /Install threat-detect binary/);
        assert.match(job, /Execute threat detection with AWF/);
        assert.match(job, /COPILOT_MODEL: .*'claude-sonnet-5'/);
        assert.match(job, /environment: copilot-pat-pool/);
    }
});
