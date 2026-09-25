import assert from "node:assert/strict";
import { execFile, execFileSync } from "node:child_process";
import { cp, mkdtemp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test, { after, before } from "node:test";
import { fileURLToPath, pathToFileURL } from "node:url";
import { promisify } from "node:util";
import { expandContext, finish, prepare, printBatch, record, rulesHash } from "../workflow.mjs";
import { completeSubmission, getSourceTools, requestSourceTools, startSourceServer, submissionReceipt } from "../source-tools.mjs";
import { collectWorkflowDiagnostics } from "../diagnostics.mjs";

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const workflowDirectory = path.join(repository, ".github/workflows");
const environmentNames = ["GITHUB_SHA", "GITHUB_OUTPUT", "GITHUB_STEP_SUMMARY", "STALE_REFERENCE_INPUT"];
const environment = Object.fromEntries(environmentNames.map(name => [name, process.env[name]]));
before(() =>
{
    for (const name of environmentNames)
    {
        delete process.env[name];
    }
});
after(() =>
{
    for (const [name, value] of Object.entries(environment))
    {
        if (value !== undefined)
        {
            process.env[name] = value;
        }
    }
});

async function fixture(t, padding = 0)
{
    const root = await mkdtemp(path.join(os.tmpdir(), "stale-reference-workflow-"));
    t.after(() => rm(root, { recursive: true, force: true }));
    const files = {
        "src/Sample.cs": [
            "namespace Example.Tests;",
            ...Array(padding).fill(""),
            "[TestClass]",
            "public class SampleTests",
            "{",
            "    [TestMethod]",
            '    [Ignore("https://github.com/dotnet/sdk/issues/123")]',
            "    public void Works() {}",
            "}",
        ].join("\n"),
        ".github/stale-reference-check/collect.mjs": "// collector rules",
        ".github/stale-reference-check/interpretations.mjs": "// validation rules",
        ".github/stale-reference-check/workflow.mjs": "// orchestration rules",
        ".github/stale-reference-check/source-tools.mjs": "// bounded source reader rules",
        ".github/stale-reference-check/diagnostics.mjs": "// runtime diagnostics",
        ".github/workflows/stale-reference-interpret.md": "# Interpretation rules",
    };
    for (const [file, content] of Object.entries(files))
    {
        await mkdir(path.dirname(path.join(root, file)), { recursive: true });
        await writeFile(path.join(root, file), `${content}\n`);
    }
    execFileSync("git", ["init", "--quiet", root]);
    execFileSync("git", ["-C", root, "add", "."]);
    execFileSync("git", ["-C", root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
        "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "fixture"]);
    return root;
}

const logger = { info() {}, warn() {} };

async function stageSubmission(root, payload)
{
    const directory = path.join(root, ".stale-reference-check/input");
    const tools = await getSourceTools(directory);
    const accepted = await tools.prepareInterpretations({ payload: JSON.stringify(payload) });
    await writeFile(path.join(directory, "submission.json"), JSON.stringify(tools.submission()));
    await writeFile(path.join(directory, "context-evidence.json"), JSON.stringify(tools.evidence()));
    return { type: "record_interpretations", ...accepted, payload: tools.submission().payload };
}

test("submission validation rejects malformed JSON synchronously and accepts a corrected immutable payload", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const directory = path.join(root, ".stale-reference-check/input");
    const server = await startSourceServer(directory);
    t.after(() => new Promise(resolve => server.close(resolve)));
    const payload = JSON.stringify({
        schemaVersion: 1,
        results: batch.candidates.map(candidate => ({
            candidateId: candidate.id, status: "irrelevant", reason: "Fixture.", actions: [],
        })),
    });
    await assert.rejects(requestSourceTools(directory, "prepare-interpretations",
        { payload: payload.slice(0, -2) }), /Invalid JSON.*2 attempts remaining/);
    await assert.rejects(requestSourceTools(directory, "submission"), /No validated submission/);
    const accepted = await requestSourceTools(directory, "prepare-interpretations", { payload });
    assert.match(accepted.receipt, /^[a-f0-9]{64}$/);
    assert.deepEqual(await requestSourceTools(directory, "prepare-interpretations", { payload }), accepted);
    await assert.rejects(requestSourceTools(directory, "prepare-interpretations",
        { payload: payload.replace("Fixture.", "Changed.") }), /already accepted/);
    const submission = await requestSourceTools(directory, "submission");
    assert.deepEqual(submission, { schemaVersion: 1, receipt: accepted.receipt, payload: JSON.parse(payload) });
    await writeFile(path.join(directory, "submission.json"), JSON.stringify(submission));
    const output = path.join(root, "output.json");
    await writeFile(output, JSON.stringify({ items: [{ type: "record_interpretations", ...accepted }] }));
    await assert.rejects(record(root, output), /payload inspected by threat detection/);
    await completeSubmission(directory, output);
    assert.deepEqual(JSON.parse(await readFile(output)).items[0].payload, JSON.parse(payload),
        "Threat detection must inspect the actual payload, not merely its receipt.");
    await record(root, output);
    assert.deepEqual(JSON.parse(await readFile(path.join(root,
        ".stale-reference-check/results/interpretations.json"))), JSON.parse(payload));
});

test("native object submission serializes in the actual MCP script and retains host validation", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const directory = path.join(root, ".stale-reference-check/input");
    for (const file of ["source-tools.mjs", "interpretations.mjs", "collect.mjs"])
    {
        await writeFile(path.join(directory, file),
            await readFile(path.join(repository, ".github/stale-reference-check", file)));
    }
    const server = await startSourceServer(directory);
    t.after(() => new Promise(resolve => server.close(resolve)));
    const previous = process.env.STALE_REFERENCE_PRIVATE;
    process.env.STALE_REFERENCE_PRIVATE = directory;
    t.after(() =>
    {
        if (previous === undefined) delete process.env.STALE_REFERENCE_PRIVATE;
        else process.env.STALE_REFERENCE_PRIVATE = previous;
    });
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-interpret.md"), "utf8");
    const tool = workflow.slice(workflow.indexOf("  prepare_interpretations:"), workflow.indexOf("\nsafe-outputs:"));
    const script = tool.slice(tool.indexOf("    script: |") + "    script: |".length);
    const invoke = new (Object.getPrototypeOf(async function () {}).constructor)("payload", script);
    const payload = { schemaVersion: 1, results: batch.candidates.map(candidate => ({
        candidateId: candidate.id, status: "irrelevant", reason: "Quotes \" and slash \\ and newline\n\u00e9.", actions: [],
    })) };
    await assert.rejects(invoke(JSON.stringify(payload)), /Submission rejected:.*2 attempts remaining/);
    await assert.rejects(invoke({ schemaVersion: 1, results: [] }), /expected candidate IDs.*1 attempts remaining/);
    const accepted = await invoke(payload);
    assert.equal(accepted.receipt, submissionReceipt(payload));
    assert.deepEqual((await requestSourceTools(directory, "submission")).payload, payload);
    payload.results[0].reason = "Changed after acceptance.";
    await assert.rejects(invoke(payload), /already accepted/);
});

test("invalid candidates and blockers can be corrected but submission attempts are bounded", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const tools = await getSourceTools(path.join(root, ".stale-reference-check/input"));
    const candidate = batch.candidates[0];
    const payload = { schemaVersion: 1, results: [{
        candidateId: candidate.id, status: "actionable", reason: "An issue blocks the test.",
        actions: [{
            kind: "ignore", anchor: "Example.Tests.SampleTests.Works",
            testNames: ["Example.Tests.SampleTests.Works"],
            startLine: candidate.seedLine, endLine: candidate.seedLine,
            urls: ["https://github.com/dotnet/sdk/blob/main/src/Sample.cs"],
        }],
    }] };
    await assert.rejects(tools.prepareInterpretations({ payload: JSON.stringify(payload) }),
        /Candidate [a-f0-9]{64}: Action URL must identify.*2 attempts remaining/);
    await assert.rejects(tools.prepareInterpretations({ payload: JSON.stringify({ schemaVersion: 1, results: [] }) }),
        /exactly the expected candidate IDs.*1 attempts remaining/);
    payload.results[0].actions[0].urls = ["https://github.com/dotnet/sdk/issues/123"];
    assert.match((await tools.prepareInterpretations({ payload: JSON.stringify(payload) })).receipt, /^[a-f0-9]{64}$/);

    const exhaustedRoot = await fixture(t);
    await prepare({ repoRoot: exhaustedRoot, logger });
    const exhausted = await getSourceTools(path.join(exhaustedRoot, ".stale-reference-check/input"));
    for (let attempt = 0; attempt < 3; attempt++)
    {
        await assert.rejects(exhausted.prepareInterpretations({ payload: "{}" }),
            new RegExp(`${2 - attempt} attempts remaining`));
    }
    await assert.rejects(exhausted.prepareInterpretations({ payload: JSON.stringify(payload) }), /budget is exhausted/);
    assert.throws(() => exhausted.submission(), /No validated submission/);
    await assert.rejects(readFile(path.join(exhaustedRoot, ".stale-reference-check/input/submission.json")),
        { code: "ENOENT" });
});

