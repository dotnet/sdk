// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Base class for reporting the api compat diagnostics.
    /// </summary>
    public class ApiCompatDiagnostics
    {
        public ApiCompatDiagnostics(string compatibilityReason, string header, string noWarn, (string, string)[] ignoredDifferences, IEnumerable<CompatDifference> differences)
        {
            CompatibilityReason = compatibilityReason;
            Header = header;
            Differences = new(noWarn, ignoredDifferences);
            Differences.AddRange(differences);
        }

        public DiagnosticBag<CompatDifference> Differences { get; private set; }
        public string CompatibilityReason { get; private set; }
        public string Header { get; private set; }
    }
}
