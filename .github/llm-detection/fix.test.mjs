import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import {
    checkAudit, detectorPath, marker, prepareContents, selectArtifact, testsPath,
    validateArtifact, validateContents, validateInputs, validateOutput,
} from "./fix.mjs";

const detector = readFileSync(detectorPath, "utf8");
const tests = readFileSync(testsPath, "utf8");
const prepared = prepareContents(detector, tests, "opencode", "OPENCODE_FIXTURE");
const revision = "a".repeat(40);
const proposal = () => ({
    items: [{
        type: "create_pull_request",
        branch: "llm-detection-fix/opencode",
        title: "Recognize the current OpenCode identity signal",
        body: `${marker}\nAudit run: https://github.com/dotnet/sdk/actions/runs/12345\n` +
            `Assignment: https://github.com/upstream/harness/blob/${revision}/src/index.ts#L1-L5\n` +
            `Propagation: https://github.com/upstream/harness/blob/${revision}/src/shell.ts#L10-L20`,
    }],
});

test("adds one alias, preserves legacy matching and every existing test", () => {
    assert.equal(validateContents(detector, tests, prepared.detector, prepared.tests, "opencode"), "OPENCODE_FIXTURE");
    assert.ok(prepared.detector.includes('"OPENCODE_FIXTURE"'));
    assert.ok(prepared.detector.includes('"OPENCODE_AI"'));
    assert.equal(prepared.tests.split("new object[]").length, tests.split("new object[]").length + 4);
    assert.ok(prepared.tests.includes('{ "OPENCODE_FIXTURE", "" } }, null!'));
    assert.ok(prepared.tests.includes('{ "OPENCODE_FIXTURE", "false" } }, "opencode"'));
    assert.ok(prepared.tests.includes('{ "OPENCODE_AI", "1" }, { "OPENCODE_FIXTURE", "1" }'));
});

test("preserves CRLF files", () => {
    const before = [detector, tests].map(text => text.replaceAll("\r\n", "\n").replaceAll("\n", "\r\n"));
    const result = prepareContents(...before, "opencode", "OPENCODE_FIXTURE");
    assert.equal(validateContents(...before, result.detector, result.tests, "opencode"), "OPENCODE_FIXTURE");
});

test("publisher transport budget covers the complete generated file blobs", () => {
    const workflow = readFileSync(".github/workflows/llm-detection-fix.md", "utf8");
    const budget = workflow.match(/^\s+max-patch-size: (\d+)$/m);
    assert.ok(budget);
    const generated = prepareContents(detector, tests, "opencode", `OPENCODE_${"X".repeat(55)}`);
    assert.ok(Buffer.byteLength(generated.detector) + Buffer.byteLength(generated.tests) <= Number(budget[1]) * 1024);
});

for (const [harness, variable] of [
    ["unknown", "UNKNOWN"], ["generic_agent", "AGENT"], ["opencode", "AGENT"],
    ["opencode", "OPENCODE_AI"], ["opencode", "OPENCODE_AI_EXTRA\""], ["aider", "AIDER"],
    ["opencode", "opencode"], ["opencode", "OPENCODEOTHER"], ["opencode", "OPENCODE_\n"],
]) {
    test(`rejects ineligible alias ${JSON.stringify([harness, variable])}`, () => {
        assert.throws(() => prepareContents(detector, tests, harness, variable));
    });
}

for (const [name, changedDetector, changedTests] of [
    ["legacy removal", prepared.detector.replace('"OPENCODE_AI", ', ""), prepared.tests],
    ["unrelated rule", prepared.detector.replace('"CLAUDE_CODE"', '"CLAUDE_CHANGED"'), prepared.tests],
    ["rule semantics", prepared.detector.replace('new AnyPresentEnvironmentRule(getEnvironmentVariable, "OPENCODE_AI"', 'new AllPresentEnvironmentRule(getEnvironmentVariable, "OPENCODE_AI"'), prepared.tests],
    ["new label", prepared.detector.replace('"opencode",', '"opencode-new",'), prepared.tests],
    ["changed method", prepared.detector.replace("return results.Length", "throw new Exception(); return results.Length"), prepared.tests],
    ["test deletion", prepared.detector, prepared.tests.replace('        new object[] { new Dictionary<string, string> { { "OPENCODE_AI", "1" } }, "opencode" },', "")],
    ["test code injection", prepared.detector, prepared.tests.replace("MockBehavior.Strict", "MaliciousCode()")],
    ["incomplete coverage", prepared.detector, tests],
]) {
    test(`rejects ${name}`, () => {
        assert.throws(() => validateContents(detector, tests, changedDetector, changedTests, "opencode"));
    });
}

