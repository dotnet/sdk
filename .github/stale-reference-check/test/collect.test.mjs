// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { collect, git } from '../collect.mjs';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));

async function fixture(t, files) {
    const root = await mkdtemp(path.join(testDirectory, '.collect-'));
    t.after(() => rm(root, { recursive: true, force: true }));
    await git(root, ['init', '--quiet']);
    await git(root, ['config', 'core.autocrlf', 'false']);
    for (const [name, content] of Object.entries(files)) {
        const file = path.join(root, ...name.split('/'));
        await mkdir(path.dirname(file), { recursive: true });
        await writeFile(file, content);
    }
    await git(root, ['add', '.']);
    await git(root, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--quiet', '-m', 'Fixture']);
    return root;
}

test('collects same-line, adjacent and multiline C#/XML seeds without requiring a URL', async t => {
    const root = await fixture(t, {
        'test/ActualTests.cs': [
            'namespace Sample.Tests;',
            'class Cases {',
            '[Ignore(',
            '    "https://github.com/dotnet/sdk/issues/123")]',
            'public void First() {}',
            '// TODO: remove once the next release is consumed.',
            '// https://github.com/dotnet/runtime/pull/456',
            'public void Second() {}',
            '// workaround https://github.com/dotnet/sdk/issues/789',
            '}',
        ].join('\n'),
        'src/Build.targets': '<Project>\n<!-- Remove this when fixed\nhttps://github.com/dotnet/msbuild/issues/456\n-->\n</Project>',
        'scripts/check.ps1': '# TODO: investigate\n# https://github.com/dotnet/sdk/issues/123\nWrite-Output "hello"\n',
        'scripts/Dockerfile': '# Can be removed once the base image is updated.\n# https://github.com/dotnet/sdk/pull/123\nFROM example\n',
        'src/NotAnAttribute.cs': 'var behavior = Ignore;\n// Ignore ordinary historical references.\n',
    });
    const manifest = await collect(root, { rulesHash: 'rules-1' });
    assert.equal(manifest.schemaVersion, 1);
    assert.equal(manifest.headSha, (await git(root, ['rev-parse', 'HEAD'])).trim());
    assert.equal(manifest.candidates.length, 6);
    const tests = manifest.candidates.filter(candidate => candidate.path === 'test/ActualTests.cs');
    assert.deepEqual(tests.map(candidate => candidate.seedLine), [3, 6, 9]);
    assert.deepEqual(tests.map(candidate => candidate.kindHint), ['ignore', 'todo', 'workaround']);
    assert.equal(new Set(tests.map(candidate => candidate.windowId)).size, 1);
    assert.equal(new Set(tests.map(candidate => candidate.id)).size, 3);
    assert.match(tests[0].context, /4:     "https:\/\/github.com\/dotnet\/sdk\/issues\/123"/);
    assert.ok(manifest.candidates.every(candidate => !candidate.path.includes('\\')));
    assert.deepEqual(await collect(root, { rulesHash: 'rules-1' }), manifest);
});

test('uses tracked committed files and never modifies checkout, index, or HEAD', async t => {
    const root = await fixture(t, { 'src/Owned.cs': '// TODO https://github.com/dotnet/sdk/issues/1\n' });
    await writeFile(path.join(root, 'src', 'Untracked.cs'), '// TODO untracked');
    await writeFile(path.join(root, 'src', 'Owned.cs'), '// changed outside committed source\n');
    const index = await readFile(path.join(root, '.git', 'index'));
    const status = await git(root, ['status', '--porcelain=v1']);
    const head = await git(root, ['rev-parse', 'HEAD']);
    const result = await collect(root, { rulesHash: 'rules' });
    assert.deepEqual(result.candidates.map(candidate => candidate.path), ['src/Owned.cs']);
    assert.match(result.candidates[0].context, /TODO/);
    assert.deepEqual(await readFile(path.join(root, '.git', 'index')), index);
    assert.equal(await git(root, ['status', '--porcelain=v1']), status);
    assert.equal(await git(root, ['rev-parse', 'HEAD']), head);
});

test('excludes docs, prompts, localization, generated attributes, vendoring and test input trees', async t => {
    const ignored = [
        'docs/example.cs', 'documentation/example.targets', '.github/agents/sample.js',
        '.github/prompts/sample.ps1', '.github/skills/check/example.cs',
        'test/TestAssets/Example/Program.cs', 'test/Suite/Fixtures/Input.cs',
        'test/Suite/TestData/Program.cs', 'test/Suite/Baselines/Sample.xml',
        'test/Suite/Snapshots/Program.cs', 'src/Resources.resx', 'src/Resources.xlf',
        'src/Foo.g.cs', 'src/Foo.Designer.cs', 'src/Expected.verified.xml',
        'eng/common/build.ps1', 'vendor/third.cs', 'src/Copy.cs', 'src/Vendor/Sub.cs',
        '.github/ISSUE_TEMPLATE/10_bug_report.yml',
        'src/GeneratedByAttribute.cs', 'src/VendoredByAttribute.cs',
        'src/ProjectTemplates/Web/Program.cs', '.github/workflows/example.lock.yml',
    ];
    const files = Object.fromEntries(ignored.map(name => [name, '// TODO https://github.com/dotnet/sdk/issues/1']));
    Object.assign(files, {
        '.gitattributes': 'src/GeneratedByAttribute.cs linguist-generated=true\nsrc/VendoredByAttribute.cs linguist-vendored\nsrc/Owned.cs linguist-generated=false\n',
        'eng/vendored-files.json': JSON.stringify({ entries: [{ local_path: 'src/Copy.cs' }, { local_path: 'src/Vendor' }] }),
        'src/Owned.cs': '// TODO https://github.com/dotnet/sdk/issues/2',
        'src/Copy.cs.extra.cs': '// TODO not a path-prefix match https://github.com/dotnet/sdk/issues/123',
        'src/VendorExtra/Owned.cs': '// TODO not a directory-prefix match https://github.com/dotnet/sdk/issues/123',
        '.github/scripts/owned.mjs': '// TODO real automation https://github.com/dotnet/sdk/issues/123',
    });
    const root = await fixture(t, files);
    const result = await collect(root, { rulesHash: 'rules' });
    assert.deepEqual(result.candidates.map(candidate => candidate.path), [
        '.github/scripts/owned.mjs', 'src/Copy.cs.extra.cs', 'src/Owned.cs', 'src/VendorExtra/Owned.cs',
    ]);
});

test('keeps separate windows, exact twenty-line context, Unicode paths and whole-blob IDs', async t => {
    const lines = Array.from({ length: 180 }, (_, index) => `// line ${index + 1}`);
    lines[25] = '// TODO first';
    lines[75] = '// TODO second';
    lines[170] = '// https://github.com/dotnet/sdk/issues/123';
    const root = await fixture(t, { 'src/Ünicode space.cs': lines.join('\n') });
    const before = await collect(root, { rulesHash: 'rules' });
    assert.deepEqual(before.candidates.map(({ startLine, endLine }) => [startLine, endLine]), [[6, 46], [56, 96]]);
    assert.equal(before.candidates[0].sourceLineCount, 180);
    lines[179] = '// unrelated edit outside both windows';
    await writeFile(path.join(root, 'src', 'Ünicode space.cs'), lines.join('\n'));
    await git(root, ['add', '.']);
    await git(root, ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '--quiet', '-m', 'Change']);
    const after = await collect(root, { rulesHash: 'rules' });
    assert.equal(before.candidates[0].context, after.candidates[0].context);
    assert.notEqual(before.candidates[0].id, after.candidates[0].id);
    assert.notEqual(before.candidates[0].blobSha, after.candidates[0].blobSha);
});