test("receipts cannot authorize missing, altered, or unvalidated source results", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const directory = path.join(root, ".stale-reference-check/input");
    const tools = await getSourceTools(directory);
    const server = await startSourceServer(directory);
    t.after(() => new Promise(resolve => server.close(resolve)));
    const payload = { schemaVersion: 1, results: batch.candidates.map(candidate => ({
        candidateId: candidate.id, status: "irrelevant", reason: "Fixture.", actions: [],
    })) };
    const output = path.join(root, "output.json");
    await writeFile(output, JSON.stringify({ items: [{ type: "record_interpretations", receipt: "0".repeat(64) }] }));
    await assert.rejects(completeSubmission(directory, output), /No validated submission/);
    const accepted = await tools.prepareInterpretations({ payload: JSON.stringify(payload) });
    await assert.rejects(completeSubmission(directory, output), /receipt does not match/);
    await writeFile(output, JSON.stringify({ items: [{ type: "record_interpretations", ...accepted }] }));
    await completeSubmission(directory, output);
    const validOutput = await readFile(output, "utf8");
    const altered = JSON.parse(validOutput);
    altered.items[0].payload.results[0].reason = "Altered after acceptance.";
    await writeFile(output, JSON.stringify(altered));
    await assert.rejects(record(root, output), /payload inspected by threat detection/);
    await writeFile(output, validOutput);
    const submissionFile = path.join(directory, "submission.json");
    const validSubmission = await readFile(submissionFile, "utf8");
    const badSubmission = JSON.parse(validSubmission);
    badSubmission.payload.results[0].candidateId = "0".repeat(64);
    await writeFile(submissionFile, JSON.stringify(badSubmission));
    await assert.rejects(record(root, output), /receipt does not match/);
    // Even a matching receipt cannot replace committed-source validation.
    badSubmission.receipt = submissionReceipt(badSubmission.payload);
    await writeFile(submissionFile, JSON.stringify(badSubmission));
    await writeFile(output, JSON.stringify({ items: [{
        type: "record_interpretations", receipt: badSubmission.receipt, payload: badSubmission.payload,
    }] }));
    await assert.rejects(record(root, output), /Unknown, unexpected/);
    await writeFile(submissionFile, validSubmission);
    await writeFile(output, validOutput);
    await writeFile(path.join(root, "src/Sample.cs"), "// Changed since collection\n");
    await assert.rejects(record(root, output), /Source blob mismatch/);
    await assert.rejects(readFile(path.join(root, ".stale-reference-check/results/interpretations.json")),
        { code: "ENOENT" });
});

