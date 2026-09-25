// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from 'node:assert/strict';
import test from 'node:test';
import { finalize, mergeActions, normalizeActionSource, targetIds } from '../finalize.mjs';

const repository = 'dotnet/sdk';
const headSha = 'a'.repeat(40);
const closedAt = '2026-08-01T12:00:00Z';
const blocker = 'https://github.com/dotnet/runtime/issues/123';
const completed = { state: 'closed', state_reason: 'completed', closed_at: closedAt };
const logger = { warn() {} };

function action(overrides = {}) {
    return {
        candidateId: 'candidate-1', kind: 'ignore', path: 'test/Tests/FeatureTests.cs',
        anchor: 'Tests.FeatureTests.Works', testNames: ['Tests.FeatureTests.Works'],
        startLine: 42, endLine: 42, sourceExcerpt: `[Ignore("${blocker}")]`,
        urls: [blocker], additionalConditions: [],
        ...overrides,
    };
}

function mock({ open = [], closed = [], get, paginate, create } = {}) {
    const calls = { reads: [], lists: [], writes: [] };
    const state = { open: [...open], closed: [...closed] };
    const github = {
        rest: {
            issues: {
                get: async args => {
                    calls.reads.push(args);
                    return { data: get ? await get(args) : completed };
                },
                listForRepo() {},
                create: async args => {
                    calls.writes.push(args);
                    if (create) {
                        return create(args, state, calls);
                    }
                    const created = { number: 1000 + calls.writes.length, body: args.body, state: 'open' };
                    state.open.push(created);
                    return { data: created };
                },
            },
            pulls: { get: async () => { throw new Error('Unexpected PR lookup'); } },
        },
        paginate: async (route, args) => {
            assert.equal(route, github.rest.issues.listForRepo);
            calls.lists.push(args);
            return paginate ? paginate(args, state, calls) : [...state[args.state]];
        },
    };
    return { github, calls, state };
}

async function run(api, actions = [action()], options = {}) {
    return finalize({ github: api.github, repository, headSha, actions, dryRun: false, logger, ...options });
}

async function proposalFor(input = action()) {
    return (await run(mock(), [input], { dryRun: true })).proposed[0];
}

test('fixed ignore template and code-owned labels include pinned evidence and actual-test follow-up', async () => {
    const input = action({
        urls: [`${blocker}?source=repo#x`],
        sourceExcerpt: `[Ignore("${blocker}?source=repo#x")]`,
        additionalConditions: ['The SDK must consume the fixed dependency version.'],
        title: 'MODEL TITLE', labels: ['arbitrary-label'], body: 'MODEL BODY',
    });
    const api = mock();
    const result = await run(api, [input]);
    assert.equal(result.created.length, 1);
    const payload = api.calls.writes[0];
    assert.deepEqual(payload.labels, ['cookie', 'agentic-workflows', 'stale-issue-detection']);
    assert.equal(payload.title, 'Revalidate ignored test: Tests.FeatureTests.Works');
    assert.equal(payload.owner, 'dotnet');
    assert.equal(payload.repo, 'sdk');
    assert.deepEqual(payload.request, { retries: 0 });
    for (const expected of [
        'Target branch: **main**', 'potentially stale, not proven obsolete', 'No tests were run',
        'No root cause is claimed', 'Fully qualified test: `Tests.FeatureTests.Works`',
        `https://github.com/dotnet/sdk/blob/${headSha}/test/Tests/FeatureTests.cs#L42-L42`,
        input.sourceExcerpt, 'issue closed as completed at 2026-08-01T12:00:00Z',
        `Original source URL: <${blocker}?source=repo#x>`, input.additionalConditions[0],
        'Remove the relevant Ignore', 'run-tests skill', 'tests actually execute',
        'selecting zero tests', 'retain the new failure evidence',
    ]) {
        assert.ok(payload.body.includes(expected), expected);
    }
    assert.match(payload.body, /^<!-- stale-reference:v1:[a-f0-9]{64} -->/);
    assert.match(payload.body, /<!-- stale-reference-evidence:v1:[a-f0-9]{64} -->/);
    assert.ok(!payload.body.includes('MODEL'));
    assert.equal(api.calls.lists.filter(call => call.state === 'open').length, 2);
});

