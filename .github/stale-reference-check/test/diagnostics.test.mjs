// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { copyFileSync, mkdirSync, readFileSync, rmSync, symlinkSync, truncateSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { collectDiagnostics, collectWorkflowDiagnostics, formatDiagnostics, formatWorkflowDiagnostics } from "../diagnostics.mjs";

const requestPath = join("sandbox", "firewall", "logs", "api-proxy-logs", "token-usage.jsonl");
const alternateRequestPaths = [
    join("sandbox", "firewall-audit-logs", "api-proxy-logs", "token-usage.jsonl"),
    join("sandbox", "firewall", "audit", "api-proxy-logs", "token-usage.jsonl"),
];
const rpcPath = join("mcp-logs", "rpc-messages.jsonl");
const budgetMessage = "two-window context expansion budget is exhausted";
const deniedMessage = "Permission denied and could not request permission from user";
const modulePath = fileURLToPath(new URL("../diagnostics.mjs", import.meta.url));

function fixture(t) {
    const directory = resolve("artifacts", "log", `stale-reference-diagnostics-${randomUUID()}`);
    mkdirSync(directory, { recursive: true });
    t.after(() => rmSync(directory, { recursive: true, force: true }));
    return directory;
}

function write(directory, path, value) {
    const target = join(directory, path);
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, typeof value === "string" ? value : JSON.stringify(value), "utf8");
}

function jsonl(directory, path, rows) {
    write(directory, path, rows.map(row => JSON.stringify(row)).join("\n") + "\n");
}

function usage(overrides = {}) {
    return {
        event: "token_usage",
        _schema: "token-usage/v0.28.14",
        request_id: "request-1",
        model: "claude-sonnet-5",
        status: 200,
        input_tokens: 10,
        output_tokens: 20,
        cache_read_tokens: 30,
        cache_write_tokens: 40,
        duration_ms: 100,
        ai_credits_this_response: 0.1,
        ...overrides,
    };
}

function request(name, id, server = "mcpscripts") {
    return {
        _schema: "rpc-message/v2",
        event: "rpc_request",
        direction: "OUT",
        server_id: server,
        method: "tools/call",
        payload: { jsonrpc: "2.0", id, method: "tools/call", params: { name, arguments: { secret: "DO-NOT-ECHO" } } },
    };
}

function response(payload = { result: {} }, overrides = {}) {
    return {
        _schema: "rpc-message/v2",
        event: "rpc_response",
        direction: "IN",
        server_id: "mcpscripts",
        payload,
        ...overrides,
    };
}

function complete(directory) {
    write(directory, "agent_usage.json", {
        input_tokens: 10, output_tokens: 20, cache_read_tokens: 30, cache_write_tokens: 40,
        ambient_context: 6, ai_credits: 0.1, primary_model: "claude-sonnet-5",
    });
    jsonl(directory, requestPath, [usage()]);
    write(directory, "agent-stdio.log", "Ordinary bounded log content.");
    jsonl(directory, rpcPath, [request("read_batch"), response()]);
}