test("submission transport preserves large UTF-8 payloads and enforces size and concurrency limits", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const directory = path.join(root, ".stale-reference-check/input");
    const server = await startSourceServer(directory);
    t.after(() => new Promise(resolve => server.close(resolve)));
    await assert.rejects(requestSourceTools(directory, "prepare-interpretations",
        { payload: " ".repeat(512 * 1024 + 1) }), /at most 512 KiB/);
    const payload = JSON.stringify({ schemaVersion: 1, results: batch.candidates.map(candidate => ({
        candidateId: candidate.id, status: "irrelevant", reason: "\u00e9".repeat(4000), actions: [],
    })) });
    assert.ok(Buffer.byteLength(payload) > 4096);
    const tools = await getSourceTools(directory);
    const pending = tools.prepareInterpretations({ payload });
    await assert.rejects(tools.prepareInterpretations({ payload }), /being validated/);
    await pending;
    const accepted = await requestSourceTools(directory, "prepare-interpretations", { payload });
    assert.equal(tools.submission().payload.results[0].reason, "\u00e9".repeat(4000));
    assert.equal(accepted.receipt, submissionReceipt(JSON.parse(payload)));
});

test("packaged submission tools work in a separate process without a source checkout", async t =>
{
    const root = await fixture(t);
    for (const file of ["source-tools.mjs", "interpretations.mjs", "collect.mjs"])
    {
        await writeFile(path.join(root, ".github/stale-reference-check", file),
            await readFile(path.join(repository, ".github/stale-reference-check", file)));
    }
    const { batch } = await prepare({ repoRoot: root, logger });
    const directory = await mkdtemp(path.join(os.tmpdir(), "stale-reference-private-"));
    t.after(() => rm(directory, { recursive: true, force: true }));
    await cp(path.join(root, ".stale-reference-check/input"), directory, { recursive: true });
    const script = path.join(directory, "source-tools.mjs");
    const payload = JSON.stringify({ schemaVersion: 1, results: batch.candidates.map(candidate => ({
        candidateId: candidate.id, status: "irrelevant", reason: "Fixture.", actions: [],
    })) });
    const client = "const {getSourceTools}=await import(process.argv[1]);" +
        "console.log(JSON.stringify(await (await getSourceTools(process.argv[2])).prepareInterpretations({payload:process.argv[3]})));";
    const { stdout } = await promisify(execFile)(process.execPath,
        ["--input-type=module", "-e", client, pathToFileURL(script).href, directory, payload], { cwd: directory });
    assert.equal(JSON.parse(stdout).receipt, submissionReceipt(JSON.parse(payload)));
});

test("prepare creates a current-commit manifest and bounded batch without a cache", async (t) =>
{
    const root = await fixture(t);
    const result = await prepare({ repoRoot: root, logger });
    assert.equal(result.batch.candidates.length, 1);
    const manifest = JSON.parse(await readFile(path.join(root, ".stale-reference-check/input/manifest.json")));
    assert.match(manifest.headSha, /^[a-f0-9]{40}$/);
    assert.equal(manifest.rulesHash, await rulesHash(root));
    assert.equal(manifest.candidates[0].path, "src/Sample.cs");
});

test("malformed cache is diagnosed and recollected, never treated as interpreted", async (t) =>
{
    const root = await fixture(t);
    const cache = path.join(root, ".stale-reference-check/state/cache.json");
    await mkdir(path.dirname(cache), { recursive: true });
    await writeFile(cache, "{broken");
    const warnings = [];
    const result = await prepare({ repoRoot: root, logger: { info() {}, warn(message) { warnings.push(message); } } });
    assert.equal(result.batch.candidates.length, 1);
    assert.match(warnings.join("\n"), /invalid interpretation cache JSON/);
});

test("record validates all candidates and rejects duplicate safe-output calls", async (t) =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const item = await stageSubmission(root, {
        schemaVersion: 1,
        results: batch.candidates.map(candidate => ({
            candidateId: candidate.id,
            status: "irrelevant",
            reason: "Fixture classification.",
            actions: [],
        })),
    });
    const output = path.join(root, "output.json");
    process.env.GITHUB_STEP_SUMMARY = path.join(root, "summary.md");
    t.after(() => delete process.env.GITHUB_STEP_SUMMARY);
    await writeFile(output, JSON.stringify({ items: [item] }));
    await record(root, output);
    const recorded = JSON.parse(await readFile(path.join(root, ".stale-reference-check/results/interpretations.json")));
    assert.equal(recorded.results.length, 1);
    assert.deepEqual(JSON.parse(await readFile(path.join(root, ".stale-reference-check/results/summary.json"))),
        { schemaVersion: 1, actionable: 0, irrelevant: 1, deferred: 0 });
    assert.match(await readFile(process.env.GITHUB_STEP_SUMMARY, "utf8"), /0 actionable; 1 irrelevant; 0 deferred/);
    await writeFile(output, JSON.stringify({ items: [item, item] }));
    await assert.rejects(record(root, output), /exactly one/);
});