test("accepts a pinned, scoped proposal or an explicit no-op", () => {
    assert.equal(validateOutput(proposal(), "12345", "opencode", "dotnet/sdk"), true);
    assert.equal(validateOutput({ items: [{ type: "noop", message: "Already supported." }] }, "12345", "opencode", "dotnet/sdk"), false);
});

for (const [name, mutate] of [
    ["empty output", output => { output.items = []; }],
    ["duplicate outputs", output => { output.items.push(output.items[0]); }],
    ["missing data", output => { output.items[0].type = "missing_data"; }],
    ["alternate base", output => { output.items[0].base = "other"; }],
    ["alternate repository", output => { output.items[0].repo = "other/repo"; }],
    ["alternate patch", output => { output.items[0].patch = "/tmp/other.patch"; }],
    ["wrong branch", output => { output.items[0].branch = "main"; }],
    ["wrong marker", output => { output.items[0].body = output.items[0].body.replace(marker, ""); }],
    ["wrong audit", output => { output.items[0].body = output.items[0].body.replace("/12345", "/99999"); }],
    ["tracker closing keyword", output => { output.items[0].body += "\nFixes #42"; }],
    ["unpinned evidence", output => { output.items[0].body = output.items[0].body.replaceAll(revision, "main"); }],
    ["mixed revisions", output => { output.items[0].body = output.items[0].body.replace(revision, "b".repeat(40)); }],
    ["duplicate evidence", output => { output.items[0].body = output.items[0].body.replace("src/shell.ts#L10-L20", "src/index.ts#L1-L5"); }],
]) {
    test(`rejects proposal with ${name}`, () => {
        const output = proposal();
        mutate(output);
        assert.throws(() => validateOutput(output, "12345", "opencode", "dotnet/sdk"));
    });
}

for (const [runId, harness] of [["123; echo token", "opencode"], ["123", "$(command)"], ["", "opencode"], ["-1", "opencode"], ["9999999999999999", "opencode"]]) {
    test(`rejects unsafe dispatch ${JSON.stringify([runId, harness])}`, () => {
        assert.throws(() => validateInputs(runId, harness));
    });
}

function auditFixture() {
    const repo = { owner: "dotnet", repo: "sdk" };
    const run = {
        path: ".github/workflows/llm-detection-audit.lock.yml", repository: { full_name: "dotnet/sdk" },
        head_repository: { full_name: "dotnet/sdk" }, head_branch: "main", event: "schedule",
        status: "completed", conclusion: "success", html_url: "https://github.com/dotnet/sdk/actions/runs/12345",
    };
    const issue = {
        number: 42, title: "[CLI agent detection] Evidence report", user: { login: "github-actions[bot]" },
        labels: [{ name: "agentic-workflows" }], state: "open",
        body: "Workflow marker: gh-aw-workflow-id: llm-detection-audit\n" + run.html_url,
    };
    const issues = [issue], pulls = [];
    const github = {
        rest: {
            actions: { getWorkflowRun: async () => ({ data: run }) },
            issues: { listForRepo: Symbol("issues") }, pulls: { list: Symbol("pulls") },
        },
        paginate: async (method, args) => {
            if (method === github.rest.issues.listForRepo) {
                assert.deepEqual(args, {
                    ...repo, state: "all", creator: "github-actions[bot]", labels: "agentic-workflows", per_page: 100,
                });
                return issues;
            }
            return pulls;
        },
    };
    return { github, context: { repo }, run, issue, issues, pulls };
}

test("accepts a successful trusted audit and its open tracker", async () => {
    const { github, context } = auditFixture();
    assert.equal(await checkAudit(github, context, "12345", "opencode"), 42);
});

test("selects only the immutable artifact from the current attempt", () => {
    const current = { id: 2, name: "agent", expired: false, created_at: "2026-10-29T02:05:00Z" };
    const earlier = { ...current, id: 1, created_at: "2026-10-29T01:00:00Z" };
    assert.equal(selectArtifact([earlier, current], "2026-10-29T02:00:00Z"), 2);
    assert.throws(() => selectArtifact([earlier], "2026-10-29T02:00:00Z"));
    assert.throws(() => selectArtifact([{ ...current, expired: true }], "2026-10-29T02:00:00Z"));
    assert.throws(() => selectArtifact([current, current], "2026-10-29T02:00:00Z"));
});

