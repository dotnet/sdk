// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1024: Use properties where appropriate
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpUsePropertiesWhereAppropriateFixer : UsePropertiesWhereAppropriateFixer
    {
    }
}