test("collects deterministic whitelisted metrics without using findings or narrative counts", t => {
    const directory = fixture(t);
    complete(directory);
    jsonl(directory, requestPath, [
        usage({ request_id: "one", model: "model-z", extra: "DO-NOT-ECHO" }),
        usage({ request_id: "two", model: "model-a", status: 500, ai_credits_this_response: 0.2 }),
    ]);
    write(directory, "agent-stdio.log", `${deniedMessage}\n${deniedMessage}`);
    jsonl(directory, rpcPath, [
        request("read_context"), response({ _error: "transport error", _raw: "DO-NOT-ECHO" }, { error: budgetMessage }),
        request("read_context", 2), response({ id: 2, error: { message: budgetMessage } }),
        request("read_context", 3), response({ id: 3, result: { content: [{ type: "text", text: budgetMessage }] } }),
        request("record_interpretations", undefined, "safeoutputs"),
        response({ _error: "invalid" }, { server_id: "safeoutputs", error: "Invalid arguments: results/schemaVersion" }),
        request("record_interpretations", undefined, "safeoutputs"), response({ result: {} }, { server_id: "safeoutputs" }),
    ]);
    write(directory, "agent_output.json", { items: [{ type: "record_interpretations", payload: "DO-NOT-ECHO" }], errors: [] });
    const report = collectDiagnostics(directory);
    assert.deepEqual(report, collectDiagnostics(directory));
    assert.equal(report.schemaVersion, 1);
    assert.deepEqual(report.metrics, {
        requestCount: 2, models: ["model-a", "model-z"], modelDurationMs: 200,
        inputTokens: 20, outputTokens: 40, cacheReadTokens: 60, cacheWriteTokens: 80, aiCredits: 0.3,
        agentUsage: {
            inputTokens: 10, outputTokens: 20, cacheReadTokens: 30, cacheWriteTokens: 40,
            ambientContextTokens: 6, aiCredits: 0.1, primaryModel: "claude-sonnet-5",
        },
        deniedCommands: 2, contextReadCalls: 3, contextBudgetFailures: 2, rejectedOutputSubmissions: 1,
    });
    assert.deepEqual(report.diagnostics, [
        {
            source: "agent-stdio.log",
            code: "permission-denied",
            message: "Permission denials occurred during agent execution; see the occurrence count in metrics.",
        },
        {
            source: "mcp-logs/rpc-messages.jsonl",
            code: "context-budget-exhausted",
            message: "Context expansion requests exhausted their budget; see the failure count in metrics.",
        },
        {
            source: "mcp-logs/rpc-messages.jsonl",
            code: "output-submission-rejected",
            message: "Output submissions were rejected; see the rejection count in metrics. This does not validate any subsequent submission.",
        },
    ]);
    assert.doesNotMatch(JSON.stringify(report) + formatDiagnostics(report), /DO-NOT-ECHO|results\/schemaVersion|actionable/);
    assert.match(formatDiagnostics(report), /Model requests \| 2/);
    assert.match(formatDiagnostics(report), /do not validate or authorize findings/);
    for (const diagnostic of report.diagnostics) {
        assert.ok(formatDiagnostics(report).includes(diagnostic.message));
    }
});

test("matches ID-bearing out-of-order RPC responses and nested MCP errors", t => {
    const directory = fixture(t);
    complete(directory);
    jsonl(directory, rpcPath, [
        request("read_context", 1), request("record_interpretations", 2, "safeoutputs"),
        response({ id: 2, result: { isError: true, content: [{ type: "text", text: "Invalid arguments: DO-NOT-ECHO" }] } }, { server_id: "safeoutputs" }),
        response({ id: 1, result: { isError: true, content: [{ type: "text", text: budgetMessage }] } }),
    ]);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.contextBudgetFailures, 1);
    assert.equal(report.metrics.rejectedOutputSubmissions, 1);
    assert.deepEqual(report.diagnostics.map(item => item.code), ["context-budget-exhausted", "output-submission-rejected"]);
});

test("zero runtime failure counts produce no runtime warnings", t => {
    const directory = fixture(t);
    complete(directory);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.deniedCommands, 0);
    assert.equal(report.metrics.contextBudgetFailures, 0);
    assert.equal(report.metrics.rejectedOutputSubmissions, 0);
    assert.deepEqual(report.diagnostics, []);
});

test("synchronous submission rejections are counted without exposing payload errors", t => {
    const directory = fixture(t);
    complete(directory);
    jsonl(directory, rpcPath, [
        request("prepare_interpretations"),
        response({ result: { isError: true, content: [{
            type: "text", text: "Submission rejected: Invalid JSON: DO-NOT-ECHO. 2 attempts remaining.",
        }] } }),
        request("prepare_interpretations"), response({ result: { content: [] } }),
        request("record_interpretations", undefined, "safeoutputs"), response({ result: {} }, { server_id: "safeoutputs" }),
    ]);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.rejectedOutputSubmissions, 1);
    assert.equal(report.metrics.contextReadCalls, 0);
    assert.doesNotMatch(JSON.stringify(report), /DO-NOT-ECHO/);
});

