// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from 'node:assert/strict';
import test from 'node:test';
import { createReferenceResolver, listRepositoryIssues, normalizeReference, normalizeRepository } from '../github.mjs';

const closedAt = '2026-08-01T12:00:00Z';
const completed = { state: 'closed', state_reason: 'completed', closed_at: closedAt };
const pause = async () => {};

function api({ issue = async () => ({ data: completed }), pull, paginate } = {}) {
    const calls = { issues: [], pulls: [], pages: [] };
    return {
        calls,
        rest: {
            issues: {
                get: async args => { calls.issues.push(args); return issue(args); },
                listForRepo() {},
            },
            pulls: { get: async args => { calls.pulls.push(args); return pull(args); } },
        },
        paginate: async (route, args) => {
            calls.pages.push(args);
            return paginate(route, args);
        },
    };
}

test('normalization retains source and collapses case, query, fragment and issue/PR route', () => {
    const original = 'https://GitHub.com/DotNet/SDK/issues/123?source=test#issuecomment-5';
    const normalized = normalizeReference(original);
    assert.deepEqual(normalized, {
        owner: 'dotnet', repo: 'sdk', number: 123, key: 'dotnet/sdk/123',
        url: 'https://github.com/dotnet/sdk/issues/123', original,
    });
    assert.equal(normalizeReference('https://github.com/dotnet/sdk/pull/123/').key, normalized.key);
    assert.deepEqual(normalizeRepository('DotNet/NuGet.Client'), { owner: 'dotnet', repo: 'nuget.client' });
});

test('rejects untrusted hosts, malformed owners/repos/numbers and normalized URL tricks', () => {
    for (const url of [
        'http://github.com/dotnet/sdk/issues/1',
        'https://evil.test/dotnet/sdk/issues/1',
        'https://github.com.evil.test/dotnet/sdk/issues/1',
        'https://github.com@evil.test/dotnet/sdk/issues/1',
        'https://user@github.com/dotnet/sdk/issues/1',
        'https://github.com:443/dotnet/sdk/issues/1',
        'https://github.com/-dotnet/sdk/issues/1',
        'https://github.com/dot--net/sdk/issues/1',
        'https://github.com/dotnet/../issues/1',
        'https://github.com/dotnet/sdk/../sdk/issues/1',
        'https://github.com/dotnet/%73dk/issues/1',
        'https://github.com/dotnet/sdk/issues/0',
        'https://github.com/dotnet/sdk/issues/-1',
        'https://github.com/dotnet/sdk/issues/01',
        'https://github.com/dotnet/sdk/issues/9007199254740993',
        'https://github.com/dotnet/sdk/issues/1/files',
        'https://github.com/dotnet/sdk/issues/1#<script>',
        'https://github.com\\dotnet\\sdk\\issues\\1',
        ' https://github.com/dotnet/sdk/issues/1',
    ]) {
        assert.throws(() => normalizeReference(url), undefined, url);
    }
});

test('unique canonical references are read once, including concurrent calls and issue aliases for PRs', async () => {
    const github = api({
        issue: async () => ({ data: { ...completed, pull_request: {} } }),
        pull: async () => ({ data: { state: 'closed', closed_at: closedAt, merged_at: closedAt } }),
    });
    const resolver = createReferenceResolver({ github, sleep: pause });
    const urls = [
        'https://github.com/dotnet/sdk/issues/1#x',
        'https://github.com/DOTNET/SDK/pull/1?x=y',
        'https://github.com/dotnet/sdk/issues/1#x',
    ];
    const [result, single] = await Promise.all([resolver.resolveAll(urls), resolver.resolve(urls[1])]);
    assert.equal(result.references.length, 1);
    assert.equal(result.references[0].kind, 'pull');
    assert.equal(result.references[0].qualifies, true);
    assert.equal(single.original, urls[1]);
    assert.deepEqual(result.references[0].originalUrls, urls.slice(0, 2));
    assert.equal(github.calls.issues.length, 1);
    assert.equal(github.calls.pulls.length, 1);
    assert.deepEqual(github.calls.pulls[0], {
        owner: 'dotnet', repo: 'sdk', pull_number: 1, request: { retries: 0 },
    });
});

test('eligibility requires completed issues or authoritative merged_at, not closure or a merged flag', async t => {
    const cases = [
        ['completed', completed, undefined, true, true],
        ['not planned', { ...completed, state_reason: 'not_planned' }, undefined, false, true],
        ['unknown closure', { state: 'closed', closed_at: closedAt }, undefined, false, true],
        ['open', { state: 'open', state_reason: null }, undefined, false, true],
        ['reopened', { ...completed, state: 'open', state_reason: 'reopened' }, undefined, false, true],
        ['closed unmerged PR', { ...completed, pull_request: {} },
            { state: 'closed', merged: true, merged_at: null }, false, true],
        ['open PR', { state: 'open', pull_request: {} },
            { state: 'open', merged_at: null }, false, true],
        ['merged PR', { ...completed, pull_request: {} },
            { state: 'closed', merged_at: closedAt }, true, true],
        ['missing merge state', { ...completed, pull_request: {} }, { state: 'closed' }, false, false],
        ['missing issue date', { state: 'closed', state_reason: 'completed' }, undefined, false, false],
        ['invalid issue date', { ...completed, closed_at: '0' }, undefined, false, false],
    ];
    for (const [name, issue, pull, qualifies, known] of cases) {
        await t.test(name, async () => {
            const github = api({
                issue: async () => ({ data: issue }),
                pull: async () => ({ data: pull }),
            });
            const result = await createReferenceResolver({ github, sleep: pause })
                .resolve('https://github.com/dotnet/sdk/issues/1');
            assert.equal(result.qualifies, qualifies);
            assert.equal(result.known, known);
        });
    }
});