test('TODO/workaround tasks check prerequisites/backcompat', async () => {
    for (const kind of ['todo', 'workaround']) {
        const input = action({
            kind, path: 'src/Tasks/Fix.cs', anchor: 'Namespace.Fix.Apply', testNames: [],
            sourceExcerpt: `// ${kind}: remove when ${blocker} is consumed`,
        });
        const proposal = await proposalFor(input);
        assert.deepEqual(proposal.labels, ['cookie', 'agentic-workflows', 'stale-issue-detection']);
        assert.ok(proposal.body.includes('across supported versions'));
        assert.ok(proposal.body.includes('fixed-version consumption'));
        assert.ok(proposal.body.includes('Owning anchor: `Namespace.Fix.Apply`'));
        assert.ok(proposal.body.includes('smallest appropriate regression check'));
        const testProposal = await proposalFor({ ...input, path: 'test/Tests/Fix.cs' });
        assert.deepEqual(testProposal.labels, ['cookie', 'agentic-workflows', 'stale-issue-detection']);
    }
});

test('identity excludes line, checkout, reference URL, data rows, and arbitrary model IDs', async () => {
    const input = action();
    assert.deepEqual(targetIds(repository, input), targetIds('DotNet/SDK', {
        ...input, candidateId: 'untrusted-id', startLine: 100, endLine: 101,
        urls: ['https://github.com/dotnet/runtime/issues/999'],
        sourceExcerpt: '[DataRow(123)]\n[Ignore("another blocker")]',
    }));
    const first = await proposalFor(input);
    const moved = (await run(mock(), [{ ...input, startLine: 100, endLine: 100 }], {
        dryRun: true, headSha: 'b'.repeat(40),
    })).proposed[0];
    assert.equal(first.fingerprint, moved.fingerprint);
    assert.notEqual(first.body, moved.body);
    assert.notDeepEqual(targetIds(repository, input), targetIds(repository, {
        ...input, testNames: ['Tests.FeatureTests.Other'],
    }));
});

test('comment identity uses normalized actionable source and owning anchor, not location/context', () => {
    const input = action({
        kind: 'todo', path: 'src/File.cs', anchor: 'N.C.M', testNames: [],
        sourceExcerpt: '// TODO remove this\n// when fixed',
    });
    assert.equal(normalizeActionSource(input.sourceExcerpt), 'TODO remove this when fixed');
    assert.deepEqual(targetIds(repository, input), targetIds(repository, {
        ...input, startLine: 500, endLine: 501, sourceExcerpt: '  /* TODO remove this when fixed */ ',
    }));
    assert.notDeepEqual(targetIds(repository, input), targetIds(repository, {
        ...input, anchor: 'N.C.Other',
    }));
    assert.notDeepEqual(targetIds(repository, input), targetIds(repository, {
        ...input, sourceExcerpt: '// TODO remove this only after another prerequisite',
    }));
});

test('all blockers qualify; open/not-planned/unknown/failed/empty references suppress filing', async () => {
    const extra = 'https://github.com/dotnet/roslyn/issues/2';
    for (const second of [
        { state: 'open' },
        { ...completed, state_reason: 'not_planned' },
        { ...completed, state_reason: null },
        null,
    ]) {
        const api = mock({ get: async args => {
            if (args.repo === 'roslyn') {
                if (!second) {
                    throw { status: 404 };
                }
                return second;
            }
            return completed;
        } });
        const result = await run(api, [action({ urls: [blocker, extra] })]);
        assert.equal(result.created.length, 0);
        assert.equal(result.skipped[0].reason, 'unresolved-blockers');
        assert.equal(api.calls.writes.length, 0);
    }
    assert.equal((await run(mock(), [action({ urls: [] })])).created.length, 0);
    assert.equal((await run(mock(), [action({ urls: [blocker, extra] })])).created.length, 1);
});

