using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class EncodingConfig : IEncodingConfig
    {
        private readonly Func<object>[] _values;
        private readonly List<byte[]> _variableKeys;

        public EncodingConfig(IEngineConfig config, Encoding encoding)
        {
            _variableKeys = new List<byte[]>();
            Encoding = encoding;
            LineEndings = new TokenTrie();
            Whitespace = new TokenTrie();
            WhitespaceOrLineEnding = new TokenTrie();
            Variables = new TokenTrie();

            foreach (string token in config.LineEndings)
            {
                byte[] tokenBytes = encoding.GetBytes(token);
                LineEndings.AddToken(tokenBytes);
                WhitespaceOrLineEnding.AddToken(tokenBytes);
            }

            foreach (string token in config.Whitespaces)
            {
                byte[] tokenBytes = encoding.GetBytes(token);
                Whitespace.AddToken(tokenBytes);
                WhitespaceOrLineEnding.AddToken(tokenBytes);
            }

            _values = new Func<object>[config.Variables.Count];
            ExpandVariables(config, encoding);
        }

        public Encoding Encoding { get; }

        public ITokenTrie LineEndings { get; }

        public IReadOnlyList<byte[]> VariableKeys => _variableKeys;

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
                byte[] keyBytes = encoding.GetBytes(formattedKey);
                _variableKeys.Add(keyBytes);
                _values[Variables.AddToken(keyBytes)] = () => config.Variables[key];
            }
        }
    }
}