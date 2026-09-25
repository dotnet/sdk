// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { setTimeout } from 'node:timers/promises';

const ownerPattern = '[a-z0-9](?:[a-z0-9-]{0,37}[a-z0-9])?';
const repositoryPattern = '[a-z0-9_.-]{1,100}';
const repositoryRegex = new RegExp(`^(${ownerPattern})/(${repositoryPattern})$`, 'i');
const referenceRegex = new RegExp(
    `^https://github\\.com/(${ownerPattern})/(${repositoryPattern})/(issues|pull)/([1-9][0-9]*)/?(?:[?#][^\\s<>]*)?$`, 'i');
const readAttempts = 3;
const readConcurrency = 4;
const maximumRetryDelayMs = 2000;

export function normalizeRepository(repository) {
    const match = typeof repository === 'string' && repositoryRegex.exec(repository);
    if (!match || match[1].includes('--') || ['.', '..'].includes(match[2])) {
        throw new Error('Expected a GitHub owner/repository.');
    }
    return { owner: match[1].toLowerCase(), repo: match[2].toLowerCase() };
}

export function normalizeReference(original) {
    const match = typeof original === 'string' && referenceRegex.exec(original);
    if (!match) {
        throw new Error('Expected an HTTPS github.com issue or pull-request URL.');
    }
    // Canonical keys intentionally discard route, query, and fragment because
    // GitHub may expose the same item through /issues/ and /pull/ URLs.
    const { owner, repo } = normalizeRepository(`${match[1]}/${match[2]}`);
    const number = Number(match[4]);
    if (!Number.isSafeInteger(number)) {
        throw new Error('GitHub reference number is out of range.');
    }
    return {
        original, owner, repo, number,
        key: `${owner}/${repo}/${number}`,
        url: `https://github.com/${owner}/${repo}/${match[3].toLowerCase()}/${number}`,
    };
}

function retryDelay(error, attempt) {
    const status = error?.status ?? error?.response?.status;
    const headers = error?.response?.headers ?? {};
    const rateLimited = status === 429 ||
        (status === 403 && (headers['x-ratelimit-remaining'] === '0' || headers['retry-after'] !== undefined));
    if (!rateLimited && ![500, 502, 503, 504].includes(status)) {
        return null;
    }
    const retryAfter = headers['retry-after'];
    let delay = 250 * (2 ** attempt);
    if (retryAfter !== undefined) {
        const seconds = Number(retryAfter);
        delay = Number.isFinite(seconds) ? seconds * 1000 : Date.parse(retryAfter) - Date.now();
    } else if (rateLimited && headers['x-ratelimit-reset'] !== undefined) {
        delay = Number(headers['x-ratelimit-reset']) * 1000 - Date.now();
    }
    // A long rate-limit wait is deferred to a later run, not retried prematurely.
    return Number.isFinite(delay) && delay <= maximumRetryDelayMs ? Math.max(0, delay) : null;
}

async function readWithRetry(operation, sleep) {
    for (let attempt = 0; ; attempt++) {
        try {
            return await operation();
        } catch (error) {
            const delay = retryDelay(error, attempt);
            if (attempt + 1 >= readAttempts || delay === null) {
                throw error;
            }
            await sleep(delay);
        }
    }
}

function limiter() {
    let active = 0;
    const waiting = [];
    return async operation => {
        if (active >= readConcurrency) {
            await new Promise(resolve => waiting.push(resolve));
        } else {
            active++;
        }
        try {
            return await operation();
        } finally {
            const next = waiting.shift();
            if (next) {
                next();
            } else {
                active--;
            }
        }
    };
}

function validDate(value) {
    return typeof value === 'string' &&
        /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?Z$/.test(value) && Number.isFinite(Date.parse(value));
}

export function errorDiagnostic(code, error, details = {}) {
    const status = error?.status ?? error?.response?.status;
    return { code, ...details, ...(Number.isInteger(status) ? { status } : {}) };
}