test("record refuses artifacts prepared with different rules", async (t) =>
{
    const root = await fixture(t);
    await prepare({ repoRoot: root, logger });
    await writeFile(path.join(root, ".github/workflows/stale-reference-interpret.md"), "changed rules");
    await assert.rejects(record(root, path.join(root, "unused.json")), /rules do not match/);
});

async function recordIrrelevantBatch(root, batch)
{
    const payload = {
        schemaVersion: 1,
        results: batch.candidates.map(candidate => ({
            candidateId: candidate.id,
            status: "irrelevant",
            reason: "No remaining source action in this fixture.",
            actions: [],
        })),
    };
    const output = path.join(root, "output.json");
    await writeFile(output, JSON.stringify({
        items: [await stageSubmission(root, payload)],
    }));
    await record(root, output);
}

test("first-run recording persists interpretations and cached-only finalization needs no agent artifact", async (t) =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    await recordIrrelevantBatch(root, batch);
    const github = new Proxy({}, { get() { throw new Error("An irrelevant batch must not call GitHub."); } });
    const options = { github, repository: "dotnet/sdk", repoRoot: root, dryRun: false, logger };
    const first = await finish(options);
    assert.equal(first.created.length, 0);
    assert.equal(first.interpretations.irrelevant, 1);
    assert.deepEqual(first.interpretations.batch, { actionable: 0, irrelevant: 1, deferred: 0 });
    assert.equal(first.runtime.metrics.requestCount, null);
    const workflowRuntime = collectWorkflowDiagnostics(root, path.join(root, "missing-detection"));
    await writeFile(path.join(root, ".stale-reference-check/results/workflow-runtime.json"), JSON.stringify(workflowRuntime));
    assert.ok(first.diagnostics.some(diagnostic => diagnostic.type === "agent-runtime" && diagnostic.code === "missing"));
    const next = await prepare({ repoRoot: root, logger });
    assert.equal(next.batch.candidates.length, 0);
    await rm(path.join(root, ".stale-reference-check/results/interpretations.json"));
    const second = await finish(options);
    assert.equal(second.created.length, 0);
    assert.equal(second.interpretations.remaining, 0);
    assert.deepEqual(second.interpretations.batch, { actionable: 0, irrelevant: 0, deferred: 0 });
    assert.equal(second.runtime, null);
    assert.equal(second.workflowRuntime, null);
});

test("preview does not save production interpretations", async (t) =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    await recordIrrelevantBatch(root, batch);
    const github = new Proxy({}, { get() { throw new Error("No GitHub calls expected."); } });
    await finish({ github, repository: "dotnet/sdk", repoRoot: root, dryRun: true, logger });
    await assert.rejects(readFile(path.join(root, ".stale-reference-check/state/cache.json")), { code: "ENOENT" });
    assert.equal((await prepare({ repoRoot: root, logger })).batch.candidates.length, 1);
});

test("runtime warnings survive finalization without using the agent's narrative counts", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    await writeFile(path.join(root, "agent-stdio.log"),
        "Permission denied and could not request permission from user\n15 actionable; 8 irrelevant\n");
    await recordIrrelevantBatch(root, batch);
    const workflowRuntime = collectWorkflowDiagnostics(root, path.join(root, "missing-detection"));
    await writeFile(path.join(root, ".stale-reference-check/results/workflow-runtime.json"), JSON.stringify(workflowRuntime));
    process.env.GITHUB_STEP_SUMMARY = path.join(root, "summary.md");
    t.after(() => delete process.env.GITHUB_STEP_SUMMARY);
    const github = new Proxy({}, { get() { throw new Error("No GitHub calls expected."); } });
    const report = await finish({ github, repository: "dotnet/sdk", repoRoot: root, dryRun: true, logger });
    assert.equal(report.runtime.metrics.deniedCommands, 1);
    assert.ok(report.diagnostics.some(diagnostic =>
        diagnostic.type === "agent-runtime" && diagnostic.code === "permission-denied"));
    assert.deepEqual(report.interpretations.batch, { actionable: 0, irrelevant: 1, deferred: 0 });
    const saved = JSON.parse(await readFile(path.join(root, ".stale-reference-check/report.json")));
    assert.deepEqual(saved.runtime, report.runtime);
    assert.deepEqual(saved.workflowRuntime, workflowRuntime);
    assert.ok(report.diagnostics.some(diagnostic =>
        diagnostic.type === "workflow-runtime" && diagnostic.stage === "detection" && diagnostic.code === "missing"));
    assert.match(await readFile(process.env.GITHUB_STEP_SUMMARY, "utf8"),
        /AI credits \(requests\) \| Unavailable \| Unavailable \| Unavailable/);
});