test('duplicate records union blockers and conditions rather than losing unresolved constraints', async () => {
    const second = action({
        candidateId: 'candidate-2', urls: ['https://github.com/dotnet/roslyn/issues/2'],
        additionalConditions: ['Requires an SDK version update.'],
    });
    const merged = mergeActions(repository, [action(), second, action()]);
    assert.equal(merged.length, 1);
    assert.equal(merged[0].urls.length, 2);
    assert.deepEqual(merged[0].candidateIds, ['candidate-1', 'candidate-2']);
    assert.deepEqual(merged[0].additionalConditions, second.additionalConditions);
    const api = mock({ get: async args => args.repo === 'roslyn' ? { state: 'open' } : completed });
    assert.equal((await run(api, [action(), second])).created.length, 0);
    assert.equal(api.calls.reads.length, 2);
    const passing = mock();
    const result = await run(passing, [action(), second, action()]);
    assert.equal(result.created.length, 1);
    assert.equal(passing.calls.reads.length, 2);
    assert.ok(result.created[0].body.includes(second.additionalConditions[0]));
});

test('duplicate ordering does not change the merged actionable kind or evidence', async () => {
    const todo = action({
        kind: 'todo', testNames: [], sourceExcerpt: `// TODO remove workaround ${blocker}`,
    });
    const workaround = { ...todo, candidateId: 'second', kind: 'workaround' };
    const forward = await run(mock(), [todo, workaround], { dryRun: true });
    const reverse = await run(mock(), [workaround, todo], { dryRun: true });
    assert.deepEqual(forward.proposed, reverse.proposed);
});

test('same reference for different tests produces different tasks but only one remote lookup', async () => {
    const api = mock();
    const result = await run(api, [
        action(), action({ candidateId: 'second', testNames: ['Tests.FeatureTests.Other'] }),
    ]);
    assert.equal(result.created.length, 2);
    assert.equal(api.calls.reads.length, 1);
    assert.notDeepEqual(result.created[0].targetIds, result.created[1].targetIds);
});

test('merged PR task records the original issues URL and authoritative merge date', async () => {
    const api = mock({ get: async () => ({ ...completed, pull_request: {} }) });
    api.github.rest.pulls.get = async () => ({
        data: { state: 'closed', merged_at: '2026-07-31T12:00:00Z', closed_at: closedAt },
    });
    const result = await run(api);
    assert.equal(result.created.length, 1);
    assert.ok(result.created[0].body.includes('https://github.com/dotnet/runtime/pull/123'));
    assert.ok(result.created[0].body.includes(`Original source URL: <${blocker}>`));
    assert.ok(result.created[0].body.includes('pull request merged at 2026-07-31T12:00:00Z'));
});

test('body marker survives mutable title/labels, pagination, and absent interpretation cache', async () => {
    const proposal = await proposalFor();
    const api = mock({
        open: [
            ...Array.from({ length: 110 }, (_, i) => ({
                number: i + 1, state: 'open', body: 'unrelated',
            })),
            { number: 111, state: 'open', title: 'Renamed by a maintainer', labels: [], body: proposal.body },
        ],
    });
    const result = await run(api);
    assert.equal(result.skipped[0].reason, 'open-duplicate');
    assert.equal(result.skipped[0].number, 111);
    assert.equal(api.calls.writes.length, 0);
    assert.ok(api.calls.lists.filter(call => call.state === 'open').every(call => !('labels' in call)));
});

test('unmarked legacy task uses exact FQN, not shared references or partial method names', async () => {
    for (const body of [
        'Please re-enable `Tests.FeatureTests.Works`',
        'Please re-enable Tests.FeatureTests.Works on Linux.',
    ]) {
        const result = await run(mock({ open: [{ number: 7, state: 'open', body }] }));
        assert.equal(result.skipped[0].reason, 'open-duplicate');
    }
    for (const body of [
        `Fix ${blocker}`, 'Tests.FeatureTests.WorksSomething', 'Other.Tests.FeatureTests.Works',
    ]) {
        assert.equal((await run(mock({ open: [{ number: 7, state: 'open', body }] }))).created.length, 1);
    }
    const titleOnly = await run(mock({ open: [{
        number: 7, state: 'open', title: 'Re-enable Tests.FeatureTests.Works', body: null,
    }] }));
    assert.equal(titleOnly.skipped[0].reason, 'open-duplicate');
});

