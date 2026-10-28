// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        public override void RegisterLanguageSpecificChecks(CompilationStartAnalysisContext context, ConcurrentDictionary<INamedTypeSymbol, object?> instantiatedTypes)
        {
            context.RegisterSyntaxNodeAction(context =>
            {
                var usingDirective = (UsingDirectiveSyntax)context.Node;

                if (usingDirective.Alias != null &&
                    usingDirective.DescendantNodes().OfType<GenericNameSyntax>().Any() &&
                    context.SemanticModel.GetDeclaredSymbol(usingDirective) is IAliasSymbol aliasSymbol &&
                    aliasSymbol.Target is INamedTypeSymbol namedTypeSymbol &&
                    namedTypeSymbol.IsGenericType)
                {
                    var generics = namedTypeSymbol.TypeParameters.Zip(namedTypeSymbol.TypeArguments, (parameter, argument) => (parameter, argument));
                    ProcessGenericTypes(generics, instantiatedTypes);
                }
            }, SyntaxKind.UsingDirective);
        }
    }
}
