// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
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
                    if (assignment.Right.Kind() is SyntaxKind.ObjectCreationExpression or SyntaxKind.ImplicitObjectCreationExpression &&
                        model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IFieldSymbol field &&
                        disposableFields.Contains(field))
                    {
                        return new[] { field };
                    }
                }
                else if (node is FieldDeclarationSyntax fieldDeclarationSyntax)
                {
                    var fields = new List<IFieldSymbol>();
                    foreach (VariableDeclaratorSyntax fieldInit in fieldDeclarationSyntax.Declaration.Variables)
                    {
                        if (fieldInit.Initializer?.Value.Kind() is SyntaxKind.ObjectCreationExpression or SyntaxKind.ImplicitObjectCreationExpression &&
                            model.GetDeclaredSymbol(fieldInit, cancellationToken) is IFieldSymbol field &&
                            disposableFields.Contains(field))
                        {
                            fields.Add(field);
                        }
                    }

                    return fields;
                }

                return Enumerable.Empty<IFieldSymbol>();
            }
        }
    }
}
