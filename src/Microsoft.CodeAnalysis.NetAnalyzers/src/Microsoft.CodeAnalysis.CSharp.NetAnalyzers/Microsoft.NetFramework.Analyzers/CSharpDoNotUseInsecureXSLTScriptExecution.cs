// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.NetFramework.Analyzers;
using Microsoft.NetFramework.Analyzers.Helpers;
using Microsoft.NetFramework.CSharp.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.CSharp.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotUseInsecureXSLTScriptExecutionAnalyzer : DoNotUseInsecureXSLTScriptExecutionAnalyzer<SyntaxKind>
    {
        protected override SyntaxNodeAnalyzer GetAnalyzer(CodeBlockStartAnalysisContext<SyntaxKind> context, CompilationSecurityTypes types)
        {
            SyntaxNodeAnalyzer analyzer = new SyntaxNodeAnalyzer(types, CSharpSyntaxNodeHelper.Default);
            context.RegisterSyntaxNodeAction(
                analyzer.AnalyzeNode,
                SyntaxKind.InvocationExpression,
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxKind.VariableDeclarator);

            return analyzer;
        }
    }
}
