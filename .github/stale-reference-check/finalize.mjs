// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { createHash } from 'node:crypto';
import { normalizeActionSource } from './source-anchors.mjs';
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

export { normalizeActionSource };

export function targetIds(repository, action) {
    const { owner, repo } = normalizeRepository(repository);
    const prefix = ['stale-reference', 1, `${owner}/${repo}`, action.path];
    if (action.kind === 'ignore') {
        return unique(action.testNames.map(name => hash([...prefix, 'test', name])));
    }
    return [hash([...prefix, 'comment', action.anchor, normalizeActionSource(action.seedText)])];
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
        const sections = targetSections(body);
        return matches(markerPattern, body).length && (sections.length ? sections : [body]).some(section =>
            section.includes(`\nSource path: ${inlineCode(group.path)}\n`) &&
            group.sources.some(source => section.includes(`\nOwning anchor: ${inlineCode(source.anchor)}\n`)));
    });
    const history = [...new Map([...exact, ...related].map(issue => [issue.number, issue])).values()];
    return { history };
}

function evidenceFingerprint(group, references) {
    return hash({
        version: 1,
        kind: group.kind,
        ids: group.ids,
        source: unique(group.sources.map(source => normalizeActionSource(source.seedText))),
        conditions: group.additionalConditions,
        references: references.map(reference => ({
            key: reference.key, kind: reference.kind, state: reference.state,
            stateReason: reference.stateReason, closedAt: reference.closedAt, mergedAt: reference.mergedAt,
        })).sort((a, b) => a.key.localeCompare(b.key)),
    });
}

function blockerMarker(repository, references) {
    const { owner, repo } = normalizeRepository(repository);
    return `<!-- stale-reference-blockers:v1:${hash([`${owner}/${repo}`, unique(references.map(reference => reference.key))])} -->`;
}

const findingsStart = '<!-- stale-reference-findings:v1 -->';
const findingsEnd = '<!-- /stale-reference-findings:v1 -->';

function targetSections(body) {
    const blocks = [...body.matchAll(/<!-- stale-reference-target:v([12]):([a-f0-9]{64}:[a-f0-9]{64}) -->\n([\s\S]*?)<!-- \/stale-reference-target:v\1:\2 -->/g)];
    const prefix = body.slice(0, body.search(/<!-- stale-reference-target:v[12]:/));
    return [...(blocks.length && matches(markerPattern, prefix).length ? [prefix] : []),
        ...blocks.map(match => match[3])];
}

function findingsRange(body) {
    const starts = [...body.matchAll(/^<!-- stale-reference-findings:v1 -->$/gm)];
    const ends = [...body.matchAll(/^<!-- \/stale-reference-findings:v1 -->$/gm)];
    if (starts.length !== 1 || ends.length !== 1 || starts[0].index >= ends[0].index ||
        !body.includes('\n## Follow-up\n', ends[0].index)) {
        throw new Error('Tracking issue has a missing or ambiguous findings section.');
    }
    return { start: starts[0].index, end: ends[0].index };
}

function targetBlock({ repository, headSha, group, references, history }) {
    const { owner, repo } = normalizeRepository(repository);
    const base = `https://github.com/${owner}/${repo}`;
    const path = group.path.split('/').map(encodeURIComponent).join('/');
    const fingerprint = evidenceFingerprint(group, references);
    const key = `${group.ids[0]}:${fingerprint}`;
    return [
        `<!-- stale-reference-target:v2:${key} -->`,
        ...group.ids.map(marker),
        `<!-- stale-reference-evidence:v1:${fingerprint} -->`,
        ...(group.kind === 'ignore' ? group.ids.map(id =>
            `<!-- stale-reference-evidence:v2:${id}:${evidenceFingerprint({ ...group, ids: [id] }, references)} -->`) : []),
        `### ${group.kind === 'ignore' ? 'Ignored tests' : group.kind === 'todo' ? 'TODO' : 'Workaround'}`,
        '',
        `Source path: ${inlineCode(group.path)}`,
        ...(group.kind === 'ignore'
            ? group.testNames.map(name => `Fully qualified test: ${inlineCode(name)}`)
            : unique(group.sources.map(source => source.anchor)).map(anchor => `Owning anchor: ${inlineCode(anchor)}`)),
        '', '#### Source evidence', '',
        ...group.sources.flatMap(source => [
            `[Commit-pinned source, lines ${source.startLine}-${source.endLine}](${base}/blob/${headSha}/${path}#L${source.startLine}-L${source.endLine})`,
            '', fenced(source.excerpt), '',
        ]),
        '#### Additional prerequisites (not verified)', '',
        ...(group.additionalConditions.length
            ? group.additionalConditions.flatMap(condition => [fenced(condition), ''])
            : ['None identified in the source; this does not establish that none exist.', '']),
        ...(history.length ? [
            '#### Previous tracking history', '',
            ...history.map(issue => `- ${base}/issues/${issue.number} (closed)`), '',
        ] : []),
        `<!-- /stale-reference-target:v2:${key} -->`,
    ].join('\n');
}

