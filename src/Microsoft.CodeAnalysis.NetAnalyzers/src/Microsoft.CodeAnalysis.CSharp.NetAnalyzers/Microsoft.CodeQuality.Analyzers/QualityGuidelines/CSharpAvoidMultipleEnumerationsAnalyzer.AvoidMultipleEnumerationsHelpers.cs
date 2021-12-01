// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    internal partial class CSharpAvoidMultipleEnumerationsAnalyzer
    {
        private class CSharpAvoidMultipleEnumerationsHelpers : AvoidMultipleEnumerationsHelpers
        {
            public static readonly CSharpAvoidMultipleEnumerationsHelpers Instance = new();
        }
    }
}