test("expanded source evidence survives recording, separate jobs, and cached-only state checks", async (t) =>
{
    const root = await fixture(t, 50);
    const { batch } = await prepare({ repoRoot: root, logger });
    const output = path.join(root, "output.json");
    const payload = {
        schemaVersion: 1,
        results: [{
            candidateId: batch.candidates[0].id,
            status: "actionable",
            reason: "The test is ignored pending this issue.",
            actions: [{
                kind: "ignore",
                anchor: "Example.Tests.SampleTests.Works",
                testNames: ["Example.Tests.SampleTests.Works"],
                startLine: batch.candidates[0].seedLine,
                endLine: batch.candidates[0].seedLine + 1,
                urls: ["https://github.com/dotnet/sdk/issues/123"],
                additionalConditions: [],
            }],
        }],
    };
    await assert.rejects(stageSubmission(root, payload), /namespace|qualified|context/i);
    const directory = path.join(root, ".stale-reference-check/input");
    const server = await startSourceServer(directory);
    try
    {
        await requestSourceTools(directory, "read-context",
            { candidateId: batch.candidates[0].id, startLine: 1, endLine: 1 });
        await writeFile(path.join(directory, "context-evidence.json"),
            JSON.stringify(await requestSourceTools(directory, "evidence")));
    }
    finally
    {
        await new Promise(resolve => server.close(resolve));
    }
    await writeFile(output, JSON.stringify({ items: [await stageSubmission(root, payload)] }));
    await record(root, output);
    await rm(path.join(directory, "context-evidence.json"));
    let resolved = false;
    let lookups = 0;
    const issues = {
        async get()
        {
            lookups++;
            return { data: {
                number: 123, state: resolved ? "closed" : "open",
                state_reason: resolved ? "completed" : null,
                closed_at: resolved ? "2026-09-01T00:00:00Z" : null,
                html_url: "https://github.com/dotnet/sdk/issues/123",
            } };
        },
        async listForRepo() { return { data: [] }; },
        async create() { throw new Error("Neither a blocked run nor a preview may create issues."); },
    };
    const paginate = async (method, parameters) => (await method(parameters)).data;
    paginate.iterator = async function* (method, parameters) { yield await method(parameters); };
    const github = { rest: { issues }, paginate };
    const options = { github, repository: "dotnet/sdk", repoRoot: root, logger };
    const blocked = await finish({ ...options, dryRun: false });
    assert.equal(blocked.proposed.length, 0);
    resolved = true;
    const next = await prepare({ repoRoot: root, logger });
    assert.equal(next.batch.candidates.length, 0);
    await rm(path.join(root, ".stale-reference-check/results"), { recursive: true });
    const preview = await finish({ ...options, dryRun: true });
    assert.equal(preview.proposed.length, 1);
    assert.equal(preview.created.length, 0);
    assert.equal(lookups, 2);
});

test("a missing required interpretation artifact cannot authorize finalization", async (t) =>
{
    const root = await fixture(t);
    await prepare({ repoRoot: root, logger });
    await assert.rejects(finish({
        github: null, repository: "dotnet/sdk", repoRoot: root, dryRun: false, logger,
    }), { code: "ENOENT" });
    await assert.rejects(readFile(path.join(root, ".stale-reference-check/state/cache.json")), { code: "ENOENT" });
});

test("context expansion is limited to the batch and two windows per candidate", async (t) =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const request = { candidateId: batch.candidates[0].id, startLine: 1, endLine: 8 };
    const output = t.mock.method(process.stdout, "write", () => true);
    try
    {
        await assert.rejects(expandContext(root, { ...request, candidateId: "not-in-the-batch" }), /only.*this batch/);
        await expandContext(root, request);
        await expandContext(root, request);
        await assert.rejects(expandContext(root, request), /budget.*exhausted/);
    }
    finally
    {
        output.mock.restore();
    }
});

test("local batch reads use the same bounded text pages as the MCP reader", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const output = t.mock.method(process.stdout, "write", () => true);
    await printBatch(root);
    const text = output.mock.calls[0].arguments[0];
    assert.match(text, /^Batch page 1 of 1/);
    assert.ok(text.includes(batch.candidates[0].context));
    await assert.rejects(printBatch(root, 2), /Batch page/);
});

test("host-side source tools expose only the batch and enforce shared expansion limits", async (t) =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const directory = path.join(root, ".stale-reference-check/input");
    const tools = await getSourceTools(directory);
    assert.ok(tools.readBatch().includes(batch.candidates[0].id));
    const request = { candidateId: batch.candidates[0].id, startLine: 1, endLine: 8 };
    assert.throws(() => tools.readContext({ ...request, candidateId: "../manifest.json" }), /only available/);
    assert.throws(() => tools.readContext({ ...request, endLine: 81 }), /1 and 80/);
    assert.match(tools.readContext(request).context, /Example\.Tests/);
    const sameTools = await getSourceTools(directory);
    sameTools.readContext(request);
    assert.throws(() => tools.readContext(request), /budget is exhausted/);
});

test("driver serializes main-only runs and supports cached-only finalization", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-check.yml"), "utf8");
    assert.match(workflow, /group: stale-reference-check/);
    assert.match(workflow, /cancel-in-progress: false/);
    assert.match(workflow, /github\.repository == 'dotnet\/sdk' && github\.ref == 'refs\/heads\/main'/);
    assert.match(workflow, /needs\.collect\.outputs\.has_misses != 'true' \|\| needs\.interpret\.result == 'success'/);
    assert.match(workflow, /if: needs\.interpret\.result == 'success'\r?\n        with:\r?\n          name: stale-reference-interpretations/);
    assert.match(workflow, /if: needs\.interpret\.result == 'success'\r?\n        with:\r?\n          name: stale-reference-workflow-runtime/);
    assert.match(workflow, /inputs\.dry_run/);
    assert.match(workflow, /env\.DRY_RUN != 'true'/);
    assert.doesNotMatch(workflow, /pull_request:/);
    assert.equal((workflow.match(/issues: write/g) ?? []).length, 1);
    const cacheKeys = [...workflow.matchAll(/key: stale-reference-v1-[^\n]+/g)].map(([match]) => match);
    assert.ok(cacheKeys.length >= 2, "expected both a cache restore key and a cache save key");
    for (const key of cacheKeys) {
        assert.match(key, /\$\{\{ github\.repository_id \}\}-\$\{\{ github\.ref_name \}\}-/,
            `cache key must use github.ref_name, not a hardcoded branch name: ${key}`);
    }
});

