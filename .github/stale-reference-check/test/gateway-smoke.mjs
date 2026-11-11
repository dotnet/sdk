// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from "node:assert/strict";
import { execFileSync, spawn } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import os from "node:os";
import path from "node:path";
import { setTimeout as delay } from "node:timers/promises";
import { maxSubmissionBytes, sendRecordingRequest } from "../source-tools.mjs";

const lock = await readFile(new URL("../../workflows/stale-reference-interpret.lock.yml", import.meta.url), "utf8");
const image = process.argv[2] ?? lock.match(/ghcr\.io\/github\/gh-aw-mcpg:v[\d.]+@sha256:[a-f0-9]{64}/)?.[0];
assert.match(image, /^ghcr\.io\/github\/gh-aw-mcpg:v[\d.]+@sha256:[a-f0-9]{64}$/);

const tools = [
    { name: "read_batch", description: "Read synthetic source.", inputSchema: { type: "object" } },
    { name: "prepare_interpretations", description: "Prepare synthetic output.",
        inputSchema: { type: "object", properties: { payload: { type: "object" } }, required: ["payload"] } },
    { name: "record_interpretations", description: "Record synthetic output.",
        inputSchema: { type: "object", properties: { receipt: { type: "string" }, payload: { type: "string" } },
            required: ["receipt", "payload"] } },
];
const preparedPayloads = new Map();
const recordedPayloads = [];
const token = randomUUID();
const backend = createServer(async (request, response) =>
{
    if (request.headers.authorization !== token)
    {
        response.writeHead(401).end();
        return;
    }
    if (request.method !== "POST")
    {
        response.writeHead(405).end();
        return;
    }
    let text = "";
    for await (const chunk of request)
    {
        text += chunk;
    }
    const message = JSON.parse(text);
    if (message.id === undefined)
    {
        response.writeHead(202).end();
        return;
    }
    if (!["initialize", "tools/list", "tools/call"].includes(message.method))
    {
        response.writeHead(200, { "Content-Type": "application/json" });
        response.end(JSON.stringify({
            jsonrpc: "2.0", id: message.id, error: { code: -32601, message: "Unknown fixture method." },
        }));
        return;
    }
    const serializedPayload = message.method === "tools/call" && message.params.name === "prepare_interpretations"
        ? JSON.stringify(message.params.arguments.payload) : null;
    if (serializedPayload)
    {
        preparedPayloads.set(createHash("sha256").update(serializedPayload).digest("hex"), serializedPayload);
    }
    const recording = message.method === "tools/call" && message.params.name === "record_interpretations";
    if (recording)
    {
        assert.equal(message.params.arguments.payload, preparedPayloads.get(message.params.arguments.receipt));
        recordedPayloads.push(message.params.arguments.payload);
    }
    const result = message.method === "initialize"
        ? { protocolVersion: "2025-11-25", capabilities: { tools: {} }, serverInfo: { name: "fixture", version: "1" } }
        : message.method === "tools/list" ? { tools }
        : serializedPayload ? { content: [{ type: "text", text: JSON.stringify({
            receipt: createHash("sha256").update(serializedPayload).digest("hex"),
        }) }] }
        : recording ? { content: [{ type: "text", text: JSON.stringify({ result: "success" }) }] }
        : { content: [{ type: "text", text: "synthetic fixture" }] };
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(JSON.stringify({ jsonrpc: "2.0", id: message.id, result }));
});
await new Promise(resolve => backend.listen(0, "0.0.0.0", resolve));
const name = `stale-reference-gateway-${randomUUID()}`;
const temporary = await mkdtemp(path.join(os.tmpdir(), "stale-reference-gateway-"));
const child = spawn("docker", ["run", "--rm", "-i", "--name", name,
    "--add-host", "host.docker.internal:host-gateway", "-p", "127.0.0.1::8080",
    "-v", "/var/run/docker.sock:/var/run/docker.sock:ro",
    "-e", "MCP_GATEWAY_PORT=8080", "-e", "MCP_GATEWAY_DOMAIN=localhost",
    "-e", "MCP_GATEWAY_AGENT_ID", image, "--config-stdin"], {
    stdio: ["pipe", "pipe", "pipe"],
    env: { ...process.env, MCP_GATEWAY_AGENT_ID: token },
});
let log = "";
for (const stream of [child.stdout, child.stderr])
{
    stream.on("data", chunk => { log = (log + chunk.toString()).slice(-16000); });
}
let spawnError;
child.on("error", error => { spawnError = error; });
child.stdin.on("error", error => { spawnError = error; });
child.stdin.end(JSON.stringify({
    mcpServers: {
        fixture: {
            type: "http",
            url: `http://host.docker.internal:${backend.address().port}`,
            headers: { Authorization: token },
        },
        safeoutputs: {
            type: "http",
            url: `http://host.docker.internal:${backend.address().port}`,
            headers: { Authorization: token },
        },
    },
    gateway: { port: 8080, agentId: token, domain: "localhost", startupTimeout: 30 },
}));

