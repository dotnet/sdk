// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { assertRepoPath, numberedContext, readSource, sourceId, sourceLines } from './collect.mjs';

const statuses = new Set(['actionable', 'irrelevant', 'insufficient_context']);
const kinds = new Set(['ignore', 'todo', 'workaround']);
const rawActionKeys = ['kind', 'anchor', 'testNames', 'startLine', 'endLine', 'urls', 'additionalConditions'];
const enrichedActionKeys = [...rawActionKeys, 'candidateId', 'path', 'sourceExcerpt'];
// Only results restored/validated by this module may carry derived fields. JSON from the agent cannot opt in.
const validatedResultObjects = new WeakSet();

function requireCondition(condition, message) {
    if (!condition) throw new Error(message);
}

function object(value, label) {
    requireCondition(value !== null && typeof value === 'object' && !Array.isArray(value)
        && (Object.getPrototypeOf(value) === Object.prototype || Object.getPrototypeOf(value) === null),
    `${label} must be an object.`);
}

function keys(value, allowed, label) {
    object(value, label);
    requireCondition(Object.keys(value).every(key => allowed.includes(key)), `${label} contains an unknown field.`);
}

function text(value, label, limit = 4096) {
    requireCondition(typeof value === 'string' && value.trim().length > 0 && value.length <= limit
        && !/[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]/.test(value), `${label} must be nonempty bounded text.`);
}

function stringList(value, label, { max = 16, length = 1024, required = false } = {}) {
    requireCondition(Array.isArray(value) && value.length <= max && (!required || value.length > 0), `${label} must be a bounded array.`);
    for (const entry of value) text(entry, label, length);
    requireCondition(new Set(value).size === value.length, `${label} contains duplicates.`);
}

function range(value, lineCount, limit = Infinity) {
    requireCondition(Number.isSafeInteger(value.startLine) && Number.isSafeInteger(value.endLine)
        && value.startLine >= 1 && value.endLine >= value.startLine && value.endLine <= lineCount
        && value.endLine - value.startLine + 1 <= limit, 'Invalid or unbounded source line range.');
}

function checkManifest(manifest) {
    object(manifest, 'Manifest');
    requireCondition(manifest.schemaVersion === 1, 'Unsupported manifest schemaVersion.');
    text(manifest.rulesHash, 'Manifest rulesHash', 256);
    requireCondition(/^(?:[a-f0-9]{40}|[a-f0-9]{64})$/.test(manifest.headSha), 'Invalid manifest headSha.');
    requireCondition(Array.isArray(manifest.candidates), 'Manifest candidates must be an array.');
    const candidates = new Map();
    for (const candidate of manifest.candidates) {
        object(candidate, 'Candidate');
        assertRepoPath(candidate.path);
        requireCondition(/^(?:[a-f0-9]{40}|[a-f0-9]{64})$/.test(candidate.blobSha), 'Invalid candidate blobSha.');
        requireCondition(Number.isSafeInteger(candidate.sourceLineCount) && candidate.sourceLineCount > 0, 'Invalid source line count.');
        range(candidate, candidate.sourceLineCount);
        requireCondition(Number.isSafeInteger(candidate.seedLine) && candidate.seedLine >= candidate.startLine
            && candidate.seedLine <= candidate.endLine, 'Candidate seed is outside its window.');
        requireCondition(candidate.id === sourceId(candidate.path, candidate.blobSha, candidate.seedLine), 'Invalid candidate source identity.');
        requireCondition(!candidates.has(candidate.id), 'Duplicate candidate identity.');
        requireCondition(typeof candidate.context === 'string' && kinds.has(candidate.kindHint), 'Invalid candidate context or hint.');
        contextLines(candidate);
        candidates.set(candidate.id, candidate);
    }
    return candidates;
}

function contextLines(evidence) {
    requireCondition(typeof evidence.context === 'string', 'Evidence context must be text.');
    const result = new Map();
    const lines = evidence.context.split('\n');
    requireCondition(lines.length === evidence.endLine - evidence.startLine + 1, 'Evidence context line count mismatch.');
    lines.forEach((line, index) => {
        const number = evidence.startLine + index;
        const prefix = `${number}: `;
        requireCondition(line.startsWith(prefix), 'Evidence context line numbering mismatch.');
        result.set(number, line.slice(prefix.length));
    });
    return result;
}

