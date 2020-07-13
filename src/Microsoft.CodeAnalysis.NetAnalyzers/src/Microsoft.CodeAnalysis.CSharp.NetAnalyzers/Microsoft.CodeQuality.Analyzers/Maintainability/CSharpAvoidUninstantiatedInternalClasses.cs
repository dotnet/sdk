// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpAvoidUninstantiatedInternalClasses : AvoidUninstantiatedInternalClassesAnalyzer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisPerformance", "RS1012:Start action has no registered actions.", Justification = "End action is registered in parent class.")]
        public override void RegisterLanguageSpecificChecks(CompilationStartAnalysisContext startContext, ConcurrentDictionary<INamedTypeSymbol, object?> instantiatedTypes)
        {
            startContext.RegisterSyntaxNodeAction(context =>
            {
                var usingDirective = (UsingDirectiveSyntax)context.Node;

                if (usingDirective.Alias != null &&
                    context.SemanticModel.GetDeclaredSymbol(usingDirective) is IAliasSymbol aliasSymbol &&
                    aliasSymbol.Target is INamedTypeSymbol namedTypeSymbol &&
                    namedTypeSymbol.IsGenericType)
                {
                    var genericTypesWithNewConstraint = namedTypeSymbol.TypeParameters.Zip(namedTypeSymbol.TypeArguments, (parameter, argument) => (parameter, argument))
                        .Where(tuple => tuple.parameter.HasConstructorConstraint && tuple.argument is INamedTypeSymbol);
                    foreach ((var _, var argument) in genericTypesWithNewConstraint)
                    {
                        instantiatedTypes.TryAdd((INamedTypeSymbol)argument, null);
                    }
                }
            }, SyntaxKind.UsingDirective);
        }
    }
}