test("agent records source interpretations without issue writes or GitHub tools", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-interpret.md"), "utf8");
    const header = workflow.split("---")[1];
    assert.match(header, /workflow_call:/);
    assert.match(header, /record-interpretations:/);
    assert.match(header, /needs\.detection\.outputs\.detection_success == 'true'/);
    assert.doesNotMatch(header, /issues: write|pull-requests: write|create-issue: true/);
    assert.doesNotMatch(header, /^\s+github:\s*$/m);
    assert.match(header, /github: false/);
    assert.match(header, /checkout: false/);
    assert.match(header, /edit: false/);
    assert.doesNotMatch(header, /bash: \[node\]/);
    assert.match(header, /artifact-ids: \$\{\{ inputs\.input_artifact_id \}\}/);
    assert.match(header, /report-failed-jobs: false/);
    assert.match(header, /conclusion:\s*\n\s*if: \$\{\{ false \}\}/);
    assert.match(header, /ref: \$\{\{ github\.sha \}\}/);
});

test("agent transport uses native bounded text tools rather than shell serialization", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-interpret.md"), "utf8");
    const header = workflow.split("---")[1];
    assert.match(header, /cli-proxy: false/);
    assert.match(header, /bash: false/);
    assert.match(header, /read_batch:[\s\S]*?page:[\s\S]*?type: number/);
    assert.match(header, /read_context:[\s\S]*?candidateId:/);
    assert.match(header, /prepare_interpretations:[\s\S]*?payload:[\s\S]*?type: object/);
    assert.match(header, /return requestSourceTools\(directory, "prepare-interpretations", \{ payload: JSON\.stringify\(payload\) \}\)/);
    assert.match(header, /return formatContext\(await requestSourceTools/);
    assert.match(workflow, /Do not pass `schemaVersion` or `results` as top-level tool arguments/);
    assert.match(workflow, /After queueing the receipt, stop/);
    assert.match(workflow, /https:\/\/github\.com\/OWNER\/REPO\/issues\/NUMBER/);
    assert.match(workflow, /https:\/\/github\.com\/OWNER\/REPO\/pull\/NUMBER/);
    assert.match(workflow, /Source-code links \(`\/blob\/`, `\/tree\/`\)/);
    assert.match(workflow, /A single unsupported URL rejects the entire batch/);
    const generated = await readFile(path.join(workflowDirectory, "stale-reference-interpret.lock.yml"), "utf8");
    assert.match(generated, /"name": "prepare_interpretations",[\s\S]*?"inputSchema": \{[\s\S]*?"payload": \{[^}]*"type": "object"/);
    const execution = generated.match(/      - name: Execute GitHub Copilot CLI\r?\n([\s\S]*?)(?=^      - )/m)?.[1] ?? "";
    assert.ok(execution.includes("--allow-tool mcpscripts --allow-tool safeoutputs"), "Native MCP tools must be allowed.");
    assert.doesNotMatch(execution, /--allow-tool.*shell\(|--allow-all-tools/);
    assert.doesNotMatch(generated, /GH_AW_MCP_CLI_SERVERS|mcp_cli_tools_with_safeoutputs_prompt/);
    assert.doesNotMatch(header, /^strict: false$/m);
    assert.doesNotMatch(header, /container_pins/);
    const gateway = generated.match(/ghcr\.io\/github\/gh-aw-mcpg:v0\.4\.25@sha256:[a-f0-9]{64}/)?.[0];
    assert.ok(gateway, "The compiler default gateway image must be immutable.");
    const startGateway = generated.match(/      - name: Start MCP Gateway\r?\n([\s\S]*?)(?=^      - )/m)?.[1] ?? "";
    assert.match(startGateway, /ghcr\.io\/github\/gh-aw-mcpg:v0\.4\.25'/,
        "The executed gateway must use the compiler default image.");
});

test("validated payloads reach threat detection before the trusted recorder authorizes recording", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-interpret.md"), "utf8");
    assert.match(workflow, /at most three attempts total/);
    assert.match(workflow, /complete "\$STALE_REFERENCE_PRIVATE" \/tmp\/gh-aw\/agent_output\.json/);
    assert.match(workflow, /stale-reference-private\/submission\.json/);
    const generated = await readFile(path.join(workflowDirectory, "stale-reference-interpret.lock.yml"), "utf8");
    const collect = generated.indexOf("id: collect_output");
    const expand = generated.indexOf('complete "$STALE_REFERENCE_PRIVATE" /tmp/gh-aw/agent_output.json');
    const artifactUpload = generated.indexOf("name: Upload agent artifacts");
    assert.ok(collect > 0 && expand > collect && artifactUpload > expand,
        "Trusted submission expansion must follow output collection and precede the agent artifact upload.");
    const recordJob = generated.match(/^  record_interpretations:\r?\n([\s\S]*?)(?=^  \w+:\r?\n)/m)?.[1] ?? "";
    assert.match(recordJob, /needs\.detection\.result == 'success' && needs\.detection\.outputs\.detection_success == 'true'/);
    assert.match(recordJob, /\$\{\{ needs\.agent\.outputs\.artifact_prefix \}\}agent/);
    assert.match(recordJob, /GH_AW_AGENT_OUTPUT: \$\{\{ runner\.temp \}\}\/gh-aw\/safe-jobs\/agent_output\.json/);
});

test("helper CI verifies native gateway compatibility when the generated workflow changes", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-check-tests.yml"), "utf8");
    assert.doesNotMatch(workflow, /aw\.json/);
    assert.match(workflow, /run: node \.github\/stale-reference-check\/test\/gateway-smoke\.mjs/);
    assert.doesNotMatch(workflow, /COPILOT_PAT|issues: write/);
});