function expansionsFor(candidate, expansions = candidate.contextExpansions ?? []) {
    requireCondition(Array.isArray(expansions) && expansions.length <= 2, 'At most two context expansions are accepted per candidate.');
    for (const expansion of expansions) {
        keys(expansion, ['candidateId', 'path', 'blobSha', 'startLine', 'endLine', 'context', 'sourceLineCount'], 'Context expansion');
        requireCondition(expansion.candidateId === candidate.id && expansion.path === candidate.path
            && expansion.blobSha === candidate.blobSha && expansion.sourceLineCount === candidate.sourceLineCount,
        'Context expansion provenance mismatch.');
        range(expansion, candidate.sourceLineCount, 80);
        contextLines(expansion);
        requireCondition(Buffer.byteLength(expansion.context, 'utf8') <= 65536, 'Context expansion exceeds 64 KiB; request a smaller range.');
    }
    return expansions;
}

function evidenceFor(candidate, expansions) {
    const evidence = contextLines(candidate);
    for (const expansion of expansionsFor(candidate, expansions)) {
        for (const [number, line] of contextLines(expansion)) {
            requireCondition(!evidence.has(number) || evidence.get(number) === line, 'Conflicting source evidence.');
            evidence.set(number, line);
        }
    }
    return evidence;
}

function evidenceText(evidence) {
    return [...evidence].sort((a, b) => a[0] - b[0]).map(entry => entry[1]).join('\n');
}

