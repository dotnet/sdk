// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1032: Implement standard exception constructors
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpImplementStandardExceptionConstructorsAnalyzer : ImplementStandardExceptionConstructorsAnalyzer
    {
        protected override string GetConstructorSignatureNoParameter(ISymbol symbol)
        {
            return $"public {symbol.Name}()";
        }

        protected override string GetConstructorSignatureStringTypeParameter(ISymbol symbol)
        {
            return $"public {symbol.Name}(string message)";
        }

        protected override string GetConstructorSignatureStringAndExceptionTypeParameter(ISymbol symbol)
        {
            return $"public {symbol.Name}(string message, Exception innerException)";
        }
    }
}