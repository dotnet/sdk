// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        // ['a', 'b', 'c'] => "abc"
        // [(byte)'a', (byte)'b', (byte)'c'] => "abc"u8
        protected override SyntaxNode? TryReplaceArrayCreationWithInlineLiteralExpression(IOperation operation)
        {
            if (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            if (operation is IInvocationOperation invocation)
            {
                if (UseSearchValuesAnalyzer.IsConstantStringToCharArrayInvocation(invocation, out _))
                {
                    Debug.Assert(invocation.Instance is not null);
                    return invocation.Instance!.Syntax;
                }

                return null;
            }

            ITypeSymbol? elementType = null;

            if (operation.Type is IArrayTypeSymbol arrayType)
            {
                elementType = arrayType.ElementType;
            }
            else if (operation.Type is INamedTypeSymbol namedType)
            {
                if (namedType.TypeArguments is [var typeArgument])
                {
                    Debug.Assert(namedType.Name.Contains("Span", StringComparison.Ordinal), namedType.Name);

                    elementType = typeArgument;
                }
            }

            if (elementType is not null)
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

                if (operation.Syntax is ExpressionSyntax creationSyntax &&
                    CSharpUseSearchValuesAnalyzer.IsConstantByteOrCharArrayCreationExpression(operation.SemanticModel!, creationSyntax, values, out _) &&
                    values.Count <= 128 &&                  // Arbitrary limit to avoid emitting huge literals
                    !ContainsAnyComments(creationSyntax))   // Avoid removing potentially valuable comments
                {
                    if (isByte)
                    {
                        foreach (char c in values)
                        {
                            if (c > 127)
                            {
                                // We shouldn't turn non-ASCII byte values into Utf8StringLiterals as that may change behavior.
                                // e.g. (byte)'ÿ' means 0xFF, but "ÿ"u8 is two bytes (0xC3, 0xBF) that encode that character in UTF8.
                                return null;
                            }
                        }
                    }

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