function sourceUrls(source) {
    const references = source.match(/https:\/\/github\.com\/[^\s"'<>()[\]{}]+|[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?\/[A-Za-z0-9_.-]+#[1-9]\d*/g) ?? [];
    return new Set(references.map(reference => {
        // Qualified shorthand has no issue/PR route, so it is normalized as
        // an issue; pull requests must use an explicit /pull/ URL.
        const shorthand = /^([^/]+)\/([^#]+)#([1-9]\d+)$/.exec(reference);
        if (shorthand) return `https://github.com/${shorthand[1]}/${shorthand[2]}/issues/${shorthand[3]}`;
        return reference.replace(/[.,;:]+$/, '');
    }));
}

function escapeRegex(value) {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function checkTestNames(action, source, candidate, evidence) {
    stringList(action.testNames, 'testNames', { max: 100, length: 512, required: true });
    for (const name of action.testNames) {
        requireCondition(/^(?:@?[A-Za-z_]\w*\.){2,}@?[A-Za-z_]\w*(?:\([^()\r\n]*\))?$/.test(name),
            'Ignore testNames must be fully qualified declaration identities, not data-row names.');
        const segments = name.replace(/\(.*$/, '').split('.');
        const method = segments.pop();
        const type = segments.pop();
        requireCondition(new RegExp(`\\b(?:class|struct|Class|Module)\\s+${escapeRegex(type)}\\b`).test(source)
            && new RegExp(`\\b${escapeRegex(method)}\\s*(?:<[^>]+>)?\\s*\\(`).test(source),
        'Test identity has no declaration in supplied evidence.');
        const namespaces = [...source.matchAll(/\bnamespace\s+([\w.]+)|\bNamespace\s+([\w.]+)/g)]
            .flatMap(match => (match[1] ?? match[2]).split('.'));
        requireCondition(namespaces.length > 0 && segments[0] === namespaces[0]
            && segments.every(segment => namespaces.includes(segment)
                || new RegExp(`\\b(?:class|struct|Class|Module)\\s+${escapeRegex(segment)}\\b`).test(source)),
        'Test namespace or containing type is not present in supplied evidence; request context or defer.');
    }
    // Class ignores need complete, unambiguous coverage. Do not guess inherited or nested tests.
    const fromSeed = [...evidence].filter(([line]) => line >= candidate.seedLine).sort((a, b) => a[0] - b[0]).map(entry => entry[1]).join('\n');
    const after = fromSeed.replace(/^[\s\S]*?\bIgnore(?:Attribute)?\b[\s\S]*?(?:\]|>)/, '');
    const declaration = /(?:\b(class|struct)\s+(\w+)|\b(?:void|Task|ValueTask)(?:<[^>]+>)?\s+(\w+)\s*\()/im.exec(after);
    if (declaration?.[1]) {
        requireCondition(evidence.size === candidate.sourceLineCount
            && [...source.matchAll(/\b(?:class|struct)\s+\w+/gi)].length === 1
            && !new RegExp(`\\b${escapeRegex(declaration[2])}\\s*(?:<[^>]+>)?\\s*:`).test(source),
        'Class-level Ignore coverage is ambiguous; return insufficient_context.');
        const tests = [...source.matchAll(/\[(?:TestMethod|DataTestMethod)(?:Attribute)?(?:\([^)]*\))?\][\s\S]*?\b(?:void|Task|ValueTask)\s+(\w+)\s*\(/g)]
            .map(match => match[1]);
        requireCondition(tests.length > 0 && tests.length === action.testNames.length
            && tests.every(method => action.testNames.some(name => name.replace(/\(.*$/, '').endsWith(`.${method}`))),
        'Class-level Ignore requires every affected test identity.');
    } else {
        requireCondition(action.testNames.length === 1, 'A method Ignore must identify exactly one test.');
        if (declaration?.[3]) {
            requireCondition(action.testNames[0].replace(/\(.*$/, '').endsWith(`.${declaration[3]}`),
                'Ignore test identity does not match the following declaration.');
        }
    }
}

function validateAction(action, candidate, evidence, enriched = false) {
    keys(action, enriched ? enrichedActionKeys : rawActionKeys, 'Action');
    requireCondition(kinds.has(action.kind), 'Invalid action kind.');
    text(action.anchor, 'Action anchor', 1024);
    requireCondition(!/[\r\n]/.test(action.anchor), 'Action anchor must be a single declaration/site identity.');
    range(action, candidate.sourceLineCount, 80);
    requireCondition(action.startLine <= candidate.seedLine && candidate.seedLine <= action.endLine,
        'Action span must contain its own seed.');
    const excerpt = [];
    for (let line = action.startLine; line <= action.endLine; line++) {
        requireCondition(evidence.has(line), 'Action span was not supplied as source evidence.');
        excerpt.push(evidence.get(line));
    }
    const sourceExcerpt = excerpt.join('\n');
    requireCondition(Buffer.byteLength(sourceExcerpt, 'utf8') <= 65536, 'Action source excerpt exceeds 64 KiB.');
    const seedText = evidence.get(candidate.seedLine);
    const pattern = action.kind === 'ignore' ? /\bIgnore(?:Attribute)?\b/i
        : action.kind === 'todo' ? /\bTODO\b/i : /\b(work[ -]?around|remov(?:e|ed|al)|delet(?:e|ed|ion)|revisit|re-?enabl(?:e|ed)|temporary|temporarily)\b/i;
    requireCondition(pattern.test(seedText), 'Action kind does not match its seed.');
    stringList(action.urls, 'Action urls', { required: true, length: 2048 });
    const nearbyEvidence = new Map([...evidence].filter(([line]) => line >= candidate.startLine - 80 && line <= candidate.endLine + 80));
    const urls = sourceUrls(evidenceText(nearbyEvidence));
    for (const url of action.urls) {
        requireCondition(/^https:\/\/github\.com\/[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+\/(?:issues|pull)\/[1-9]\d*\/?(?:[?#][^\s]*)?$/.test(url),
            'Action URL must identify a public GitHub issue or pull request.');
        requireCondition(urls.has(url), 'Action URL is not present in supplied nearby source evidence.');
    }
    stringList(action.additionalConditions ?? [], 'additionalConditions');
    if (action.kind === 'ignore') {
        const attributeContext = evidenceText(new Map([...evidence].filter(([line]) =>
            line >= Math.max(candidate.startLine, action.startLine - 5) && line <= action.endLine)));
        requireCondition(/(?:\[|<|,)\s*(?:[\w.]+\.)?Ignore(?:Attribute)?\b/.test(attributeContext),
            'Ignore action must identify an actual attribute, not an incidental mention.');
        checkTestNames(action, evidenceText(evidence), candidate, evidence);
    } else {
        requireCondition(action.testNames === undefined || (Array.isArray(action.testNames) && action.testNames.length === 0),
            'Only ignore actions may identify tests.');
    }
    const result = {
        candidateId: candidate.id, kind: action.kind, path: candidate.path, anchor: action.anchor,
        testNames: action.testNames ?? [], startLine: action.startLine, endLine: action.endLine,
        sourceExcerpt, urls: [...action.urls], additionalConditions: action.additionalConditions ?? [],
    };
    if (enriched) {
        requireCondition(action.candidateId === result.candidateId && action.path === result.path
            && action.sourceExcerpt === result.sourceExcerpt, 'Cached action provenance mismatch.');
    }
    return result;
}

function validateResult(result, candidate, { cached = false } = {}) {
    keys(result, cached ? ['candidateId', 'status', 'reason', 'actions', 'contextExpansions']
        : ['candidateId', 'status', 'reason', 'actions'], 'Interpretation result');
    requireCondition(result.candidateId === candidate.id && statuses.has(result.status), 'Invalid interpretation identity or status.');
    text(result.reason, 'Interpretation reason');
    requireCondition(Array.isArray(result.actions) && result.actions.length <= 16, 'Actions must be a bounded array.');
    requireCondition(result.status === 'actionable' ? result.actions.length > 0 : result.actions.length === 0,
        'Actionable results require actions; other statuses must not contain actions.');
    const expansions = expansionsFor(candidate, cached ? result.contextExpansions ?? [] : undefined);
    const evidence = evidenceFor(candidate, expansions);
    const actions = result.actions.map(action => validateAction(action, candidate, evidence, cached));
    requireCondition(new Set(actions.map(action => JSON.stringify(action))).size === actions.length, 'Duplicate actions.');
    const validated = {
        candidateId: result.candidateId, status: result.status, reason: result.reason, actions,
        ...(expansions.length ? { contextExpansions: structuredClone(expansions) } : {}),
    };
    validatedResultObjects.add(validated);
    return validated;
}

// Validates a persisted cache against the current manifest and discards anything that
// no longer applies: the whole cache is dropped if rulesHash changed (interpretation
// rules changed, so prior verdicts can't be trusted), and per-candidate entries are
// dropped if the candidate no longer exists in this run's manifest. Never re-admits
// 'insufficient_context' results, since those were deliberately excluded from caching.
function compatibleCache(cache, manifest, candidates) {
    const empty = { schemaVersion: 1, rulesHash: manifest.rulesHash, entries: {}, cursor: 0 };
    if (cache === null || cache === undefined) return empty;
    try {
        keys(cache, ['schemaVersion', 'rulesHash', 'entries', 'cursor'], 'Cache');
        requireCondition(cache.schemaVersion === 1, 'Unsupported cache schemaVersion.');
        text(cache.rulesHash, 'Cache rulesHash', 256);
        object(cache.entries, 'Cache entries');
        requireCondition(Number.isSafeInteger(cache.cursor) && cache.cursor >= 0, 'Invalid cache cursor.');
        for (const [id, result] of Object.entries(cache.entries)) {
            requireCondition(/^[a-f0-9]{64}$/.test(id), 'Invalid cache entry identity.');
            keys(result, ['candidateId', 'status', 'reason', 'actions', 'contextExpansions'], 'Cached result');
            requireCondition(result.candidateId === id && ['actionable', 'irrelevant'].includes(result.status),
                'Invalid cached result identity or status; deferred results must not be cached.');
            text(result.reason, 'Cached reason');
            requireCondition(Array.isArray(result.actions) && result.actions.length <= 16
                && (result.status === 'actionable' ? result.actions.length > 0 : result.actions.length === 0),
            'Invalid cached action count.');
            for (const action of result.actions) {
                keys(action, enrichedActionKeys, 'Cached action');
                requireCondition(action.candidateId === id && kinds.has(action.kind), 'Invalid cached action identity or kind.');
                assertRepoPath(action.path);
                text(action.anchor, 'Cached anchor', 1024);
                text(action.sourceExcerpt, 'Cached source excerpt', 65536);
                range(action, Number.MAX_SAFE_INTEGER, 80);
                stringList(action.urls, 'Cached urls', { required: true, length: 2048 });
                stringList(action.testNames, 'Cached testNames', { max: 100, length: 512, required: action.kind === 'ignore' });
                stringList(action.additionalConditions, 'Cached additionalConditions');
            }
            requireCondition(result.contextExpansions === undefined
                || (Array.isArray(result.contextExpansions) && result.contextExpansions.length <= 2), 'Invalid cached context expansions.');
        }
        if (cache.rulesHash !== manifest.rulesHash) return empty;
        const entries = {};
        for (const [id, result] of Object.entries(cache.entries)) {
            if (!candidates.has(id)) continue;
            requireCondition(result.status !== 'insufficient_context', 'Deferred results must not be cached.');
            entries[id] = validateResult(result, candidates.get(id), { cached: true });
        }
        return { ...empty, entries, cursor: manifest.candidates.length ? cache.cursor % manifest.candidates.length : 0 };
    } catch (error) {
        throw new Error(`Invalid interpretation cache: ${error.message}`, { cause: error });
    }
}

export function selectBatch(manifest, cache, { maxWindows = 25, maxBytes = 65536 } = {}) {
    const candidates = checkManifest(manifest);
    requireCondition(Number.isSafeInteger(maxWindows) && maxWindows > 0 && maxWindows <= 25
        && Number.isSafeInteger(maxBytes) && maxBytes > 0 && maxBytes <= 65536, 'Invalid interpretation batch limits.');
    const compatible = compatibleCache(cache, manifest, candidates);
    const selected = [];
    const windows = new Set();
    const diagnostics = [];
    let bytes = 0;
    let nextCursor = compatible.cursor;
    for (let offset = 0; offset < manifest.candidates.length; offset++) {
        const index = (compatible.cursor + offset) % manifest.candidates.length;
        const candidate = manifest.candidates[index];
        nextCursor = (index + 1) % manifest.candidates.length;
        if (Object.hasOwn(compatible.entries, candidate.id)) continue;
        const window = JSON.stringify([candidate.path, candidate.blobSha, candidate.startLine, candidate.endLine]);
        const cost = Buffer.byteLength(candidate.context, 'utf8');
        if (cost > maxBytes) {
            diagnostics.push(`oversized-window: ${candidate.id} (${candidate.path}:${candidate.seedLine}) uses ${cost} bytes, exceeding the ${maxBytes}-byte initial context budget; it remains in the inventory and was not truncated.`);
            continue;
        }
        if ((!windows.has(window) && windows.size === maxWindows) || bytes + cost > maxBytes) {
            nextCursor = index;
            break;
        }
        windows.add(window);
        bytes += cost;
        selected.push(candidate);
    }
    return { batch: { schemaVersion: 1, candidates: selected }, cache: compatible, nextCursor, diagnostics };
}

export async function readContext(repoRoot, manifest, { candidateId, startLine, endLine }) {
    const candidates = checkManifest(manifest);
    requireCondition(candidates.has(candidateId), 'Unknown candidate for context lookup.');
    const candidate = candidates.get(candidateId);
    range({ startLine, endLine }, candidate.sourceLineCount, 80);
    const lines = await readSource(repoRoot, candidate);
    const context = numberedContext(lines, startLine, endLine);
    requireCondition(Buffer.byteLength(context, 'utf8') <= 65536, 'Context expansion exceeds 64 KiB; request a smaller range.');
    return {
        candidateId, path: candidate.path, blobSha: candidate.blobSha, startLine, endLine,
        context, sourceLineCount: lines.length,
    };
}

export async function validateInterpretations(payload, manifest, { repoRoot, expectedCandidateIds, contextEvidence } = {}) {
    return validateWithSources(payload, manifest, expectedCandidateIds, contextEvidence,
        candidate => readSource(repoRoot, candidate));
}

// Private collection snapshots allow the same checks while the agent has no source checkout.
// The recording job must still call validateInterpretations against committed source.
export async function validateSnapshotInterpretations(payload, manifest, { sources, expectedCandidateIds, contextEvidence }) {
    return validateWithSources(payload, manifest, expectedCandidateIds, contextEvidence, candidate => {
        requireCondition(Object.hasOwn(sources, candidate.path) && typeof sources[candidate.path] === 'string',
            'The trusted source snapshot is missing.');
        const lines = sourceLines(sources[candidate.path]);
        requireCondition(lines.length === candidate.sourceLineCount, 'Source snapshot line count mismatch.');
        return lines;
    });
}

async function validateWithSources(payload, manifest, expectedCandidateIds, contextEvidence, readCandidateSource) {
    const candidates = checkManifest(manifest);
    keys(payload, ['schemaVersion', 'results'], 'Interpretation payload');
    requireCondition(payload.schemaVersion === 1, 'Unsupported interpretation schemaVersion.');
    requireCondition(Array.isArray(expectedCandidateIds) && new Set(expectedCandidateIds).size === expectedCandidateIds.length
        && expectedCandidateIds.every(id => candidates.has(id)), 'Expected candidate IDs must be explicit, unique, and current.');
    requireCondition(Array.isArray(payload.results) && payload.results.length === expectedCandidateIds.length,
        'Interpretation output must contain exactly the expected candidate IDs.');
    const expected = new Set(expectedCandidateIds);
    const sources = new Map();
    if (contextEvidence !== undefined) {
        keys(contextEvidence, ['schemaVersion', 'expansions'], 'Context evidence');
        requireCondition(contextEvidence.schemaVersion === 1, 'Unsupported context evidence schemaVersion.');
        requireCondition(Array.isArray(contextEvidence.expansions)
            && contextEvidence.expansions.length <= expected.size * 2, 'Context evidence exceeds the expansion budget.');
        for (let expansion of contextEvidence.expansions) {
            object(expansion, 'Context expansion');
            requireCondition(expected.has(expansion.candidateId), 'Context evidence contains an unexpected candidate ID.');
            const candidate = candidates.get(expansion.candidateId);
            if (expansion.context === undefined) {
                keys(expansion, ['candidateId', 'startLine', 'endLine'], 'Trusted context receipt');
                range(expansion, candidate.sourceLineCount, 80);
                const fileKey = `${candidate.path}\0${candidate.blobSha}`;
                if (!sources.has(fileKey)) sources.set(fileKey, await readCandidateSource(candidate));
                expansion = {
                    ...expansion, path: candidate.path, blobSha: candidate.blobSha,
                    sourceLineCount: candidate.sourceLineCount,
                    context: numberedContext(sources.get(fileKey), expansion.startLine, expansion.endLine),
                };
            }
            const contextExpansions = [...(candidate.contextExpansions ?? []), expansion];
            expansionsFor(candidate, contextExpansions);
            candidates.set(candidate.id, { ...candidate, contextExpansions });
        }
    }
    const seen = new Set();
    const results = [];
    for (const result of payload.results) {
        object(result, 'Interpretation result');
        requireCondition(expected.has(result.candidateId) && !seen.has(result.candidateId),
            'Unknown, unexpected, or duplicate interpretation candidate ID.');
        seen.add(result.candidateId);
        const candidate = candidates.get(result.candidateId);
        const cached = validatedResultObjects.has(result);
        const expansions = expansionsFor(candidate, cached ? result.contextExpansions ?? [] : undefined);
        const fileKey = `${candidate.path}\0${candidate.blobSha}`;
        if (!sources.has(fileKey)) sources.set(fileKey, await readCandidateSource(candidate));
        const lines = sources.get(fileKey);
        for (const evidence of [candidate, ...expansions]) {
            requireCondition(evidence.context === numberedContext(lines, evidence.startLine, evidence.endLine),
                'Source evidence does not match the current blob.');
        }
        try {
            results.push(validateResult(result, candidate, { cached }));
        } catch (error) {
            throw new Error(`Candidate ${candidate.id}: ${error.message}`, { cause: error });
        }
    }
    return results;
}

// Merges freshly validated results into a compatibility-filtered cache (see
// compatibleCache): each candidateId's entry is overwritten unless the fresh result is
// 'insufficient_context', in which case any stale cached entry for it is removed rather
// than kept, since deferred results must never be treated as final. The cursor wraps
// modulo the manifest size so batch scanning resumes correctly as candidates are added
// or removed between runs.
export function mergeCache(cache, manifest, validatedResults, { nextCursor } = {}) {
    const candidates = checkManifest(manifest);
    const compatible = compatibleCache(cache, manifest, candidates);
    requireCondition(Array.isArray(validatedResults), 'Validated results must be an array.');
    requireCondition(Number.isSafeInteger(nextCursor) && nextCursor >= 0, 'A valid nextCursor is required.');
    const seen = new Set();
    for (const result of validatedResults) {
        requireCondition(candidates.has(result.candidateId) && !seen.has(result.candidateId), 'Unknown or duplicate validated result.');
        seen.add(result.candidateId);
        const validated = validateResult(result, candidates.get(result.candidateId), { cached: true });
        if (validated.status !== 'insufficient_context') compatible.entries[result.candidateId] = validated;
        else delete compatible.entries[result.candidateId];
    }
    compatible.cursor = manifest.candidates.length ? nextCursor % manifest.candidates.length : 0;
    return compatible;
}

export function getCachedResults(cache, manifest) {
    const candidates = checkManifest(manifest);
    const compatible = compatibleCache(cache, manifest, candidates);
    return manifest.candidates.flatMap(candidate => Object.hasOwn(compatible.entries, candidate.id) ? [compatible.entries[candidate.id]] : []);
}