try
{
    for (let attempt = 0; !log.includes("Routes:"); attempt++)
    {
        if (spawnError || child.exitCode !== null || attempt >= 240)
        {
            throw new Error(`Gateway did not start: ${spawnError?.message ?? log}`);
        }
        await delay(500);
    }
    const address = execFileSync("docker", ["port", name, "8080/tcp"], { encoding: "utf8" }).trim();
    const config = path.join(temporary, "mcp-config.json");
    await writeFile(config, JSON.stringify({ mcpServers: { safeoutputs: {
        type: "http", url: `http://${address}/mcp/safeoutputs`, headers: { Authorization: token },
    } } }), { mode: 0o600 });
    const url = `http://${address}/mcp/fixture`;
    let id = 0;
    let session;
    let version = "2026-07-28";
    async function rpc(method, params, notification = false)
    {
        const headers = {
            Authorization: token, "Content-Type": "application/json",
            Accept: "application/json, text/event-stream",
            "MCP-Protocol-Version": version, "Mcp-Method": method,
        };
        if (session)
        {
            headers["Mcp-Session-Id"] = session;
        }
        const response = await fetch(url, {
            method: "POST", headers,
            body: JSON.stringify({ jsonrpc: "2.0", ...(notification ? {} : { id: ++id }), method, params }),
            signal: AbortSignal.timeout(15000),
        });
        session = response.headers.get("mcp-session-id") ?? session;
        const body = await response.text();
        if (!body && notification)
        {
            assert.ok(response.ok);
            return null;
        }
        const data = body.split("\n").find(line => line.startsWith("data: "));
        return JSON.parse(data ? data.slice(6) : body);
    }

    const discovery = await rpc("server/discover", {
        _meta: {
            "io.modelcontextprotocol/protocolVersion": version,
            "io.modelcontextprotocol/clientCapabilities": {},
        },
    });
    // A successful stateless probe leaves native clients without the gateway's required session.
    assert.equal(discovery.error?.code, -32022, "The stateful gateway must reject stateless discovery.");
    assert.ok(!discovery.error.data.supported.includes(version));
    assert.ok(discovery.error.data.supported.includes("2025-11-25"));
    version = "2025-11-25";
    const initialized = await rpc("initialize", {
        protocolVersion: version, capabilities: {}, clientInfo: { name: "native-client-fixture", version: "1" },
    });
    assert.ok(initialized.result, JSON.stringify(initialized));
    version = initialized.result.protocolVersion;
    assert.ok(session, "Fallback initialization must establish a stateful session.");
    await rpc("notifications/initialized", {}, true);
    const listed = await rpc("tools/list", {});
    assert.ok(listed.result?.tools, `Native tool discovery failed: ${JSON.stringify(listed)}`);
    assert.deepEqual(listed.result.tools.map(tool => tool.name).sort(), tools.map(tool => tool.name).sort());
    const read = await rpc("tools/call", { name: "read_batch", arguments: {} });
    assert.equal(read.result?.content?.[0]?.text, "synthetic fixture", JSON.stringify(read));
    for (const character of ["\\", "&"])
    {
        const payload = { value: "" };
        const remaining = maxSubmissionBytes - Buffer.byteLength(JSON.stringify(payload));
        const encodedLength = character === "\\" ? 2 : 1;
        payload.value = character.repeat(Math.floor(remaining / encodedLength)) + "a".repeat(remaining % encodedLength);
        const expectedPayload = JSON.stringify(payload);
        assert.equal(Buffer.byteLength(expectedPayload), maxSubmissionBytes);
        const called = await rpc("tools/call", { name: "prepare_interpretations", arguments: { payload } });
        const accepted = JSON.parse(called.result?.content?.[0]?.text);
        assert.deepEqual(accepted, { receipt: createHash("sha256").update(expectedPayload).digest("hex") });
        assert.equal(accepted.receipt, createHash("sha256").update(expectedPayload).digest("hex"));
        assert.deepEqual(await sendRecordingRequest(config, { receipt: accepted.receipt, payload: expectedPayload }), { queued: true });
        assert.equal(recordedPayloads.at(-1), expectedPayload, "Trusted queueing must preserve the full bounded payload without file tools.");
    }
    console.log(`Native discovery, session initialization, tools/list, and bounded native recording passed: ${image}`);
}
finally
{
    try
    {
        execFileSync("docker", ["rm", "--force", name], { stdio: "pipe" });
    }
    catch (error)
    {
        if (child.exitCode === null && !spawnError)
        {
            throw error;
        }
    }
    finally
    {
        backend.closeAllConnections();
        await new Promise(resolve => backend.close(resolve));
        await rm(temporary, { recursive: true, force: true });
    }
}
