// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core
{
    internal class EnvironmentInfo : IEnvironmentInfo
    {
        private readonly IReadOnlyDictionary<string, object> _source;

        public EnvironmentInfo(IReadOnlyDictionary<string, object> source)
        {
            _source = source;
        }

        public int Count => _source.Count;

        public IEnumerable<string> Keys => _source.Keys;

        public IEnumerable<object> Values => _source.Values;

        public object this[string key] => _source[key];

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _source.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool ContainsKey(string key) => _source.ContainsKey(key);

        public bool TryGetValue(string key, out object value) => _source.TryGetValue(key, out value);
    }
}
