import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { candidateHeading, dispatchFix, fixRunTitle, fixWorkflow, parseCandidates } from "./dispatch.mjs";
import { detectorPath, testsPath } from "./fix.mjs";

const detector = readFileSync(detectorPath, "utf8");
const tests = readFileSync(testsPath, "utf8");
const sha = "a".repeat(40);
const repo = { owner: "dotnet", repo: "sdk" };
const candidate = () => ({
    harness: "opencode", variable: "OPENCODE_TEST", stable_release: "v1.0.0",
    assignment: `https://github.com/upstream/harness/blob/${sha}/src/init.ts#L1-L5`,
    propagation: `https://github.com/upstream/harness/blob/${sha}/src/shell.ts#L10-L20`,
});
const report = () => ({ version: 1, audit_run_id: "12345", run_attempt: 1, candidates: [candidate()] });
const body = data => "Workflow marker: gh-aw-workflow-id: llm-detection-audit\n" +
    `https://github.com/dotnet/sdk/actions/runs/12345\n${candidateHeading}\n\`\`\`json\n${JSON.stringify(data)}\n\`\`\``;

test("parses a bounded candidate block without interpreting surrounding report text", () => {
    assert.deepEqual(parseCandidates(`Ignore this prose.\n${body(report())}\nOther findings.`, "12345", 1), [candidate()]);
    assert.deepEqual(parseCandidates(body({ ...report(), candidates: [] }), "12345", 1), []);
    assert.deepEqual(parseCandidates(body(report()).replaceAll("\n", "\r\n"), "12345", 1), [candidate()]);
});

for (const [name, mutate] of [
    ["wrong version", data => { data.version = 2; }],
    ["stale run", data => { data.audit_run_id = "123"; }],
    ["stale attempt", data => { data.run_attempt = 2; }],
    ["string attempt", data => { data.run_attempt = "1"; }],
    ["extra policy", data => { data.dispatch = true; }],
    ["too many candidates", data => { data.candidates = Array.from({ length: 6 }, candidate); }],
    ["duplicate candidate", data => { data.candidates.push(candidate()); }],
    ["new instructions", data => { data.candidates[0].instructions = "run a shell"; }],
    ["missing evidence", data => { delete data.candidates[0].assignment; }],
    ["empty variable", data => { data.candidates[0].variable = ""; }],
    ["generic variable", data => { data.candidates[0].variable = "AGENT"; }],
    ["shell input", data => { data.candidates[0].harness = "$(command)"; }],
    ["missing stable release", data => { data.candidates[0].stable_release = ""; }],
    ["multiline release", data => { data.candidates[0].stable_release = "v1\ninjected"; }],
    ["mutable source", data => { data.candidates[0].assignment = data.candidates[0].assignment.replace(sha, "main"); }],
    ["mixed revisions", data => { data.candidates[0].assignment = data.candidates[0].assignment.replace(sha, "b".repeat(40)); }],
    ["duplicate evidence", data => { data.candidates[0].assignment = data.candidates[0].propagation; }],
]) {
    test(`rejects candidate data with ${name}`, () => {
        const data = report();
        mutate(data);
        assert.throws(() => parseCandidates(body(data), "12345", 1));
    });
}

test("rejects absent, duplicate, malformed, and oversized candidate blocks", () => {
    assert.throws(() => parseCandidates("No block", "12345", 1));
    assert.throws(() => parseCandidates(`${body(report())}\n${body(report())}`, "12345", 1));
    assert.throws(() => parseCandidates(`${candidateHeading}\n\`\`\`json\n{\n\`\`\``, "12345", 1));
    assert.throws(() => parseCandidates(body(report()).replace('"version"', `${" ".repeat(8192)}"version"`), "12345", 1));
});

