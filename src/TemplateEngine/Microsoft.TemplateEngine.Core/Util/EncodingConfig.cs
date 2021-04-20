// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Util
{
    public class EncodingConfig : IEncodingConfig
    {
        private readonly Func<object>[] _values;
        private readonly List<IToken> _variableKeys;

        public EncodingConfig(IEngineConfig config, Encoding encoding)
        {
            _variableKeys = new List<IToken>();
            Encoding = encoding;
            LineEndings = new TokenTrie();
            Whitespace = new TokenTrie();
            WhitespaceOrLineEnding = new TokenTrie();
            Variables = new TokenTrie();

            foreach (string token in config.LineEndings)
            {
                IToken tokenBytes = token.Token(encoding);
                LineEndings.AddToken(tokenBytes);
                WhitespaceOrLineEnding.AddToken(tokenBytes);
            }

            foreach (string token in config.Whitespaces)
            {
                IToken tokenBytes = token.Token(encoding);
                Whitespace.AddToken(tokenBytes);
                WhitespaceOrLineEnding.AddToken(tokenBytes);
            }

            _values = new Func<object>[config.Variables.Count];
            ExpandVariables(config, encoding);
        }

        public Encoding Encoding { get; }

        public ITokenTrie LineEndings { get; }

        public IReadOnlyList<IToken> VariableKeys => _variableKeys;

        public IReadOnlyList<Func<object>> VariableValues => _values;

        public ITokenTrie Variables { get; }

        public ITokenTrie Whitespace { get; }

        public ITokenTrie WhitespaceOrLineEnding { get; }

        public object this[int index] => _values[index]();

        private void ExpandVariables(IEngineConfig config, Encoding encoding)
        {
            foreach (string key in config.Variables.Keys)
            {
                string formattedKey = string.Format(config.VariableFormatString, key);
                IToken keyBytes = formattedKey.Token(encoding);
                _variableKeys.Add(keyBytes);
                _values[Variables.AddToken(keyBytes)] = () => config.Variables[key];
            }
        }
    }
}
