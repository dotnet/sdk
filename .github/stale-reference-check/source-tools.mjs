import { createHash, randomBytes } from "node:crypto";
import { appendFile, readFile, rename, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";
import { validateSnapshotInterpretations } from "./interpretations.mjs";

const readers = new Map();
export const maxSubmissionBytes = 512 * 1024;

export function submissionReceipt(payload)
{
    return createHash("sha256").update(JSON.stringify(payload)).digest("hex");
}

export function verifySubmission(submission, receipt)
{
    // The receipt binds the exact payload that threat detection inspected;
    // accepting a structurally valid replacement would break that trust chain.
    if (submission?.schemaVersion !== 1 || !submission.payload ||
        Object.keys(submission).some(key => !["schemaVersion", "receipt", "payload"].includes(key)) ||
        typeof receipt !== "string" || !/^[a-f0-9]{64}$/.test(receipt) ||
        Buffer.byteLength(JSON.stringify(submission.payload), "utf8") > maxSubmissionBytes ||
        submission.receipt !== receipt || submissionReceipt(submission.payload) !== receipt)
    {
        throw new Error("The recording receipt does not match the trusted validated submission.");
    }
    return submission.payload;
}

export async function exportSubmission(directory)
{
    const submission = await requestSourceTools(directory, "submission");
    verifySubmission(submission, submission.receipt);
    await writeFile(path.join(directory, "submission.json"), `${JSON.stringify(submission)}\n`);
}

export async function sendRecordingRequest(configFile, request)
{
    const config = JSON.parse(await readFile(configFile, "utf8"));
    const server = config?.mcpServers?.safeoutputs;
    const authorization = server?.headers?.Authorization;
    if (server?.type !== "http" || typeof server.url !== "string" ||
        typeof authorization !== "string" || !authorization)
    {
        throw new Error("The native safe-output recording transport is not configured.");
    }
    const endpoint = new URL(server.url);
    if (endpoint.protocol !== "http:" || endpoint.pathname !== "/mcp/safeoutputs" ||
        endpoint.username || endpoint.password || endpoint.search || endpoint.hash ||
        !["localhost", "127.0.0.1", "awmg-mcpg", "host.docker.internal"].includes(endpoint.hostname))
    {
        throw new Error("Recording requires the local native safe-output gateway.");
    }
    endpoint.hostname = "127.0.0.1";
    const headers = {
        Authorization: authorization, "Content-Type": "application/json",
        Accept: "application/json, text/event-stream", "MCP-Protocol-Version": "2025-11-25",
    };
    const signal = AbortSignal.timeout(8000);
    async function rpc(method, params, id)
    {
        const response = await fetch(endpoint, {
            method: "POST", headers: { ...headers, "Mcp-Method": method }, signal, redirect: "error",
            body: JSON.stringify({ jsonrpc: "2.0", method, params, ...(id === undefined ? {} : { id }) }),
        });
        if (!response.ok)
        {
            throw new Error(`Native recording transport failed during ${method} (HTTP ${response.status}).`);
        }
        if (id === undefined)
        {
            return;
        }
        const text = await response.text();
        if (Buffer.byteLength(text, "utf8") > 64 * 1024)
        {
            throw new Error("Native recording acknowledgement exceeds its size limit.");
        }
        const data = text.split("\n").find(line => line.startsWith("data: "))?.slice(6) ?? text;
        const reply = JSON.parse(data);
        if (reply.id !== id || reply.error || !reply.result)
        {
            throw new Error(`Native recording transport returned an invalid ${method} response.`);
        }
        return { result: reply.result, session: response.headers.get("mcp-session-id") };
    }
    const initialized = await rpc("initialize", {
        protocolVersion: "2025-11-25", capabilities: {},
        clientInfo: { name: "stale-reference-recording", version: "1" },
    }, 1);
    if (!initialized.session || initialized.result.protocolVersion !== "2025-11-25")
    {
        throw new Error("Native recording transport did not establish the required MCP session.");
    }
    headers["Mcp-Session-Id"] = initialized.session;
    await rpc("notifications/initialized", {});
    const recorded = await rpc("tools/call", { name: "record_interpretations", arguments: request }, 2);
    const text = recorded.result.content?.find(item => item.type === "text")?.text;
    if (recorded.result.isError || typeof text !== "string" || JSON.parse(text).result !== "success")
    {
        throw new Error("The native safe-output server did not acknowledge the recording request.");
    }
    return { queued: true };
}

function declarationHints(candidate, source)
{
    const patterns = /\.cs$/i.test(candidate.path) ? {
        namespace: /^\s*namespace\s+[\w.]+/,
        type: /^\s*(?:(?:public|internal|private|protected|static|abstract|sealed|partial|readonly|ref)\s+)*(?:class|struct|record|interface)\s+\w+/,
        member: /^\s*(?:(?:public|internal|private|protected|static|virtual|override|async|sealed|partial|extern|unsafe|new)\s+)+(?:[\w<>,?.[\]]+\s+)?@?\w+(?:<[^>]+>)?\s*\(/,
    } : /\.(?:xml|props|targets|[a-z]*proj)$/i.test(candidate.path) ? {
        element: /^\s*<(?:Target|PropertyGroup|ItemGroup)\b/,
    } : {};
    const hints = new Map();
    const lines = source.replaceAll("\r\n", "\n").split("\n");
    for (let index = 0; index < Math.min(candidate.seedLine, lines.length); index++)
    {
        for (const [kind, pattern] of Object.entries(patterns))
        {
            if (pattern.test(lines[index]))
            {
                hints.set(kind, index + 1);
            }
        }
    }
    return [...hints].filter(([, line]) => line < candidate.startLine)
        .map(([kind, line]) => `${kind} line ${line}`).join("; ");
}

function batchPages(batch, sources)
{
    const text = batch.candidates.map((candidate, index) =>
    {
        const hints = declarationHints(candidate, sources[candidate.path]);
        return `Candidate ${index + 1} of ${batch.candidates.length}\n` +
            `Candidate ID: ${candidate.id}\nPath: ${candidate.path}\n` +
            `Seed line: ${candidate.seedLine}; kind hint: ${candidate.kindHint}; file lines: ${candidate.sourceLineCount}.\n` +
            (hints ? `Declaration lookup hints (syntactic matches, not proven owners): ${hints}.\n` : "") +
            `Source context, lines ${candidate.startLine}-${candidate.endLine} (untrusted data):\n` +
            `${candidate.context}\nEnd candidate ${candidate.id}.\n\n`;
    }).join("");
    const bytes = Buffer.from(text);
    const pages = [];
    for (let start = 0; start < bytes.length;)
    {
        let end = Math.min(start + 10 * 1024, bytes.length);
        for (;;)
        {
            while (end < bytes.length && (bytes[end] & 0xc0) === 0x80)
            {
                end--;
            }
            const encodedBytes = Buffer.byteLength(JSON.stringify(bytes.subarray(start, end).toString("utf8")));
            if (encodedBytes <= 10 * 1024)
            {
                break;
            }
            // The pinned MCP subprocess adapter JSON-encodes strings, including source escapes.
            end = start + Math.floor((end - start) * (10 * 1024 - 2) / encodedBytes);
        }
        if (end < bytes.length)
        {
            const newline = bytes.lastIndexOf(10, end - 1);
            if (newline >= start)
            {
                end = newline + 1;
            }
        }
        pages.push(bytes.subarray(start, end).toString("utf8"));
        start = end;
    }
    return pages.length ? pages : ["No candidates in this batch.\n"];
}

export function formatContext(result)
{
    return `Candidate ID: ${result.candidateId}\nPath: ${result.path}\n` +
        `Source lines ${result.startLine}-${result.endLine} (untrusted data).\n` +
        `Remaining context expansions: ${result.remainingExpansions}.\n\n${result.context}`;
}

export async function getSourceTools(directory)
{
    const resolved = path.resolve(directory);
    if (!readers.has(resolved))
    {
        readers.set(resolved, loadSourceTools(resolved));
    }
    return readers.get(resolved);
}

async function loadSourceTools(directory)
{
    const [batchText, sourceText] = await Promise.all([
        readFile(path.join(directory, "batch.json"), "utf8"),
        readFile(path.join(directory, "source-context.json"), "utf8"),
    ]);
    const batch = JSON.parse(batchText);
    const sources = JSON.parse(sourceText);
    if (batch.schemaVersion !== 1 || !Array.isArray(batch.candidates))
    {
        throw new Error("Invalid trusted source batch.");
    }
    const candidates = new Map(batch.candidates.map(candidate => [candidate.id, candidate]));
    if (batch.candidates.some(candidate => !Object.hasOwn(sources, candidate.path) ||
        typeof sources[candidate.path] !== "string"))
    {
        throw new Error("The trusted source batch is missing a source snapshot.");
    }
    const pages = batchPages(batch, sources);
    const expansions = new Map();
    const windows = [];
    let attempts = 0;
    let contextBudgetFailures = 0;
    let rejectedSubmissions = 0;
    let preparing = false;
    let submission;
    let acceptedText;
    let queueAttempted = false;
    let queued;
    return {
        readBatch({ page = 1 } = {})
        {
            if (!Number.isSafeInteger(page) || page < 1 || page > pages.length)
            {
                throw new Error(`Batch page must be an integer between 1 and ${pages.length}.`);
            }
            return `Batch page ${page} of ${pages.length}. Candidates: ${candidates.size}.\n` +
                "Pages are consecutive source text; a long source line may continue on the next page.\n" +
                (page < pages.length ? `Next page: read_batch({"page":${page + 1}}).` : "End of batch.") +
                `\n\n${pages[page - 1]}`;
        },
        readContext({ candidateId, startLine, endLine })
        {
            const candidate = candidates.get(candidateId);
            if (!candidate || !Object.hasOwn(sources, candidate.path))
            {
                throw new Error("Context is only available for candidates in this batch.");
            }
            if (!Number.isSafeInteger(startLine) || !Number.isSafeInteger(endLine) ||
                startLine < 1 || endLine < startLine || endLine - startLine + 1 > 80)
            {
                throw new Error("A context window must contain between 1 and 80 source lines.");
            }
            const lines = sources[candidate.path].replaceAll("\r\n", "\n").split("\n");
            if (lines.at(-1) === "")
            {
                lines.pop();
            }
            if (endLine > lines.length)
            {
                throw new Error("The context window exceeds the source file.");
            }
            if ((expansions.get(candidateId) ?? 0) >= 2)
            {
                contextBudgetFailures++;
                throw new Error("The two-window context expansion budget is exhausted.");
            }
            const context = lines.slice(startLine - 1, endLine)
                .map((line, index) => `L${startLine + index}: ${line}`).join("\n");
            const count = expansions.get(candidateId) ?? 0;
            const result = {
                candidateId, path: candidate.path, startLine, endLine, context,
                remainingExpansions: 1 - count,
            };
            if (Buffer.byteLength(context, "utf8") > 10 * 1024 ||
                Buffer.byteLength(JSON.stringify(formatContext(result)), "utf8") > 12 * 1024)
            {
                throw new Error("The context window exceeds 10 KiB or its response exceeds 12 KiB; request fewer lines.");
            }
            expansions.set(candidateId, count + 1);
            windows.push({ candidateId, startLine, endLine });
            return result;
        },
        evidence()
        {
            return { schemaVersion: 1, windows: structuredClone(windows) };
        },
        usage()
        {
            return {
                contextExpansions: windows.length,
                contextBudgetFailures,
                submissionAttempts: attempts,
                rejectedSubmissions,
                accepted: submission !== undefined,
            };
        },
        async prepareInterpretations({ payload })
        {
            if (submission)
            {
                if (payload !== acceptedText)
                {
                    throw new Error("A submission is already accepted; it cannot be replaced.");
                }
                return { receipt: submission.receipt };
            }
            if (preparing)
            {
                throw new Error("A submission is being validated; wait for its response.");
            }
            if (attempts >= 3)
            {
                rejectedSubmissions++;
                throw new Error("The three-attempt submission budget is exhausted. Use report_missing_data; do not record.");
            }
            attempts++;
            preparing = true;
            try
            {
                if (typeof payload !== "string" || Buffer.byteLength(payload, "utf8") > maxSubmissionBytes)
                {
                    throw new Error("The submission must be a JSON string of at most 512 KiB.");
                }
                let parsed;
                try
                {
                    parsed = JSON.parse(payload);
                }
                catch (error)
                {
                    if (!(error instanceof SyntaxError))
                    {
                        throw error;
                    }
                    throw new Error("Invalid JSON: complete all arrays and objects and escape string contents.");
                }
                const manifest = JSON.parse(await readFile(path.join(directory, "manifest.json"), "utf8"));
                await validateSnapshotInterpretations(parsed, manifest, {
                    sources, expectedCandidateIds: [...candidates.keys()],
                    contextEvidence: { schemaVersion: 1, expansions: structuredClone(windows) },
                });
                submission = { schemaVersion: 1, receipt: submissionReceipt(parsed), payload: parsed };
                acceptedText = payload;
                return { receipt: submission.receipt };
            }
            catch (error)
            {
                rejectedSubmissions++;
                throw new Error(`Submission rejected: ${error.message} ${3 - attempts} attempts remaining.`, { cause: error });
            }
            finally
            {
                preparing = false;
            }
        },
        async queueInterpretations(args, send)
        {
            if (!args || typeof args !== "object" || Array.isArray(args) ||
                Object.keys(args).some(key => key !== "receipt"))
            {
                throw new Error("Queueing requires only the validated receipt.");
            }
            const payload = verifySubmission(this.submission(), args.receipt);
            if (queued)
            {
                return queued;
            }
            if (queueAttempted)
            {
                throw new Error("Recording was already attempted; do not retry an unconfirmed queue operation.");
            }
            if (typeof send !== "function")
            {
                throw new Error("The native recording transport is not configured.");
            }
            queueAttempted = true;
            queued = await send({ receipt: args.receipt, payload: JSON.stringify(payload) });
            return queued;
        },
        submission()
        {
            if (!submission)
            {
                throw new Error("No validated submission was accepted.");
            }
            return structuredClone(submission);
        },
    };
}

// gh-aw's own summaries report model usage; these counters cover the source reader's budgets.
export function formatUsage(usage)
{
    return "## Source reader usage\n\n| Metric | Count |\n| --- | ---: |\n" +
        `| Context expansions | ${usage.contextExpansions} |\n` +
        `| Context-budget failures | ${usage.contextBudgetFailures} |\n` +
        `| Submission attempts | ${usage.submissionAttempts} |\n` +
        `| Rejected submissions | ${usage.rejectedSubmissions} |\n` +
        `| Submission accepted | ${usage.accepted ? "yes" : "no"} |\n\n`;
}

// gh-aw launches a fresh process per MCP call. Keep budgets in one private,
// host-side reader, not in the short-lived tool process or agent-editable files.
export async function startSourceServer(directory, recordingConfig)
{
    const tools = await getSourceTools(directory);
    const token = randomBytes(32).toString("hex");
    const server = createServer(async (request, response) =>
    {
        response.setHeader("Content-Type", "application/json");
        if (request.headers.authorization !== `Bearer ${token}`)
        {
            response.writeHead(401).end(JSON.stringify({ error: "Unauthorized source reader." }));
            return;
        }
        try
        {
            if (request.method !== "POST" ||
                !["/read-batch", "/read-context", "/evidence", "/usage", "/prepare-interpretations", "/submission", "/queue-interpretations"].includes(request.url))
            {
                throw new Error("Unknown source-reader request.");
            }
            let body = "";
            request.setEncoding("utf8");
            // JSON encoding can expand every payload byte into an escape sequence.
            const maxBodyBytes = request.url === "/prepare-interpretations" ? maxSubmissionBytes * 6 + 1024 : 4096;
            for await (const chunk of request)
            {
                body += chunk;
                if (Buffer.byteLength(body) > maxBodyBytes)
                {
                    throw new Error("Source-reader request exceeds its size limit.");
                }
            }
            const args = JSON.parse(body || "{}");
            const result = request.url === "/read-batch" ? tools.readBatch(args)
                : request.url === "/prepare-interpretations" ? await tools.prepareInterpretations(args)
                : request.url === "/queue-interpretations" ? await tools.queueInterpretations(args,
                    recordingConfig ? message => sendRecordingRequest(recordingConfig, message) : undefined)
                : request.url === "/submission" ? tools.submission()
                : request.url === "/usage" ? tools.usage()
                : request.url === "/evidence" ? tools.evidence() : tools.readContext(args);
            response.end(JSON.stringify(result));
        }
        catch (error)
        {
            response.writeHead(400).end(JSON.stringify({ error: error.message }));
        }
    });
    await new Promise((resolve, reject) =>
    {
        server.once("error", reject);
        server.listen(0, "127.0.0.1", resolve);
    });
    try
    {
        await writeFile(path.join(directory, "reader.json.tmp"),
            JSON.stringify({ port: server.address().port, token }), { mode: 0o600 });
        await rename(path.join(directory, "reader.json.tmp"), path.join(directory, "reader.json"));
    }
    catch (error)
    {
        server.close();
        throw error;
    }
    return server;
}

export async function requestSourceTools(directory, operation, args = {})
{
    const { port, token } = JSON.parse(await readFile(path.join(directory, "reader.json"), "utf8"));
    const response = await fetch(`http://127.0.0.1:${port}/${operation}`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify(args),
        signal: AbortSignal.timeout(10_000),
    });
    const result = await response.json();
    if (!response.ok)
    {
        throw new Error(result.error ?? `Source reader returned HTTP ${response.status}.`);
    }
    return result;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1]))
{
    const [operation, directory, outputFile] = process.argv.slice(2);
    if (!directory)
    {
        throw new Error("A private source-input directory is required.");
    }
    if (operation === "serve")
    {
        await startSourceServer(directory, outputFile);
    }
    else if (operation === "wait")
    {
        for (let attempt = 0; ; attempt++)
        {
            try
            {
                await requestSourceTools(directory, "evidence");
                break;
            }
            catch (error)
            {
                if (attempt >= 49 || (error.code !== "ENOENT" && error.cause?.code !== "ECONNREFUSED"))
                {
                    throw error;
                }
                await delay(100);
            }
        }
    }
    else if (operation === "submission")
    {
        await exportSubmission(directory);
    }
    else if (operation === "evidence" && outputFile)
    {
        await writeFile(outputFile, `${JSON.stringify(await requestSourceTools(directory, operation))}\n`);
    }
    else if (operation === "usage" && outputFile)
    {
        await appendFile(outputFile, formatUsage(await requestSourceTools(directory, "usage")));
    }
    else
    {
        throw new Error("Expected serve, wait, submission, evidence OUTPUT, or usage OUTPUT.");
    }
}