test('lookup failures are unknown, cached, and never mistaken for completed issues', async t => {
    for (const status of [401, 403, 404, 422, 500]) {
        await t.test(String(status), async () => {
            const github = api({ issue: async () => { throw { status }; } });
            const resolver = createReferenceResolver({ github, sleep: pause });
            const result = await resolver.resolveAll([
                'https://github.com/dotnet/sdk/issues/1',
                'https://github.com/dotnet/sdk/pull/1',
            ]);
            assert.equal(result.references[0].known, false);
            assert.equal(result.references[0].qualifies, false);
            assert.deepEqual(result.diagnostics, [{
                code: 'reference-lookup-failed', reference: 'dotnet/sdk/1', status,
            }]);
            assert.equal(github.calls.issues.length, status === 500 ? 3 : 1);
            assert.equal(github.calls.pulls.length, 0);
        });
    }
    const github = api({
        issue: async () => ({ data: { ...completed, pull_request: {} } }),
        pull: async () => { throw { status: 404 }; },
    });
    const result = await createReferenceResolver({ github, sleep: pause })
        .resolve('https://github.com/dotnet/sdk/pull/1');
    assert.equal(result.known, false);
});

test('retries only explicit transient/rate-limit reads within bounded delays', async () => {
    let attempts = 0;
    const delays = [];
    const github = api({
        issue: async () => {
            if (attempts++ === 0) {
                throw { status: 403, response: { headers: { 'retry-after': '1' } } };
            }
            return { data: completed };
        },
    });
    const result = await createReferenceResolver({ github, sleep: async delay => delays.push(delay) })
        .resolve('https://github.com/dotnet/sdk/issues/1');
    assert.equal(result.qualifies, true);
    assert.deepEqual(delays, [1000]);
    for (const failure of [
        new Error('Unclassified connection error'),
        { status: 429, response: { headers: { 'retry-after': '120' } } },
    ]) {
        const failing = api({ issue: async () => { throw failure; } });
        const failed = await createReferenceResolver({ github: failing, sleep: pause })
            .resolve('https://github.com/dotnet/sdk/issues/1');
        assert.equal(failed.known, false);
        assert.equal(failing.calls.issues.length, 1);
    }
});

test('lookup concurrency is bounded across distinct URLs', async () => {
    let active = 0;
    let peak = 0;
    const github = api({ issue: async () => {
        active++;
        peak = Math.max(peak, active);
        await new Promise(resolve => setImmediate(resolve));
        active--;
        return { data: completed };
    } });
    const result = await createReferenceResolver({ github, sleep: pause })
        .resolveAll(Array.from({ length: 19 }, (_, i) => `https://github.com/dotnet/sdk/issues/${i + 1}`));
    assert.equal(result.references.length, 19);
    assert.equal(github.calls.issues.length, 19);
    assert.equal(peak, 4);
});

test('invalid reference is diagnosed without an API request', async () => {
    const github = api();
    const result = await createReferenceResolver({ github }).resolveAll(['https://evil.test/issues/1']);
    assert.equal(result.references[0].known, false);
    assert.equal(result.diagnostics[0].code, 'invalid-reference');
    assert.equal(github.calls.issues.length, 0);
});

test('issue listing consumes full pagination, filters PRs and never label-filters open issues', async () => {
    const pages = [
        Array.from({ length: 100 }, (_, i) => ({ number: i + 1, body: '', state: 'open' })),
        [{ number: 101, body: 'final page marker', state: 'open' },
            { number: 102, body: '', state: 'open', pull_request: {} }],
    ];
    let visited = 0;
    const github = api({ paginate: async (route, args) => {
        assert.equal(route, github.rest.issues.listForRepo);
        assert.equal(args.per_page, 100);
        if (args.state === 'open') {
            assert.equal('labels' in args, false);
            return pages.flatMap(page => { visited++; return page; });
        }
        assert.equal(args.labels, 'agentic-workflows');
        return [{ number: 103, body: null, state: 'closed' }];
    } });
    const open = await listRepositoryIssues({ github, repository: 'DotNet/SDK', state: 'open' });
    assert.equal(visited, 2);
    assert.equal(open.length, 101);
    assert.equal(open.at(-1).number, 101);
    assert.equal((await listRepositoryIssues({ github, repository: 'DotNet/SDK', state: 'closed' })).length, 1);
});

test('failed, incomplete and malformed listings throw rather than returning an empty success', async () => {
    for (const paginate of [
        async () => { throw { status: 404 }; },
        async () => undefined,
        async () => [{ number: 1, state: 'open' }],
        async () => [{ number: 1, state: 'closed', body: '' }],
    ]) {
        await assert.rejects(listRepositoryIssues({
            github: api({ paginate }), repository: 'dotnet/sdk', state: 'open', sleep: pause,
        }));
    }
});

test('pagination retries a transient incomplete read but never accepts its partial pages', async () => {
    let attempts = 0;
    const delays = [];
    const github = api({ paginate: async () => {
        if (++attempts < 3) {
            throw { status: 503 };
        }
        return [{ number: 201, body: 'page three', state: 'open' }];
    } });
    const issues = await listRepositoryIssues({
        github, repository: 'dotnet/sdk', state: 'open', sleep: async delay => delays.push(delay),
    });
    assert.equal(issues[0].number, 201);
    assert.equal(attempts, 3);
    assert.deepEqual(delays, [250, 500]);
    assert.ok(github.calls.pages.every(call => call.request.retries === 0));
});
