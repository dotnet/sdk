// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { createHash } from "node:crypto";
import { appendFileSync, closeSync, constants, fstatSync, lstatSync, openSync, readSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const sources = {
    agent: "agent_usage.json",
    requestsAudit: "sandbox/firewall-audit-logs/api-proxy-logs/token-usage.jsonl",
    requestsFirewallAudit: "sandbox/firewall/audit/api-proxy-logs/token-usage.jsonl",
    requests: "sandbox/firewall/logs/api-proxy-logs/token-usage.jsonl",
    stdio: "agent-stdio.log",
    rpc: "mcp-logs/rpc-messages.jsonl",
};
const messages = {
    missing: "Artifact is missing; its metrics are unavailable.",
    malformed: "Artifact contains malformed or unsupported records; affected metrics are unavailable.",
    incomplete: "Required fields or unambiguous request/response pairs are missing; affected metrics are unavailable.",
    limit: "Artifact exceeds the diagnostic read limit; its metrics are unavailable.",
    unsafe: "Artifact path is a symbolic link or is not a regular file; its metrics are unavailable.",
    empty: "Artifact is empty; its metrics are unavailable.",
    conflict: "Token-usage artifacts contain conflicting records for the same request; request metrics are unavailable.",
    "permission-denied": "Permission denials occurred during agent execution; see the occurrence count in metrics.",
    "context-budget-exhausted": "Context expansion requests exhausted their budget; see the failure count in metrics.",
    "output-submission-rejected": "Output submissions were rejected; see the rejection count in metrics. This does not validate any subsequent submission.",
};
const tokenFields = {
    inputTokens: "input_tokens",
    outputTokens: "output_tokens",
    cacheReadTokens: "cache_read_tokens",
    cacheWriteTokens: "cache_write_tokens",
};
const maxBytes = 16 * 1024 * 1024;
const maxLineBytes = 512 * 1024;
const maxRecords = 10000;
const deniedMessage = "Permission denied and could not request permission from user";
const budgetMessage = "two-window context expansion budget is exhausted";
const isObject = value => value !== null && typeof value === "object" && !Array.isArray(value);
const isCount = value => Number.isSafeInteger(value) && value >= 0;
const isCredits = value => typeof value === "number" && Number.isFinite(value) && value >= 0;
const isModel = value => typeof value === "string" && /^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,99}$/.test(value);
const emptyTokens = () => Object.fromEntries(Object.keys(tokenFields).map(key => [key, null]));
const emptyRequestMetrics = () => ({
    requestCount: null, models: null, modelDurationMs: null, ...emptyTokens(), aiCredits: null,
});

class Unavailable extends Error {
    constructor(code) {
        super(code);
        this.code = code;
    }
}

// Read only fixed artifact paths, reject links and special files, and cap even growing files.
function withArtifact(directory, source, limit, diagnostics, read) {
    let fd;
    try {
        let path = resolve(directory);
        const root = lstatSync(path);
        if (root.isSymbolicLink() || !root.isDirectory()) {
            throw new Unavailable("unsafe");
        }
        const parts = source.split("/");
        for (let i = 0; i < parts.length; i++) {
            path = join(path, parts[i]);
            const stat = lstatSync(path);
            if (stat.isSymbolicLink() || (i < parts.length - 1 ? !stat.isDirectory() : !stat.isFile())) {
                throw new Unavailable("unsafe");
            }
        }
        fd = openSync(path, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0) | (constants.O_NONBLOCK ?? 0));
        const stat = fstatSync(fd);
        if (!stat.isFile()) {
            throw new Unavailable("unsafe");
        }
        if (stat.size > limit) {
            throw new Unavailable("limit");
        }
        let total = 0;
        function* chunks() {
            const buffer = Buffer.alloc(64 * 1024);
            let length;
            while ((length = readSync(fd, buffer, 0, buffer.length, null)) !== 0) {
                total += length;
                if (total > limit) {
                    throw new Unavailable("limit");
                }
                yield buffer.subarray(0, length);
            }
            if (total === 0) {
                throw new Unavailable("empty");
            }
        }
        return read(chunks());
    } catch (error) {
        const code = error instanceof Unavailable ? error.code : error.code === "ENOENT" ? "missing" : null;
        if (!code) {
            throw error;
        }
        diagnostics.push({ source, code, message: messages[code] });
        return null;
    } finally {
        if (fd !== undefined) {
            closeSync(fd);
        }
    }
}