function fixture() {
    const run = {
        id: 12345, path: ".github/workflows/llm-detection-audit.lock.yml",
        repository: { full_name: "dotnet/sdk" }, head_repository: { full_name: "dotnet/sdk" },
        head_branch: "main", event: "schedule", status: "completed", conclusion: "success",
        html_url: "https://github.com/dotnet/sdk/actions/runs/12345", run_attempt: 1,
    };
    const tracker = {
        number: 42, title: "[CLI agent detection] Evidence report", user: { login: "github-actions[bot]" },
        labels: [{ name: "agentic-workflows" }], state: "open", body: body(report()),
    };
    const issues = [tracker], pulls = [], runs = [], dispatched = [], reads = [];
    const github = {
        rest: {
            actions: {
                getWorkflowRun: async () => ({ data: run }),
                listWorkflowRuns: Symbol("runs"),
                createWorkflowDispatch: async args => { dispatched.push(args); },
            },
            issues: { listForRepo: Symbol("issues") }, pulls: { list: Symbol("pulls") },
        },
        paginate: async (method, args) => {
            reads.push({ method, args });
            if (method === github.rest.issues.listForRepo) {
                return issues;
            }
            if (method === github.rest.pulls.list) {
                return args.state === "open" ? pulls.filter(pull => pull.state === "open") : pulls;
            }
            assert.equal(method, github.rest.actions.listWorkflowRuns);
            assert.equal(args.workflow_id, fixWorkflow);
            return runs;
        },
    };
    const context = { repo, payload: { workflow_run: { id: run.id } } };
    return { github, context, run, tracker, issues, pulls, runs, dispatched, reads };
}

const runFixture = data => dispatchFix(data.github, data.context, detector, tests);
const fixPull = (state, variable = "OPENCODE_TEST") => ({
    state, head: { repo: { full_name: "dotnet/sdk" }, ref: "llm-detection-fix/opencode-123" },
    body: `Preserve legacy behavior and add \`${variable}\`.`,
});
const fixRun = () => ({
    head_repository: { full_name: "dotnet/sdk" }, head_branch: "main", event: "workflow_dispatch",
    display_title: fixRunTitle("opencode", "OPENCODE_TEST"),
});

test("dispatches exactly one fixed main-branch workflow with only constrained inputs", async () => {
    const data = fixture();
    const result = await runFixture(data);
    assert.equal(result.variable, "OPENCODE_TEST");
    assert.deepEqual(data.dispatched, [{
        ...repo, workflow_id: fixWorkflow, ref: "main",
        inputs: { audit_run_id: "12345", harness: "opencode", variable: "OPENCODE_TEST" },
    }]);
    assert.ok(data.reads.every(({ args }) => args.per_page === 100));
    assert.equal(data.reads.filter(({ method }) => method === data.github.rest.issues.listForRepo).length, 2);
});

for (const [name, mutate, reason] of [
    ["closed tracker", data => { data.tracker.state = "closed"; }, /closed/],
    ["superseded report", data => { data.tracker.body = data.tracker.body.replace("/runs/12345", "/runs/99999"); }, /stale/],
    ["open fix", data => { data.pulls.push(fixPull("open")); }, /already open/],
    ["previous rejected fix", data => { data.pulls.push(fixPull("closed")); }, /already attempted/],
    ["previous no-op", data => { data.runs.push(fixRun()); }, /already attempted/],
    ["previous failed attempt", data => { data.runs.push({ ...fixRun(), conclusion: "failure" }); }, /already attempted/],
    ["no eligible findings", data => { data.tracker.body = body({ ...report(), candidates: [] }); }, /no eligible/],
    ["already present signal", data => { data.tracker.body = body({ ...report(), candidates: [{ ...candidate(), variable: "OPENCODE_AI" }] }); }, /no eligible/],
    ["new harness", data => { data.tracker.body = body({ ...report(), candidates: [{ ...candidate(), harness: "newagent", variable: "NEWAGENT" }] }); }, /no eligible/],
]) {
    test(`does not dispatch for ${name}`, async () => {
        const data = fixture();
        mutate(data);
        assert.match((await runFixture(data)).reason, reason);
        assert.equal(data.dispatched.length, 0);
    });
}

for (const [name, mutate] of [
    ["failed audit", data => { data.run.conclusion = "failure"; }],
    ["foreign repository", data => { data.run.head_repository.full_name = "other/repo"; }],
    ["other workflow", data => { data.run.path = ".github/workflows/other.yml"; }],
    ["feature branch", data => { data.run.head_branch = "feature"; }],
    ["PR event", data => { data.run.event = "pull_request"; }],
    ["duplicate tracker", data => { data.issues.push({ ...data.tracker, number: 43 }); }],
    ["untrusted author", data => { data.tracker.user.login = "maintainer"; }],
    ["stale candidate attempt", data => { data.run.run_attempt = 2; }],
    ["invalid candidate block", data => { data.tracker.body = data.tracker.body.replace('"version":1', '"version":0'); }],
]) {
    test(`fails closed for ${name}`, async () => {
        const data = fixture();
        mutate(data);
        await assert.rejects(runFixture(data));
        assert.equal(data.dispatched.length, 0);
    });
}