function isBlockerContainer(issue, repository, references) {
    const body = issue.body ?? '';
    const { owner, repo } = normalizeRepository(repository);
    if (!body.includes(`\nRepository: \`${owner}/${repo}\`\n`)) {
        return false;
    }
    if (body.includes('<!-- stale-reference-blockers:v1:')) {
        return body.includes(blockerMarker(repository, references));
    }
    // Adopt only legacy tasks whose recorded resolutions prove the exact blocker set.
    const expected = JSON.stringify(unique(references.map(reference => reference.key)));
    const legacyBlocks = targetSections(body);
    if (body.includes('<!-- stale-reference-target:v1:') && !legacyBlocks.length) return false;
    const sections = legacyBlocks.length ? legacyBlocks : [body];
    return matches(markerPattern, body).length > 0 && sections.every(section => {
        const urls = [...section.matchAll(/^- ([^\n ]+): (?:issue closed as completed|pull request merged)/gm)];
        if (!urls.length || urls.some(match =>
            !/^https:\/\/github\.com\/[\w.-]+\/[\w.-]+\/(?:issues|pull)\/[1-9]\d{0,14}$/.test(match[1]))) return false;
        const keys = urls.map(match => match[1].slice('https://github.com/'.length)
            .replace(/\/(?:issues|pull)\//, '/').toLowerCase());
        return JSON.stringify(unique(keys)) === expected;
    });
}

function referenceResolution(reference) {
    return `- ${reference.url}: ${reference.kind === 'pull'
        ? `pull request merged at ${reference.mergedAt} (state: ${reference.state})`
        : `issue closed as completed at ${reference.closedAt}`}.`;
}

function commentCovered(issue, entry, repository, ambiguousNames) {
    const body = issue.body ?? '';
    if (body.includes('<!-- stale-reference-blockers:v1:') &&
        !body.includes(blockerMarker(repository, entry.references))) return false;
    if (entry.group.kind === 'ignore') {
        if (body.includes('<!-- stale-reference-target:v2:')) {
            return targetSections(body).some(block => entry.group.ids.every(id =>
                block.includes(`<!-- stale-reference-evidence:v2:${id}:${evidenceFingerprint({ ...entry.group, ids: [id] }, entry.references)} -->`)) &&
                entry.group.testNames.every(name => block.includes(`Fully qualified test: ${inlineCode(name)}`)) &&
                entry.group.sources.every(source =>
                    normalizeActionSource(block).includes(normalizeActionSource(source.seedText))) &&
                entry.group.additionalConditions.every(condition => block.includes(fenced(condition))) &&
                entry.references.every(reference => body.includes(referenceResolution(reference)))) ||
                Boolean(findOpenDuplicate([{ ...issue, body: body.slice(0, body.indexOf('<!-- stale-reference-target:v2:')) }],
                    entry.group, ambiguousNames));
        }
        return Boolean(findOpenDuplicate([issue], entry.group, ambiguousNames));
    }
    if (/<!-- stale-reference-target:v[12]:/.test(body)) {
        const fingerprint = evidenceFingerprint(entry.group, entry.references);
        const key = `${entry.group.ids[0]}:${fingerprint}`;
        for (const version of [1, 2]) {
            const start = `<!-- stale-reference-target:v${version}:${key} -->`;
            const end = `<!-- /stale-reference-target:v${version}:${key} -->`;
            let offset = -1;
            while ((offset = body.indexOf(start, offset + 1)) !== -1) {
                const endOffset = body.indexOf(end, offset + start.length);
                if (endOffset === -1) continue;
                const block = body.slice(offset + start.length, endOffset);
                if (block.includes(marker(entry.group.ids[0])) &&
                    block.includes(`<!-- stale-reference-evidence:v1:${fingerprint} -->`) &&
                    (version === 1
                        ? ['## Canonical target', '## Source evidence', '## Verified reference resolution',
                            '## Additional prerequisites (not verified)', '## Follow-up']
                        : ['#### Source evidence', '#### Additional prerequisites (not verified)'])
                        .every(section => block.includes(section)) &&
                    entry.group.sources.every(source =>
                        block.includes(`Owning anchor: ${inlineCode(source.anchor)}`) &&
                        normalizeActionSource(block).includes(normalizeActionSource(source.seedText))) &&
                    entry.references.every(reference => (version === 1 ? block : body).includes(referenceResolution(reference))) &&
                    entry.group.additionalConditions.every(condition => block.includes(fenced(condition)))) {
                    return true;
                }
            }
        }
        const legacy = body.slice(0, body.search(/<!-- stale-reference-target:v[12]:/));
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

async function finalizeBlockers({
    github, repository, targetBranch, headSha, dryRun, entries, readTracking, report, result, remembered, ambiguousNames,
}) {
    const { owner, repo } = normalizeRepository(repository);
    const blockers = new Map();
    for (const entry of entries) {
        const key = blockerMarker(repository, entry.references);
        if (!blockers.has(key)) blockers.set(key, []);
        blockers.get(key).push(entry);
    }
    let halted = false;
    for (const [container, blockerEntries] of blockers) {
        const references = blockerEntries[0].references.map(reference => ({
            ...reference,
            originalUrls: unique(blockerEntries.flatMap(entry =>
                entry.references.find(item => item.key === reference.key).originalUrls)),
        })).sort((a, b) => a.key.localeCompare(b.key));
        const candidateIds = unique(blockerEntries.flatMap(entry => entry.group.candidateIds));
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
        const missing = blockerEntries.flatMap(original => {
            let entry = original;
            if (entry.group.kind === 'ignore') {
                const testNames = entry.group.testNames.filter(name => {
                    const group = { ...entry.group, testNames: [name],
                        ids: targetIds(repository, { ...entry.group, testNames: [name] }) };
                    const closure = trackingClosure(tracking.closed, group);
                    const duplicate = open.find(issue => commentCovered(issue, { ...entry, group }, repository, ambiguousNames));
                    if (!closure && !duplicate) return true;
                    result.skipped.push({ candidateIds: group.candidateIds, targetIds: group.ids,
                        ...(closure ?? { reason: 'open-duplicate', number: duplicate.number }) });
                    return false;
                });
                if (!testNames.length) return [];
                entry = { ...entry, group: { ...entry.group, testNames,
                    ids: targetIds(repository, { ...entry.group, testNames }) } };
                return [entry];
            }
            const closure = trackingClosure(tracking.closed, entry.group);
            if (closure) {
                result.skipped.push({ candidateIds: entry.group.candidateIds, ...closure });
                return [];
            }
            const duplicate = open.find(issue => commentCovered(issue, entry, repository, ambiguousNames));
            if (duplicate) {
                result.skipped.push({
                    candidateIds: entry.group.candidateIds, reason: 'open-duplicate', number: duplicate.number,
                });
                return [];
            }
            return [entry];
        });
        if (!missing.length) continue;
        const containers = open.filter(issue => isBlockerContainer(issue, repository, references));
        const pendingIds = unique(missing.flatMap(entry => entry.group.candidateIds));
        if (containers.length > 1) {
            report({ code: 'ambiguous-blocker-tracking', blockerKeys: references.map(reference => reference.key),
                numbers: containers.map(issue => issue.number) });
            result.skipped.push({ candidateIds: pendingIds, reason: 'ambiguous-blocker-tracking' });
            continue;
        }
        let existing = containers[0];
        if (existing && !dryRun) {
            try {
                const { data } = await github.rest.issues.get({ owner, repo, issue_number: existing.number });
                if (data?.number !== existing.number || data.state !== 'open' || data.pull_request ||
                    typeof data.body !== 'string' || !isBlockerContainer(data, repository, references)) {
                    throw new Error('Tracking issue changed identity or is no longer open.');
                }
                existing = data;
            } catch (error) {
                report(errorDiagnostic('tracking-read-failed', error, { candidateIds: pendingIds }));
                result.skipped.push({ candidateIds: pendingIds, reason: 'tracking-unavailable' });
                halted = true;
                continue;
            }
        }
        const additions = missing.flatMap(entry => {
            if (!existing) return [entry];
            if (entry.group.kind === 'ignore') {
                const testNames = entry.group.testNames.filter(name => {
                    const group = { ...entry.group, testNames: [name],
                        ids: targetIds(repository, { ...entry.group, testNames: [name] }) };
                    if (!commentCovered(existing, { ...entry, group }, repository, ambiguousNames)) return true;
                    result.skipped.push({ candidateIds: group.candidateIds, targetIds: group.ids,
                        reason: 'open-duplicate', number: existing.number });
                    return false;
                });
                return testNames.length ? [{ ...entry, group: { ...entry.group, testNames,
                    ids: targetIds(repository, { ...entry.group, testNames }) } }] : [];
            }
            if (!commentCovered(existing, entry, repository, ambiguousNames)) return [entry];
            result.skipped.push({
                candidateIds: entry.group.candidateIds, reason: 'open-duplicate', number: existing.number,
            });
            return [];
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
        const title = `Revalidate references to ${references.map(reference =>
            `${reference.key.slice(0, reference.key.lastIndexOf('/'))}#${reference.key.split('/').at(-1)}`).join(', ')}`.slice(0, 256);
        const header = [
            container, '## Potentially stale references', '',
            `Repository: \`${owner}/${repo}\``,
            `Target branch: **${targetBranch}**.`, '',
            'These sites are potentially stale, not proven obsolete. Revalidate each separately.',
            'No tests were run by this discovery workflow. Upstream resolution does not prove that a test passes,',
            'that a fixed dependency version is consumed, or that removing a workaround is safe. No root cause is claimed.',
            'Closing this issue as completed does not suppress targets still present in source.',
            '', '## Verified reference resolution', '',
            ...references.flatMap(reference => [
                referenceResolution(reference),
                ...reference.originalUrls.map(url => `  - Original source URL: <${url}>`),
            ]),
            '',
        ].join('\n');
        const base = `https://github.com/${owner}/${repo}`;
        const encodedBranch = targetBranch.split('/').map(encodeURIComponent).join('/');
        const followUp = [
            '## Follow-up', '',
            `1. Work against \`${targetBranch}\`; verify all prerequisites, fixed-version consumption, and behavior`,
            '   across supported versions. Historical compatibility reasons may still require this code.',
            '2. Remove the relevant Ignore for the listed tests, preserving unrelated ignores and conditions.',
            '   Remove or update each TODO/workaround only when justified; otherwise retain it and record why.',
            `3. Cover a justified change with the smallest appropriate regression check. Use the repository`,
            `   [run-tests skill](${base}/blob/${encodedBranch}/.github/skills/run-tests/SKILL.md) for SDK tests`,
            '   on the required platform. Verify that the tests actually execute rather than being skipped or selecting zero tests.',
            '4. If tests pass, submit the focused change. Otherwise retain the new failure evidence and unresolved',
            '   prerequisites here; do not assume the original root cause or silently discard failures.',
            '',
        ].join('\n');
        let body;
        try {
            if (existing && !references.every(reference => existing.body.includes(referenceResolution(reference)))) {
                throw new Error('Recorded blocker resolution changed; review the shared evidence before appending.');
            }
            if (existing?.body.includes('<!-- stale-reference-blockers:v1:')) {
                const range = findingsRange(existing.body);
                body = existing.body.slice(0, range.end) + blocks.join('\n\n') + '\n\n' + existing.body.slice(range.end);
            } else {
                const findings = `## Findings\n\n${findingsStart}\n\n${blocks.join('\n\n')}\n\n${findingsEnd}\n\n`;
                body = existing
                    ? `${container}\n${existing.body}\n\n${findings}${followUp}`
                    : `${header}\n${findings}${followUp}`;
            }
            findingsRange(body);
        } catch (error) {
            report(errorDiagnostic('tracking-layout-invalid', error, { candidateIds: pendingIds, reason: error.message }));
            result.skipped.push({ candidateIds: pendingIds, reason: 'tracking-layout-invalid' });
            continue;
        }
        if (body.length > 65536) {
            report({ code: 'tracking-body-too-large', candidateIds: pendingIds, length: body.length });
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
            remembered.set(existing?.number ?? container, {
                number: existing?.number ?? container, title: proposal.title, body,
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
        const acceptedMutation = issue => {
            if (!Number.isSafeInteger(issue?.number) || issue.number <= 0 || issue.pull_request ||
                (existing && issue.number !== existing.number) ||
                issue.state !== 'open' || !isBlockerContainer(issue, repository, references) ||
                !references.every(reference => issue.body.includes(referenceResolution(reference))) ||
                !issue.body.includes('## Follow-up')) return false;
            const range = findingsRange(issue.body);
            const findings = issue.body.slice(range.start, range.end);
            return blocks.every(block => findings.includes(block));
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
            if (!acceptedMutation(response.data)) {
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
                const accepted = current.find(acceptedMutation);
                if (accepted) {
                    record(accepted.number, true);
                    remembered.set(accepted.number, {
                        ...accepted, labels: accepted.labels ?? (existing ? existing.labels : proposal.labels),
                    });
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
        // Refresh before each blocker batch so prior mutations and closure decisions
        // are visible before proposing another creation or append.
        const [open, closed] = await Promise.all(['open', 'closed'].map(state =>
            listRepositoryIssues({ github, repository, state })));
        return { open, closed };
    }
    try {
        await readTracking();
    } catch (error) {
        report(errorDiagnostic('tracking-list-failed', error));
        result.skipped.push(...eligible.map(({ group }) => ({
            candidateIds: group.candidateIds, reason: 'tracking-unavailable',
        })));
        return result;
    }
    await finalizeBlockers({
        github, repository, targetBranch, headSha, dryRun, readTracking, report, result,
        entries: eligible, remembered: new Map(), ambiguousNames,
    });
    return result;
}