test("missing artifacts and missing directory produce unavailable metrics, not zeros", t => {
    const directory = fixture(t);
    for (const path of [directory, join(directory, "absent")]) {
        const report = collectDiagnostics(path);
        for (const [key, value] of Object.entries(report.metrics)) {
            assert.deepEqual(value, key === "agentUsage"
                ? { inputTokens: null, outputTokens: null, cacheReadTokens: null, cacheWriteTokens: null, ambientContextTokens: null, aiCredits: null, primaryModel: null }
                : null);
        }
        assert.equal(report.diagnostics.length, 4);
        assert.ok(report.diagnostics.every(item => item.code === "missing"));
        assert.match(formatDiagnostics(report), /Unavailable/);
    }
});

test("malformed and empty artifacts invalidate their metrics instead of publishing partial totals", t => {
    const directory = fixture(t);
    write(directory, "agent_usage.json", "{broken");
    write(directory, requestPath, JSON.stringify(usage()) + "\n{broken");
    write(directory, "agent-stdio.log", "");
    write(directory, rpcPath, JSON.stringify(request("read_context")) + "\n{broken");
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.requestCount, null);
    assert.equal(report.metrics.contextReadCalls, null);
    assert.equal(report.metrics.deniedCommands, null);
    assert.deepEqual(report.diagnostics.map(item => item.code), ["malformed", "malformed", "empty", "malformed"]);
});

test("invalid request fields invalidate request totals while independent metrics survive", t => {
    const directory = fixture(t);
    complete(directory);
    write(directory, "agent_usage.json", { input_tokens: "10", primary_model: "<script>DO-NOT-ECHO</script>" });
    jsonl(directory, requestPath, [
        usage(),
        usage({ request_id: "two", output_tokens: -1, cache_read_tokens: null, duration_ms: 0.1,
            ai_credits_this_response: "0", model: "bad\n|DO-NOT-ECHO" }),
    ]);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.requestCount, null);
    assert.equal(report.metrics.inputTokens, null);
    for (const key of ["outputTokens", "cacheReadTokens", "modelDurationMs", "aiCredits", "models"]) {
        assert.equal(report.metrics[key], null);
    }
    assert.equal(report.diagnostics.length, 2);
    assert.ok(report.diagnostics.every(item => item.code === "incomplete"));
    assert.doesNotMatch(JSON.stringify(report) + formatDiagnostics(report), /DO-NOT-ECHO/);
});

test("unknown schemas, invalid request IDs and unsafe totals are not silently accepted", t => {
    const directory = fixture(t);
    complete(directory);
    for (const rows of [
        [usage({ _schema: "token-usage/v9.0.0" })],
        [usage({ request_id: 42 })],
        [{ event: "something_else" }],
        [null],
    ]) {
        jsonl(directory, requestPath, rows);
        const report = collectDiagnostics(directory);
        assert.equal(report.metrics.requestCount, null);
        assert.ok(report.diagnostics.some(item => item.code === "malformed"));
    }
    jsonl(directory, requestPath, [usage({ input_tokens: Number.MAX_SAFE_INTEGER }), usage({ request_id: "two" })]);
    assert.equal(collectDiagnostics(directory).metrics.inputTokens, null);
    jsonl(directory, rpcPath, [{ ...request("read_context"), _schema: "rpc-message/v99" }]);
    assert.equal(collectDiagnostics(directory).metrics.contextReadCalls, null);
});

test("each alternate token log works alone without missing-path warnings", t => {
    const directory = fixture(t);
    complete(directory);
    const expected = collectDiagnostics(directory);
    rmSync(join(directory, requestPath));
    for (const path of alternateRequestPaths) {
        jsonl(directory, path, [usage()]);
        assert.deepEqual(collectDiagnostics(directory), expected);
        rmSync(join(directory, path));
    }
});