function parseJson(text) {
    try {
        return JSON.parse(text);
    } catch {
        throw new Unavailable("malformed");
    }
}

function* records(chunks, includeLine = false) {
    let pending = Buffer.alloc(0);
    let count = 0;
    for (const chunk of chunks) {
        pending = Buffer.concat([pending, chunk]);
        let end;
        while ((end = pending.indexOf(10)) !== -1) {
            if (end > maxLineBytes) {
                throw new Unavailable("limit");
            }
            const line = pending.subarray(0, end).toString("utf8");
            pending = pending.subarray(end + 1);
            if (line.trim()) {
                if (++count > maxRecords) {
                    throw new Unavailable("limit");
                }
                const row = parseJson(line);
                yield includeLine ? { row, line } : row;
            }
        }
        if (pending.length > maxLineBytes) {
            throw new Unavailable("limit");
        }
    }
    const last = pending.toString("utf8");
    if (last.trim()) {
        if (++count > maxRecords) {
            throw new Unavailable("limit");
        }
        const row = parseJson(last);
        yield includeLine ? { row, line: last } : row;
    }
    if (!count) {
        throw new Unavailable("empty");
    }
}

function agentUsage(chunks) {
    const buffers = Array.from(chunks, chunk => Buffer.from(chunk));
    const row = parseJson(Buffer.concat(buffers).toString("utf8"));
    if (!isObject(row)) {
        throw new Unavailable("malformed");
    }
    return {
        ...Object.fromEntries(Object.entries(tokenFields).map(([key, field]) => [key, isCount(row[field]) ? row[field] : null])),
        ambientContextTokens: isCount(row.ambient_context) ? row.ambient_context : null,
        aiCredits: isCredits(row.ai_credits) ? row.ai_credits : null,
        primaryModel: isModel(row.primary_model) ? row.primary_model : null,
    };
}

function requestUsage(directory, diagnostics) {
    const result = { requestCount: 0, models: [], modelDurationMs: 0, ...emptyTokens(), aiCredits: 0 };
    const fields = { ...tokenFields, modelDurationMs: "duration_ms", aiCredits: "ai_credits_this_response" };
    for (const key of Object.keys(fields)) {
        result[key] = 0;
    }
    const models = new Set();
    const seen = new Map();
    const failures = [];
    let available = false;
    for (const source of [sources.requestsAudit, sources.requestsFirewallAudit, sources.requests]) {
        const read = withArtifact(directory, source, maxBytes, failures, chunks => {
            for (const { row, line } of records(chunks, true)) {
                if (!isObject(row) || row.event !== "token_usage" || !/^token-usage\/v0\.\d+\.\d+$/.test(row._schema ?? "") ||
                    (row.request_id != null && (typeof row.request_id !== "string" || row.request_id.length > 200))) {
                    throw new Unavailable("malformed");
                }
                if (!isModel(row.model) || Object.entries(fields).some(([key, field]) =>
                    !(key === "aiCredits" ? isCredits : isCount)(row[field]))) {
                    throw new Unavailable("incomplete");
                }
                // Keep only fingerprints: IDs deduplicate parsed fields; absent IDs require an exact line copy.
                const key = row.request_id ? `${row.event}:${row.request_id}`
                    : `line:${createHash("sha256").update(line).digest("hex")}`;
                const fingerprint = createHash("sha256").update(JSON.stringify([
                    row._schema, row.timestamp, row.model, row.status, ...Object.values(fields).map(field => row[field]),
                ])).digest("hex");
                if (seen.has(key)) {
                    if (seen.get(key) !== fingerprint) {
                        throw new Unavailable("conflict");
                    }
                    continue;
                }
                if (seen.size >= maxRecords) {
                    throw new Unavailable("limit");
                }
                seen.set(key, fingerprint);
                result.requestCount++;
                models.add(row.model);
                for (const [key, field] of Object.entries(fields)) {
                    const sum = result[key] + row[field];
                    if (!(key === "aiCredits" ? isCredits : isCount)(sum)) {
                        throw new Unavailable("incomplete");
                    }
                    result[key] = sum;
                }
            }
            return true;
        });
        available ||= read === true;
    }
    const invalid = failures.filter(failure => failure.code !== "missing");
    if (invalid.length || !available) {
        diagnostics.push(...(invalid.length ? invalid : [
            { source: sources.requests, code: "missing", message: messages.missing },
        ]));
        return null;
    }
    result.models = [...models].sort();
    // Credits are recorded to five decimal places; avoid binary floating-point noise.
    if (result.aiCredits !== null) {
        result.aiCredits = Number(result.aiCredits.toFixed(8));
    }
    return result;
}

