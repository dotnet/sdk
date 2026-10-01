// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

function requireSource(condition, message) {
    if (!condition) throw new Error(`Cannot establish comment site identity: ${message}`);
}

export function normalizeActionSource(source) {
    return source.split(/\r?\n/).map(line => line.trim()
        .replace(/^(?:\/\/+|\/\*+|\*+\/?|<!--|')\s?/, '')
        .replace(/\s?(?:\*\/|-->)$/, '')).join(' ').replace(/\s+/g, ' ').trim();
}

function literalEnd(source, start) {
    const quote = source[start];
    const delimiter = /^"+/.exec(source.slice(start))?.[0];
    if (quote === '"' && delimiter.length >= 3) {
        const end = source.indexOf(delimiter, start + delimiter.length);
        requireSource(end !== -1, 'unterminated raw string.');
        return end + delimiter.length;
    }
    const prefix = source.slice(Math.max(0, start - 2), start);
    const verbatim = quote === '"' && /(?:@|@\$)$/.test(prefix);
    const interpolated = quote === '"' && /(?:\$|\$@|@\$)$/.test(prefix);
    for (let index = start + 1; index < source.length; index++) {
        if (source[index] === '\\' && !verbatim) {
            index++;
        } else if (source[index] === quote) {
            if (verbatim && source[index + 1] === quote) index++;
            else return index + 1;
        } else if (interpolated && source[index] === '{') {
            if (source[index + 1] === '{') {
                index++;
                continue;
            }
            let depth = 1;
            while (++index < source.length && depth) {
                if (source[index] === '"' || source[index] === "'") {
                    index = literalEnd(source, index) - 1;
                } else if (source.startsWith('//', index)) {
                    const end = source.indexOf('\n', index);
                    requireSource(end !== -1, 'unterminated interpolation.');
                    index = end;
                } else if (source.startsWith('/*', index)) {
                    const end = source.indexOf('*/', index + 2);
                    requireSource(end !== -1, 'unterminated interpolation comment.');
                    index = end + 1;
                } else if (source[index] === '{') depth++;
                else if (source[index] === '}') depth--;
            }
            requireSource(depth === 0, 'unterminated interpolation.');
            index--;
        }
    }
    throw new Error('Cannot establish comment site identity: unterminated string or character literal.');
}

function* csharpTokens(source) {
    let line = 1;
    for (let start = 0; start < source.length;) {
        let end;
        let kind = 'code';
        if (source.startsWith('//', start)) {
            end = source.indexOf('\n', start);
            if (end === -1) end = source.length;
            kind = 'comment';
        } else if (source.startsWith('/*', start)) {
            const close = source.indexOf('*/', start + 2);
            requireSource(close !== -1, 'unterminated comment.');
            end = close + 2;
            kind = 'comment';
        } else if (source[start] === '"' || source[start] === "'") {
            end = literalEnd(source, start);
            kind = 'literal';
        } else if (source[start] === '#' && /(?:^|\n)[ \t]*$/.test(source.slice(0, start))) {
            end = source.indexOf('\n', start);
            if (end === -1) end = source.length;
            kind = 'whitespace';
        } else {
            const token = /^(?:\s+|@?[\p{L}_][\p{L}\p{N}\p{M}_]*|=>|.)/u.exec(source.slice(start))[0];
            end = start + token.length;
            if (/^\s/.test(token)) kind = 'whitespace';
        }
        const value = source.slice(start, end);
        const endLine = line + (value.match(/\n/g)?.length ?? 0);
        if (kind !== 'whitespace') yield { kind, value, startLine: line, endLine };
        line = endLine;
        start = end;
    }
}

function headerText(tokens) {
    return tokens.map((token, index) => {
        const previous = tokens[index - 1];
        const tight = value => value?.kind === 'code' && /^[.()[\]<>,]$/.test(value.value);
        const separator = index === 0 || tight(previous) || tight(token) ? '' : ' ';
        return separator + token.value.replace(/[\r\n\t]/g, character => ({ '\r': '\\r', '\n': '\\n', '\t': '\\t' })[character]);
    }).join('');
}

function containerHeader(tokens) {
    return tokens.some((token, index) => token.kind === 'code'
        && /^(?:namespace|class|struct|interface|record|enum)$/.test(token.value)
        && /^@?[\p{L}_]/u.test(tokens[index + 1]?.value ?? ''));
}

function recordComment(sites, token, anchor) {
    const site = { anchor };
    for (let line = token.startLine; line <= token.endLine; line++) {
        requireSource(!sites.has(line), 'multiple comments share a seed line.');
        sites.set(line, site);
    }
    return site;
}

function csharpSites(source) {
    const sites = new Map();
    const scopes = [];
    let namespace = [];
    let header = [];
    let pending = [];
    const anchor = () => `csharp: ${[...namespace, ...scopes.map(scope => scope.header)].join(' / ') || 'file'}`;
    for (const token of csharpTokens(source)) {
        if (token.kind === 'comment') {
            pending.push(recordComment(sites, token, anchor()));
            continue;
        }
        if (token.kind === 'code' && token.value === '{') {
            const container = containerHeader(header);
            const leadingDeclaration = scopes.every(scope => scope.container);
            scopes.push({ header: headerText(header) || 'block', container });
            if (leadingDeclaration) for (const site of pending) site.anchor = anchor();
            header = [];
            pending = [];
        } else if (token.kind === 'code' && token.value === '}') {
            requireSource(scopes.length > 0, 'unmatched closing brace.');
            scopes.pop();
            header = [];
            pending = [];
        } else if (token.kind === 'code' && token.value === ';') {
            if (header[0]?.value === 'namespace') {
                requireSource(namespace.length === 0 && scopes.length === 0, 'ambiguous file-scoped namespace.');
                namespace = [headerText(header)];
            }
            header = [];
            pending = [];
        } else {
            header.push(token);
        }
    }
    requireSource(scopes.length === 0, 'unclosed scope (including ambiguous conditional compilation).');
    return sites;
}

function xmlSites(source) {
    const sites = new Map();
    const scopes = [];
    let pending = [];
    let line = 1;
    const anchor = () => `xml: ${scopes.map(scope => scope.header).join(' / ') || 'file'}`;
    const tokens = /<!--[\s\S]*?-->|<!\[CDATA\[[\s\S]*?\]\]>|<\?[\s\S]*?\?>|<\/?[^<>"']*(?:(?:"[^"]*"|'[^']*')[^<>"']*)*>|[^<]+/gy;
    for (let start = 0; start < source.length;) {
        tokens.lastIndex = start;
        const match = tokens.exec(source);
        requireSource(match !== null, 'unsupported or malformed XML.');
        const value = match[0];
        const endLine = line + (value.match(/\n/g)?.length ?? 0);
        if (value.startsWith('<!--')) {
            pending.push(recordComment(sites, { startLine: line, endLine }, anchor()));
        } else if (/^<\/[\w:.-]+\s*>$/.test(value)) {
            const name = /^<\/([\w:.-]+)/.exec(value)[1];
            requireSource(scopes.at(-1)?.name === name, 'unmatched XML closing element.');
            scopes.pop();
            pending = [];
        } else if (/^<[A-Za-z_][\w:.-]*(?:\s|\/?>)/.test(value)) {
            const name = /^<([\w:.-]+)/.exec(value)[1];
            const attributes = [];
            const remainder = value.slice(name.length + 1).replace(/\/?>$/, '')
                .replace(/\s+([\w:.-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g, (_, key, double, single) => {
                    attributes.push([key, double ?? single]);
                    return '';
                });
            requireSource(remainder.trim() === '' && new Set(attributes.map(([key]) => key)).size === attributes.length,
                'unsupported or duplicate XML attributes.');
            attributes.sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0);
            scopes.push({ name, header: name + attributes.map(([key, val]) => `[${key}=${JSON.stringify(val)}]`).join('') });
            for (const site of pending) site.anchor = anchor();
            pending = [];
            if (value.endsWith('/>')) scopes.pop();
        } else if (value.startsWith('<') && !value.startsWith('<?') && !value.startsWith('<![CDATA[')) {
            throw new Error('Cannot establish comment site identity: unsupported XML declaration.');
        }
        line = endLine;
        start = tokens.lastIndex;
    }
    requireSource(scopes.length === 0, 'unclosed XML element.');
    return sites;
}

// This is a lexical source-site path, not a compiler symbol or a model assertion.
// Indistinguishable repeated comments are rejected rather than assigned shifting ordinals.
export function sourceAnchors(lines, path) {
    const source = lines.join('\n');
    const sites = path.endsWith('.cs') ? csharpSites(source) : xmlSites(source);
    const identities = new Map();
    const ambiguous = new Set();
    for (const [line, site] of sites) {
        const identity = `${site.anchor}\0${normalizeActionSource(lines[line - 1])}`;
        const previous = identities.get(identity);
        if (previous && previous.site !== site) {
            ambiguous.add(previous.line);
            ambiguous.add(line);
        }
        identities.set(identity, { site, line });
    }
    return seedLine => {
        const site = sites.get(seedLine);
        requireSource(site !== undefined, 'the action seed is not a comment.');
        requireSource(!ambiguous.has(seedLine), 'identical comments have the same structural anchor; defer this candidate.');
        requireSource(site.anchor.length <= 1024 && !/[\x00-\x1f\x7f]/.test(site.anchor),
            'the structural anchor is not bounded single-line text.');
        return site.anchor;
    };
}