test("overlapping token log copies deduplicate event and request ID", t => {
    const directory = fixture(t);
    complete(directory);
    const first = usage({ request_id: "first" });
    const second = usage({ request_id: "second", model: "another-model" });
    jsonl(directory, requestPath, [first, second, first]);
    jsonl(directory, alternateRequestPaths[0], [first, first]);
    jsonl(directory, alternateRequestPaths[1], [
        Object.fromEntries(Object.entries(first).reverse()), second,
    ]);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.requestCount, 2);
    assert.equal(report.metrics.inputTokens, 20);
    assert.equal(report.metrics.modelDurationMs, 200);
    assert.equal(report.metrics.aiCredits, 0.2);
    assert.deepEqual(report.metrics.models, ["another-model", "claude-sonnet-5"]);
    assert.deepEqual(report.diagnostics, []);
});

test("distinct token records across all three paths merge into complete totals", t => {
    const directory = fixture(t);
    complete(directory);
    jsonl(directory, alternateRequestPaths[0], [usage({ request_id: "second", ai_credits_this_response: 0.2 })]);
    jsonl(directory, alternateRequestPaths[1], [usage({ request_id: "third", ai_credits_this_response: 0.3 })]);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.requestCount, 3);
    assert.equal(report.metrics.inputTokens, 30);
    assert.equal(report.metrics.outputTokens, 60);
    assert.equal(report.metrics.cacheReadTokens, 90);
    assert.equal(report.metrics.cacheWriteTokens, 120);
    assert.equal(report.metrics.modelDurationMs, 300);
    assert.equal(report.metrics.aiCredits, 0.6);
    assert.deepEqual(report.diagnostics, []);
});

test("ID-less token records deduplicate exact lines, not inferred request identities", t => {
    const directory = fixture(t);
    complete(directory);
    const line = JSON.stringify(usage({ request_id: undefined }));
    write(directory, requestPath, line + "\n" + line);
    write(directory, alternateRequestPaths[0], line + "\n");
    write(directory, alternateRequestPaths[1], line + " \n");
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.requestCount, 2);
    assert.equal(report.metrics.inputTokens, 20);
    assert.deepEqual(report.diagnostics, []);
});

test("present malformed, incomplete or empty copies invalidate the entire request aggregate", t => {
    const directory = fixture(t);
    complete(directory);
    for (const [text, code] of [
        [JSON.stringify(usage()) + "\n{DO-NOT-ECHO", "malformed"],
        [JSON.stringify(usage({ input_tokens: undefined })), "incomplete"],
        ["", "empty"],
    ]) {
        write(directory, alternateRequestPaths[0], text);
        const report = collectDiagnostics(directory);
        for (const key of ["requestCount", "models", "modelDurationMs", "inputTokens", "outputTokens",
            "cacheReadTokens", "cacheWriteTokens", "aiCredits"]) {
            assert.equal(report.metrics[key], null);
        }
        assert.equal(report.metrics.agentUsage.inputTokens, 10);
        assert.deepEqual(report.diagnostics.map(item => item.code), [code]);
        assert.equal(report.diagnostics[0].source, alternateRequestPaths[0].split("\\").join("/"));
        assert.doesNotMatch(JSON.stringify(report) + formatDiagnostics(report), /DO-NOT-ECHO/);
    }
});

test("conflicting copies never silently select one request record", t => {
    const directory = fixture(t);
    complete(directory);
    for (const overrides of [{ input_tokens: 11 }, { model: "different-model" }, { status: 500 }]) {
        jsonl(directory, alternateRequestPaths[0], [usage(overrides)]);
        const report = collectDiagnostics(directory);
        assert.equal(report.metrics.requestCount, null);
        assert.equal(report.metrics.inputTokens, null);
        assert.equal(report.metrics.aiCredits, null);
        assert.deepEqual(report.diagnostics.map(item => item.code), ["conflict"]);
        assert.match(formatDiagnostics(report), /conflicting records/);
    }
    rmSync(join(directory, alternateRequestPaths[0]));
    jsonl(directory, requestPath, [usage(), usage({ output_tokens: 21 })]);
    assert.deepEqual(collectDiagnostics(directory).diagnostics.map(item => item.code), ["conflict"]);
});

