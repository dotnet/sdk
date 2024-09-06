// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
