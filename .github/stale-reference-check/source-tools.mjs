import { createHash, randomBytes } from "node:crypto";
import { readFile, rename, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";
import { validateSnapshotInterpretations } from "./interpretations.mjs";

const readers = new Map();
const maxSubmissionBytes = 512 * 1024;

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

// Expand the receipt in the trusted post-step, before the framework uploads the
// agent artifact. Threat detection must inspect the full payload, not just its hash.
export async function completeSubmission(directory, outputFile)
{
    const submission = await requestSourceTools(directory, "submission");
    const output = JSON.parse(await readFile(outputFile, "utf8"));
    const items = Array.isArray(output?.items) ? output.items.filter(item => item?.type === "record_interpretations") : null;
    if (!Array.isArray(items) || items.length !== 1 ||
        Object.keys(items[0]).some(key => key !== "type" && key !== "receipt"))
    {
        throw new Error("Expected exactly one unexpanded record_interpretations receipt.");
    }
    items[0].payload = verifySubmission(submission, items[0].receipt);
    await writeFile(path.join(directory, "submission.json"), `${JSON.stringify(submission)}\n`);
    await writeFile(outputFile, `${JSON.stringify(output)}\n`);
}

function declarationHints(candidate, source)
{
    const patterns = /\.(cs|vb)$/i.test(candidate.path) ? {
        namespace: /^\s*namespace\s+[\w.]+/i,
        type: /^\s*(?:(?:public|internal|private|protected|static|abstract|sealed|partial|readonly|ref|friend|mustinherit|notinheritable)\s+)*(?:class|struct|record|interface|module)\s+\w+/i,
        member: /^\s*(?:(?:public|internal|private|protected|static|virtual|override|async|sealed|partial|extern|unsafe|new|shared|overrides)\s+)+(?:[\w<>,?.[\]]+\s+)?@?\w+(?:<[^>]+>)?\s*\(/i,
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
    let preparing = false;
    let submission;
    let acceptedText;
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
                throw new Error("The three-attempt submission budget is exhausted. Report missing_data; do not record.");
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
                throw new Error(`Submission rejected: ${error.message} ${3 - attempts} attempts remaining.`, { cause: error });
            }
            finally
            {
                preparing = false;
            }
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

// gh-aw launches a fresh process per MCP call. Keep budgets in one private,
// host-side reader, not in the short-lived tool process or agent-editable files.
export async function startSourceServer(directory)
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
                !["/read-batch", "/read-context", "/evidence", "/prepare-interpretations", "/submission"].includes(request.url))
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
                : request.url === "/submission" ? tools.submission()
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
        await startSourceServer(directory);
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
    else if (operation === "complete" && outputFile)
    {
        await completeSubmission(directory, outputFile);
    }
    else if (operation === "evidence" && outputFile)
    {
        await writeFile(outputFile, `${JSON.stringify(await requestSourceTools(directory, operation))}\n`);
    }
    else
    {
        throw new Error("Expected serve, wait, evidence, or complete with an output path.");
    }
}