test("unsafe and oversized alternates cannot be bypassed by a usable token log", t => {
    const directory = fixture(t);
    complete(directory);
    const path = join(directory, alternateRequestPaths[0]);
    mkdirSync(path, { recursive: true });
    let report = collectDiagnostics(directory);
    assert.equal(report.metrics.requestCount, null);
    assert.deepEqual(report.diagnostics.map(item => item.code), ["unsafe"]);
    rmSync(path, { recursive: true });
    writeFileSync(path, "");
    truncateSync(path, 16 * 1024 * 1024 + 1);
    report = collectDiagnostics(directory);
    assert.equal(report.metrics.requestCount, null);
    assert.deepEqual(report.diagnostics.map(item => item.code), ["limit"]);
});

test("incomplete or ambiguous RPC exchanges do not imply zero failures", t => {
    const directory = fixture(t);
    complete(directory);
    for (const [rows, contextFailures, outputFailures] of [
        [[request("read_context")], null, null],
        [[request("read_context"), request("read_batch"), response({ error: { message: budgetMessage } })], null, null],
        [[response()], null, null],
        [[request("record_interpretations", undefined, "safeoutputs"), response({}, { server_id: "safeoutputs" })], 0, null],
        [[request("record_interpretations", undefined, "safeoutputs"), request("noop", undefined, "safeoutputs"),
            response({ error: { message: "Invalid arguments" } }, { server_id: "safeoutputs" }),
            response({ result: {} }, { server_id: "safeoutputs" })], 0, null],
    ]) {
        jsonl(directory, rpcPath, rows);
        const report = collectDiagnostics(directory);
        assert.equal(report.metrics.contextBudgetFailures, contextFailures);
        assert.equal(report.metrics.rejectedOutputSubmissions, outputFailures);
        assert.ok(report.diagnostics.some(item => item.code === "incomplete"));
    }
});

test("parallel ID-less source calls do not invalidate separately attributable output rejections", t => {
    const directory = fixture(t);
    complete(directory);
    for (const failContext of [false, true]) {
        jsonl(directory, rpcPath, [
            request("read_batch"), request("read_batch"),
            response({ id: 42, result: { content: [{ type: "text", text: budgetMessage }] } }),
            response({ id: 41, result: {} }),
            request("read_context"), request("read_batch"),
            response({ id: 44, result: {} }),
            response(failContext ? { _error: "transport error" } : { result: {} }, failContext ? { error: budgetMessage } : {}),
            request("record_interpretations", undefined, "safeoutputs"),
            response({ _error: "invalid" }, { server_id: "safeoutputs", error: "Invalid arguments" }),
        ]);
        const report = collectDiagnostics(directory);
        assert.equal(report.metrics.contextReadCalls, 1);
        assert.equal(report.metrics.contextBudgetFailures, failContext ? 1 : 0);
        assert.equal(report.metrics.rejectedOutputSubmissions, 1);
        assert.deepEqual(report.diagnostics.map(item => item.code), failContext
            ? ["context-budget-exhausted", "output-submission-rejected"] : ["output-submission-rejected"]);
    }
});

test("budget signatures in other servers and successful source content are not failures", t => {
    const directory = fixture(t);
    complete(directory);
    jsonl(directory, rpcPath, [
        request("read_context"), response({ result: { content: [{ type: "text", text: budgetMessage }] } }),
        request("noop", undefined, "other"), response({ error: { message: budgetMessage } }, { server_id: "other" }),
    ]);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.contextBudgetFailures, 0);
    assert.deepEqual(report.diagnostics, []);
});