for (const [name, mutate] of [
    ["different workflow", fixture => { fixture.run.path = ".github/workflows/other.yml"; }],
    ["fork run", fixture => { fixture.run.head_repository.full_name = "fork/sdk"; }],
    ["non-main run", fixture => { fixture.run.head_branch = "feature"; }],
    ["PR trigger", fixture => { fixture.run.event = "pull_request"; }],
    ["failed run", fixture => { fixture.run.conclusion = "failure"; }],
    ["closed tracker", fixture => { fixture.issue.state = "closed"; }],
    ["human tracker", fixture => { fixture.issue.user.login = "human"; }],
    ["duplicate trackers", fixture => { fixture.issues.push({ ...fixture.issue }); }],
    ["stale report", fixture => { fixture.issue.body = fixture.issue.body.replace("/12345", "/99999"); }],
    ["run ID prefix collision", fixture => { fixture.issue.body = fixture.issue.body.replace("/12345", "/123456"); }],
    ["open proposal", fixture => { fixture.pulls.push({ head: { repo: { full_name: "dotnet/sdk" }, ref: "llm-detection-fix/opencode" } }); }],
]) {
    test(`rejects audit with ${name}`, async () => {
        const fixture = auditFixture();
        mutate(fixture);
        await assert.rejects(checkAudit(fixture.github, fixture.context, "12345", "opencode"));
    });
}

function patchFixture(t, mutate = () => {}) {
    const root = mkdtempSync(join(tmpdir(), "llm-fix-test-"));
    t.after(() => rmSync(root, { recursive: true, force: true }));
    const repo = join(root, "repo"), artifacts = join(root, "artifacts");
    mkdirSync(repo); mkdirSync(artifacts);
    const git = (...args) => execFileSync("git", ["-c", "core.autocrlf=false", "-c", "core.hooksPath=", ...args], { cwd: repo, encoding: "utf8" });
    const write = (path, text) => {
        mkdirSync(dirname(resolve(repo, path)), { recursive: true });
        writeFileSync(resolve(repo, path), text);
    };
    git("init", "--quiet"); git("config", "core.autocrlf", "false");
    git("config", "core.filemode", "false");
    git("config", "user.name", "Fixer fixture"); git("config", "user.email", "fixture@example.invalid");
    write(detectorPath, detector.replaceAll("\r\n", "\n"));
    write(testsPath, tests.replaceAll("\r\n", "\n"));
    git("add", "."); git("commit", "--quiet", "-m", "Baseline");
    const baseline = git("rev-parse", "HEAD").trim();
    const result = prepareContents(readFileSync(resolve(repo, detectorPath), "utf8"), readFileSync(resolve(repo, testsPath), "utf8"), "opencode", "OPENCODE_FIXTURE");
    write(detectorPath, result.detector); write(testsPath, result.tests);
    mutate({ root: repo, write, git });
    git("add", "."); git("commit", "--quiet", "-m", "Add alias");
    writeFileSync(join(artifacts, "aw-llm-detection-fix-opencode.patch"), git("format-patch", "-1", "--stdout"));
    git("switch", "--quiet", "--detach", baseline);
    writeFileSync(join(artifacts, "agent_output.json"), JSON.stringify(proposal()));
    return { root: repo, artifacts, git };
}