test('ambiguous FQN across paths requires the path for legacy dedup', async () => {
    const other = action({ candidateId: 'other', path: 'test/OtherTests/FeatureTests.cs' });
    const api = mock({ open: [{
        number: 7, state: 'open', body: 'Re-enable Tests.FeatureTests.Works in test/OtherTests/FeatureTests.cs',
    }] });
    const result = await run(api, [action(), other]);
    assert.equal(result.created.length, 1);
    assert.equal(result.skipped.length, 1);
    assert.deepEqual(result.skipped[0].candidateIds, ['other']);
});

test('legacy comment task requires exact source comment and path, not the blocker alone', async () => {
    const input = action({
        kind: 'todo', path: 'src/File.cs', anchor: 'N.C.M', testNames: [],
        sourceExcerpt: `// TODO remove the fallback after ${blocker}`,
    });
    const duplicate = mock({ open: [{
        number: 7, state: 'open', body: `Review src/File.cs:\n${input.sourceExcerpt}`,
    }] });
    assert.equal((await run(duplicate, [input])).skipped[0].reason, 'open-duplicate');
    const unrelated = mock({ open: [{ number: 7, state: 'open', body: `src/File.cs: ${blocker}` }] });
    assert.equal((await run(unrelated, [input])).created.length, 1);
});

test('class ignore markers cover each member; any existing member suppresses the entire group', async () => {
    const input = action({
        anchor: 'Tests.FeatureTests',
        testNames: ['Tests.FeatureTests.Works', 'Tests.FeatureTests.Other'],
    });
    const proposal = await proposalFor(input);
    assert.equal([...proposal.body.matchAll(/^<!-- stale-reference:v1:/gm)].length, 2);
    assert.equal(proposal.targetIds.length, 2);
    const memberProposal = await proposalFor();
    const api = mock({ open: [{ number: 7, state: 'open', body: memberProposal.body }] });
    const result = await run(api, [input]);
    assert.equal(result.skipped[0].reason, 'open-duplicate');
    assert.equal(result.created.length, 0);
    const combined = mergeActions(repository, [input, action(), action({
        candidateId: 'overlap', testNames: ['Tests.FeatureTests.Other', 'Tests.FeatureTests.Third'],
    })]);
    assert.equal(combined.length, 1);
    assert.equal(combined[0].ids.length, 3);
});

test('dry-run computes identical payloads with strictly zero mutation calls', async () => {
    const previewApi = mock({ create: async () => assert.fail('Dry-run attempted mutation') });
    const preview = await run(previewApi, [action()], { dryRun: true });
    const live = await run(mock());
    assert.deepEqual(preview.proposed, live.proposed);
    assert.deepEqual(preview.created, []);
    assert.deepEqual(previewApi.calls.writes, []);
});

test('five-issue cap is hardcoded and excludes duplicates across cached/fresh/repeated actions', async () => {
    const actions = Array.from({ length: 8 }, (_, i) => action({
        candidateId: `candidate-${i}`, testNames: [`Tests.FeatureTests.Case${i}`],
    }));
    const api = mock();
    const result = await run(api, [...actions, ...actions], { maximumOpenIssues: 100 });
    assert.equal(result.created.length, 5);
    assert.equal(api.calls.writes.length, 5);
    assert.equal(result.skipped.filter(skip => skip.reason === 'open-issue-cap').length, 3);
    assert.equal(api.calls.reads.length, 1);
    const dry = await run(mock(), [...actions, ...actions], { dryRun: true });
    assert.equal(dry.proposed.length, 5);
    assert.equal(dry.skipped.filter(skip => skip.reason === 'open-issue-cap').length, 3);
});

