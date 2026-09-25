// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { formatContext, getSourceTools } from "../source-tools.mjs";

async function reader(t, { context, lines, count = 1, startLine = 1, seedLine = startLine } = {})
{
    const directory = await mkdtemp(path.join(os.tmpdir(), "stale-reference-pages-"));
    t.after(() => rm(directory, { recursive: true, force: true }));
    lines ??= ["// TODO: https://github.com/dotnet/sdk/issues/123", "public void Example() {}"];
    context ??= lines.slice(startLine - 1).map((line, index) => `${startLine + index}: ${line}`).join("\n");
    const candidates = Array.from({ length: count }, (_, index) => ({
        id: String(index).padStart(64, "0"),
        path: "src/Example.cs",
        kindHint: "todo",
        seedLine,
        startLine,
        endLine: lines.length,
        sourceLineCount: lines.length,
        context,
    }));
    const batch = { schemaVersion: 1, candidates };
    await writeFile(path.join(directory, "batch.json"), JSON.stringify(batch));
    await writeFile(path.join(directory, "source-context.json"), JSON.stringify({ "src/Example.cs": lines.join("\n") }));
    return { directory, candidates, tools: await getSourceTools(directory) };
}

function readPages(tools)
{
    const first = tools.readBatch();
    assert.equal(typeof first, "string");
    const count = Number(first.match(/^Batch page 1 of (\d+)\./)?.[1]);
    assert.ok(count >= 1);
    return Array.from({ length: count }, (_, index) => tools.readBatch({ page: index + 1 }));
}

const contents = pages => pages.map(page => page.slice(page.indexOf("\n\n") + 2)).join("");

test("batch pages render real source lines without JSON escaping and retain each candidate's context", async t =>
{
    const { tools, candidates, directory } = await reader(t, { count: 2 });
    const text = contents(readPages(tools));
    for (const candidate of candidates)
    {
        assert.ok(text.includes(`Candidate ID: ${candidate.id}`));
    }
    assert.equal(text.split(candidates[0].context).length - 1, 2);
    assert.ok(text.includes("\n1: // TODO:"));
    assert.doesNotMatch(text, /\\n2:/);
    assert.deepEqual(JSON.parse(await readFile(path.join(directory, "batch.json"))).candidates, candidates);
    assert.deepEqual(tools.evidence(), { schemaVersion: 1, windows: [] });
});

test("large batches and individual long Unicode lines are fully paginated below the tool output limit", async t =>
{
    const lines = Array.from({ length: 8 }, (_, i) => `// ${i} ${"\u03bb".repeat(5000)}`);
    const { tools, candidates } = await reader(t, { lines });
    const pages = readPages(tools);
    assert.ok(pages.length > 1);
    for (const page of pages)
    {
        assert.ok(Buffer.byteLength(page, "utf8") <= 12 * 1024);
        assert.ok(Buffer.byteLength(JSON.stringify(page), "utf8") <= 12 * 1024);
        assert.doesNotMatch(page, /\uFFFD/);
    }
    assert.ok(contents(pages).includes(candidates[0].context));
    assert.match(pages[0], /Next page: read_batch\(\{"page":2\}\)/);
    assert.match(pages.at(-1), /End of batch/);
    assert.equal(tools.readBatch({ page: 1 }), pages[0]);
});

test("page requests reject invalid cursors rather than skipping or truncating candidates", async t =>
{
    const { tools } = await reader(t);
    for (const page of [0, -1, 1.5, "1", null, Infinity, 100000])
    {
        assert.throws(() => tools.readBatch({ page }), /page/i);
    }
});

test("page bounds include MCP JSON encoding for escape-heavy source without losing bytes", async t =>
{
    const lines = ['"\\\t\u0001'.repeat(6000), "// " + "\u{1F600}".repeat(4000)];
    const { tools, candidates } = await reader(t, { lines });
    const pages = readPages(tools);
    for (const page of pages)
    {
        const wireText = JSON.stringify(page);
        assert.ok(Buffer.byteLength(wireText, "utf8") <= 12 * 1024);
        assert.equal(JSON.parse(wireText), page);
        assert.doesNotMatch(page, /\uFFFD/);
    }
    assert.ok(contents(pages).includes(candidates[0].context));
});

test("declaration lookup hints point to source locations without spending expansion evidence", async t =>
{
    const lines = [
        "namespace Example;",
        "public class Sample",
        "{",
        "    public async Task Work()",
        "    {",
        ...Array(90).fill("        // implementation"),
        "        // TODO: https://github.com/dotnet/sdk/issues/123",
        "    }",
        "}",
    ];
    const { tools } = await reader(t, { lines, startLine: 90, seedLine: 96 });
    const text = contents(readPages(tools));
    assert.match(text, /namespace line 1/);
    assert.match(text, /type line 2/);
    assert.match(text, /member line 4/);
    assert.match(text, /not proven owners/);
    assert.doesNotMatch(text, /1: namespace Example/);
    assert.deepEqual(tools.evidence(), { schemaVersion: 1, windows: [] });
});

test("oversized expansions are rejected before spending the two-window allowance", async t =>
{
    const { tools, candidates } = await reader(t, { lines: ["x".repeat(12 * 1024), "// short"] });
    const candidateId = candidates[0].id;
    assert.throws(() => tools.readContext({ candidateId, startLine: 1, endLine: 1 }), /10 KiB/);
    assert.equal(tools.evidence().windows.length, 0);
    tools.readContext({ candidateId, startLine: 2, endLine: 2 });
    tools.readContext({ candidateId, startLine: 2, endLine: 2 });
    assert.throws(() => tools.readContext({ candidateId, startLine: 2, endLine: 2 }), /budget is exhausted/);
});

test("context rendering preserves line breaks, identifies the candidate, and shows remaining allowance", async t =>
{
    const { tools, candidates } = await reader(t);
    const request = { candidateId: candidates[0].id, startLine: 1, endLine: 2 };
    const first = formatContext(tools.readContext(request));
    assert.ok(first.includes(candidates[0].id));
    assert.match(first, /Remaining context expansions: 1/);
    assert.match(first, /\nL1: \/\/ TODO:.*\nL2: public void Example/);
    assert.match(formatContext(tools.readContext(request)), /Remaining context expansions: 0/);
});

test("context response bounds include MCP encoding and a rejected window does not consume a receipt", async t =>
{
    const { tools, candidates } = await reader(t, { lines: ["\t".repeat(7000), "// short"] });
    const candidateId = candidates[0].id;
    assert.throws(() => tools.readContext({ candidateId, startLine: 1, endLine: 1 }), /12 KiB/);
    assert.equal(tools.evidence().windows.length, 0);
    const result = tools.readContext({ candidateId, startLine: 2, endLine: 2 });
    assert.equal(result.remainingExpansions, 1);
});

test("empty batches remain readable and missing source snapshots fail explicitly", async t =>
{
    const { tools } = await reader(t, { count: 0 });
    assert.match(tools.readBatch(), /Candidates: 0[\s\S]*No candidates/);
    const { directory } = await reader(t);
    const changedDirectory = path.join(directory, "incomplete");
    await mkdir(changedDirectory);
    await writeFile(path.join(changedDirectory, "batch.json"), await readFile(path.join(directory, "batch.json")));
    await writeFile(path.join(changedDirectory, "source-context.json"), "{}");
    await assert.rejects(getSourceTools(changedDirectory), /missing a source snapshot/);
});
