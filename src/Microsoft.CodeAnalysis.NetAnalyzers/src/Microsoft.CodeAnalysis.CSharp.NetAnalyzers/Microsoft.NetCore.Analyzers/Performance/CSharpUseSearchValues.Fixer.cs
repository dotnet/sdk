// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <inheritdoc/>
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpUseSearchValuesFixer : UseSearchValuesFixer
    {
        protected override async ValueTask<(SyntaxNode TypeDeclaration, INamedTypeSymbol? TypeSymbol, bool IsRealType)> GetTypeSymbolAsync(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
        {
            SyntaxNode? typeDeclarationOrCompilationUnit = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();

            typeDeclarationOrCompilationUnit ??= await node.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            return typeDeclarationOrCompilationUnit is TypeDeclarationSyntax typeDeclaration
                ? (typeDeclaration, semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken), IsRealType: true)
                : (typeDeclarationOrCompilationUnit, semanticModel.GetDeclaredSymbol((CompilationUnitSyntax)typeDeclarationOrCompilationUnit, cancellationToken)?.ContainingType, IsRealType: false);
        }

        protected override SyntaxNode ReplaceSearchValuesFieldName(SyntaxNode node)
        {
            if (node is FieldDeclarationSyntax fieldDeclaration &&
                fieldDeclaration.Declaration is { } declaration &&
                declaration.Variables is [var declarator])
            {
                var newDeclarator = declarator.ReplaceToken(declarator.Identifier, declarator.Identifier.WithAdditionalAnnotations(RenameAnnotation.Create()));
                return fieldDeclaration.WithDeclaration(declaration.WithVariables(new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(newDeclarator)));
            }

            return node;
        }

        protected override SyntaxNode GetDeclaratorInitializer(SyntaxNode syntax)
        {
            if (syntax is VariableDeclaratorSyntax variableDeclarator)
            {
                return variableDeclarator.Initializer!.Value;
            }

            if (syntax is PropertyDeclarationSyntax propertyDeclaration)
            {
                return CSharpUseSearchValuesAnalyzer.TryGetPropertyGetterExpression(propertyDeclaration)!;
            }

            throw new InvalidOperationException($"Expected 'VariableDeclaratorSyntax' or 'PropertyDeclarationSyntax', got {syntax.GetType().Name}");
        }

        // new[] { 'a', 'b', 'c' } => "abc"
        // new[] { (byte)'a', (byte)'b', (byte)'c' } => "abc"u8
        // "abc".ToCharArray() => "abc"
        protected override SyntaxNode? TryReplaceArrayCreationWithInlineLiteralExpression(IOperation operation)
        {
            if (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            if (operation is IArrayCreationOperation arrayCreation &&
                arrayCreation.GetElementType() is { } elementType)
            {
                bool isByte = elementType.SpecialType == SpecialType.System_Byte;

                if (isByte &&
                    (operation.SemanticModel?.Compilation is not CSharpCompilation compilation ||
                    compilation.LanguageVersion < (LanguageVersion)1100)) // LanguageVersion.CSharp11
                {
                    // Can't use Utf8StringLiterals
                    return null;
                }

                List<char> values = new();

                if (arrayCreation.Syntax is ExpressionSyntax creationSyntax &&
                    CSharpUseSearchValuesAnalyzer.IsConstantByteOrCharArrayCreationExpression(operation.SemanticModel!, creationSyntax, values, out _) &&
                    values.Count <= 128 &&                  // Arbitrary limit to avoid emitting huge literals
                    !ContainsAnyComments(creationSyntax))   // Avoid removing potentially valuable comments
                {
                    string valuesString = string.Concat(values);
                    string stringLiteral = SymbolDisplay.FormatLiteral(valuesString, quote: true);

                    const SyntaxKind Utf8StringLiteralExpression = (SyntaxKind)8756;
                    const SyntaxKind Utf8StringLiteralToken = (SyntaxKind)8520;

                    return SyntaxFactory.LiteralExpression(
                        isByte ? Utf8StringLiteralExpression : SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Token(
                            leading: default,
                            kind: isByte ? Utf8StringLiteralToken : SyntaxKind.StringLiteralToken,
                            text: isByte ? $"{stringLiteral}u8" : stringLiteral,
                            valueText: valuesString,
                            trailing: default));
                }
            }
            else if (operation is IInvocationOperation invocation)
            {
                if (UseSearchValuesAnalyzer.IsConstantStringToCharArrayInvocation(invocation, out _))
                {
                    Debug.Assert(invocation.Instance is not null);
                    return invocation.Instance!.Syntax;
                }
            }

            return null;
        }

        private static bool ContainsAnyComments(SyntaxNode node)
        {
            foreach (SyntaxTrivia trivia in node.DescendantTrivia(node.Span))
            {
                if (trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia)
                {
                    return true;
                }
            }

            return false;
        }
    }
}