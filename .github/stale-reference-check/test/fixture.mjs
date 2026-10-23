// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { git } from '../collect.mjs';

// Creates a temporary git repository containing `files` (repo-relative path -> content) in one commit.
export async function createGitRepo(t, files, { directory, prefix }) {
    const root = await mkdtemp(path.join(directory, prefix));
    t.after(() => rm(root, { recursive: true, force: true }));
    await git(root, ['init', '--quiet']);
    await git(root, ['config', 'core.autocrlf', 'false']);
    for (const [name, content] of Object.entries(files)) {
        const file = path.join(root, ...name.split('/'));
        await mkdir(path.dirname(file), { recursive: true });
        await writeFile(file, content);
    }
    await git(root, ['add', '.']);
    await git(root, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Fixture']);
    return root;
}
