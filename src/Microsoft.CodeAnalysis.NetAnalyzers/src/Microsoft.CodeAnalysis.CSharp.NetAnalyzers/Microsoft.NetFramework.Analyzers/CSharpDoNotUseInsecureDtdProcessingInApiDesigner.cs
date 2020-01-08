// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.NetFramework.Analyzers;
using Microsoft.NetFramework.Analyzers.Helpers;
using Microsoft.NetFramework.CSharp.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.CSharp.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotUseInsecureDtdProcessingInApiDesignAnalyzer : DoNotUseInsecureDtdProcessingInApiDesignAnalyzer
    {
        protected override SymbolAndNodeAnalyzer GetAnalyzer(CompilationStartAnalysisContext context, CompilationSecurityTypes types, Version targetFrameworkVersion)
        {
            SymbolAndNodeAnalyzer analyzer = new SymbolAndNodeAnalyzer(types, CSharpSyntaxNodeHelper.Default, targetFrameworkVersion);
            context.RegisterSyntaxNodeAction(analyzer.AnalyzeNode, SyntaxKind.MethodDeclaration, SyntaxKind.ConstructorDeclaration);

            return analyzer;
        }
    }
}