test('the cap is based on total open cap-labeled issues across runs, not this run\'s creations', async () => {
    const existing = Array.from({ length: 5 }, (_, i) => ({
        number: 500 + i, state: 'open', body: `pre-existing ${i}`, labels: ['stale-issue-detection'],
    }));
    const api = mock({ open: existing });
    const result = await run(api, [action()]);
    assert.equal(result.created.length, 0);
    assert.equal(api.calls.writes.length, 0);
    assert.equal(result.skipped[0].reason, 'open-issue-cap');
});

test('closing a capped issue frees a slot on a later run', async () => {
    const existing = Array.from({ length: 5 }, (_, i) => ({
        number: 500 + i, state: 'open', body: `pre-existing ${i}`, labels: ['stale-issue-detection'],
    }));
    const api = mock({ open: existing });
    assert.equal((await run(api, [action()])).skipped[0].reason, 'open-issue-cap');
    api.state.open = api.state.open.filter(issue => issue.number !== 500);
    const result = await run(api, [action()]);
    assert.equal(result.created.length, 1);
});

test('open issues without the cap label do not count toward the cap', async () => {
    const existing = Array.from({ length: 5 }, (_, i) => ({
        number: 500 + i, state: 'open', body: `pre-existing ${i}`, labels: ['some-other-label'],
    }));
    const api = mock({ open: existing });
    const result = await run(api, [action()]);
    assert.equal(result.created.length, 1);
    assert.deepEqual(api.calls.writes[0].labels, ['cookie', 'agentic-workflows', 'stale-issue-detection']);
});

test('a second run creates no duplicates and reevaluates reference state', async () => {
    const api = mock();
    assert.equal((await run(api)).created.length, 1);
    const second = await run(api);
    assert.equal(second.created.length, 0);
    assert.equal(second.skipped[0].reason, 'open-duplicate');
    assert.equal(api.calls.reads.length, 2);
    assert.equal(api.calls.writes.length, 1);
});

test('initial listing failure in either open or closed history fails closed before any writes', async () => {
    for (const failingState of ['open', 'closed']) {
        const api = mock({ paginate: async args => {
            if (args.state === failingState) {
                throw { status: 403 };
            }
            return [];
        } });
        const result = await run(api);
        assert.equal(api.calls.writes.length, 0);
        assert.equal(result.diagnostics[0].code, 'tracking-list-failed');
        assert.equal(result.skipped[0].reason, 'tracking-unavailable');
    }
});

test('refresh immediately before POST sees a new issue and suppresses creation', async () => {
    const proposal = await proposalFor();
    let openReads = 0;
    const api = mock({ paginate: async args => {
        if (args.state === 'open' && ++openReads === 2) {
            return [{ number: 7, state: 'open', body: proposal.body }];
        }
        return [];
    } });
    const result = await run(api);
    assert.equal(api.calls.writes.length, 0);
    assert.equal(result.skipped[0].number, 7);
});

test('refresh failure halts writes rather than trusting a stale successful listing', async () => {
    let openReads = 0;
    const api = mock({ paginate: async args => {
        if (args.state === 'open' && ++openReads === 2) {
            throw { status: 404 };
        }
        return [];
    } });
    const result = await run(api, [
        action(), action({ candidateId: 'another', testNames: ['Tests.FeatureTests.Other'] }),
    ]);
    assert.equal(api.calls.writes.length, 0);
    assert.equal(result.diagnostics[0].code, 'tracking-refresh-failed');
    assert.ok(result.skipped.some(skip => skip.reason === 'filing-halted'));
});

test('successful creates are remembered even if a later list is temporarily stale', async () => {
    const api = mock({ paginate: async () => [] });
    const result = await run(api, [action(), action()]);
    assert.equal(result.created.length, 1);
    assert.equal(api.calls.writes.length, 1);
});

test('ambiguous POST success is reconciled by markers without sending a second POST', async () => {
    const api = mock({ create: async (args, state) => {
        state.open.push({ number: 7, state: 'open', body: args.body });
        throw { status: 502 };
    } });
    const result = await run(api);
    assert.equal(api.calls.writes.length, 1);
    assert.equal(result.created.length, 1);
    assert.equal(result.created[0].number, 7);
    assert.equal(result.created[0].reconciled, true);
    assert.equal(result.diagnostics[0].code, 'create-failed');
});

