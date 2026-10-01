// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from 'node:assert/strict';
import test from 'node:test';
import { buildProposal, finalize, mergeActions } from '../finalize.mjs';
import { createReferenceResolver } from '../github.mjs';
import { completed, createGitHubMock as mock } from './github-mock.mjs';

const repository = 'dotnet/sdk';
const headSha = 'a'.repeat(40);
const blocker = 'https://github.com/dotnet/runtime/issues/123';
const otherBlocker = 'https://github.com/dotnet/roslyn/issues/456';
const logger = { warn() {} };

function todo(name, overrides = {}) {
    return {
        candidateId: name, kind: 'todo', path: 'src/File.cs', anchor: `N.C.${name}`,
        testNames: [], startLine: 10, endLine: 10,
        seedText: `// TODO remove fallback after ${blocker}`,
        sourceExcerpt: `// TODO remove fallback after ${blocker}`,
        urls: [blocker], additionalConditions: [], ...overrides,
    };
}

function run(api, actions, options = {}) {
    return finalize({ github: api.github, repository, headSha, actions, dryRun: false, logger, ...options });
}

async function preview(actions) {
    return (await run(mock(), actions, { dryRun: true })).proposed[0];
}

test('same-blocker sites across files share one issue with shared guidance and separate findings', async () => {
    const a = todo('A');
    const b = todo('B', {
        kind: 'workaround', path: 'src/Other.cs', sourceExcerpt: `// workaround ${blocker}`,
        additionalConditions: ['Consume the fixed dependency version.'],
    });
    const api = mock();
    const result = await run(api, [a, b]);
    assert.equal(result.created.length, 1);
    assert.equal(api.calls.writes.length, 1);
    assert.equal(result.created[0].targetIds.length, 2);
    const body = result.created[0].body;
    for (const text of ['N.C.A', 'N.C.B', blocker, a.path, b.path, b.additionalConditions[0]]) {
        assert.ok(body.includes(text), text);
    }
    assert.equal([...body.matchAll(/^<!-- stale-reference-target:v2:/gm)].length, 2);
    for (const text of ['## Findings', '## Follow-up', '## Verified reference resolution',
        'Repository:', 'Target branch:', 'No tests were run by this discovery workflow.']) {
        assert.equal(body.split(text).length - 1, 1, text);
    }
    assert.equal([...body.matchAll(/^### (?:TODO|Workaround)$/gm)].length, 2);
    assert.deepEqual((await run(mock(), [b, a])).proposed, result.proposed);
});

test('same target unions blockers before eligibility, while other sites in the file can qualify', async () => {
    const a = todo('A');
    const conflicting = { ...a, candidateId: 'A-duplicate', urls: [otherBlocker] };
    const b = todo('B');
    const api = mock({ issue: async args => args.repo === 'roslyn' ? { state: 'open' } : completed });
    const result = await run(api, [a, conflicting, b]);
    assert.equal(result.created.length, 1);
    assert.deepEqual(result.created[0].candidateIds, ['B']);
    assert.deepEqual(result.skipped[0].candidateIds, ['A', 'A-duplicate']);
    assert.equal(result.skipped[0].reason, 'unresolved-blockers');
});

test('unrelated blockers in one file get separate issues, while co-anchored blocker sets group across files', async () => {
    const paired = (name, path) => todo(name, {
        path, urls: [otherBlocker, blocker],
        seedText: `// TODO remove after ${blocker} and ${otherBlocker}`,
        sourceExcerpt: `// TODO remove after ${blocker} and ${otherBlocker}`,
    });
    const inputs = [
        todo('A'),
        todo('B', { urls: [otherBlocker], seedText: `// TODO ${otherBlocker}`, sourceExcerpt: `// TODO ${otherBlocker}` }),
        paired('C', 'src/File.cs'), paired('D', 'src/Other.cs'),
    ];
    const result = await run(mock(), inputs);
    assert.equal(result.created.length, 3);
    const single = result.created.find(issue => issue.candidateIds.includes('A'));
    const other = result.created.find(issue => issue.candidateIds.includes('B'));
    const joint = result.created.find(issue => issue.candidateIds.includes('C'));
    assert.deepEqual(single.candidateIds, ['A']);
    assert.ok(!single.body.includes(otherBlocker));
    assert.deepEqual(other.candidateIds, ['B']);
    assert.ok(!other.body.includes(blocker));
    assert.deepEqual(joint.candidateIds, ['C', 'D']);
    assert.ok(joint.body.includes(blocker));
    assert.ok(joint.body.includes(otherBlocker));
    assert.deepEqual((await run(mock(), [...inputs].reverse())).proposed, result.proposed);
});

test('canonical URL aliases and mixed finding kinds share a blocker container across files', async () => {
    const api = mock();
    const result = await run(api, [
        todo('A'),
        { ...todo('Test', { path: 'test/Other.cs', urls: [`${blocker}?source=repo#context`] }),
            kind: 'ignore', testNames: ['N.C.Test'] },
    ]);
    assert.equal(result.created.length, 1);
    assert.equal(result.created[0].targetIds.length, 2);
    assert.ok(result.created[0].body.includes('Original source URL: <' + blocker + '?source=repo#context>'));
    assert.equal(api.calls.reads.length, 1);
    assert.equal((await run(api, [todo('A')])).created.length, 0);
});

test('a mixed-blocker legacy file issue is not adopted as a blocker container', async () => {
    const entries = [todo('A'), todo('B', { urls: [otherBlocker] })];
    const blocks = [];
    for (const input of entries) {
        const { references } = await createReferenceResolver({ github: mock().github }).resolveAll(input.urls);
        const proposal = buildProposal({ repository, headSha, group: mergeActions(repository, [input])[0], references });
        const key = `${proposal.targetIds[0]}:${proposal.fingerprint}`;
        blocks.push(`<!-- stale-reference-target:v1:${key} -->\n${proposal.body}<!-- /stale-reference-target:v1:${key} -->`);
    }
    const oldBody = blocks.join('\n');
    const api = mock({ open: [{ number: 7, state: 'open', body: oldBody }] });
    const result = await run(api, [todo('C')]);
    assert.equal(result.created.length, 1);
    assert.equal(result.updated.length, 0);
    assert.equal(api.state.open[0].body, oldBody);
    assert.equal((await run(api, entries)).created.length + api.calls.updates.length, 0);
});

test('malformed findings boundaries defer appending without rewriting human content', async () => {
    const old = await preview([todo('A')]);
    const end = '<!-- /stale-reference-findings:v1 -->';
    const start = '<!-- stale-reference-findings:v1 -->';
    for (const body of [
        old.body.replace(end, ''), old.body.replace(start, ''), `${old.body}\n${end}`,
        old.body.replace(start, 'temporary-findings-boundary').replace(end, start)
            .replace('temporary-findings-boundary', end),
    ]) {
        const api = mock({ open: [{ number: 7, state: 'open', body }] });
        const result = await run(api, [todo('B')]);
        assert.equal(result.skipped[0].reason, 'tracking-layout-invalid');
        assert.equal(result.diagnostics[0].code, 'tracking-layout-invalid');
        assert.equal(api.calls.writes.length + api.calls.updates.length, 0);
        assert.equal(api.state.open[0].body, body);
    }
});

test('new-format ignored-test prerequisites append fresh evidence without duplicating shared guidance', async () => {
    const input = { ...todo('Test'), kind: 'ignore', testNames: ['N.C.Test'] };
    const api = mock();
    await run(api, [input]);
    const changed = { ...input, additionalConditions: ['Consume compatible packages.'] };
    const result = await run(api, [changed]);
    assert.equal(result.updated.length, 1);
    assert.equal(result.updated[0].body.split('## Follow-up').length - 1, 1);
    assert.equal((await run(api, [changed])).updated.length, 0);
});

test('ignored-test formatting and line moves do not append unchanged evidence', async () => {
    const input = { ...todo('Test'), kind: 'ignore', testNames: ['N.C.Test'],
        sourceExcerpt: `[Ignore("${blocker}")]` };
    const api = mock();
    await run(api, [input]);
    const result = await run(api, [{ ...input, startLine: 200, endLine: 200,
        sourceExcerpt: `    ${input.sourceExcerpt}   ` }], { headSha: 'b'.repeat(40) });
    assert.equal(result.created.length + result.updated.length, 0);
    assert.equal(result.skipped[0].reason, 'open-duplicate');
});

test('source excerpts containing findings boundaries are deferred before any mutation', async () => {
    const api = mock();
    const result = await run(api, [todo('A', {
        sourceExcerpt: `// TODO ${blocker}\n<!-- /stale-reference-findings:v1 -->`,
    })]);
    assert.equal(result.skipped[0].reason, 'tracking-layout-invalid');
    assert.equal(api.calls.writes.length + api.calls.updates.length, 0);
});

test('changed resolution dates defer modern and legacy container updates before mutation', async () => {
    const input = todo('A');
    const { references } = await createReferenceResolver({ github: mock().github }).resolveAll(input.urls);
    const legacy = buildProposal({ repository, headSha, group: mergeActions(repository, [input])[0], references });
    for (const body of [legacy.body, (await preview([input])).body]) {
        const api = mock({
            open: [{ number: 7, state: 'open', body }],
            issue: async () => ({ ...completed, closed_at: '2026-09-01T12:00:00Z' }),
        });
        const result = await run(api, [todo('B')]);
        assert.equal(result.skipped[0].reason, 'tracking-layout-invalid');
        assert.match(result.diagnostics[0].reason, /resolution changed/);
        assert.equal(api.calls.updates.length + api.calls.writes.length, 0);
    }
});

test('uncertain updates cannot reconcile against a different issue number', async () => {
    const old = await preview([todo('A')]);
    const api = mock({
        open: [{ number: 7, state: 'open', body: old.body }],
        update: async (args, state) => {
            state.open[0].body = args.body;
            throw new Error('timeout');
        },
        getTracking: async (args, state, calls) => ({
            ...state.open[0], number: calls.updates.length ? 999 : 7,
        }),
    });
    const result = await run(api, [todo('B')]);
    assert.equal(result.updated.length, 0);
    assert.equal(result.skipped[0].reason, 'update-outcome-unknown');
    assert.equal(api.calls.updates.length, 1);
});

test('uncertain creates require the findings boundaries and shared follow-up as well as complete blocks', async () => {
    for (const removed of ['<!-- /stale-reference-findings:v1 -->', '## Follow-up']) {
        const api = mock({ create: async (args, state) => {
            state.open.push({ number: 7, state: 'open', body: args.body.replace(removed, '') });
            throw new Error('timeout');
        } });
        const result = await run(api, [todo('A')]);
        assert.equal(result.created.length, 0);
        assert.equal(result.skipped[0].reason, 'create-outcome-unknown');
        assert.equal(api.calls.writes.length, 1);
    }
});

test('later same-blocker sites across files append within findings and preserve human edits and labels', async () => {
    const a = todo('A');
    const b = todo('B', { path: 'src/Other.cs' });
    const api = mock();
    const first = await run(api, [a]);
    const tracked = api.state.open[0];
    tracked.title = 'Human title';
    tracked.labels = ['human-label'];
    tracked.body += '\nHuman investigation notes.\n';
    const original = tracked.body;
    const second = await run(api, [a, b]);
    assert.equal(second.created.length, 0);
    assert.equal(second.updated.length, 1);
    assert.equal(second.updated[0].number, first.created[0].number);
    assert.deepEqual(second.updated[0].candidateIds, ['B']);
    const end = original.indexOf('<!-- /stale-reference-findings:v1 -->');
    assert.ok(tracked.body.startsWith(original.slice(0, end)));
    assert.ok(tracked.body.endsWith(original.slice(end)));
    assert.ok(tracked.body.indexOf('Owning anchor: `N.C.B`') < tracked.body.indexOf('## Follow-up'));
    assert.equal(tracked.body.split('## Follow-up').length - 1, 1);
    assert.equal(tracked.title, 'Human title');
    assert.deepEqual(tracked.labels, ['human-label']);
    assert.deepEqual(api.calls.updates[0].request, { retries: 0 });
    assert.ok(!('title' in api.calls.updates[0]));
    const third = await run(api, [a, b]);
    assert.equal(third.updated.length, 0);
    assert.equal(third.created.length, 0);
    assert.equal(api.calls.updates.length, 1);
});

test('changed target prerequisites append fresh evidence rather than suppressing it', async () => {
    const a = todo('A');
    const api = mock();
    await run(api, [a]);
    const result = await run(api, [{ ...a, additionalConditions: ['Also requires compatible packages.'] }]);
    assert.equal(result.updated.length, 1);
    assert.ok(result.updated[0].body.includes('Also requires compatible packages.'));
});

test('line moves, checkout changes and comment formatting do not append unchanged targets', async () => {
    const a = todo('A');
    const api = mock();
    await run(api, [a]);
    const result = await run(api, [{
        ...a, startLine: 200, endLine: 201,
        sourceExcerpt: `/* TODO remove fallback\n * after ${blocker} */`,
    }], { headSha: 'b'.repeat(40) });
    assert.equal(result.updated.length + result.created.length, 0);
    assert.equal(result.skipped[0].reason, 'open-duplicate');
});

test('completed aggregate closure refiles only targets remaining in current source', async () => {
    const a = todo('A');
    const b = todo('B');
    const original = await preview([a, b]);
    const closed = [{ number: 7, state: 'closed', state_reason: 'completed', body: original.body }];
    const api = mock({ closed });
    const result = await run(api, [b]);
    assert.equal(result.created.length, 1);
    assert.deepEqual(result.created[0].candidateIds, ['B']);
    assert.ok(result.created[0].body.includes('N.C.B'));
    assert.ok(!result.created[0].body.includes('N.C.A'));
    assert.ok(result.created[0].body.includes('https://github.com/dotnet/sdk/issues/7 (closed)'));
    assert.equal((await run(mock({ closed }), [])).created.length, 0);
    assert.equal((await run(api, [b])).created.length, 0);
});

test('completed targets join an existing file issue rather than creating another', async () => {
    const a = todo('A');
    const b = todo('B');
    const old = await preview([a]);
    const current = await preview([b]);
    const api = mock({
        closed: [{ number: 7, state: 'closed', state_reason: 'completed', body: old.body }],
        open: [{ number: 8, state: 'open', body: current.body }],
    });
    const result = await run(api, [a, b]);
    assert.equal(result.created.length, 0);
    assert.equal(result.updated.length, 1);
    assert.equal(result.updated[0].number, 8);
    assert.ok(result.updated[0].body.includes('issues/7 (closed)'));
});

test('not-planned history suppresses covered targets but not new targets in the file', async () => {
    const a = todo('A');
    const b = todo('B');
    const old = await preview([a, b]);
    const api = mock({ closed: [{ number: 7, state: 'closed', state_reason: 'not_planned', body: old.body }] });
    const result = await run(api, [a, b, todo('C')]);
    assert.equal(result.created.length, 1);
    assert.deepEqual(result.created[0].candidateIds, ['C']);
    assert.equal(result.skipped.filter(item => item.reason === 'declined-closed-task').length, 2);
    assert.equal((await run(mock({ closed: api.state.closed }), [a, b])).created.length, 0);
    const altered = await run(mock({ closed: api.state.closed }), [{
        ...a, additionalConditions: ['New prerequisites do not override an explicit rejection.'],
    }]);
    assert.equal(altered.skipped[0].reason, 'declined-closed-task');
});

test('unknown tracking closure defers affected targets instead of filing replacements', async () => {
    const a = todo('A');
    const old = await preview([a]);
    for (const state_reason of [null, undefined, 'unexpected']) {
        const api = mock({ closed: [{ number: 7, state: 'closed', state_reason, body: old.body }] });
        const result = await run(api, [a]);
        assert.equal(result.created.length, 0);
        assert.equal(result.skipped[0].reason, 'unknown-tracking-closure');
    }
});

test('legacy marked single-blocker TODO can become a blocker container without discarding its content', async () => {
    const a = todo('A');
    const { references } = await createReferenceResolver({ github: mock().github }).resolveAll(a.urls);
    const old = buildProposal({
        repository, headSha, group: mergeActions(repository, [a])[0], references,
    });
    const api = mock({ open: [{ number: 7, state: 'open', title: old.title, body: old.body }] });
    const result = await run(api, [a, todo('B')]);
    assert.equal(result.created.length, 0);
    assert.equal(result.updated.length, 1);
    assert.ok(result.updated[0].body.includes(old.body));
    assert.match(result.updated[0].body, /^<!-- stale-reference-blockers:v1:/);
    assert.deepEqual(result.updated[0].candidateIds, ['B']);
    const next = await run(api, [a, todo('B')]);
    assert.equal(next.updated.length + next.created.length, 0);
});

test('ignored tests and comments referencing the same blocker share a tracking issue', async () => {
    const a = todo('A');
    const ignored = { ...a, kind: 'ignore', testNames: ['N.C.Test'] };
    const old = await preview([ignored]);
    const result = await run(mock({ open: [{ number: 7, state: 'open', body: old.body }] }), [a]);
    assert.equal(result.created.length, 0);
    assert.equal(result.updated.length, 1);
    assert.ok(result.updated[0].body.includes('Fully qualified test: `N.C.Test`'));
    assert.ok(result.updated[0].body.includes('Owning anchor: `N.C.A`'));
});

test('multiple same-blocker containers defer additions without choosing or creating another issue', async () => {
    const a = await preview([todo('A')]);
    const b = await preview([todo('B')]);
    const api = mock({ open: [
        { number: 7, state: 'open', body: a.body }, { number: 8, state: 'open', body: b.body },
    ] });
    const result = await run(api, [todo('C')]);
    assert.equal(api.calls.writes.length + api.calls.updates.length, 0);
    assert.equal(result.skipped[0].reason, 'ambiguous-blocker-tracking');
    assert.equal(result.diagnostics[0].code, 'ambiguous-blocker-tracking');
});

test('updates are allowed at the open cap, but new blocker issues are not', async () => {
    const a = await preview([todo('A')]);
    const api = mock({ open: [
        { number: 7, state: 'open', body: a.body, labels: ['stale-issue-detection'] },
        ...Array.from({ length: 4 }, (_, i) => ({
            number: 10 + i, state: 'open', body: 'Other task', labels: ['stale-issue-detection'],
        })),
    ] });
    const result = await run(api, [todo('B'), todo('C', { path: 'src/Other.cs', urls: [otherBlocker] })]);
    assert.equal(result.updated.length, 1);
    assert.equal(result.created.length, 0);
    assert.equal(result.skipped[0].reason, 'open-issue-cap');
});

test('dry-run includes update payloads identical to live and performs no mutations', async () => {
    const old = await preview([todo('A')]);
    const open = [{ number: 7, state: 'open', body: old.body }];
    const api = mock({ open, update: async () => assert.fail('Dry-run mutation') });
    const dry = await run(api, [todo('B')], { dryRun: true });
    const live = await run(mock({ open }), [todo('B')]);
    assert.deepEqual(dry.proposed, live.proposed);
    assert.equal(dry.proposed[0].operation, 'update');
    assert.equal(dry.created.length + dry.updated.length, 0);
    assert.equal(api.calls.writes.length + api.calls.updates.length, 0);
});

test('comment creations consume the same cap as ignored-test creations in dry-run and live', async () => {
    const inputs = Array.from({ length: 5 }, (_, i) => todo(`A${i}`, {
        path: `src/File${i}.cs`, urls: [blocker.replace('123', String(123 + i))],
    }));
    inputs.push({ ...todo('Test'), kind: 'ignore', testNames: ['N.C.Test'], urls: [otherBlocker] });
    for (const dryRun of [true, false]) {
        const result = await run(mock(), inputs, { dryRun });
        assert.equal(result.proposed.length, 5);
        assert.equal(result.skipped[0].reason, 'open-issue-cap');
    }
});

test('fresh issue read preserves edits made after listing and defers if the issue closes', async () => {
    const old = await preview([todo('A')]);
    for (const closes of [false, true]) {
        const api = mock({
            open: [{ number: 7, state: 'open', body: old.body }],
            getTracking: async (args, state) => ({
                ...state.open[0], state: closes ? 'closed' : 'open',
                body: `${state.open[0].body}\nFresh human notes.`,
            }),
        });
        const result = await run(api, [todo('B')]);
        if (closes) {
            assert.equal(api.calls.updates.length, 0);
            assert.equal(result.skipped[0].reason, 'tracking-unavailable');
        } else {
            assert.equal(result.updated.length, 1);
            assert.ok(result.updated[0].body.includes('Fresh human notes.'));
        }
    }
});

test('ambiguous update success reconciles complete evidence without retrying', async () => {
    const old = await preview([todo('A')]);
    const api = mock({
        open: [{ number: 7, state: 'open', body: old.body }],
        update: async (args, state) => {
            state.open[0].body = args.body;
            throw new Error('timeout');
        },
    });
    const result = await run(api, [todo('B')]);
    assert.equal(result.updated.length, 1);
    assert.equal(result.updated[0].reconciled, true);
    assert.equal(api.calls.updates.length, 1);
    assert.equal(result.diagnostics[0].code, 'update-failed');
});

test('markers without complete evidence do not reconcile an uncertain update; later writes halt', async () => {
    const old = await preview([todo('A')]);
    const api = mock({
        open: [{ number: 7, state: 'open', body: old.body }],
        update: async (args, state) => {
            state.open[0].body = args.body.replace(/\/\/ TODO remove fallback[^\n]*/g, 'missing source evidence');
            throw new Error('timeout');
        },
    });
    const result = await run(api, [todo('B'), todo('C', { path: 'src/Other.cs', urls: [otherBlocker] })]);
    assert.equal(result.updated.length, 0);
    assert.equal(api.calls.updates.length, 1);
    assert.equal(api.calls.writes.length, 0);
    assert.deepEqual(result.skipped.map(item => item.reason), ['update-outcome-unknown', 'filing-halted']);
    api.github.rest.issues.update = async args => {
        api.state.open[0].body = args.body;
        return { data: { ...api.state.open[0] } };
    };
    const recovered = await run(api, [todo('B')]);
    assert.equal(recovered.updated.length, 1);
    assert.equal((await run(api, [todo('B')])).updated.length, 0);
});

test('ambiguous comment creation reconciles all blocks without another POST', async () => {
    const api = mock({ create: async (args, state) => {
        state.open.push({ number: 7, state: 'open', body: args.body });
        throw new Error('timeout');
    } });
    const result = await run(api, [todo('A'), todo('B')]);
    assert.equal(result.created.length, 1);
    assert.equal(result.created[0].reconciled, true);
    assert.equal(api.calls.writes.length, 1);
    assert.equal(result.created[0].targetIds.length, 2);
});

test('incomplete comment creation and failed reconciliation halt all later writes', async () => {
    for (const failedRead of [false, true]) {
        const api = mock({
            create: async (args, state) => {
                state.open.push({ number: 7, state: 'open', body: args.body.replace('Owning anchor: `N.C.B`', '') });
                throw new Error('timeout');
            },
            paginate: async (args, state, calls) => {
                if (failedRead && calls.writes.length) throw new Error('unavailable');
                return state[args.state];
            },
        });
        const result = await run(api, [
            todo('A'), todo('B'), todo('C', { path: 'src/Other.cs', urls: [otherBlocker] }),
            { ...todo('Test'), kind: 'ignore', path: 'test/Z.cs', testNames: ['N.C.Test'],
                urls: [blocker.replace('123', '789')] },
        ]);
        assert.equal(result.created.length, 0);
        assert.equal(api.calls.writes.length, 1);
        assert.deepEqual(result.skipped.map(item => item.reason),
            ['create-outcome-unknown', 'filing-halted', 'filing-halted']);
        if (failedRead) assert.ok(result.diagnostics.some(item => item.code === 'create-reconciliation-failed'));
    }
});

test('append read failure and reconciliation read failure halt later mutations', async () => {
    const old = await preview([todo('A')]);
    for (const failBeforeUpdate of [false, true]) {
        const api = mock({
            open: [{ number: 7, state: 'open', body: old.body }],
            getTracking: async (args, state, calls) => {
                if (failBeforeUpdate || calls.updates.length) throw new Error('unavailable');
                return state.open[0];
            },
            update: async () => { throw new Error('timeout'); },
        });
        const result = await run(api, [todo('B'), todo('C', { path: 'src/Other.cs', urls: [otherBlocker] })]);
        assert.equal(api.calls.updates.length, failBeforeUpdate ? 0 : 1);
        assert.equal(api.calls.writes.length, 0);
        assert.equal(result.skipped[1].reason, 'filing-halted');
        assert.equal(result.diagnostics.at(-1).code,
            failBeforeUpdate ? 'tracking-read-failed' : 'update-reconciliation-failed');
    }
});

test('latest refresh sees a newly created blocker container and appends rather than creating another', async () => {
    const old = await preview([todo('A')]);
    let listings = 0;
    const api = mock({ paginate: async (args, state) => {
        if (args.state === 'open' && ++listings === 2) {
            state.open.push({ number: 7, state: 'open', body: old.body });
        }
        return state[args.state];
    } });
    const result = await run(api, [todo('B')]);
    assert.equal(result.created.length, 0);
    assert.equal(result.updated.length, 1);
    assert.equal(result.updated[0].number, 7);
});

test('won\'t-fix subsets of a class ignore do not suppress other tests in that class', async () => {
    const base = { ...todo('Test'), kind: 'ignore', testNames: ['N.C.A'] };
    const old = await preview([base]);
    const result = await run(mock({
        closed: [{ number: 7, state: 'closed', state_reason: 'not_planned', body: old.body }],
    }), [{ ...base, testNames: ['N.C.A', 'N.C.B'] }]);
    assert.equal(result.created.length, 1);
    assert.ok(result.created[0].body.includes('Fully qualified test: `N.C.B`'));
    assert.ok(!result.created[0].body.includes('Fully qualified test: `N.C.A`'));
    assert.equal(result.skipped[0].reason, 'declined-closed-task');
});

test('oversized append is explicitly deferred instead of splitting into another issue', async () => {
    const old = await preview([todo('A')]);
    const api = mock({ open: [{ number: 7, state: 'open', body: old.body + 'x'.repeat(65536) }] });
    const result = await run(api, [todo('B')]);
    assert.equal(result.skipped[0].reason, 'tracking-body-too-large');
    assert.equal(api.calls.updates.length + api.calls.writes.length, 0);
});

test('related history matches a file and anchor in the same finding, not in separate findings', async () => {
    const old = await preview([todo('A'), todo('B', { path: 'src/Other.cs' })]);
    const result = await run(mock({ closed: [{
        number: 7, state: 'closed', state_reason: 'completed', body: old.body,
    }] }), [todo('B', { seedText: `// TODO updated fallback ${blocker}`,
        sourceExcerpt: `// TODO updated fallback ${blocker}` })]);
    assert.equal(result.created.length, 1);
    assert.ok(!result.created[0].body.includes('issues/7 (closed)'));
});
