// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import assert from 'node:assert/strict';
import test from 'node:test';
import { sourceAnchors } from '../source-anchors.mjs';

const seed = '// TODO https://github.com/dotnet/sdk/issues/123';
const anchor = (lines, file = 'src/File.cs', line = lines.findIndex(text => text.includes('TODO')) + 1) =>
    sourceAnchors(lines, file)(line);

test('C# literals, comments, attributes and nested scopes cannot fabricate declaration paths', () => {
    const lines = [
        'namespace Outer { namespace Inner {',
        '/* class Fake { void Missing() { */',
        '[Attribute("class Imaginary {")]',
        'class Real {',
        'void Run() {',
        'var normal = "class Bogus { \\"quoted\\" }";',
        'var verbatim = @"class Fake { ""quoted"" }";',
        'var raw = """class Fake { "quoted" }""";',
        'var interpolated = $"text {Call("nested", new Thing { Value = \'}\' })}";',
        'var interpolatedVerbatim = $@"text {Call(@"nested", \'{\')}";',
        'var rawInterpolated = $$"""text {{Call("nested")}} { class Fake }""";',
        seed,
        '} } } }',
    ];
    assert.equal(anchor(lines), 'csharp: namespace Outer / namespace Inner / [Attribute("class Imaginary {")]class Real / void Run()');
    assert.throws(() => anchor(['var text = "TODO https://github.com/dotnet/sdk/issues/123";']), /not a comment/);
});

test('overloads, different types and different control-flow blocks have distinct lexical sites', () => {
    const lines = [
        'namespace N;',
        'class First {',
        `void Run(int value) { ${seed}`,
        '}',
        `void Run(string value) { ${seed}`,
        '}',
        'void Branches() {',
        `if (a) { ${seed}`,
        '}',
        `if (b) { ${seed}`,
        '}',
        '} }',
        'class Second {',
        `void Run(int value) { ${seed}`,
        '} }',
    ];
    const sites = sourceAnchors(lines, 'src/File.cs');
    const anchors = [3, 5, 8, 10, 14].map(line => sites(line));
    assert.equal(new Set(anchors).size, anchors.length);
});

test('indistinguishable sites defer instead of relying on line numbers or occurrence ordinals', () => {
    const lines = ['class C { void Run() {', seed, 'Call();', seed, '} }'];
    const sites = sourceAnchors(lines, 'src/File.cs');
    assert.throws(() => sites(2), /identical comments/);
    assert.throws(() => sites(4), /identical comments/);
    const xml = ['<Project>', `<PropertyGroup><!-- TODO ${seed} --></PropertyGroup>`,
        `<PropertyGroup><!-- TODO ${seed} --></PropertyGroup>`, '</Project>'];
    const xmlSites = sourceAnchors(xml, 'src/File.props');
    assert.throws(() => xmlSites(2), /identical comments/);
    assert.throws(() => xmlSites(3), /identical comments/);
});

test('XML attributes have stable ordering and comments can anchor following elements', () => {
    const first = ['<Project>', `<!-- TODO ${seed} -->`, '<Target Name="A" Condition="true" />', '</Project>'];
    const second = ['<Project>', `<!-- TODO ${seed} -->`, "<Target Condition='true' Name='A' />", '</Project>'];
    assert.equal(anchor(first, 'src/File.targets'), 'xml: Project / Target[Condition="true"][Name="A"]');
    assert.equal(anchor(first, 'src/File.targets'), anchor(second, 'src/File.targets'));
});

test('multiline comments retain their owning site and reject ambiguity only at the matching seed', () => {
    const lines = ['class C { void Run() {', '/*', ` * TODO ${seed}`, ' *', ' */',
        '/*', ' * other comment', ' *', ' */', '} }'];
    assert.equal(anchor(lines), 'csharp: class C / void Run()');
    const literal = ['[Attribute("""first', 'second () line""")]', 'class C {', seed, '}'];
    assert.equal(anchor(literal), 'csharp: [Attribute("""first\\nsecond () line""")]class C');
    assert.notEqual(anchor(literal), anchor(literal.map(line => line.replace('() line', '()line'))));
});

test('unsupported, malformed and overlong structures are explicit failures', () => {
    for (const lines of [
        ['}', seed], ['class C {', seed], ['var s = "unclosed', seed], ['/* unclosed', seed],
        ['namespace A;', 'namespace B;', seed],
        [`class ${'X'.repeat(1024)} {`, seed, '}'],
    ]) assert.throws(() => anchor(lines), /Cannot establish comment site identity/);
    for (const lines of [
        ['<Project>', `<!-- TODO ${seed} -->`, '</Other>'],
        ['<Project>', `<!-- TODO ${seed} -->`],
        ['<!DOCTYPE Project>', '<Project>', `<!-- TODO ${seed} -->`, '</Project>'],
        ['<Project Name="A" Name="B">', `<!-- TODO ${seed} -->`, '</Project>'],
    ]) assert.throws(() => anchor(lines, 'src/File.props'), /Cannot establish comment site identity/);
});