test('unreconciled POST failure halts remaining filing; no automatic POST retry', async () => {
    const api = mock({ create: async () => { throw { status: 502 }; } });
    const result = await run(api, [
        action(), action({ candidateId: 'other', testNames: ['Tests.FeatureTests.Other'] }),
    ]);
    assert.equal(api.calls.writes.length, 1);
    assert.equal(result.created.length, 0);
    assert.ok(result.skipped.some(skip => skip.reason === 'create-outcome-unknown'));
    assert.ok(result.skipped.some(skip => skip.reason === 'filing-halted'));
});

test('partial create failure retains previous successes and halts the remaining budget', async () => {
    const api = mock({ create: async (args, state, calls) => {
        if (calls.writes.length === 2) {
            throw { status: 502 };
        }
        const issue = { number: 7, state: 'open', body: args.body };
        state.open.push(issue);
        return { data: issue };
    } });
    const inputs = Array.from({ length: 4 }, (_, index) => action({
        candidateId: `candidate-${index}`, testNames: [`Tests.FeatureTests.Case${index}`],
    }));
    const result = await run(api, inputs);
    assert.equal(api.calls.writes.length, 2);
    assert.equal(result.created.length, 1);
    assert.equal(result.created[0].number, 7);
    assert.equal(result.skipped.filter(skip => skip.reason === 'create-outcome-unknown').length, 1);
    assert.equal(result.skipped.filter(skip => skip.reason === 'filing-halted').length, 2);
});

test('reconciled creates count against the same hard five-issue cap', async () => {
    const api = mock({ create: async (args, state, calls) => {
        state.open.push({ number: 100 + calls.writes.length, state: 'open', body: args.body });
        throw { status: 502 };
    } });
    const inputs = Array.from({ length: 7 }, (_, index) => action({
        candidateId: `candidate-${index}`, testNames: [`Tests.FeatureTests.Case${index}`],
    }));
    const result = await run(api, inputs);
    assert.equal(api.calls.writes.length, 5);
    assert.equal(result.created.length, 5);
    assert.ok(result.created.every(created => created.reconciled));
    assert.equal(result.skipped.filter(skip => skip.reason === 'open-issue-cap').length, 2);
});

test('failed ambiguous-create reconciliation also fails closed', async () => {
    const api = mock({
        create: async () => { throw new Error('timeout'); },
        paginate: async (args, state, calls) => {
            if (calls.writes.length) {
                throw { status: 403 };
            }
            return [];
        },
    });
    const result = await run(api);
    assert.equal(api.calls.writes.length, 1);
    assert.deepEqual(result.diagnostics.map(diagnostic => diagnostic.code),
        ['create-failed', 'create-reconciliation-failed']);
    assert.equal(result.created.length, 0);
});

test('unchanged closed tracking task suppresses refiling despite moved lines or new checkout', async () => {
    const proposal = await proposalFor();
    const api = mock({ closed: [{
        number: 7, state: 'closed', title: 'Declined', body: proposal.body,
    }] });
    const result = await run(api, [action({ startLine: 80, endLine: 80 })], { headSha: 'b'.repeat(40) });
    assert.equal(api.calls.writes.length, 0);
    assert.equal(result.skipped[0].reason, 'unchanged-closed-task');
    assert.equal(result.skipped[0].number, 7);
    assert.ok(api.calls.lists.some(call => call.state === 'closed' && call.labels === 'agentic-workflows'));
});

