// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from 'node:assert/strict';
import { mkdtemp, mkdir, rm, symlink, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { collect, git } from '../collect.mjs';
import { getCachedResults, mergeCache, readContext, selectBatch, validateInterpretations } from '../interpretations.mjs';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const url = 'https://github.com/dotnet/sdk/issues/123';
const otherUrl = 'https://github.com/dotnet/runtime/pull/456';

async function fixture(t, source, filePath = 'test/Cases.cs') {
    const root = await mkdtemp(path.join(testDirectory, '.interpretations-'));
    t.after(() => rm(root, { recursive: true, force: true }));
    await git(root, ['init', '--quiet']);
    await git(root, ['config', 'core.autocrlf', 'false']);
    const file = path.join(root, ...filePath.split('/'));
    await mkdir(path.dirname(file), { recursive: true });
    await writeFile(file, source);
    await git(root, ['add', '.']);
    await git(root, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--quiet', '-m', 'Fixture']);
    return { root, file, manifest: await collect(root, { rulesHash: 'rules-1' }) };
}

function result(candidate, actions = [], status = actions.length ? 'actionable' : 'irrelevant') {
    return { candidateId: candidate.id, status, reason: 'Source interpretation.', actions };
}

function action(candidate, overrides = {}) {
    return {
        kind: candidate.kindHint, anchor: 'Sample.Tests.Cases.Run', startLine: candidate.seedLine,
        endLine: candidate.seedLine, urls: [url],
        ...(candidate.kindHint === 'ignore' ? { testNames: ['Sample.Tests.Cases.Run'] } : {}),
        ...overrides,
    };
}

function validate(f, results, expectedCandidateIds = f.manifest.candidates.map(candidate => candidate.id)) {
    return validateInterpretations({ schemaVersion: 1, results }, f.manifest, { repoRoot: f.root, expectedCandidateIds });
}

test('validates exact source spans, multiline Ignore and separate adjacent actions', async t => {
    const f = await fixture(t, [
        'namespace Sample.Tests;',
        'class Cases {',
        '[Ignore(',
        `"${url}")]`,
        '[TestMethod]',
        'public void Run() {}',
        '// TODO expand platform support',
        `// ${otherUrl}`,
        '}',
    ].join('\n'));
    const [ignore, todo] = f.manifest.candidates;
    const results = await validate(f, [
        result(ignore, [action(ignore, { endLine: 4 })]),
        result(todo, [action(todo, { urls: [otherUrl], anchor: 'Sample.Tests.Cases' })]),
    ]);
    assert.equal(results[0].actions[0].sourceExcerpt, `[Ignore(\n"${url}")]`);
    assert.equal(results[0].actions[0].path, 'test/Cases.cs');
    assert.equal(results[0].actions[0].candidateId, ignore.id);
    assert.deepEqual(results[0].actions[0].urls, [url]);
    assert.deepEqual(results[1].actions[0].urls, [otherUrl]);
    assert.deepEqual(results[1].actions[0].testNames, []);
});

test('accepts XML workaround conditions and historical irrelevant classifications', async t => {
    const f = await fixture(t, `<Project>\n<!-- Workaround until the fix is consumed: ${url} -->\n</Project>`, 'src/Build.targets');
    const candidate = f.manifest.candidates[0];
    const results = await validate(f, [result(candidate, [action(candidate, { anchor: 'Project', additionalConditions: ['Consume the fixed package.'] })])]);
    assert.deepEqual(results[0].actions[0].additionalConditions, ['Consume the fixed package.']);
    assert.equal((await validate(f, [result(candidate)]))[0].status, 'irrelevant');
});

test('rejects supporting code links even alongside a valid issue blocker', async t => {
    const codeUrl = 'https://github.com/dotnet/msbuild/blob/main/src/Build/Construction/Solution/SolutionProjectGenerator.cs#L659-L672';
    const f = await fixture(t, `// TODO replace duplicated logic when the API is public: ${url}\n// Current implementation: ${codeUrl}\n`);
    const candidate = f.manifest.candidates[0];
    await assert.rejects(validate(f, [result(candidate, [action(candidate, { urls: [url, codeUrl] })])]),
        /Action URL must identify a public GitHub issue or pull request/);
    const validated = await validate(f, [result(candidate, [action(candidate)])]);
    assert.deepEqual(validated[0].actions[0].urls, [url]);
});

test('requires bounded supplied namespace context for nested parameterized test names', async t => {
    const lines = [
        'namespace Sample.Tests;',
        'class Outer {',
        'class Inner {',
        ...Array(90).fill('// spacer'),
        `[Ignore("${url}")]`,
        '[TestMethod]',
        '[DataRow(1)]',
        'public void Run(int value) {}',
        '}', '}',
    ];
    const f = await fixture(t, lines.join('\n'));
    const candidate = f.manifest.candidates[0];
    const raw = result(candidate, [action(candidate, { testNames: ['Sample.Tests.Outer.Inner.Run(System.Int32)'] })]);
    await assert.rejects(validate(f, [raw]), /declaration|namespace/);
    const expansion = await readContext(f.root, f.manifest, { candidateId: candidate.id, startLine: 1, endLine: 3 });
    candidate.contextExpansions = [expansion];
    const validated = await validate(f, [raw]);
    assert.equal(validated[0].actions[0].testNames[0], 'Sample.Tests.Outer.Inner.Run(System.Int32)');
    assert.deepEqual(validated[0].contextExpansions, [expansion]);
    const cache = mergeCache(null, f.manifest, validated, { nextCursor: 0 });
    const nextRun = await collect(f.root, { rulesHash: 'rules-1' });
    assert.equal(getCachedResults(cache, nextRun).length, 1);
    assert.equal(selectBatch(nextRun, cache).batch.candidates.length, 0);
    const cached = getCachedResults(JSON.parse(JSON.stringify(cache)), nextRun);
    assert.deepEqual(await validateInterpretations({ schemaVersion: 1, results: cached }, nextRun,
        { repoRoot: f.root, expectedCandidateIds: [candidate.id] }), validated);
    await assert.rejects(validateInterpretations({ schemaVersion: 1, results: JSON.parse(JSON.stringify(cached)) }, nextRun,
        { repoRoot: f.root, expectedCandidateIds: [candidate.id] }), /unknown field/);
});

test('class Ignore requires complete coverage and ambiguous inheritance is deferred', async t => {
    const f = await fixture(t, [
        'namespace Sample.Tests;',
        `[Ignore("${url}")]`,
        'class Cases {',
        '[TestMethod]',
        'public void First() {}',
        '[TestMethod]',
        'public void Second() {}',
        '}',
    ].join('\n'));
    const candidate = f.manifest.candidates[0];
    const raw = result(candidate, [action(candidate, { testNames: ['Sample.Tests.Cases.First', 'Sample.Tests.Cases.Second'] })]);
    assert.equal((await validate(f, [raw]))[0].actions[0].testNames.length, 2);
    raw.actions[0].testNames.pop();
    await assert.rejects(validate(f, [raw]), /every affected test/);
    raw.actions[0].endLine = 8;
    await assert.rejects(validate(f, [raw]), /every affected test/);
    const ambiguous = await fixture(t, `namespace Sample.Tests;\n[Ignore("${url}")]\nclass Cases : Base {\n[TestMethod]\npublic void Run() {}\n}`);
    await assert.rejects(validate(ambiguous, [result(ambiguous.manifest.candidates[0], [action(ambiguous.manifest.candidates[0])])]), /ambiguous/);
    const deferred = await validate(ambiguous, [result(ambiguous.manifest.candidates[0], [], 'insufficient_context')]);
    assert.equal(Object.keys(mergeCache(null, ambiguous.manifest, deferred, { nextCursor: 0 }).entries).length, 0);
});

test('rejects incomplete IDs, duplicates, unknown schemas, extra fields and impossible actions', async t => {
    const f = await fixture(t, `// TODO ${url}\n// TODO ${otherUrl}\n`);
    const [first, second] = f.manifest.candidates;
    const valid = [result(first, [action(first)]), result(second)];
    await validate(f, valid);
    await assert.rejects(validate(f, valid.slice(0, 1)), /exactly/);
    await assert.rejects(validate(f, [valid[0], valid[0]]), /duplicate/);
    await assert.rejects(validate(f, [{ ...valid[0], candidateId: 'invented' }, valid[1]]), /Unknown/);
    await assert.rejects(validateInterpretations({ schemaVersion: 2, results: valid }, f.manifest, { repoRoot: f.root, expectedCandidateIds: [first.id, second.id] }), /schemaVersion/);
    for (const patch of [
        { path: 'other/file.cs' }, { labels: ['cookie'] }, { targetRepo: 'evil/repo' },
        { hash: 'agent-value' }, { sourceExcerpt: 'invented' }, { anchor: '' },
        { urls: ['https://github.com/dotnet/sdk/issues/12'] },
        { urls: ['https://github.com/dotnet/sdk/issues/999'] },
        { urls: ['https://example.com/dotnet/sdk/issues/123'] },
        { urls: [] }, { startLine: second.seedLine, endLine: second.seedLine },
        { startLine: 0 }, { endLine: 500 }, { kind: 'ignore' }, { testNames: ['Invented.Test'] },
    ]) {
        await assert.rejects(validate(f, [result(first, [action(first, patch)]), result(second)]));
    }
    await assert.rejects(validate(f, [{ ...valid[0], status: 'irrelevant' }, valid[1]]), /must not contain actions/);
    await assert.rejects(validate(f, [result(first, [], 'actionable'), valid[1]]), /require actions/);
    await assert.rejects(validate(f, [result(first, [action(first), action(first)]), valid[1]]), /Duplicate actions/);
    await assert.rejects(validate(f, valid, [first.id, first.id]), /unique/);
});

test('rejects non-qualified, invented and wrong following test identities', async t => {
    const f = await fixture(t, `namespace Sample.Tests;\nclass Cases {\n[Ignore("${url}")]\n[TestMethod]\npublic void Run() {}\npublic void Other() {}\n}`);
    const candidate = f.manifest.candidates[0];
    for (const name of ['Run', 'Cases.Run', 'Fake.Namespace.Cases.Run', 'Sample.Tests.Cases.Missing', 'Sample.Tests.Cases.Other']) {
        await assert.rejects(validate(f, [result(candidate, [action(candidate, { testNames: [name] })])]));
    }
});

test('source edits outside snippets, forged evidence, traversal and sibling prefixes are rejected', async t => {
    const f = await fixture(t, [`// TODO ${url}`, ...Array(100).fill('// spacer')].join('\n'));
    const candidate = f.manifest.candidates[0];
    const raw = result(candidate, [action(candidate)]);
    candidate.context = candidate.context.replace('spacer', 'forged');
    await assert.rejects(validate(f, [raw]), /evidence does not match/);
    f.manifest = await collect(f.root, { rulesHash: 'rules-1' });
    await writeFile(f.file, [`// TODO ${url}`, ...Array(99).fill('// spacer'), '// changed'].join('\n'));
    await assert.rejects(validate(f, [raw]), /blob mismatch/);
    for (const badPath of ['../sibling/Cases.cs', '/absolute.cs', 'C:/absolute.cs', 'test\\Cases.cs', 'test/../Cases.cs']) {
        const forged = structuredClone(f.manifest);
        forged.candidates[0].path = badPath;
        await assert.rejects(validateInterpretations({ schemaVersion: 1, results: [raw] }, forged, { repoRoot: f.root, expectedCandidateIds: [candidate.id] }), /path/);
    }
});

test('a symlink to a sibling whose name shares the repository prefix cannot escape', async t => {
    const f = await fixture(t, `// TODO ${url}`);
    const sibling = `${f.root}-sibling`;
    await mkdir(sibling);
    t.after(() => rm(sibling, { recursive: true, force: true }));
    const outside = path.join(sibling, 'Cases.cs');
    await writeFile(outside, `// TODO ${url}`);
    await rm(f.file);
    try {
        await symlink(outside, f.file, 'file');
    } catch (error) {
        if (error.code === 'EPERM') {
            t.skip('Windows account cannot create file symlinks.');
            return;
        }
        throw error;
    }
    await assert.rejects(validate(f, [result(f.manifest.candidates[0])]), /escapes|symlink/);
});

test('context reads are same-file, at most eighty lines, and verify the blob', async t => {
    const f = await fixture(t, [`// TODO ${url}`, ...Array(150).fill('// spacer')].join('\n'));
    const candidate = f.manifest.candidates[0];
    const read = await readContext(f.root, f.manifest, { candidateId: candidate.id, startLine: 1, endLine: 80 });
    assert.equal(read.context.split('\n').length, 80);
    assert.equal(read.path, candidate.path);
    for (const range of [{ startLine: 1, endLine: 81 }, { startLine: 0, endLine: 5 }, { startLine: 150, endLine: 152 }, { startLine: 5, endLine: 4 }]) {
        await assert.rejects(readContext(f.root, f.manifest, { candidateId: candidate.id, ...range }), /range/);
    }
    await assert.rejects(readContext(f.root, f.manifest, { candidateId: 'unknown', startLine: 1, endLine: 2 }), /Unknown/);
    candidate.contextExpansions = [read, read, read];
    await assert.rejects(validate(f, [result(candidate)]), /two context/);
    const huge = await fixture(t, `// TODO ${url}\n// ${'x'.repeat(65536)}`);
    await assert.rejects(readContext(huge.root, huge.manifest,
        { candidateId: huge.manifest.candidates[0].id, startLine: 1, endLine: 2 }), /64 KiB/);
});

test('nearby URL expansions must have actually been supplied and cannot invent source', async t => {
    const lines = ['// TODO remove once resolved', ...Array(25).fill('// spacer'), `// ${url}`, ...Array(100).fill('// spacer'), `// ${otherUrl}`];
    const f = await fixture(t, lines.join('\n'));
    const candidate = f.manifest.candidates[0];
    const raw = result(candidate, [action(candidate)]);
    await assert.rejects(validate(f, [raw]), /not present/);
    candidate.contextExpansions = [await readContext(f.root, f.manifest, { candidateId: candidate.id, startLine: 21, endLine: 30 })];
    await validate(f, [raw]);
    candidate.contextExpansions.push(await readContext(f.root, f.manifest, { candidateId: candidate.id, startLine: 125, endLine: 128 }));
    await assert.rejects(validate(f, [result(candidate, [action(candidate, { urls: [otherUrl] })])]), /not present/);
    candidate.contextExpansions[0].context = candidate.contextExpansions[0].context.replace(url, 'forged');
    await assert.rejects(validate(f, [raw]), /evidence does not match/);
});

test('cache hits skip interpretation; rules and whole-file source changes invalidate', async t => {
    const f = await fixture(t, [`// TODO ${url}`, ...Array(100).fill('// spacer')].join('\n'));
    const candidate = f.manifest.candidates[0];
    const validated = await validate(f, [result(candidate, [action(candidate)])]);
    const cache = mergeCache(null, f.manifest, validated, { nextCursor: 0 });
    assert.deepEqual(getCachedResults(cache, f.manifest), validated);
    assert.equal(selectBatch(f.manifest, cache).batch.candidates.length, 0);
    assert.equal(selectBatch({ ...f.manifest, rulesHash: 'new-rules' }, cache).batch.candidates.length, 1);
    await writeFile(f.file, [`// TODO ${url}`, ...Array(100).fill('// changed')].join('\n'));
    await git(f.root, ['add', '.']);
    await git(f.root, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--quiet', '-m', 'Change']);
    const changed = await collect(f.root, { rulesHash: 'rules-1' });
    assert.equal(selectBatch(changed, cache).batch.candidates.length, 1);
    assert.equal(getCachedResults(cache, changed).length, 0);
    assert.equal(getCachedResults(cache, { ...f.manifest, candidates: [] }).length, 0);
});

test('cached and fresh results can be revalidated together without allowing agent-derived fields', async t => {
    const f = await fixture(t, `// TODO ${url}\n// TODO ${otherUrl}`);
    const [first, second] = f.manifest.candidates;
    const initial = await validate(f, [result(first, [action(first)])], [first.id]);
    const cache = mergeCache(null, f.manifest, initial, { nextCursor: 1 });
    const restored = getCachedResults(cache, f.manifest);
    const fresh = result(second, [action(second, { urls: [otherUrl] })]);
    assert.equal((await validate(f, [...restored, fresh])).length, 2);
    restored[0].actions[0].sourceExcerpt = 'forged';
    await assert.rejects(validate(f, [...restored, fresh]), /provenance/);
});

test('host expansion evidence validates against an immutable inventory and survives cache replay', async t => {
    const f = await fixture(t, [
        'namespace Sample.Tests;',
        'class Cases {',
        ...Array(90).fill('// spacer'),
        `[Ignore("${url}")]`,
        '[TestMethod]',
        'public void Run() {}',
        '}',
    ].join('\n'));
    const candidate = f.manifest.candidates[0];
    const payload = { schemaVersion: 1, results: [result(candidate, [action(candidate)])] };
    const original = structuredClone(f.manifest);
    const expansion = await readContext(f.root, f.manifest, { candidateId: candidate.id, startLine: 1, endLine: 2 });
    const options = {
        repoRoot: f.root, expectedCandidateIds: [candidate.id],
        contextEvidence: { schemaVersion: 1, expansions: [expansion] },
    };
    const verified = await validateInterpretations(payload, f.manifest, options);
    assert.deepEqual(f.manifest, original);
    assert.deepEqual(verified[0].contextExpansions, [expansion]);
    const receipt = { candidateId: candidate.id, startLine: 1, endLine: 2 };
    const persistedReceipts = JSON.parse(JSON.stringify({ schemaVersion: 1, expansions: [receipt] }));
    assert.deepEqual(await validateInterpretations(payload, original, { ...options, contextEvidence: persistedReceipts }), verified);
    const cache = mergeCache(null, original, verified, { nextCursor: 0 });
    const restored = getCachedResults(JSON.parse(JSON.stringify(cache)), original);
    assert.deepEqual(await validateInterpretations({ schemaVersion: 1, results: restored }, original,
        { repoRoot: f.root, expectedCandidateIds: [candidate.id] }), verified);
    for (const contextEvidence of [
        { schemaVersion: 2, expansions: [expansion] },
        { schemaVersion: 1, expansions: [expansion], extra: true },
        { schemaVersion: 1, expansions: [expansion, expansion, expansion] },
        { schemaVersion: 1, expansions: [{ ...expansion, candidateId: 'unknown' }] },
        { schemaVersion: 1, expansions: [{ ...expansion, path: 'other.cs' }] },
        { schemaVersion: 1, expansions: [{ ...expansion, context: expansion.context.replace('Sample', 'Forged') }] },
        { schemaVersion: 1, expansions: [{ ...receipt, path: 'other.cs' }] },
        { schemaVersion: 1, expansions: [{ ...receipt, endLine: 81 }] },
    ]) {
        await assert.rejects(validateInterpretations(payload, original, { ...options, contextEvidence }));
    }
});

test('missing cache is normal but malformed/schema-invalid/forged caches are explicit failures', async t => {
    const f = await fixture(t, `// TODO ${url}`);
    assert.equal(selectBatch(f.manifest, null).batch.candidates.length, 1);
    const candidate = f.manifest.candidates[0];
    const validated = await validate(f, [result(candidate, [action(candidate)])]);
    const good = mergeCache(null, f.manifest, validated, { nextCursor: 0 });
    for (const cache of [
        'invalid JSON', {}, { ...good, schemaVersion: 2 }, { ...good, cursor: -1 },
        { ...good, entries: [] }, { ...good, remoteStates: {} },
        { ...good, entries: { ['0'.repeat(64)]: null } },
        { ...good, rulesHash: 'outdated', entries: { ['0'.repeat(64)]: {} } },
        { ...good, entries: { [candidate.id]: { ...validated[0], status: 'insufficient_context' } } },
    ]) {
        assert.throws(() => selectBatch(f.manifest, cache), /Invalid interpretation cache/);
    }
    const forged = structuredClone(good);
    forged.entries[candidate.id].actions[0].path = 'invented.cs';
    assert.throws(() => getCachedResults(forged, f.manifest), /provenance/);
    forged.entries[candidate.id].actions[0].path = candidate.path;
    forged.entries[candidate.id].actions[0].sourceExcerpt = 'forged';
    assert.throws(() => getCachedResults(forged, f.manifest), /provenance/);
});

test('batch windows and bytes are bounded without truncation, with fair deferred continuation', async t => {
    const lines = [];
    for (let index = 0; index < 28; index++) lines.push(`// TODO ${url}`, ...Array(45).fill('// spacer'));
    const f = await fixture(t, lines.join('\n'));
    const first = selectBatch(f.manifest, null);
    assert.equal(first.batch.candidates.length, 25);
    assert.equal(first.nextCursor, 25);
    const deferred = first.batch.candidates.map(candidate => result(candidate, [], 'insufficient_context'));
    const cache = mergeCache(first.cache, f.manifest, deferred, { nextCursor: first.nextCursor });
    assert.equal(Object.keys(cache.entries).length, 0);
    const next = selectBatch(f.manifest, cache);
    assert.equal(next.batch.candidates[0].id, f.manifest.candidates[25].id);
    const bytes = selectBatch(f.manifest, null, { maxBytes: Buffer.byteLength(f.manifest.candidates[0].context) });
    assert.equal(bytes.batch.candidates.length, 1);
    assert.ok(bytes.diagnostics.length > 0);
    const oversized = selectBatch(f.manifest, null, { maxBytes: 10 });
    assert.equal(oversized.batch.candidates.length, 0);
    assert.equal(oversized.diagnostics.length, 28);
    assert.ok(oversized.diagnostics.every(diagnostic => typeof diagnostic === 'string' && diagnostic.includes('not truncated')));
    assert.equal(f.manifest.candidates.length, 28);
    assert.throws(() => selectBatch(f.manifest, null, { maxWindows: 26 }), /limits/);
});

test('overlapping seeds retain IDs and count every serialized context byte', async t => {
    const f = await fixture(t, `// TODO ${url}\n// workaround ${otherUrl}`);
    const batch = selectBatch(f.manifest, null, { maxWindows: 1 });
    assert.equal(batch.batch.candidates.length, 2);
    assert.equal(new Set(batch.batch.candidates.map(candidate => candidate.id)).size, 2);
    const one = selectBatch(f.manifest, null, { maxBytes: Buffer.byteLength(f.manifest.candidates[0].context) });
    assert.equal(one.batch.candidates.length, 1);
    assert.equal(one.nextCursor, 1);
});