function deniedCommands(chunks) {
    let count = 0;
    let tail = "";
    for (const chunk of chunks) {
        const text = tail + chunk.toString("utf8");
        let start = 0;
        let match;
        while ((match = text.indexOf(deniedMessage, start)) !== -1) {
            count++;
            start = match + deniedMessage.length;
        }
        tail = text.slice(Math.max(start, text.length - deniedMessage.length + 1));
    }
    return count;
}

function rpcUsage(chunks) {
    const result = { contextReadCalls: 0, contextBudgetFailures: 0, rejectedOutputSubmissions: 0 };
    const pendingOutputs = [];
    let contextBalance = 0;
    let contextIncomplete = false;
    let outputIncomplete = false;
    for (const row of records(chunks)) {
        if (!isObject(row) || row._schema !== "rpc-message/v2" ||
            !["rpc_request", "rpc_response"].includes(row.event) ||
            typeof row.server_id !== "string" || row.server_id.length > 200 || !isObject(row.payload)) {
            throw new Unavailable("malformed");
        }
        const payload = row.payload;
        if (payload.id !== undefined &&
            !(typeof payload.id === "string" && payload.id.length <= 200) && !Number.isSafeInteger(payload.id)) {
            throw new Unavailable("malformed");
        }
        if (row.event === "rpc_request") {
            if (row.direction !== "OUT" || typeof payload.method !== "string") {
                throw new Unavailable("malformed");
            }
            const name = payload.method === "tools/call" ? payload.params?.name : null;
            if (payload.method === "tools/call" && typeof name !== "string") {
                throw new Unavailable("malformed");
            }
            if (row.server_id === "mcpscripts") {
                contextBalance++;
                if (name === "read_context") {
                    result.contextReadCalls++;
                }
            }
            if (row.server_id === "safeoutputs") {
                if (payload.id !== undefined && pendingOutputs.some(request => request.id === payload.id)) {
                    outputIncomplete = true;
                }
                pendingOutputs.push({ id: payload.id, name });
            }
            continue;
        }
        if (row.direction !== "IN") {
            throw new Unavailable("malformed");
        }
        const response = payload.result;
        const errors = [row.error, row.error?.message, payload.error, payload.error?.message, payload._error]
            .filter(value => typeof value === "string");
        if (response?.isError === true && Array.isArray(response.content)) {
            errors.push(...response.content.filter(item => item?.type === "text" && typeof item.text === "string").map(item => item.text));
        }
        const failed = errors.length > 0 || payload.error != null || response?.isError === true;
        if (row.server_id === "mcpscripts") {
            // Budget signatures are explicit errors, so parallel ID-less source calls need no pairing.
            if (errors.some(text => text.includes(budgetMessage))) {
                result.contextBudgetFailures++;
            }
            if (errors.some(text => text.includes("Submission rejected:"))) {
                result.rejectedOutputSubmissions++;
            }
            contextBalance--;
            if (contextBalance < 0 || (!failed && !Object.hasOwn(payload, "result"))) {
                contextIncomplete = true;
            }
        }
        if (row.server_id === "safeoutputs") {
            let index = payload.id === undefined ? -1 : pendingOutputs.findIndex(request => request.id === payload.id);
            // The gh-aw proxy currently omits request IDs. Attribute outputs only when unambiguous.
            if (index === -1 && pendingOutputs.length === 1 &&
                (pendingOutputs[0].id === undefined || payload.id === undefined)) {
                index = 0;
            }
            if (index === -1) {
                outputIncomplete = true;
                continue;
            }
            const [request] = pendingOutputs.splice(index, 1);
            if (request.name === "record_interpretations" && failed) {
                result.rejectedOutputSubmissions++;
            }
            if (!failed && !Object.hasOwn(payload, "result")) {
                outputIncomplete = true;
            }
        }
    }
    if (contextIncomplete || contextBalance !== 0) {
        result.contextBudgetFailures = null;
    }
    if (contextIncomplete || contextBalance !== 0 || outputIncomplete || pendingOutputs.length > 0) {
        result.rejectedOutputSubmissions = null;
    }
    return result;
}