test("validates the actual AM artifact and applies the exact tested patch", t => {
    const { root, artifacts, git } = patchFixture(t);
    const validated = validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk");
    assert.equal(validated.variable, "OPENCODE_FIXTURE");
    assert.match(validated.payloadHash, /^[a-f0-9]{64}$/);
    assert.equal(git("status", "--porcelain"), "");
    const checkCommand = () => spawnSync(process.execPath, [resolve(".github/llm-detection/fix.mjs"), "check", artifacts], {
        cwd: root, encoding: "utf8",
        env: { ...process.env, AUDIT_RUN_ID: "12345", HARNESS: "opencode", GITHUB_REPOSITORY: "dotnet/sdk",
            VALIDATED_PAYLOAD_HASH: validated.payloadHash, GITHUB_OUTPUT: "" },
    });
    assert.equal(checkCommand().status, 0);
    const changedProposal = proposal();
    changedProposal.items[0].body += "\nChanged after validation.";
    writeFileSync(join(artifacts, "agent_output.json"), JSON.stringify(changedProposal));
    assert.notEqual(validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk").payloadHash, validated.payloadHash);
    const rejected = checkCommand();
    assert.equal(rejected.status, 1);
    assert.match(rejected.stderr, /differs from the patch and metadata/);
    writeFileSync(join(artifacts, "agent_output.json"), JSON.stringify(proposal()));
    assert.equal(validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk", true).hasProposal, true);
    assert.match(git("diff", "--cached"), /OPENCODE/);
});

test("validates an automatic candidate's metadata and exact alias in the actual patch", t => {
    const { root, artifacts } = patchFixture(t);
    const output = proposal();
    output.items[0].body += "\nAutomatic candidate: opencode/OPENCODE_FIXTURE";
    writeFileSync(join(artifacts, "agent_output.json"), JSON.stringify(output));
    const validated = validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk", false, "OPENCODE_FIXTURE");
    assert.equal(validated.hasProposal, true);
    const command = variable => spawnSync(process.execPath, [resolve(".github/llm-detection/fix.mjs"), "check", artifacts], {
        cwd: root, encoding: "utf8",
        env: { ...process.env, AUDIT_RUN_ID: "12345", HARNESS: "opencode", GITHUB_REPOSITORY: "dotnet/sdk",
            FIX_VARIABLE: variable, VALIDATED_PAYLOAD_HASH: validated.payloadHash, GITHUB_OUTPUT: "" },
    });
    assert.equal(command("OPENCODE_FIXTURE").status, 0);
    assert.match(command("OPENCODE_OTHER").stderr, /automatically selected candidate/);
});

test("rejects automatic candidate metadata that omits its selected alias", t => {
    const { root, artifacts } = patchFixture(t);
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk", false, "OPENCODE_FIXTURE"),
        /automatically selected candidate/);
});

test("rejects an otherwise valid patch substituting a different automatic alias", t => {
    const { root, artifacts } = patchFixture(t);
    const output = proposal();
    output.items[0].body += "\nAutomatic candidate: opencode/OPENCODE_OTHER";
    writeFileSync(join(artifacts, "agent_output.json"), JSON.stringify(output));
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk", false, "OPENCODE_OTHER"),
        /automatically selected alias/);
});

test("rejects an otherwise valid patch containing a third file", t => {
    const { root, artifacts } = patchFixture(t, ({ write }) => write("unrelated.txt", "not permitted"));
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /Only the detector/);
});

test("rejects bundles, missing patches, oversized patches, and hidden multiple commits", t => {
    const { root, artifacts } = patchFixture(t);
    writeFileSync(join(artifacts, "aw-extra.bundle"), "bundle");
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /Bundle/);
    rmSync(join(artifacts, "aw-extra.bundle"));
    const patch = join(artifacts, "aw-llm-detection-fix-opencode.patch");
    const original = readFileSync(patch, "utf8");
    writeFileSync(patch, original.repeat(20));
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /16 KiB/);
    writeFileSync(patch, `${original}\nFrom ${revision} Mon Sep 17 00:00:00 2001\n`);
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /exactly one AM commit/);
    rmSync(patch);
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /Exactly one patch/);
});

test("rejects a no-op that carries a patch", t => {
    const { root, artifacts } = patchFixture(t);
    writeFileSync(join(artifacts, "agent_output.json"), JSON.stringify({ items: [{ type: "noop" }] }));
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /must not contain a patch/);
    rmSync(join(artifacts, "aw-llm-detection-fix-opencode.patch"));
    assert.equal(validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk").hasProposal, false);
});

test("accepts an AM patch at 16 KiB and rejects one byte more", t => {
    const { root, artifacts } = patchFixture(t);
    const patch = join(artifacts, "aw-llm-detection-fix-opencode.patch");
    const original = readFileSync(patch, "utf8");
    const padding = "x".repeat(16 * 1024 - Buffer.byteLength(original) - 1);
    const boundary = original.replace("\ndiff --git", `\n${padding}\ndiff --git`);
    assert.equal(Buffer.byteLength(boundary), 16 * 1024);
    writeFileSync(patch, boundary);
    assert.equal(validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk").hasProposal, true);
    writeFileSync(patch, `${boundary}\n`);
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /16 KiB/);
});

test("rejects misleading base metadata", t => {
    const { root, artifacts } = patchFixture(t);
    const patch = join(artifacts, "aw-llm-detection-fix-opencode.patch");
    const content = readFileSync(patch, "utf8");
    writeFileSync(patch, content.replace("\n", `\nX-GH-AW-Base-Commit: ${revision}\n`));
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /base metadata/);
});

test("rejects executable file modes", t => {
    const { root, artifacts } = patchFixture(t, ({ git }) => git("update-index", "--chmod=+x", detectorPath));
    assert.match(readFileSync(join(artifacts, "aw-llm-detection-fix-opencode.patch"), "utf8"), /new mode 100755/);
    assert.throws(() => validateArtifact(root, artifacts, "12345", "opencode", "dotnet/sdk"), /Only the detector/);
});
