// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer : TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer<TypeDeclarationSyntax>
    {
        protected override DisposableFieldAnalyzer GetAnalyzer(Compilation compilation)
        {
            return new CSharpDisposableFieldAnalyzer(compilation);
        }

        private class CSharpDisposableFieldAnalyzer : DisposableFieldAnalyzer
        {
            public CSharpDisposableFieldAnalyzer(Compilation compilation)
                : base(compilation)
            { }

            protected override IEnumerable<IFieldSymbol> GetDisposableFieldCreations(SyntaxNode node, SemanticModel model,
                HashSet<ISymbol> disposableFields, CancellationToken cancellationToken)
            {
                if (node is AssignmentExpressionSyntax assignment)
                {
                    // ImplicitObjectCreationExpression	8659
                    if (assignment.Right.RawKind is (int)SyntaxKind.ObjectCreationExpression or 8659 &&
                        model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IFieldSymbol field &&
                        disposableFields.Contains(field))
                    {
                        yield return field;
                    }
                }
                else if (node is FieldDeclarationSyntax fieldDeclarationSyntax)
                {
                    foreach (VariableDeclaratorSyntax fieldInit in fieldDeclarationSyntax.Declaration.Variables)
                    {
                        // ImplicitObjectCreationExpression	8659
                        if (fieldInit.Initializer?.Value.RawKind is (int)SyntaxKind.ObjectCreationExpression or 8659 &&
                            model.GetDeclaredSymbol(fieldInit, cancellationToken) is IFieldSymbol field &&
                            disposableFields.Contains(field))
                        {
                            yield return field;
                        }
                    }
                }
            }
        }
    }
}