/**
 * Collect bounded, observational metrics only. Null means unavailable, never zero.
 * Request totals and agent_usage totals stay separate; neither validates agent output.
 */
export function collectDiagnostics(directory) {
    const diagnostics = [];
    const agent = withArtifact(directory, sources.agent, 128 * 1024, diagnostics, agentUsage);
    const requests = requestUsage(directory, diagnostics);
    const denied = withArtifact(directory, sources.stdio, maxBytes, diagnostics, deniedCommands);
    const rpc = withArtifact(directory, sources.rpc, maxBytes, diagnostics, rpcUsage);
    for (const [source, values] of [[sources.agent, agent], [sources.requests, requests], [sources.rpc, rpc]]) {
        if (values && Object.values(values).some(value => value === null)) {
            diagnostics.push({ source, code: "incomplete", message: messages.incomplete });
        }
    }
    for (const [source, code, count] of [
        [sources.stdio, "permission-denied", denied],
        [sources.rpc, "context-budget-exhausted", rpc?.contextBudgetFailures],
        [sources.rpc, "output-submission-rejected", rpc?.rejectedOutputSubmissions],
    ]) {
        if (isCount(count) && count > 0) {
            diagnostics.push({ source, code, message: messages[code] });
        }
    }
    return {
        schemaVersion: 1,
        metrics: {
            ...emptyRequestMetrics(),
            ...requests,
            agentUsage: agent ?? { ...emptyTokens(), ambientContextTokens: null, aiCredits: null, primaryModel: null },
            deniedCommands: denied,
            contextReadCalls: null,
            contextBudgetFailures: null,
            rejectedOutputSubmissions: null,
            ...rpc,
        },
        diagnostics,
    };
}

export function collectWorkflowDiagnostics(interpreterDirectory, detectionDirectory) {
    const interpreter = collectDiagnostics(interpreterDirectory);
    const detectionDiagnostics = [];
    const detection = {
        metrics: { ...emptyRequestMetrics(), ...requestUsage(detectionDirectory, detectionDiagnostics) },
        diagnostics: detectionDiagnostics,
    };
    const diagnostics = [
        ...interpreter.diagnostics.map(diagnostic => ({ stage: "interpreter", ...diagnostic })),
        ...detection.diagnostics.map(diagnostic => ({ stage: "detection", ...diagnostic })),
    ];
    const total = emptyRequestMetrics();
    for (const key of Object.keys(total)) {
        const values = [interpreter.metrics[key], detection.metrics[key]];
        if (key === "models") {
            total.models = values.every(Array.isArray) ? [...new Set(values.flat())].sort() : null;
        } else if (values.every(value => value !== null)) {
            const sum = values[0] + values[1];
            const valid = key === "aiCredits" ? isCredits(sum) : isCount(sum);
            total[key] = valid ? (key === "aiCredits" ? Number(sum.toFixed(8)) : sum) : null;
            if (!valid) {
                diagnostics.push({ stage: "total", source: sources.requests, code: "incomplete", message: messages.incomplete });
            }
        }
    }
    return { schemaVersion: 1, interpreter, detection, total, diagnostics };
}

function diagnosticLines(diagnostics, includeStage = false) {
    if (!Array.isArray(diagnostics) || !diagnostics.length) {
        return [];
    }
    return ["", "### Diagnostic warnings", ...diagnostics.map(diagnostic => {
        const source = Object.values(sources).includes(diagnostic?.source) ? diagnostic.source : "diagnostics";
        const message = Object.hasOwn(messages, diagnostic?.code) ? messages[diagnostic.code] : "Diagnostic details are unavailable.";
        const stage = includeStage && ["interpreter", "detection", "total"].includes(diagnostic?.stage) ? `${diagnostic.stage}: ` : "";
        return `- ${stage}\`${source}\`: ${message}`;
    })];
}

