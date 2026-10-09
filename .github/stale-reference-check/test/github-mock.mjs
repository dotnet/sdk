// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from 'node:assert/strict';

export const closedAt = '2026-08-01T12:00:00Z';
export const completed = { state: 'closed', state_reason: 'completed', closed_at: closedAt };

// In-memory stand-in for the github-script client. Handlers return response data (not `{ data }`);
// `calls` separates reference/tracking reads, listings, creates (`writes`) and updates.
export function createGitHubMock({
    open = [], closed = [], issue, pull, paginate, create, update, getTracking, trackingRepository = 'dotnet/sdk',
} = {}) {
    const calls = { reads: [], pulls: [], lists: [], writes: [], updates: [], trackingReads: [] };
    const state = { open: [...open], closed: [...closed] };
    const github = {
        rest: {
            issues: {
                get: async args => {
                    const tracking = `${args.owner}/${args.repo}` === trackingRepository
                        ? [...state.open, ...state.closed].find(item => item.number === args.issue_number) : null;
                    if (tracking) {
                        calls.trackingReads.push(args);
                        return { data: getTracking ? await getTracking(args, state, calls) : { ...tracking } };
                    }
                    calls.reads.push(args);
                    return { data: issue ? await issue(args) : completed };
                },
                listForRepo() {},
                create: async args => {
                    calls.writes.push(args);
                    if (create) {
                        return create(args, state, calls);
                    }
                    const created = {
                        number: 1000 + calls.writes.length, title: args.title,
                        body: args.body, labels: args.labels, state: 'open',
                    };
                    state.open.push(created);
                    return { data: created };
                },
                update: async args => {
                    calls.updates.push(args);
                    if (update) return update(args, state, calls);
                    const current = state.open.find(item => item.number === args.issue_number);
                    assert.ok(current, 'Updated issue is not open');
                    Object.assign(current, { body: args.body });
                    return { data: { ...current } };
                },
            },
            pulls: {
                get: async args => {
                    calls.pulls.push(args);
                    if (!pull) {
                        throw new Error('Unexpected PR lookup');
                    }
                    return { data: await pull(args) };
                },
            },
        },
        paginate: async (route, args) => {
            assert.equal(route, github.rest.issues.listForRepo);
            calls.lists.push(args);
            return paginate ? paginate(args, state, calls) : [...state[args.state]];
        },
    };
    return { github, calls, state };
}
