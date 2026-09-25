// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { lstat, readFile, realpath } from 'node:fs/promises';
import path from 'node:path';

const ignorePattern = String.raw`(\[|<|,|^[[:space:]]*)[[:space:]]*([[:alnum:]_.]+[.])?Ignore(Attribute)?([[:space:]]*\(|[[:space:]]*\]|[[:space:]]*>|[[:space:]]*$)`;
const seedPattern = `${ignorePattern}|` + String.raw`\bTODO\b|\bwork[ -]?around\b|\b(remov(e|ed|al)|delet(e|ed|ion)|revisit|re-?enabl(e|ed))\b.*\b(when|once|after|until)\b|\b(temporary|temporarily)\b.*\b(until|fix|hack)\b`;
const sourceExtension = /\.(cs|vb|fs|fsx|c|cc|cpp|h|hpp|js|mjs|cjs|ts|tsx|jsx|py|ps1|psm1|psd1|sh|bash|cmd|bat|yml|yaml|xml|props|targets|proj|projitems|csproj|vbproj|fsproj|slnx|nuspec|razor|cshtml|config|cmake)$/i;
const excludedDirectory = /(^|\/)(documentation|docs?|prompts?|memory|skills|agents|instructions|issue_templates?|testassets|testdata|testinputs?|testresources|fixtures|__fixtures__|baselines?|snapshots?|generated|vendor|vendored|third[-_]?party|node_modules|bin|obj|artifacts|template_feed)(\/|$)/i;
// Keep collection permissive enough to find shorthand references; the validator
// later decides whether each reference can authorize a tracking action.
const githubReference = /(?:https:\/\/github\.com\/[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+\/(?:issues|pull)\/[1-9]\d*|[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?\/[A-Za-z0-9_.-]+#[1-9]\d*)(?=\/?(?:[?#]|$|[\s"'<>()[\]{},.;:]))/;

export function git(repoRoot, args, { input, allowedExitCodes = [0] } = {}) {
    return new Promise((resolve, reject) => {
        // Read through Git's object database so collection is tied to the
        // committed tree, not to untracked or working-tree-only content.
        const child = spawn('git', ['--no-pager', ...args], {
            cwd: repoRoot,
            windowsHide: true,
            env: { ...process.env, GIT_OPTIONAL_LOCKS: '0', GIT_TERMINAL_PROMPT: '0' },
            stdio: ['pipe', 'pipe', 'pipe'],
        });
        const stdout = [];
        const stderr = [];
        let size = 0;
        child.on('error', reject);
        child.stdout.on('data', data => {
            size += data.length;
            if (size > 64 * 1024 * 1024) {
                child.kill();
                reject(new Error('Git output exceeded 64 MiB; inventory was not truncated.'));
            } else {
                stdout.push(data);
            }
        });
        child.stderr.on('data', data => stderr.push(data));
        child.stdin.on('error', error => {
            if (error.code !== 'EPIPE') reject(error);
        });
        child.on('close', code => {
            if (!allowedExitCodes.includes(code)) {
                reject(new Error(`git ${args[0]} failed (${code}): ${Buffer.concat(stderr).toString('utf8')}`));
            } else {
                resolve(Buffer.concat(stdout).toString('utf8'));
            }
        });
        child.stdin.end(input);
    });
}

export function assertRepoPath(value) {
    if (typeof value !== 'string' || !value || value.includes('\\') || /[\x00-\x1f\x7f:]/.test(value)
        || path.posix.isAbsolute(value) || value.split('/').some(part => !part || part === '.' || part === '..')) {
        throw new Error('Invalid canonical repository-relative path.');
    }
    return value;
}

export function sourceLines(text) {
    const lines = text.replace(/\r\n/g, '\n').split('\n');
    if (lines.at(-1) === '') lines.pop();
    return lines;
}

export function sourceId(filePath, blobSha, seedLine) {
    return createHash('sha256').update(JSON.stringify([filePath, blobSha, seedLine])).digest('hex');
}

export function numberedContext(lines, startLine, endLine) {
    return lines.slice(startLine - 1, endLine).map((line, index) => `${startLine + index}: ${line}`).join('\n');
}

export async function readSource(repoRoot, candidate) {
    assertRepoPath(candidate.path);
    const root = await realpath(repoRoot);
    const file = path.join(root, ...candidate.path.split('/'));
    const resolved = await realpath(file);
    const relative = path.relative(root, resolved);
    if (relative.startsWith(`..${path.sep}`) || relative === '..' || path.isAbsolute(relative)
        || (await lstat(file)).isSymbolicLink()) {
        throw new Error(`Source path escapes the repository or is a symlink: ${candidate.path}`);
    }
    const bytes = await readFile(file);
    const blobSha = (await git(root, ['hash-object', `--path=${candidate.path}`, '--stdin'], { input: bytes })).trim();
    if (blobSha !== candidate.blobSha) throw new Error(`Source blob mismatch: ${candidate.path}`);
    const lines = sourceLines(await git(root, ['cat-file', 'blob', candidate.blobSha]));
    if (lines.length !== candidate.sourceLineCount) throw new Error(`Source line count mismatch: ${candidate.path}`);
    return lines;
}

function ownedSource(filePath) {
    return (sourceExtension.test(filePath) || /(^|\/)(Dockerfile(?:\.[^/]+)?|Makefile|CMakeLists\.txt)$/i.test(filePath))
        && !excludedDirectory.test(filePath)
        && !/^\.github\/stale-reference-check\/test\//i.test(filePath)
        && !/^eng\/common\//i.test(filePath)
        && !/^src\/(projecttemplates|itemtemplates|templates)\//i.test(filePath)
        && !/\.(g|g\.i|generated|designer|min|verified|received)\./i.test(filePath)
        && !/\.(lock\.yml|resx|xlf)$/i.test(filePath);
}

export async function collect(repoRoot, { rulesHash } = {}) {
    if (typeof rulesHash !== 'string' || !rulesHash.trim() || rulesHash.length > 256) {
        throw new Error('A nonempty rulesHash is required.');
    }
    const headSha = (await git(repoRoot, ['rev-parse', '--verify', 'HEAD'])).trim();
    const tree = await git(repoRoot, ['ls-tree', '-r', '-z', headSha]);
    const files = new Map();
    for (const record of tree.split('\0').filter(Boolean)) {
        const match = /^(\d+) blob ([a-f0-9]+)\t([\s\S]+)$/.exec(record);
        if (match && (match[1] === '100644' || match[1] === '100755')) {
            assertRepoPath(match[3]);
            files.set(match[3], match[2]);
        }
    }
    const vendored = [];
    if (files.has('eng/vendored-files.json')) {
        const manifest = JSON.parse(await git(repoRoot, ['cat-file', 'blob', files.get('eng/vendored-files.json')]));
        if (!Array.isArray(manifest.entries)) throw new Error('Malformed vendored-file manifest.');
        for (const entry of manifest.entries) {
            vendored.push(assertRepoPath(entry.local_path));
        }
    }
    const eligible = new Set([...files.keys()].filter(filePath => ownedSource(filePath)
        && !vendored.some(item => filePath === item || filePath.startsWith(`${item}/`))));
    if (eligible.size) {
        const attrs = (await git(repoRoot, ['check-attr', `--source=${headSha}`, '-z', '--stdin', 'linguist-generated', 'linguist-vendored'],
            { input: [...eligible].join('\0') + '\0' })).split('\0');
        for (let index = 0; index + 2 < attrs.length; index += 3) {
            if (attrs[index + 2] === 'true' || attrs[index + 2] === 'set') eligible.delete(attrs[index]);
        }
    }
    const grep = await git(repoRoot, ['grep', '-n', '-z', '-I', '-i', '-E', '-e', seedPattern, headSha, '--'],
        { allowedExitCodes: [0, 1] });
    const hits = new Map();
    const record = /([^\0]+)\0(\d+)\0([^\n]*)(?:\n|$)/g;
    for (const match of grep.matchAll(record)) {
        const filePath = match[1].slice(headSha.length + 1);
        if (!eligible.has(filePath)) continue;
        const line = Number(match[2]);
        const kindHint = /(?:\[|<|,|^\s*)\s*(?:[\w.]+\.)?Ignore(?:Attribute)?(?:\s*\(|\s*\]|\s*>|\s*$)/i.test(match[3]) ? 'ignore'
            : /\bTODO\b/i.test(match[3]) ? 'todo' : 'workaround';
        if (!hits.has(filePath)) hits.set(filePath, []);
        hits.get(filePath).push({ seedLine: line, kindHint });
    }
    const candidates = [];
    for (const filePath of [...hits.keys()].sort()) {
        const blobSha = files.get(filePath);
        const lines = sourceLines(await git(repoRoot, ['cat-file', 'blob', blobSha]));
        if (!githubReference.test(lines.join('\n'))) continue;
        const windows = [];
        for (const hit of hits.get(filePath).sort((a, b) => a.seedLine - b.seedLine)) {
            const startLine = Math.max(1, hit.seedLine - 20);
            const endLine = Math.min(lines.length, hit.seedLine + 20);
            const previous = windows.at(-1);
            if (previous && startLine <= previous.endLine) {
                previous.endLine = Math.max(previous.endLine, endLine);
                previous.seeds.push(hit);
            } else {
                windows.push({ startLine, endLine, seeds: [hit] });
            }
        }
        for (const window of windows) {
            const context = numberedContext(lines, window.startLine, window.endLine);
            const windowId = sourceId(filePath, blobSha, `${window.startLine}-${window.endLine}`);
            for (const hit of window.seeds) {
                candidates.push({
                    id: sourceId(filePath, blobSha, hit.seedLine), path: filePath, blobSha,
                    ...hit, startLine: window.startLine, endLine: window.endLine, context,
                    sourceLineCount: lines.length, windowId,
                });
            }
        }
    }
    return { schemaVersion: 1, headSha, rulesHash, candidates };
}