test('closed TODO history ignores comment formatting but preserves new prerequisites', async () => {
    const input = action({
        kind: 'todo', path: 'src/Fix.cs', anchor: 'N.C.M', testNames: [],
        sourceExcerpt: `// TODO remove when ${blocker} is consumed`,
    });
    const proposal = await proposalFor(input);
    const closed = [{ number: 7, state: 'closed', body: proposal.body }];
    const moved = await run(mock({ closed }), [{
        ...input, startLine: 100, endLine: 101,
        sourceExcerpt: `/* TODO remove when\n * ${blocker} is consumed */`,
    }]);
    assert.equal(moved.skipped[0].reason, 'unchanged-closed-task');
    const changed = await run(mock({ closed }), [{
        ...input, additionalConditions: ['Requires compatible packages for all supported frameworks.'],
    }]);
    assert.equal(changed.created.length, 1);
    assert.ok(changed.created[0].body.includes('https://github.com/dotnet/sdk/issues/7 (closed)'));
});

test('changed source or reference-resolution evidence permits a new task with closed history', async () => {
    const proposal = await proposalFor();
    for (const [input, get] of [
        [action({ sourceExcerpt: `[Ignore("Still waiting: ${blocker}")]` }), undefined],
        [action(), async () => ({ ...completed, closed_at: '2026-09-01T12:00:00Z' })],
        [action({ additionalConditions: ['Now also requires .NET 12.'] }), undefined],
    ]) {
        const api = mock({ closed: [{ number: 7, state: 'closed', body: proposal.body }], get });
        const result = await run(api, [input]);
        assert.equal(result.created.length, 1);
        assert.ok(result.created[0].body.includes('## Previous tracking history'));
        assert.ok(result.created[0].body.includes('https://github.com/dotnet/sdk/issues/7 (closed)'));
        assert.notEqual(result.created[0].fingerprint, proposal.fingerprint);
        assert.deepEqual(result.created[0].targetIds, proposal.targetIds);
    }
});

test('changed comment source has a different identity; shared upstream reference is not a duplicate', async () => {
    const previous = action({
        kind: 'todo', path: 'src/File.cs', anchor: 'N.C.M', testNames: [],
        sourceExcerpt: `// TODO ${blocker}: remove workaround`,
    });
    const proposal = await proposalFor(previous);
    const api = mock({ open: [{ number: 7, state: 'open', body: proposal.body }] });
    const result = await run(api, [{ ...previous, sourceExcerpt: `// TODO ${blocker}: remove a different workaround` }]);
    assert.equal(result.created.length, 1);
    assert.notDeepEqual(result.created[0].targetIds, proposal.targetIds);
    const historyApi = mock({ closed: [{ number: 7, state: 'closed', body: proposal.body }] });
    const withHistory = await run(historyApi, [{
        ...previous, sourceExcerpt: `// TODO ${blocker}: remove an updated workaround`,
    }]);
    assert.equal(withHistory.created.length, 1);
    assert.ok(withHistory.created[0].body.includes('https://github.com/dotnet/sdk/issues/7 (closed)'));
});

test('source excerpts containing code fences remain verbatim inside a longer fence', async () => {
    const input = action({ sourceExcerpt: `// TODO \`\`\` ${blocker}\n// \`another line\`` });
    const proposal = await proposalFor(input);
    assert.ok(proposal.body.includes(`\`\`\`\`text\n${input.sourceExcerpt}\n\`\`\`\``));
});

test('missing explicit dryRun/pinned SHA and class ignores without member identities fail before APIs', async () => {
    const api = mock();
    await assert.rejects(run(api, [action()], { dryRun: undefined }));
    await assert.rejects(run(api, [action()], { headSha: 'main' }));
    await assert.rejects(run(api, [action({ testNames: [] })]));
    assert.equal(api.calls.reads.length, 0);
    assert.equal(api.calls.writes.length, 0);
});

test('diagnostics support actions core warning and console warn, preferring warning', async () => {
    for (const method of ['warning', 'warn']) {
        const messages = [];
        const core = {
            [method](message) {
                assert.equal(this, core);
                messages.push(JSON.parse(message));
            },
            ...(method === 'warning' ? { warn() { assert.fail('Prefer core.warning'); } } : {}),
        };
        const api = mock({ get: async () => { throw { status: 404 }; } });
        const result = await run(api, [action()], { logger: core });
        assert.deepEqual(messages, result.diagnostics);
        assert.equal(messages.length, 1);
        assert.equal(messages[0].code, 'reference-lookup-failed');
    }
});