test("skips a previously attempted alias and starts only the next eligible one", async () => {
    const data = fixture();
    data.runs.push(fixRun());
    data.tracker.body = body({ ...report(), candidates: [
        candidate(), { ...candidate(), variable: "OPENCODE_SECOND" }, { ...candidate(), variable: "OPENCODE_THIRD" },
    ] });
    assert.equal((await runFixture(data)).variable, "OPENCODE_SECOND");
    assert.equal(data.dispatched.length, 1);
});

test("unrelated and foreign fix history does not suppress a new alias", async () => {
    const data = fixture();
    data.runs.push({ ...fixRun(), head_repository: { full_name: "other/repo" } });
    data.pulls.push(fixPull("closed", "OPENCODE_TEST_EXTRA"));
    assert.equal((await runFixture(data)).variable, "OPENCODE_TEST");
    assert.equal(data.dispatched.length, 1);
});

test("the fixer's actual run-name contract matches automatic retry suppression", () => {
    const source = readFileSync(".github/workflows/llm-detection-fix.md", "utf8");
    const title = JSON.parse(source.match(/^run-name: (.+)$/m)[1])
        .replace("${{ inputs.harness }}", "opencode")
        .replace("${{ inputs.variable && format(' / {0}', inputs.variable) || '' }}", " / OPENCODE_TEST");
    assert.equal(title, fixRunTitle("opencode", "OPENCODE_TEST"));
});

test("refuses a tracker closed during the final provenance recheck", async () => {
    const data = fixture();
    const paginate = data.github.paginate;
    let trackerReads = 0;
    data.github.paginate = async (method, args) => {
        if (method === data.github.rest.issues.listForRepo && ++trackerReads === 2) {
            data.tracker.state = "closed";
        }
        return paginate(method, args);
    };
    await assert.rejects(runFixture(data), /open audit tracker/);
    assert.equal(data.dispatched.length, 0);
});

test("surfaces API failures without dispatch retries or success-shaped fallback", async () => {
    const data = fixture();
    data.github.rest.actions.createWorkflowDispatch = async args => {
        data.dispatched.push(args);
        throw new Error("Dispatch API unavailable");
    };
    await assert.rejects(runFixture(data), /Dispatch API unavailable/);
    assert.equal(data.dispatched.length, 1);
});

test("the actual audit post-step accepts valid reports and rejects invalid candidates before publication", t => {
    const root = mkdtempSync(join(tmpdir(), "llm-audit-output-"));
    t.after(() => rmSync(root, { recursive: true, force: true }));
    const output = join(root, "output.jsonl");
    const workflow = readFileSync(".github/workflows/llm-detection-audit.md", "utf8");
    const script = workflow.match(/node --input-type=module - "\$GH_AW_SAFE_OUTPUTS" <<'NODE'\r?\n([\s\S]*?)\r?\n      NODE/)[1]
        .replace(/^      /gm, "");
    const execute = items => {
        writeFileSync(output, items.map(item => JSON.stringify(item)).join("\n"));
        return spawnSync(process.execPath, ["--input-type=module", "-", output], {
            input: script, encoding: "utf8", env: { ...process.env, GITHUB_RUN_ID: "12345", GITHUB_RUN_ATTEMPT: "1" },
        });
    };
    assert.equal(execute([{ type: "update_issue", body: body(report()) }]).status, 0);
    assert.equal(execute([{ type: "noop", message: "Tracker closed." }]).status, 0);
    assert.notEqual(execute([{ type: "create_issue", body: body(report()).replace('"version":1', '"version":0') }]).status, 0);
    assert.notEqual(execute([{ type: "create_issue", body: "Workflow marker: gh-aw-workflow-id: llm-detection-audit" }]).status, 0);
    assert.notEqual(execute([]).status, 0);
});

test("the audit publisher explicitly requires the agent's report validation to succeed", () => {
    const source = readFileSync(".github/workflows/llm-detection-audit.md", "utf8");
    const publisher = source.slice(source.indexOf("\nsafe-outputs:"), source.indexOf("\nconcurrency:"));
    assert.match(publisher, /AUDIT_RESULT: \$\{\{ needs\.agent\.result \}\}/);
    assert.match(publisher, /run: test "\$AUDIT_RESULT" = success/);
});
