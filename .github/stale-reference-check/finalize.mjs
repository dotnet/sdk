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
        excerpt: action.sourceExcerpt, seedText: action.seedText,
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

function closedHistory(issues, group) {
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
    return { history };
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

function fileMarker(repository, path) {
    const { owner, repo } = normalizeRepository(repository);
    return `<!-- stale-reference-file:v1:${hash([`${owner}/${repo}`, path, 'todo/workaround'])} -->`;
}

function targetBlock({ repository, targetBranch, headSha, group, references, history }) {
    const proposal = buildProposal({ repository, targetBranch, headSha, group, references, history });
    const key = `${group.ids[0]}:${proposal.fingerprint}`;
    return `<!-- stale-reference-target:v1:${key} -->\n${proposal.body}` +
        `<!-- /stale-reference-target:v1:${key} -->`;
}

function isCommentContainer(issue, repository, path) {
    const body = issue.body ?? '';
    const { owner, repo } = normalizeRepository(repository);
    if (!body.includes(`\nRepository: \`${owner}/${repo}\`\n`) ||
        !body.includes(`\nSource path: ${inlineCode(path)}\n`)) {
        return false;
    }
    return body.includes(fileMarker(repository, path)) ||
        (matches(markerPattern, body).length > 0 &&
            /Follow-up kind: \*\*(?:todo|workaround)\*\*\./.test(body));
}

function referenceResolution(reference) {
    return `- ${reference.url}: ${reference.kind === 'pull'
        ? `pull request merged at ${reference.mergedAt} (state: ${reference.state})`
        : `issue closed as completed at ${reference.closedAt}`}.`;
}

function commentCovered(issue, entry) {
    const body = issue.body ?? '';
    if (body.includes('<!-- stale-reference-target:v1:')) {
        const fingerprint = evidenceFingerprint(entry.group, entry.references);
        const key = `${entry.group.ids[0]}:${fingerprint}`;
        const start = `<!-- stale-reference-target:v1:${key} -->`;
        const end = `<!-- /stale-reference-target:v1:${key} -->`;
        let offset = -1;
        while ((offset = body.indexOf(start, offset + 1)) !== -1) {
            const endOffset = body.indexOf(end, offset + start.length);
            if (endOffset === -1) continue;
            const block = body.slice(offset + start.length, endOffset);
            if (block.includes(marker(entry.group.ids[0])) &&
                block.includes(`<!-- stale-reference-evidence:v1:${fingerprint} -->`) &&
                ['## Canonical target', '## Source evidence', '## Verified reference resolution',
                    '## Additional prerequisites (not verified)', '## Follow-up'].every(section => block.includes(section)) &&
                entry.group.sources.every(source =>
                    block.includes(`Owning anchor: ${inlineCode(source.anchor)}`) &&
                    normalizeActionSource(block).includes(normalizeActionSource(source.excerpt))) &&
                entry.references.every(reference => block.includes(referenceResolution(reference))) &&
                entry.group.additionalConditions.every(condition => block.includes(fenced(condition)))) {
                return true;
            }
        }
        const legacy = body.slice(0, body.indexOf('<!-- stale-reference-target:v1:'));
        return matches(markerPattern, legacy).includes(entry.group.ids[0]) &&
            legacy.includes(`<!-- stale-reference-evidence:v1:${fingerprint} -->`);
    }
    if (matches(markerPattern, body).length) {
        return matches(markerPattern, body).includes(entry.group.ids[0]) &&
            body.includes(`<!-- stale-reference-evidence:v1:${evidenceFingerprint(entry.group, entry.references)} -->`);
    }
    return Boolean(findOpenDuplicate([issue], entry.group));
}

function trackingClosure(issues, group) {
    const exact = issues.filter(issue =>
        matches(markerPattern, issue.body).some(id => group.ids.includes(id)));
    const declined = exact.find(issue => issue.state_reason === 'not_planned');
    if (declined) {
        return { reason: 'declined-closed-task', number: declined.number };
    }
    const unknown = exact.find(issue => issue.state_reason !== 'completed');
    return unknown ? { reason: 'unknown-tracking-closure', number: unknown.number } : null;
}

async function finalizeCommentFiles({
    github, repository, targetBranch, headSha, dryRun, entries, readTracking, report, result, remembered,
}) {
    const { owner, repo } = normalizeRepository(repository);
    const files = new Map();
    for (const entry of entries) {
        if (!files.has(entry.group.path)) files.set(entry.group.path, []);
        files.get(entry.group.path).push(entry);
    }
    let halted = false;
    for (const [path, fileEntries] of files) {
        const candidateIds = unique(fileEntries.flatMap(entry => entry.group.candidateIds));
        if (halted) {
            result.skipped.push({ candidateIds, reason: 'filing-halted' });
            continue;
        }
        let tracking;
        try {
            tracking = await readTracking();
        } catch (error) {
            report(errorDiagnostic('tracking-refresh-failed', error));
            result.skipped.push({ candidateIds, reason: 'tracking-unavailable' });
            halted = true;
            continue;
        }
        const open = [...new Map([...tracking.open, ...remembered.values()]
            .map(issue => [issue.number, issue])).values()];
        const missing = fileEntries.filter(entry => {
            const closure = trackingClosure(tracking.closed, entry.group);
            if (closure) {
                result.skipped.push({ candidateIds: entry.group.candidateIds, ...closure });
                return false;
            }
            const duplicate = open.find(issue => commentCovered(issue, entry));
            if (duplicate) {
                result.skipped.push({
                    candidateIds: entry.group.candidateIds, reason: 'open-duplicate', number: duplicate.number,
                });
                return false;
            }
            return true;
        });
        if (!missing.length) continue;
        const containers = open.filter(issue => isCommentContainer(issue, repository, path));
        const pendingIds = unique(missing.flatMap(entry => entry.group.candidateIds));
        if (containers.length > 1) {
            report({ code: 'ambiguous-file-tracking', path, numbers: containers.map(issue => issue.number) });
            result.skipped.push({ candidateIds: pendingIds, reason: 'ambiguous-file-tracking' });
            continue;
        }
        let existing = containers[0];
        if (existing && !dryRun) {
            try {
                const { data } = await github.rest.issues.get({ owner, repo, issue_number: existing.number });
                if (data?.number !== existing.number || data.state !== 'open' || data.pull_request ||
                    typeof data.body !== 'string' || !isCommentContainer(data, repository, path)) {
                    throw new Error('Tracking issue changed identity or is no longer open.');
                }
                existing = data;
            } catch (error) {
                report(errorDiagnostic('tracking-read-failed', error, { path }));
                result.skipped.push({ candidateIds: pendingIds, reason: 'tracking-unavailable' });
                halted = true;
                continue;
            }
        }
        const additions = missing.filter(entry => {
            if (!existing || !commentCovered(existing, entry)) return true;
            result.skipped.push({
                candidateIds: entry.group.candidateIds, reason: 'open-duplicate', number: existing.number,
            });
            return false;
        });
        if (!additions.length) continue;
        const capNumbers = new Set(tracking.open.filter(issue => hasCapLabel(issue.labels)).map(issue => issue.number));
        for (const issue of remembered.values()) {
            if (hasCapLabel(issue.labels)) capNumbers.add(issue.number);
        }
        if (!existing && capNumbers.size >= maximumOpenIssues) {
            result.skipped.push({ candidateIds: pendingIds, reason: 'open-issue-cap' });
            continue;
        }
        const blocks = additions.map(entry => targetBlock({
            repository, targetBranch, headSha, ...entry,
            history: closedHistory(tracking.closed, entry.group).history,
        }));
        const container = fileMarker(repository, path);
        const title = `Revalidate TODOs/workarounds in ${inlineCode(path)}`.slice(0, 256);
        const header = [
            container, '## Potentially stale TODOs/workarounds', '',
            `Repository: \`${owner}/${repo}\``,
            `Source path: ${inlineCode(path)}`,
            'Follow-up kind: **todo/workaround**.', '',
            'Each target below has its own evidence and prerequisites. Revalidate each separately.',
            'Closing this issue as completed does not suppress targets still present in source.',
            '',
        ].join('\n');
        const body = existing
            ? `${existing.body.includes(container) ? '' : `${container}\n`}${existing.body}\n\n${blocks.join('\n\n')}\n`
            : `${header}\n${blocks.join('\n\n')}\n`;
        if (body.length > 65536) {
            report({ code: 'tracking-body-too-large', path, length: body.length });
            result.skipped.push({ candidateIds: pendingIds, reason: 'tracking-body-too-large' });
            continue;
        }
        const proposal = {
            operation: existing ? 'update' : 'create',
            ...(existing ? { number: existing.number } : {}),
            title: existing?.title ?? title, body,
            ...(existing ? {} : { labels: ['cookie', 'agentic-workflows', capLabel] }),
            targetIds: unique(additions.flatMap(entry => entry.group.ids)),
            candidateIds: unique(additions.flatMap(entry => entry.group.candidateIds)),
            fingerprint: hash(additions.map(entry => evidenceFingerprint(entry.group, entry.references))),
        };
        result.proposed.push(proposal);
        if (dryRun) {
            remembered.set(existing?.number ?? path, {
                number: existing?.number ?? path, title: proposal.title, body,
                state: 'open', labels: existing ? existing.labels : proposal.labels,
            });
            continue;
        }
        const record = (number, reconciled = false) => {
            const item = { ...proposal, number, ...(reconciled ? { reconciled: true } : {}) };
            result[existing ? 'updated' : 'created'].push(item);
            remembered.set(number, {
                number, title: proposal.title, body, state: 'open',
                labels: existing ? existing.labels : proposal.labels,
            });
        };
        try {
            const response = existing
                ? await github.rest.issues.update({
                    owner, repo, issue_number: existing.number, body, request: { retries: 0 },
                })
                : await github.rest.issues.create({
                    owner, repo, title, body, labels: proposal.labels, request: { retries: 0 },
                });
            if (!Number.isSafeInteger(response.data?.number) || response.data.number <= 0 ||
                (existing && response.data.number !== existing.number)) {
                throw new Error('Mutation result has no matching issue number.');
            }
            if (response.data.state !== 'open' || typeof response.data.body !== 'string' ||
                !blocks.every(block => response.data.body.includes(block))) {
                throw new Error('Mutation result does not contain the complete target evidence.');
            }
            record(response.data.number);
        } catch (error) {
            const operation = proposal.operation;
            report(errorDiagnostic(`${operation}-failed`, error, { candidateIds: proposal.candidateIds }));
            try {
                const current = existing
                    ? [(await github.rest.issues.get({ owner, repo, issue_number: existing.number })).data]
                    : await listRepositoryIssues({ github, repository, state: 'open' });
                const accepted = current.find(issue => issue?.state === 'open' &&
                    isCommentContainer(issue, repository, path) && blocks.every(block => issue.body.includes(block)));
                if (accepted) {
                    record(accepted.number, true);
                    remembered.set(accepted.number, accepted);
                    continue;
                }
            } catch (reconcileError) {
                report(errorDiagnostic(`${operation}-reconciliation-failed`, reconcileError));
            }
            result.skipped.push({ candidateIds: proposal.candidateIds, reason: `${operation}-outcome-unknown` });
            halted = true;
        }
    }
    return halted;
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
            ? `Revalidate ignored test: ${inlineCode(group.testNames[0])}`
            : `Revalidate ignored tests in ${inlineCode(group.path)} (${group.testNames.length} tests)`)
        : `Revalidate ${group.kind === 'todo' ? 'TODO' : 'workaround'} in ${inlineCode(group.path)}`).slice(0, 256);
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
            referenceResolution(reference),
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
            'Related tasks at this target or owning anchor were closed previously. Current source still contains',
            'the targets listed here; completed closure alone does not establish that all work was resolved.',
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
    const result = { created: [], updated: [], proposed: [], skipped: [], diagnostics: [] };
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
    const commentIssues = new Map();
    const commentHalted = await finalizeCommentFiles({
        github, repository, targetBranch, headSha, dryRun, readTracking, report, result,
        entries: eligible.filter(({ group }) => group.kind !== 'ignore'),
        remembered: commentIssues,
    });
    const remembered = [...commentIssues.values()];
    let halted = commentHalted;
    for (const entry of eligible.filter(({ group }) => group.kind === 'ignore')) {
        let group = entry.group;
        const { references } = entry;
        if (halted) {
            result.skipped.push({ candidateIds: group.candidateIds, reason: 'filing-halted' });
            continue;
        }
        function retainTargets() {
            const remaining = group.testNames.filter(name => {
                const ids = targetIds(repository, { kind: 'ignore', path: group.path, testNames: [name] });
                const closure = trackingClosure(tracking.closed, { ids });
                if (!closure) return true;
                result.skipped.push({ candidateIds: group.candidateIds, targetIds: ids, ...closure });
                return false;
            });
            group = {
                ...group, testNames: remaining,
                ids: targetIds(repository, { kind: 'ignore', path: group.path, testNames: remaining }),
            };
            return remaining.length > 0;
        }
        function duplicate() {
            const open = findOpenDuplicate([...tracking.open, ...remembered], group, ambiguousNames);
            if (open) {
                return { candidateIds: group.candidateIds, reason: 'open-duplicate', number: open.number };
            }
            return null;
        }
        if (!retainTargets()) continue;
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
            for (const item of remembered.filter(issue => hasCapLabel(issue.labels))) {
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
            if (!retainTargets()) continue;
            skip = duplicate();
            if (skip) {
                result.skipped.push(skip);
                continue;
            }
            if (openCapCount() >= maximumOpenIssues) {
                result.skipped.push({ candidateIds: group.candidateIds, reason: 'open-issue-cap' });
                continue;
            }
        }
        const history = closedHistory(tracking.closed, group).history;
        const proposal = {
            operation: 'create', ...buildProposal({ repository, targetBranch, headSha, group, references, history }),
        };
        result.proposed.push(proposal);
        if (dryRun) {
            remembered.push({ number: null, body: proposal.body, state: 'open', labels: proposal.labels });
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
            remembered.push({ number: data.number, body: proposal.body, state: 'open', labels: proposal.labels });
        } catch (error) {
            report(errorDiagnostic('create-failed', error, { candidateIds: group.candidateIds }));
            // Never retry a POST. Even a timeout can mean GitHub accepted the issue.
            try {
                const open = await listRepositoryIssues({ github, repository, state: 'open' });
                const created = open.find(issue => matches(markerPattern, issue.body)
                    .some(id => group.ids.includes(id)));
                if (created) {
                    result.created.push({ ...proposal, number: created.number, reconciled: true });
                    remembered.push({ ...created, labels: created.labels ?? proposal.labels });
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
