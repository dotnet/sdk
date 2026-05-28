// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal sealed class EditorConfigOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _backing;

        public EditorConfigOptions(IReadOnlyDictionary<string, string> backing)
        {
            _backing = backing;
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            => _backing.TryGetValue(key, out value);
    }
}
