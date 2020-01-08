// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Resources;

namespace Microsoft.NetCore.CSharp.Analyzers.Resources
{
    /// <summary>
    /// CA1824: Mark assemblies with NeutralResourcesLanguageAttribute
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpMarkAssembliesWithNeutralResourcesLanguageAnalyzer : MarkAssembliesWithNeutralResourcesLanguageAnalyzer
    {
        protected override void RegisterAttributeAnalyzer(CompilationStartAnalysisContext context, Action onResourceFound)
        {
            context.RegisterSyntaxNodeAction(nc =>
            {
                if (!CheckAttribute(nc.Node))
                {
                    return;
                }

                if (!CheckResxGeneratedFile(nc.SemanticModel, nc.Node, ((AttributeSyntax)nc.Node).ArgumentList.Arguments[0].Expression, nc.CancellationToken))
                {
                    return;
                }

                onResourceFound();
            }, SyntaxKind.Attribute);
        }

        private static bool CheckAttribute(SyntaxNode node)
        {
            var attribute = node as AttributeSyntax;
            return attribute?.Name?.GetLastToken().Text?.Equals(GeneratedCodeAttribute, StringComparison.Ordinal) == true &&
                attribute.ArgumentList.Arguments.Count > 0;
        }
    }
}