test("permission-denial scanning handles chunk boundaries without retaining transcript text", t => {
    const directory = fixture(t);
    complete(directory);
    write(directory, "agent-stdio.log", "x".repeat(65536 - 10) + deniedMessage + deniedMessage + "DO-NOT-ECHO");
    assert.equal(collectDiagnostics(directory).metrics.deniedCommands, 2);
});

test("bounded JSON reads spanning chunks preserve bytes and tolerate a final JSONL line without newline", t => {
    const directory = fixture(t);
    complete(directory);
    const usagePath = join(directory, "agent_usage.json");
    const agent = JSON.parse(readFileSync(usagePath, "utf8"));
    write(directory, "agent_usage.json", { ignored: "x".repeat(65536), ...agent });
    write(directory, requestPath, JSON.stringify(usage()));
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.agentUsage.inputTokens, 10);
    assert.equal(report.metrics.requestCount, 1);
    assert.deepEqual(report.diagnostics, []);
});

test("duplicate outstanding output RPC IDs are explicitly ambiguous", t => {
    const directory = fixture(t);
    complete(directory);
    jsonl(directory, rpcPath, [
        request("noop", 1, "safeoutputs"), request("record_interpretations", 1, "safeoutputs"),
        response({ id: 1, error: { message: "Invalid arguments" } }, { server_id: "safeoutputs" }),
        response({ id: 1, result: {} }, { server_id: "safeoutputs" }),
    ]);
    const report = collectDiagnostics(directory);
    assert.equal(report.metrics.contextReadCalls, 0);
    assert.equal(report.metrics.contextBudgetFailures, 0);
    assert.equal(report.metrics.rejectedOutputSubmissions, null);
    assert.ok(report.diagnostics.some(item => item.code === "incomplete"));
});

test("caps artifact bytes, record length and record count", t => {
    const directory = fixture(t);
    complete(directory);
    truncateSync(join(directory, "agent-stdio.log"), 16 * 1024 * 1024 + 1);
    write(directory, "agent_usage.json", " ".repeat(128 * 1024 + 1));
    write(directory, requestPath, JSON.stringify(usage({ extra: "x".repeat(512 * 1024) })));
    jsonl(directory, rpcPath, Array.from({ length: 10001 }, () => request("read_context")));
    const report = collectDiagnostics(directory);
    assert.equal(report.diagnostics.length, 4);
    assert.ok(report.diagnostics.every(item => item.code === "limit"));
    assert.equal(report.metrics.deniedCommands, null);
    assert.equal(report.metrics.requestCount, null);
    assert.equal(report.metrics.contextReadCalls, null);
});

test("rejects linked directories and non-files without reading their contents", t => {
    const directory = fixture(t);
    const outside = join(directory, "outside");
    const root = join(directory, "root");
    mkdirSync(outside);
    mkdirSync(root);
    write(outside, join("firewall", "logs", "api-proxy-logs", "token-usage.jsonl"), JSON.stringify(usage()));
    symlinkSync(outside, join(root, "sandbox"), process.platform === "win32" ? "junction" : "dir");
    mkdirSync(join(root, "agent_usage.json"));
    const report = collectDiagnostics(root);
    assert.equal(report.metrics.requestCount, null);
    assert.equal(report.diagnostics.filter(item => item.code === "unsafe").length, 4);
    const linkedRoot = join(directory, "linked-root");
    symlinkSync(root, linkedRoot, process.platform === "win32" ? "junction" : "dir");
    assert.ok(collectDiagnostics(linkedRoot).diagnostics.every(item => item.code === "unsafe"));
});

test("summary cannot inject arbitrary artifact or diagnostic prose", () => {
    const summary = formatDiagnostics({
        metrics: { models: ["<img src=x>", "a\n## DO-NOT-ECHO"], requestCount: "DO-NOT-ECHO" },
        diagnostics: [{ source: "DO-NOT-ECHO", code: "DO-NOT-ECHO", message: "DO-NOT-ECHO" }],
    });
    assert.doesNotMatch(summary, /DO-NOT-ECHO|<img/);
    assert.match(summary, /Diagnostic details are unavailable/);
});