test("all entry points retain telemetry and immutable action pins", async () =>
{
    for (const file of ["stale-reference-check.yml", "stale-reference-interpret.md", "stale-reference-check-tests.yml"])
    {
        const workflow = await readFile(path.join(workflowDirectory, file), "utf8");
        assert.match(workflow, /DOTNET_CLI_TELEMETRY_SESSIONID: gha-\$\{\{ github\.repository_id \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/);
        for (const match of workflow.matchAll(/uses: ([^\s]+)/g))
        {
            if (match[1].startsWith("actions/"))
            {
                assert.match(match[1], /@[a-f0-9]{40}$/);
            }
        }
    }
});

test("interpreter bounds execution and retains detection before trusted recording", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-interpret.md"), "utf8");
    // The interpreter agent intentionally does not pin a model: a pinned preview model
    // (gpt-5.6-luna) previously caused every interpretation request to fail with an
    // immediate HTTP 400. Leave model selection to the Copilot CLI default. (The
    // separate threat-detection engine below still pins its own "detection" model.)
    const engineBlock = workflow.match(/^engine:\r?\n([\s\S]*?)(?=^\S)/m)?.[1] ?? "";
    assert.doesNotMatch(engineBlock, /^\s*model:/m);
    assert.match(workflow, /^max-turns: 64$/m);
    assert.match(workflow, /^max-ai-credits: 150$/m);
    assert.match(workflow, /^timeout-minutes: 20$/m);
    const generated = await readFile(path.join(workflowDirectory, "stale-reference-interpret.lock.yml"), "utf8");
    const agent = generated.match(/^  agent:\r?\n([\s\S]*?)(?=^  \w+:\r?\n)/m)?.[1] ?? "";
    assert.match(agent, /COPILOT_MODEL: \$\{\{ vars\.GH_AW_MODEL_AGENT_COPILOT \|\| vars\.GH_AW_DEFAULT_MODEL_COPILOT \|\| 'auto' \}\}/);
    assert.match(agent, /GH_AW_MAX_TURNS: 64/);
    assert.match(agent, /"maxRuns":64/);
    assert.match(agent, /"maxAiCredits":150/);
    assert.match(agent, /^    timeout-minutes: 60$/m);
    const execution = agent.match(/      - name: Execute GitHub Copilot CLI\r?\n([\s\S]*?)(?=^      - )/m)?.[1] ?? "";
    assert.match(execution, /timeout-minutes: 20/);
    assert.match(generated, /^  detection:\r?\n/m);
    const detection = generated.match(/^  detection:\r?\n([\s\S]*?)(?=^  \w+:\r?\n)/m)?.[1] ?? "";
    assert.match(detection, /COPILOT_MODEL: detection/);
    assert.match(detection, /COPILOT_GITHUB_TOKEN: \$\{\{ case\(needs\.pat_pool\.outputs\.pat_number/);
    const recordJob = generated.match(/^  record_interpretations:\r?\n([\s\S]*?)(?=^  \w+:\r?\n)/m)?.[1] ?? "";
    assert.match(recordJob, /needs\.detection\.result == 'success' && needs\.detection\.outputs\.detection_success == 'true'/);
});

test("agent failures retain a separate always-run runtime diagnostic artifact", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-interpret.md"), "utf8");
    assert.match(workflow, /name: Summarize interpreter runtime\r?\n\s+if: always\(\)/);
    assert.match(workflow, /name: Upload interpreter runtime diagnostics\r?\n\s+if: always\(\)/);
    const generated = await readFile(path.join(workflowDirectory, "stale-reference-interpret.lock.yml"), "utf8");
    const agent = generated.match(/^  agent:\r?\n([\s\S]*?)(?=^  \w+:\r?\n)/m)?.[1] ?? "";
    const summarize = agent.indexOf("name: Summarize interpreter runtime");
    assert.ok(summarize > agent.indexOf("name: Parse agent logs for step summary"));
    assert.match(agent.slice(summarize - 200, summarize), /if: always\(\)/);
    assert.match(agent.slice(summarize, summarize + 300), /diagnostics\.mjs" \/tmp\/gh-aw/);
    assert.match(agent, /name: stale-reference-runtime-\$\{\{ inputs\.input_artifact_id \}\}/);
    assert.match(agent, /path: \$\{\{ runner\.temp \}\}\/stale-reference-private\/runtime\.json/);
});

test("workflow usage includes separate detection traces even when recording fails", async () =>
{
    const workflow = await readFile(path.join(workflowDirectory, "stale-reference-interpret.md"), "utf8");
    const job = workflow.match(/^  runtime_diagnostics:\r?\n[\s\S]*?(?=\r?\n\r?\nimports:)/m)?.[0] ?? "";
    assert.match(job, /needs: \[activation, agent, detection\]/);
    assert.match(job, /if: always\(\) && !cancelled\(\) && needs\.activation\.result == 'success'/);
    assert.doesNotMatch(job, /needs\.(agent|detection)\.result == 'success'|COPILOT_PAT|issues: write/);
    assert.match(job, /pattern: "\{\$\{\{ needs\.activation\.outputs\.artifact_prefix \}\}agent,\$\{\{ needs\.activation\.outputs\.artifact_prefix \}\}detection\}"/);
    assert.match(job, /merge-multiple: false/);
    assert.match(job, /diagnostics\.mjs" --workflow/);
    assert.match(job, /name: stale-reference-workflow-runtime-\$\{\{ inputs\.input_artifact_id \}\}/);
    const generated = await readFile(path.join(workflowDirectory, "stale-reference-interpret.lock.yml"), "utf8");
    const generatedJob = generated.match(/^  runtime_diagnostics:\r?\n([\s\S]*?)(?=^  \w+:\r?\n|$(?![\s\S]))/m)?.[1] ?? "";
    assert.match(generatedJob, /always\(\) && !cancelled\(\)/);
    assert.match(generatedJob, /merge-multiple: false/);
    assert.match(generatedJob, /--workflow/);
    const caller = await readFile(path.join(workflowDirectory, "stale-reference-check.yml"), "utf8");
    assert.match(caller, /name: stale-reference-workflow-runtime-\$\{\{ needs\.collect\.outputs\.input_artifact_id \}\}/);
});

test("caller grants the compiler-required permissions and interpreter jobs remain read-only", async () =>
{
    const generated = await readFile(path.join(workflowDirectory, "stale-reference-interpret.lock.yml"), "utf8");
    const caller = await readFile(path.join(workflowDirectory, "stale-reference-check.yml"), "utf8");
    assert.doesNotMatch(caller, /secrets: inherit/);
    assert.match(caller, /input_artifact_id: \$\{\{ steps\.upload\.outputs\.artifact-id \}\}/);
    assert.match(caller, /name: stale-reference-interpretations-\$\{\{ needs\.collect\.outputs\.input_artifact_id \}\}/);
    const ceiling = { actions: 2, contents: 1, "copilot-requests": 2, issues: 2 };
    const rank = { none: 0, read: 1, write: 2 };
    const jobs = [...generated.matchAll(/^  ([a-z_]+):\r?\n([\s\S]*?)(?=^  [a-z_]+:\r?\n|$(?![\s\S]))/gm)];
    assert.ok(jobs.some(job => job[1] === "agent"));
    for (const [, name, body] of jobs)
    {
        const permissions = body.match(/^    permissions:\r?\n((?:      [\w-]+: \w+\r?\n)+)/m)?.[1] ?? "";
        for (const [, scope, permission] of permissions.matchAll(/      ([\w-]+): (\w+)/g))
        {
            assert.ok(rank[permission] <= (ceiling[scope] ?? 0),
                `${name} elevates ${scope} to ${permission}`);
            if (name !== "conclusion")
            {
                assert.ok(scope !== "actions" || permission !== "write", `${name} unexpectedly writes Actions data`);
            }
        }
    }
    const conclusion = jobs.find(job => job[1] === "conclusion")?.[2];
    assert.match(conclusion, /&& \(false\)/);
    assert.match(conclusion, /issues: none/);
    const safeOutputs = jobs.find(job => job[1] === "safe_outputs")?.[2];
    assert.doesNotMatch(safeOutputs, /issues: write/);
    assert.match(caller, /issues: write/);
    const agent = jobs.find(job => job[1] === "agent")?.[2];
    assert.doesNotMatch(agent, /name: Checkout repository|--allow-tool write|shell\(node\)|--allow-tool github/);
    assert.match(agent, /path: \$\{\{ runner\.temp \}\}\/stale-reference-private/);
    assert.match(agent, /artifact-ids: \$\{\{ inputs\.input_artifact_id \}\}/);
});

test("isolated concurrent MCP processes share one budget and trusted expansion evidence", async t =>
{
    const root = await fixture(t);
    const { batch } = await prepare({ repoRoot: root, logger });
    const directory = path.join(root, ".stale-reference-check/input");
    const server = await startSourceServer(directory);
    t.after(() => new Promise(resolve => server.close(resolve)));
    assert.ok((await requestSourceTools(directory, "read-batch")).includes(batch.candidates[0].id));
    await assert.rejects(requestSourceTools(directory, "read-batch", { page: 0 }), /Batch page/);
    const input = { candidateId: batch.candidates[0].id, startLine: 1, endLine: 8 };
    const client = "const {requestSourceTools}=await import(process.argv[1]);" +
        "console.log(JSON.stringify(await requestSourceTools(process.argv[2], 'read-context', JSON.parse(process.argv[3]))));";
    const results = await Promise.allSettled(Array.from({ length: 3 }, () =>
        promisify(execFile)(process.execPath, ["--input-type=module", "-e", client,
            new URL("../source-tools.mjs", import.meta.url).href, directory, JSON.stringify(input)])));
    assert.equal(results.filter(result => result.status === "fulfilled").length, 2);
    const failure = results.find(result => result.status === "rejected");
    assert.match(failure.reason.message, /two-window/);
    assert.deepEqual(await requestSourceTools(directory, "evidence"),
        { schemaVersion: 1, windows: [input, input] });
    await assert.rejects(requestSourceTools(directory, "unknown"), /Unknown source-reader/);
    const { port } = JSON.parse(await readFile(path.join(directory, "reader.json"), "utf8"));
    const denied = await fetch(`http://127.0.0.1:${port}/read-batch`, { method: "POST" });
    assert.equal(denied.status, 401);
    await denied.text();
});
