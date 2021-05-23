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
        protected override void RegisterAttributeAnalyzer(CompilationStartAnalysisContext context, Action onResourceFound, INamedTypeSymbol generatedCode)
        {
            context.RegisterSyntaxNodeAction(context =>
            {
                var attributeSyntax = (AttributeSyntax)context.Node;
                if (!CheckAttribute(attributeSyntax))
                {
                    return;
                }

                if (!CheckResxGeneratedFile(context.SemanticModel, attributeSyntax, attributeSyntax.ArgumentList.Arguments[0].Expression, generatedCode, context.CancellationToken))
                {
                    return;
                }

                onResourceFound();
            }, SyntaxKind.Attribute);
        }

        private static bool CheckAttribute(AttributeSyntax attribute)
        {
            return attribute?.Name?.GetLastToken().Text?.Equals(GeneratedCodeAttribute, StringComparison.Ordinal) == true &&
                attribute.ArgumentList.Arguments.Count > 0;
        }
    }
}