test("workflow usage separates stages and sums requests without duplicating agent usage or artifact copies", t => {
    const directory = fixture(t);
    const interpreter = join(directory, "interpreter");
    const detection = join(directory, "detection");
    complete(interpreter);
    write(interpreter, "agent_usage.json", { ai_credits: 999 });
    jsonl(interpreter, alternateRequestPaths[0], [usage()]);
    jsonl(detection, requestPath, [
        usage({ model: "detection-model", ai_credits_this_response: 0.2 }),
        usage({ request_id: "request-2", model: "detection-model", ai_credits_this_response: 0.3 }),
    ]);
    const report = collectWorkflowDiagnostics(interpreter, detection);
    assert.equal(report.interpreter.metrics.aiCredits, 0.1);
    assert.equal(report.detection.metrics.aiCredits, 0.5);
    assert.deepEqual(report.total, {
        requestCount: 3, models: ["claude-sonnet-5", "detection-model"], modelDurationMs: 300,
        inputTokens: 30, outputTokens: 60, cacheReadTokens: 90, cacheWriteTokens: 120, aiCredits: 0.6,
    });
    assert.deepEqual(report.detection.diagnostics, [], "Detection does not publish primary-agent usage or MCP logs.");
    assert.match(formatWorkflowDiagnostics(report), /AI credits \(requests\) \| 0\.1 \| 0\.5 \| 0\.6/);
});

test("unavailable detection usage never becomes a partial combined total", t => {
    const directory = fixture(t);
    complete(directory);
    const detection = join(directory, "detection");
    const missing = collectWorkflowDiagnostics(directory, detection);
    assert.equal(missing.interpreter.metrics.requestCount, 1);
    assert.ok(Object.values(missing.total).every(value => value === null));
    assert.ok(missing.diagnostics.some(diagnostic => diagnostic.stage === "detection" && diagnostic.code === "missing"));
    jsonl(detection, requestPath, [usage(), { secret: "DO-NOT-ECHO" }]);
    const malformed = collectWorkflowDiagnostics(directory, detection);
    assert.equal(malformed.total.aiCredits, null);
    assert.ok(malformed.diagnostics.some(diagnostic => diagnostic.stage === "detection" && diagnostic.code === "malformed"));
    assert.doesNotMatch(JSON.stringify(malformed) + formatWorkflowDiagnostics(malformed), /DO-NOT-ECHO/);
    assert.match(formatWorkflowDiagnostics(missing), /AI credits \(requests\) \| 0\.1 \| Unavailable \| Unavailable/);
    assert.ok(Object.values(collectWorkflowDiagnostics(detection, directory).total).every(value => value === null));
});

test("conflicting, unsafe, and overflowing stage usage cannot produce believable totals", t => {
    const directory = fixture(t);
    complete(directory);
    const detection = join(directory, "detection");
    jsonl(detection, requestPath, [usage()]);
    jsonl(detection, alternateRequestPaths[0], [usage({ ai_credits_this_response: 0.2 })]);
    let report = collectWorkflowDiagnostics(directory, detection);
    assert.equal(report.total.aiCredits, null);
    assert.ok(report.diagnostics.some(diagnostic => diagnostic.stage === "detection" && diagnostic.code === "conflict"));
    rmSync(join(detection, alternateRequestPaths[0]));
    jsonl(detection, requestPath, [usage({ duration_ms: Number.MAX_SAFE_INTEGER, ai_credits_this_response: Number.MAX_VALUE })]);
    jsonl(directory, requestPath, [usage({ ai_credits_this_response: Number.MAX_VALUE })]);
    report = collectWorkflowDiagnostics(directory, detection);
    assert.equal(report.total.modelDurationMs, null);
    assert.equal(report.total.aiCredits, null);
    assert.ok(report.diagnostics.some(diagnostic => diagnostic.stage === "total" && diagnostic.code === "incomplete"));
    const linked = join(directory, "linked-detection");
    symlinkSync(detection, linked, process.platform === "win32" ? "junction" : "dir");
    report = collectWorkflowDiagnostics(directory, linked);
    assert.equal(report.total.requestCount, null);
    assert.ok(report.diagnostics.some(diagnostic => diagnostic.stage === "detection" && diagnostic.code === "unsafe"));
});