test('excludes its own synthetic test cases without excluding real automation source', async t => {
    const root = await fixture(t, {
        '.github/stale-reference-check/test/collect.test.mjs': [
            'const input = [',
            '\'[Ignore("https://github.com/dotnet/sdk/issues/123")]\',',
            '\'// TODO remove once https://github.com/dotnet/sdk/issues/123 is fixed\',',
            '];',
        ].join('\n'),
        '.github/stale-reference-check/test/finalize.test.mjs':
            '// Workaround fixture https://github.com/dotnet/sdk/issues/123\n',
        '.github/stale-reference-check/maintenance.mjs':
            '// TODO real automation https://github.com/dotnet/sdk/issues/456\n',
    });
    const result = await collect(root, { rulesHash: 'rules' });
    assert.deepEqual(result.candidates.map(candidate => candidate.path),
        ['.github/stale-reference-check/maintenance.mjs']);
});

test('excludes files without issue/PR URLs but retains seeds with references beyond their window', async t => {
    const root = await fixture(t, {
        'src/NoUrl.cs': '// TODO remove this later\n// Workaround for an old failure\n',
        'src/NotAnIssue.cs': '// TODO https://github.com/dotnet/sdk/blob/main/file.cs\n',
        'src/InvalidNumber.cs': '// TODO https://github.com/dotnet/sdk/issues/123abc\n',
        'src/OtherHost.cs': '// TODO https://example.com/dotnet/sdk/issues/123\n',
        'src/Adjacent.cs': '// TODO remove this\n// https://github.com/dotnet/sdk/issues/123?query=value#comment\n',
        'src/Multiline.targets': '<Project>\n<!-- Workaround until consumed\nhttps://github.com/dotnet/msbuild/pull/456\n-->\n</Project>',
        'src/Distant.cs': ['// TODO investigate', ...Array(100).fill('// spacer'), '// https://github.com/dotnet/sdk/issues/789'].join('\n'),
    });
    const result = await collect(root, { rulesHash: 'rules' });
    assert.deepEqual(result.candidates.map(candidate => candidate.path), ['src/Adjacent.cs', 'src/Distant.cs', 'src/Multiline.targets']);
    const distant = result.candidates.find(candidate => candidate.path === 'src/Distant.cs');
    assert.equal(distant.seedLine, 1);
    assert.equal(distant.endLine, 21);
    assert.doesNotMatch(distant.context, /https:\/\/github\.com/);
});

test('fails closed for malformed vendoring and missing rules version', async t => {
    const root = await fixture(t, { 'eng/vendored-files.json': '{"entries":[{"wrong":"src/A.cs"}]}' });
    await assert.rejects(collect(root, { rulesHash: 'rules' }), /repository-relative path/);
    await assert.rejects(collect(root, {}), /rulesHash/);
});