export function formatWorkflowDiagnostics(report) {
    const stages = [report?.interpreter?.metrics ?? {}, report?.detection?.metrics ?? {}, report?.total ?? {}];
    const lines = [
        "## Stale-reference workflow model usage",
        "",
        "| Metric | Interpreter | Threat detection | Combined |",
        "| --- | --- | --- | --- |",
    ];
    for (const [key, label] of [
        ["requestCount", "Model requests"], ["models", "Models"],
        ["modelDurationMs", "Model duration (summed request milliseconds)"],
        ["inputTokens", "Input tokens"], ["outputTokens", "Output tokens"],
        ["cacheReadTokens", "Cache read tokens"], ["cacheWriteTokens", "Cache write tokens"],
        ["aiCredits", "AI credits (requests)"],
    ]) {
        const values = stages.map(stage => key === "models"
            ? Array.isArray(stage.models) && stage.models.length
                ? stage.models.map(model => isModel(model) ? `\`${model}\`` : "Unavailable").join(", ") : "Unavailable"
            : isCredits(stage[key]) ? String(stage[key]) : "Unavailable");
        lines.push(`| ${label} | ${values.join(" | ")} |`);
    }
    lines.push("", "Combined usage requires both stages; missing traces are unavailable, not zero. " +
        "Tokens follow each provider's accounting, and summed model duration is not workflow wall time. " +
        "Request credits do not include a second copy of agent_usage totals. These metrics never authorize findings.",
        ...diagnosticLines(report?.diagnostics, true));
    return lines.join("\n") + "\n";
}

/** Render only whitelisted metrics and fixed diagnostic messages, never artifact prose. */
export function formatDiagnostics(report) {
    const metrics = report?.metrics ?? {};
    const agent = metrics.agentUsage ?? {};
    const number = value => isCredits(value) ? String(value) : "Unavailable";
    const model = value => isModel(value) ? `\`${value}\`` : "Unavailable";
    const lines = [
        "## Stale-reference interpreter runtime diagnostics",
        "",
        "| Metric | Value |",
        "| --- | --- |",
        `| Model requests | ${number(metrics.requestCount)} |`,
        `| Models | ${Array.isArray(metrics.models) && metrics.models.length ? metrics.models.map(model).join(", ") : "Unavailable"} |`,
        `| Model duration (summed request milliseconds) | ${number(metrics.modelDurationMs)} |`,
        `| Input / output tokens | ${number(metrics.inputTokens)} / ${number(metrics.outputTokens)} |`,
        `| Cache read / write tokens | ${number(metrics.cacheReadTokens)} / ${number(metrics.cacheWriteTokens)} |`,
        `| AI credits (requests) | ${number(metrics.aiCredits)} |`,
        `| Agent primary model | ${model(agent.primaryModel)} |`,
        `| Agent input / output tokens | ${number(agent.inputTokens)} / ${number(agent.outputTokens)} |`,
        `| Agent cache read / write tokens | ${number(agent.cacheReadTokens)} / ${number(agent.cacheWriteTokens)} |`,
        `| Agent ambient context tokens | ${number(agent.ambientContextTokens)} |`,
        `| AI credits (agent usage) | ${number(agent.aiCredits)} |`,
        `| Permission-denial occurrences | ${number(metrics.deniedCommands)} |`,
        `| Context-read calls | ${number(metrics.contextReadCalls)} |`,
        `| Context-budget failures | ${number(metrics.contextBudgetFailures)} |`,
        `| Rejected output submissions | ${number(metrics.rejectedOutputSubmissions)} |`,
        "",
        "These observational metrics do not validate or authorize findings. Permission denials count literal log occurrences, not unique commands.",
    ];
    lines.push(...diagnosticLines(report?.diagnostics));
    return lines.join("\n") + "\n";
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
    const workflow = process.argv[2] === "--workflow";
    if (process.argv.length !== (workflow ? 6 : 4)) {
        throw new Error("Usage: node diagnostics.mjs <artifact-directory> <output-file> or " +
            "node diagnostics.mjs --workflow <interpreter-directory> <detection-directory> <output-file>");
    }
    const report = workflow ? collectWorkflowDiagnostics(process.argv[3], process.argv[4]) : collectDiagnostics(process.argv[2]);
    writeFileSync(process.argv.at(-1), JSON.stringify(report) + "\n", "utf8");
    if (process.env.GITHUB_STEP_SUMMARY) {
        appendFileSync(process.env.GITHUB_STEP_SUMMARY,
            workflow ? formatWorkflowDiagnostics(report) : formatDiagnostics(report), "utf8");
    }
}