test("combined summary cannot inject arbitrary stage or artifact prose", () => {
    const summary = formatWorkflowDiagnostics({
        interpreter: { metrics: { aiCredits: "DO-NOT-ECHO" } },
        detection: { metrics: { models: ["<img src=x>", "DO-NOT-ECHO\n"] } },
        total: { requestCount: "DO-NOT-ECHO" },
        diagnostics: [{ stage: "DO-NOT-ECHO", source: "DO-NOT-ECHO", code: "DO-NOT-ECHO", message: "DO-NOT-ECHO" }],
    });
    assert.doesNotMatch(summary, /DO-NOT-ECHO|<img/);
    assert.match(summary, /Diagnostic details are unavailable/);
});

test("combined CLI writes usage and warnings when the detector artifact is absent", t => {
    const directory = fixture(t);
    complete(directory);
    const output = join(directory, "workflow-runtime.json");
    const summary = join(directory, "summary.md");
    const result = spawnSync(process.execPath, [modulePath, "--workflow", directory, join(directory, "absent"), output], {
        encoding: "utf8", env: { ...process.env, GITHUB_STEP_SUMMARY: summary },
    });
    assert.equal(result.status, 0, result.stderr);
    assert.deepEqual(JSON.parse(readFileSync(output, "utf8")), collectWorkflowDiagnostics(directory, join(directory, "absent")));
    assert.match(readFileSync(summary, "utf8"), /Threat detection \| Combined/);
    assert.match(readFileSync(summary, "utf8"), /detection:.*Artifact is missing/);
});

test("CLI writes compact JSON and appends a summary even when traces are missing", t => {
    const directory = fixture(t);
    const output = join(directory, "diagnostics.json");
    const summary = join(directory, "summary.md");
    writeFileSync(summary, "Existing summary\n");
    const result = spawnSync(process.execPath, [modulePath, join(directory, "absent"), output], {
        encoding: "utf8", env: { ...process.env, GITHUB_STEP_SUMMARY: summary },
    });
    assert.equal(result.status, 0, result.stderr);
    const text = readFileSync(output, "utf8");
    assert.equal(text.trim().split("\n").length, 1);
    assert.deepEqual(JSON.parse(text), collectDiagnostics(join(directory, "absent")));
    assert.match(readFileSync(summary, "utf8"), /^Existing summary\n## Stale-reference interpreter runtime diagnostics/);
    assert.equal(result.stdout, "");
});

test("CLI propagates unexpected filesystem failures", t => {
    const directory = fixture(t);
    const result = spawnSync(process.execPath, [modulePath, directory, join(directory, "missing-parent", "output.json")], {
        encoding: "utf8", env: { ...process.env, GITHUB_STEP_SUMMARY: "" },
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /ENOENT/);
});

test("standalone CLI copied into private input needs no sibling modules", t => {
    const directory = fixture(t);
    const privateInput = join(directory, "private-input");
    const artifacts = join(directory, "agent");
    const output = join(directory, "diagnostics.json");
    mkdirSync(privateInput);
    complete(artifacts);
    write(artifacts, "agent-stdio.log", deniedMessage);
    const copiedModule = join(privateInput, "diagnostics.mjs");
    copyFileSync(modulePath, copiedModule);
    const result = spawnSync(process.execPath, [copiedModule, artifacts, output], {
        cwd: privateInput, encoding: "utf8", env: { ...process.env, GITHUB_STEP_SUMMARY: "" },
    });
    assert.equal(result.status, 0, result.stderr);
    const report = JSON.parse(readFileSync(output, "utf8"));
    assert.deepEqual(report, collectDiagnostics(artifacts));
    assert.deepEqual(report.diagnostics.map(item => item.code), ["permission-denied"]);
});
