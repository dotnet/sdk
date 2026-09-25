// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { createHash } from 'node:crypto';
import {
    createReferenceResolver, errorDiagnostic, listRepositoryIssues, normalizeRepository,
} from './github.mjs';

// Caps total open filing volume across runs (not per-run), so a run resumes filing once
// a human closes enough of the existing tracking issues to fall back under the cap.
const maximumOpenIssues = 5;
const capLabel = 'stale-issue-detection';
const markerPattern = /^<!-- stale-reference:v1:([a-f0-9]{64}) -->$/gm;
const evidencePattern = /^<!-- stale-reference-evidence:v1:([a-f0-9]{64}) -->$/gm;

function hash(value) {
    return createHash('sha256').update(JSON.stringify(value)).digest('hex');
}

function unique(values) {
    return [...new Set(values)].sort();
}

export function normalizeActionSource(source) {
    return source.split(/\r?\n/).map(line => line.trim()
        .replace(/^(?:\/\/+|\/\*+|\*+\/?|<!--|')\s?/, '')
        .replace(/\s?(?:\*\/|-->)$/, '')).join(' ').replace(/\s+/g, ' ').trim();
}

export function targetIds(repository, action) {
    const { owner, repo } = normalizeRepository(repository);
    const prefix = ['stale-reference', 1, `${owner}/${repo}`, action.path];
    if (action.kind === 'ignore') {
        return unique(action.testNames.map(name => hash([...prefix, 'test', name])));
    }
    return [hash([...prefix, 'comment', action.anchor, normalizeActionSource(action.sourceExcerpt)])];
}

function marker(id) {
    return `<!-- stale-reference:v1:${id} -->`;
}

function sourceOf(action) {
    return {
        anchor: action.anchor, startLine: action.startLine, endLine: action.endLine,
        excerpt: action.sourceExcerpt,
    };
}

function mergeGroups(groups) {
    const first = groups[0];
    return {
        ...first,
        kind: groups.some(group => group.kind === 'workaround') ? 'workaround' : first.kind,
        candidateIds: unique(groups.flatMap(group => group.candidateIds)),
        ids: unique(groups.flatMap(group => group.ids)),
        testNames: unique(groups.flatMap(group => group.testNames)),
        urls: unique(groups.flatMap(group => group.urls)),
        additionalConditions: unique(groups.flatMap(group => group.additionalConditions)),
        sources: [...new Map(groups.flatMap(group => group.sources).map(source =>
            [JSON.stringify(source), source])).values()].sort((a, b) =>
            a.startLine - b.startLine || a.excerpt.localeCompare(b.excerpt)),
    };
}

export function mergeActions(repository, actions) {
    const groups = [];
    for (const action of actions) {
        if (!['ignore', 'todo', 'workaround'].includes(action.kind) ||
            (action.kind === 'ignore' && !action.testNames?.length)) {
            throw new Error('Action has no unambiguous target.');
        }
        const group = {
            kind: action.kind, path: action.path, candidateIds: [action.candidateId],
            ids: targetIds(repository, action), testNames: unique(action.testNames ?? []),
            urls: unique(action.urls), additionalConditions: unique(action.additionalConditions),
            sources: [sourceOf(action)],
        };
        // Overlapping class/member ignores form one group, including transitive overlaps.
        const matching = groups.filter(existing => existing.ids.some(id => group.ids.includes(id)));
        for (const existing of matching) {
            groups.splice(groups.indexOf(existing), 1);
        }
        groups.push(mergeGroups([group, ...matching]));
    }
    return groups.sort((a, b) => a.path.localeCompare(b.path) || a.ids[0].localeCompare(b.ids[0]));
}

function matches(pattern, body) {
    return [...(body ?? '').matchAll(pattern)].map(match => match[1]);
}

function containsIdentity(body, identity) {
    let offset = -1;
    while ((offset = body.indexOf(identity, offset + 1)) !== -1) {
        const before = body[offset - 1] ?? '';
        const after = body[offset + identity.length] ?? '';
        if (!/[\w.+`]/.test(before) && !/[\w.+`]/.test(after)) {
            return true;
        }
        // Backticks delimit the most common visible identity representation.
        if (before === '`' && after === '`') {
            return true;
        }
    }
    return false;
}

// Labels may arrive as plain strings (tests, or a 'labels' query param echoed back) or as
// full label objects (the real GitHub REST response); accept either shape.
function hasCapLabel(labels) {
    return Array.isArray(labels) && labels.some(label =>
        (typeof label === 'string' ? label : label?.name) === capLabel);
}

export function findOpenDuplicate(issues, group, ambiguousTestNames = new Set()) {
    return issues.find(issue => {
        const body = issue.body ?? '';
        const ids = matches(markerPattern, body);
        if (ids.some(id => group.ids.includes(id))) {
            return true;
        }
        if (ids.length) {
            return false;
        }
        const visible = `${issue.title ?? ''}\n${body}`;
        if (group.kind === 'ignore') {
            return group.testNames.some(name => containsIdentity(visible, name) &&
                (!ambiguousTestNames.has(name) || visible.includes(group.path)));
        }
        return visible.includes(group.path) &&
            group.sources.some(source => body.includes(source.excerpt.trim()));
    });
}

function closedHistory(issues, group, fingerprint) {
    const exact = issues.filter(issue => matches(markerPattern, issue.body)
        .some(id => group.ids.includes(id)));
    // Changed comment text changes its target ID. Keep related owning-site history
    // visible, but never use that weaker association to suppress a new action.
    const related = group.kind === 'ignore' ? [] : issues.filter(issue => {
        const body = issue.body ?? '';
        return matches(markerPattern, body).length &&
            body.includes(`\nSource path: ${inlineCode(group.path)}\n`) &&
            group.sources.some(source => body.includes(`\nOwning anchor: ${inlineCode(source.anchor)}\n`));
    });
    const history = [...new Map([...exact, ...related].map(issue => [issue.number, issue])).values()];
    return {
        history,
        unchanged: exact.find(issue => matches(evidencePattern, issue.body).includes(fingerprint)),
    };
}

function evidenceFingerprint(group, references) {
    return hash({
        version: 1,
        kind: group.kind,
        ids: group.ids,
        source: unique(group.sources.map(source => normalizeActionSource(source.excerpt))),
        conditions: group.additionalConditions,
        references: references.map(reference => ({
            key: reference.key, kind: reference.kind, state: reference.state,
            stateReason: reference.stateReason, closedAt: reference.closedAt, mergedAt: reference.mergedAt,
        })).sort((a, b) => a.key.localeCompare(b.key)),
    });
}

function fenced(value) {
    const runs = value.match(/`+/g) ?? [];
    const fence = '`'.repeat(Math.max(3, ...runs.map(run => run.length + 1)));
    return `${fence}text\n${value}\n${fence}`;
}

function inlineCode(value) {
    const runs = value.match(/`+/g) ?? [];
    const ticks = '`'.repeat(Math.max(1, ...runs.map(run => run.length + 1)));
    const padding = value.startsWith('`') || value.endsWith('`') ? ' ' : '';
    return `${ticks}${padding}${value}${padding}${ticks}`;
}

function normalizeTargetBranch(targetBranch) {
    if (typeof targetBranch !== 'string' || !targetBranch ||
        /[\u0000-\u001f\u007f]/.test(targetBranch) || targetBranch.startsWith('/') ||
        targetBranch.endsWith('/') || targetBranch.includes('..')) {
        throw new Error('A valid target branch is required.');
    }
    return targetBranch;
}

export function buildProposal({ repository, targetBranch = 'main', headSha, group, references, history = [] }) {
    const { owner, repo } = normalizeRepository(repository);
    targetBranch = normalizeTargetBranch(targetBranch);
    const base = `https://github.com/${owner}/${repo}`;
    const encodedBranch = targetBranch.split('/').map(encodeURIComponent).join('/');
    const fingerprint = evidenceFingerprint(group, references);
    const title = (group.kind === 'ignore'
        ? (group.testNames.length === 1
            ? `Revalidate ignored test: ${group.testNames[0]}`
            : `Revalidate ignored tests in ${group.path} (${group.testNames.length} tests)`)
        : `Revalidate ${group.kind === 'todo' ? 'TODO' : 'workaround'} in ${group.path}`).slice(0, 256);
    // capLabel marks issues that count toward the total-open-issue filing cap; see
    // maximumOpenIssues below.
    const labels = ['cookie', 'agentic-workflows', capLabel];
    const path = group.path.split('/').map(encodeURIComponent).join('/');
    const body = [
        ...group.ids.map(marker),
        `<!-- stale-reference-evidence:v1:${fingerprint} -->`,
        '## Potentially stale reference — revalidation task',
        '',
        `Target branch: **${targetBranch}**. Follow-up kind: **${group.kind}**.`,
        '',
        'This site is potentially stale, not proven obsolete. All identified GitHub blockers are resolved as recorded below.',
        'No tests were run by this discovery workflow. Upstream resolution does not prove that a test passes,',
        'that a fixed dependency version is consumed, or that removing a workaround is safe. No root cause is claimed.',
        '',
        '## Canonical target',
        '',
        `Repository: \`${owner}/${repo}\``,
        `Source path: ${inlineCode(group.path)}`,
        ...group.ids.map(id => `Target ID: \`stale-reference:v1:${id}\``),
        ...(group.kind === 'ignore'
            ? group.testNames.map(name => `Fully qualified test: ${inlineCode(name)}`)
            : unique(group.sources.map(source => source.anchor)).map(anchor => `Owning anchor: ${inlineCode(anchor)}`)),
        '',
        '## Source evidence',
        '',
        ...group.sources.flatMap(source => [
            `[Commit-pinned source, lines ${source.startLine}–${source.endLine}](${base}/blob/${headSha}/${path}#L${source.startLine}-L${source.endLine})`,
            '', fenced(source.excerpt), '',
        ]),
        '## Verified reference resolution',
        '',
        ...references.flatMap(reference => [
            `- ${reference.url}: ${reference.kind === 'pull'
                ? `pull request merged at ${reference.mergedAt} (state: ${reference.state})`
                : `issue closed as completed at ${reference.closedAt}`}.`,
            ...reference.originalUrls.map(url => `  - Original source URL: <${url}>`),
        ]),
        '',
        '## Additional prerequisites (not verified)',
        '',
        ...(group.additionalConditions.length
            ? group.additionalConditions.flatMap(condition => [fenced(condition), ''])
            : ['No additional conditions were identified in the source; this does not establish that none exist.', '']),
        '## Follow-up',
        '',
        ...(group.kind === 'ignore' ? [
            `1. Work against \`${targetBranch}\`; check the source, all prerequisites above, dependency consumption, and required platform.`,
            '2. Remove the relevant Ignore for the listed tests, preserving unrelated ignores and conditions.',
            `3. Use the repository [run-tests skill](${base}/blob/${encodedBranch}/.github/skills/run-tests/SKILL.md)`,
            '   for the smallest relevant test selection on the required platform. Verify that the tests actually execute',
            '   rather than being skipped or selecting zero tests.',
            '4. If they pass, submit the focused re-enable change. If they fail, retain the new failure evidence in this',
            '   tracking task and address it here; do not assume the original root cause or silently discard the failures.',
        ] : [
            `1. Work against \`${targetBranch}\`; first verify the stated prerequisites, fixed-version consumption, and behavior`,
            '   across supported versions. Historical compatibility reasons may still require this code.',
            '2. Remove or update the TODO/workaround only when justified; otherwise retain it and record the evidence here.',
            `3. Cover a justified change with the smallest appropriate regression check. For SDK tests, use the`,
            `   [run-tests skill](${base}/blob/${encodedBranch}/.github/skills/run-tests/SKILL.md) and verify actual execution.`,
            '4. Retain failures and unresolved prerequisites in this tracking task; upstream closure is not proof of safety.',
        ]),
        ...(history.length ? [
            '', '## Previous tracking history', '',
            'Related tasks at this target or owning anchor were closed previously. The current actionable source,',
            'action, or reference-resolution evidence differs; these are history, not open duplicates.',
            ...history.map(issue => `- ${base}/issues/${issue.number} (closed)`),
        ] : []),
        '',
    ].join('\n');
    return { title, body, labels, targetIds: group.ids, candidateIds: group.candidateIds, fingerprint };
}

export async function finalize({
    github, repository, targetBranch = 'main', headSha, actions, dryRun, logger = console,
}) {
    normalizeRepository(repository);
    targetBranch = normalizeTargetBranch(targetBranch);
    if (!/^[a-f0-9]{40}$/i.test(headSha) || typeof dryRun !== 'boolean' || !Array.isArray(actions)) {
        throw new Error('A pinned checkout SHA, actions, and explicit dryRun boolean are required.');
    }
    const result = { created: [], proposed: [], skipped: [], diagnostics: [] };
    const report = diagnostic => {
        result.diagnostics.push(diagnostic);
        const warning = logger?.warning ?? logger?.warn;
        warning?.call(logger, JSON.stringify(diagnostic));
    };
    const groups = mergeActions(repository, actions);
    const namePaths = new Map();
    for (const group of groups) {
        for (const name of group.testNames) {
            if (!namePaths.has(name)) {
                namePaths.set(name, new Set());
            }
            namePaths.get(name).add(group.path);
        }
    }
    const ambiguousNames = new Set([...namePaths].filter(([, paths]) => paths.size > 1).map(([name]) => name));
    const resolver = createReferenceResolver({ github });
    const resolved = await Promise.all(groups.map(group => resolver.resolveAll(group.urls)));
    const reported = new Set();
    const eligible = [];
    for (let index = 0; index < groups.length; index++) {
        const group = groups[index];
        const { references, diagnostics } = resolved[index];
        for (const diagnostic of diagnostics) {
            const key = JSON.stringify(diagnostic);
            if (!reported.has(key)) {
                report(diagnostic);
                reported.add(key);
            }
        }
        if (!references.length || references.some(reference => !reference.known || !reference.qualifies)) {
            result.skipped.push({ candidateIds: group.candidateIds, reason: 'unresolved-blockers', references });
        } else {
            eligible.push({ group, references });
        }
    }
    if (!eligible.length) {
        return result;
    }
    async function readTracking() {
        // Both complete listings must succeed before any proposed mutation. Re-invoked
        // before each creation below (not just once) so a duplicate filed by a prior
        // iteration in this same run is visible before the next one is proposed; this
        // trades extra paginated API calls (bounded by maximumOpenIssues) for correctness.
        const [open, closed] = await Promise.all(['open', 'closed'].map(state =>
            listRepositoryIssues({ github, repository, state })));
        return { open, closed };
    }
    let tracking;
    try {
        tracking = await readTracking();
    } catch (error) {
        report(errorDiagnostic('tracking-list-failed', error));
        result.skipped.push(...eligible.map(({ group }) => ({
            candidateIds: group.candidateIds, reason: 'tracking-unavailable',
        })));
        return result;
    }
    const remembered = [];
    let halted = false;
    for (const { group, references } of eligible) {
        if (halted) {
            result.skipped.push({ candidateIds: group.candidateIds, reason: 'filing-halted' });
            continue;
        }
        function duplicate() {
            const open = findOpenDuplicate([...tracking.open, ...remembered], group, ambiguousNames);
            if (open) {
                return { candidateIds: group.candidateIds, reason: 'open-duplicate', number: open.number };
            }
            const closed = closedHistory(tracking.closed, group, evidenceFingerprint(group, references));
            if (closed.unchanged) {
                return { candidateIds: group.candidateIds, reason: 'unchanged-closed-task', number: closed.unchanged.number };
            }
            return null;
        }
        let skip = duplicate();
        if (skip) {
            result.skipped.push(skip);
            continue;
        }
        function openCapCount() {
            // Count distinct currently-open, cap-labeled issues: tracking.open reflects
            // GitHub's state (refreshed before each real creation below), and each
            // remembered item from this run adds one more — by number when it has a real
            // one, or a unique placeholder for a not-yet-created dry-run proposal — so a
            // lagging refresh can't undercount issues this run has already filed.
            const numbers = new Set(tracking.open.filter(issue => hasCapLabel(issue.labels)).map(issue => issue.number));
            for (const item of remembered) {
                numbers.add(item.number ?? Symbol());
            }
            return numbers.size;
        }
        if (openCapCount() >= maximumOpenIssues) {
            result.skipped.push({ candidateIds: group.candidateIds, reason: 'open-issue-cap' });
            continue;
        }
        if (!dryRun) {
            try {
                tracking = await readTracking();
            } catch (error) {
                report(errorDiagnostic('tracking-refresh-failed', error));
                result.skipped.push({ candidateIds: group.candidateIds, reason: 'tracking-unavailable' });
                halted = true;
                continue;
            }
            skip = duplicate();
            if (skip) {
                result.skipped.push(skip);
                continue;
            }
        }
        const history = closedHistory(tracking.closed, group, evidenceFingerprint(group, references)).history;
        const proposal = buildProposal({ repository, targetBranch, headSha, group, references, history });
        result.proposed.push(proposal);
        if (dryRun) {
            remembered.push({ number: null, body: proposal.body, state: 'open' });
            continue;
        }
        const { owner, repo } = normalizeRepository(repository);
        try {
            const { data } = await github.rest.issues.create({
                owner, repo, title: proposal.title, body: proposal.body, labels: proposal.labels,
                request: { retries: 0 },
            });
            if (!Number.isSafeInteger(data?.number) || data.number <= 0) {
                throw new Error('Create result has no issue number.');
            }
            result.created.push({ ...proposal, number: data.number });
            remembered.push({ number: data.number, body: proposal.body, state: 'open' });
        } catch (error) {
            report(errorDiagnostic('create-failed', error, { candidateIds: group.candidateIds }));
            // Never retry a POST. Even a timeout can mean GitHub accepted the issue.
            try {
                const open = await listRepositoryIssues({ github, repository, state: 'open' });
                const created = open.find(issue => matches(markerPattern, issue.body)
                    .some(id => group.ids.includes(id)));
                if (created) {
                    result.created.push({ ...proposal, number: created.number, reconciled: true });
                    remembered.push(created);
                    tracking.open = open;
                    continue;
                }
            } catch (reconcileError) {
                report(errorDiagnostic('create-reconciliation-failed', reconcileError));
            }
            result.skipped.push({ candidateIds: group.candidateIds, reason: 'create-outcome-unknown' });
            halted = true;
        }
    }
    return result;
}