export function createReferenceResolver({ github, sleep = setTimeout }) {
    const cache = new Map();
    const limit = limiter();

    async function lookup(reference) {
        const { owner, repo, number, key } = reference;
        try {
            const { data: issue } = await readWithRetry(
                () => github.rest.issues.get({ owner, repo, issue_number: number, request: { retries: 0 } }), sleep);
            if (!issue || !['open', 'closed'].includes(issue.state)) {
                throw new Error('Invalid issue response.');
            }
            if (issue.pull_request) {
                const { data: pull } = await readWithRetry(
                    () => github.rest.pulls.get({ owner, repo, pull_number: number, request: { retries: 0 } }), sleep);
                if (!pull || !['open', 'closed'].includes(pull.state) ||
                    (pull.merged_at !== null && !validDate(pull.merged_at))) {
                    throw new Error('Invalid pull-request response.');
                }
                return {
                    key, kind: 'pull', url: `https://github.com/${owner}/${repo}/pull/${number}`,
                    known: true, qualifies: validDate(pull.merged_at),
                    state: pull.state, stateReason: null,
                    closedAt: validDate(pull.closed_at) ? pull.closed_at : null, mergedAt: pull.merged_at,
                };
            }
            const qualifies = issue.state === 'closed' && issue.state_reason === 'completed';
            if (qualifies && !validDate(issue.closed_at)) {
                throw new Error('Completed issue has no valid closure date.');
            }
            return {
                key, kind: 'issue', url: `https://github.com/${owner}/${repo}/issues/${number}`,
                known: true, qualifies, state: issue.state, stateReason: issue.state_reason ?? null,
                closedAt: validDate(issue.closed_at) ? issue.closed_at : null, mergedAt: null,
            };
        } catch (error) {
            return {
                key, known: false, qualifies: false,
                diagnostic: errorDiagnostic('reference-lookup-failed', error, { reference: key }),
            };
        }
    }

    async function resolve(original) {
        let reference;
        try {
            reference = normalizeReference(original);
        } catch {
            return {
                original, known: false, qualifies: false,
                diagnostic: { code: 'invalid-reference', original },
            };
        }
        if (!cache.has(reference.key)) {
            cache.set(reference.key, limit(() => lookup(reference)));
        }
        return { ...await cache.get(reference.key), original };
    }

    async function resolveAll(urls) {
        const results = await Promise.all(urls.map(resolve));
        const references = new Map();
        const diagnostics = new Map();
        for (const result of results) {
            if (result.diagnostic) {
                diagnostics.set(result.key ?? result.original, result.diagnostic);
            }
            const key = result.key ?? result.original;
            if (!references.has(key)) {
                const { original, ...state } = result;
                references.set(key, { ...state, originalUrls: [] });
            }
            if (!references.get(key).originalUrls.includes(result.original)) {
                references.get(key).originalUrls.push(result.original);
            }
        }
        return { references: [...references.values()], diagnostics: [...diagnostics.values()] };
    }

    return { resolve, resolveAll };
}

// Open issues are fetched unfiltered by label so duplicate detection also catches
// issues filed manually by humans (who won't have applied 'agentic-workflows'); closed
// issues are filtered to that label because only workflow-created issues carry
// reusable history for suppressing refiled duplicates. See README.md's "Eligibility and
// duplicate protection" section for the full rationale.
export async function listRepositoryIssues({ github, repository, state, sleep = setTimeout }) {
    if (!['open', 'closed'].includes(state)) {
        throw new Error('Expected open or closed issues.');
    }
    const { owner, repo } = normalizeRepository(repository);
    const issues = await readWithRetry(() => github.paginate(github.rest.issues.listForRepo, {
        owner, repo, state, per_page: 100, request: { retries: 0 },
        ...(state === 'closed' ? { labels: 'agentic-workflows' } : {}),
    }), sleep);
    if (!Array.isArray(issues) || issues.some(issue =>
        !issue || !Number.isSafeInteger(issue.number) || issue.number <= 0 ||
        issue.state !== state || !(issue.body === null || typeof issue.body === 'string'))) {
        throw new Error('Incomplete or malformed issue listing.');
    }
    return issues.filter(issue => !issue.pull_request);
}
