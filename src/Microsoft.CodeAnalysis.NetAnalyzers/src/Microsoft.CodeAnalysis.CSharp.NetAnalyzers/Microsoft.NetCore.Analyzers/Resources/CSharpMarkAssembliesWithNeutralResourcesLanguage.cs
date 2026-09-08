// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        protected override void RegisterAttributeAnalyzer(CompilationStartAnalysisContext context, Func<bool> shouldAnalyze, Action<SyntaxNodeAnalysisContext> onResourceFound, INamedTypeSymbol generatedCode)
        {
            context.RegisterSyntaxNodeAction(context =>
            {
                if (!shouldAnalyze())
                {
                    return;
                }

                var attributeSyntax = (AttributeSyntax)context.Node;
                if (!CheckAttribute(attributeSyntax))
                {
                    return;
                }

                if (!CheckResxGeneratedFile(context.SemanticModel, attributeSyntax, attributeSyntax.ArgumentList?.Arguments[0].Expression, generatedCode, context.CancellationToken))
                {
                    return;
                }

                onResourceFound(context);
            }, SyntaxKind.Attribute);
        }

        private static bool CheckAttribute(AttributeSyntax attribute)
        {
            return attribute?.Name?.GetLastToken().Text?.Equals(GeneratedCodeAttribute, StringComparison.Ordinal) == true &&
                attribute.ArgumentList?.Arguments.Count > 0;
        }
    }
}