using System;
using System.Collections.Generic;
using System.Text;

namespace Mutant.Chicken
{
    public class EncodingConfig
    {
        private readonly Func<object>[] _values;
        private readonly List<byte[]> _variableKeys;

        public EncodingConfig(EngineConfig config, Encoding encoding)
        {
            _variableKeys = new List<byte[]>();
            Encoding = encoding;
            LineEndings = new SimpleTrie();
            Whitespace = new SimpleTrie();
            WhitespaceOrLineEnding = new SimpleTrie();
            Variables = new SimpleTrie();

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

        public SimpleTrie LineEndings { get; }

        public IReadOnlyList<byte[]> VariableKeys => _variableKeys;

        public IReadOnlyList<Func<object>> VariableValues => _values;

        public SimpleTrie Variables { get; }

        public SimpleTrie Whitespace { get; }

        public SimpleTrie WhitespaceOrLineEnding { get; }

        public object this[int index] => _values[index]();

        private void ExpandVariables(EngineConfig config, Encoding encoding)
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