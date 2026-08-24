// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsFixer : CodeFixProvider
    {
        /// <summary>
        /// Carries the declarators to convert, grouped by their containing field declaration.
        /// </summary>
        /// <remarks>
        /// Several array fields declared together produce one diagnostic each, and whether the declaration
        /// survives the fix at all depends on the whole set of them, so the declaration is rewritten once
        /// from the complete set rather than once per diagnostic.
        /// </remarks>
        private sealed class FixState
        {
            private readonly Dictionary<FieldDeclarationSyntax, List<VariableDeclaratorSyntax>> _declaratorsByField = new();

            public List<FieldDeclarationSyntax> OrderedFields { get; } = new();

            public List<VariableDeclaratorSyntax> GetDeclarators(FieldDeclarationSyntax fieldDeclaration)
            {
                if (!_declaratorsByField.TryGetValue(fieldDeclaration, out var declarators))
                {
                    declarators = new List<VariableDeclaratorSyntax>();
                    _declaratorsByField.Add(fieldDeclaration, declarators);
                    OrderedFields.Add(fieldDeclaration);
                }

                return declarators;
            }
        }

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer.RuleId);

        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create(
                _ => new FixState(),
                ApplyFixAsync,
                getFixedDocument: GetFixedDocument);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root is null)
            {
                return;
            }

            var document = context.Document;
            var diagnostics = context.Diagnostics;
            foreach (var diagnostic in diagnostics)
            {
                if (root.FindNode(diagnostic.Location.SourceSpan) is not VariableDeclaratorSyntax variableDeclaratorSyntax ||
                    variableDeclaratorSyntax.Parent?.Parent is not FieldDeclarationSyntax fieldDeclaration ||
                    fieldDeclaration.Declaration.Type is not ArrayTypeSyntax ||
                    fieldDeclaration.ContainsDirectives ||
                    fieldDeclaration.AttributeLists.Any(attributeList => attributeList.Target is not null) ||
                    diagnostic.AdditionalLocations.Any(location => location.SourceTree != root.SyntaxTree))
                {
                    return;
                }

                if ((await GetAsSpanReplacementsAsync(root, document, diagnostic, variableDeclaratorSyntax, context.CancellationToken).ConfigureAwait(false)).IsDefault)
                {
                    return;
                }
            }

            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsCodeFixTitle,
                cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                    document,
                    diagnostics,
                    new FixState(),
                    ApplyFixAsync,
                    GetFixedDocument,
                    cancellationToken),
                nameof(MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsCodeFixTitle));
            context.RegisterCodeFix(codeAction, diagnostics);
        }

        private static async Task ApplyFixAsync(
            Document document,
            Diagnostic diagnostic,
            SyntaxEditor editor,
            FixState state,
            CancellationToken cancellationToken)
        {
            if (diagnostic.AdditionalLocations.Any(location => location.SourceTree != editor.OriginalRoot.SyntaxTree) ||
                editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not VariableDeclaratorSyntax variableDeclaratorSyntax ||
                variableDeclaratorSyntax.Parent?.Parent is not FieldDeclarationSyntax fieldDeclarationSyntax ||
                fieldDeclarationSyntax.Declaration.Type is not ArrayTypeSyntax ||
                fieldDeclarationSyntax.ContainsDirectives ||
                fieldDeclarationSyntax.AttributeLists.Any(attributeList => attributeList.Target is not null))
            {
                return;
            }

            var asSpanReplacements = await GetAsSpanReplacementsAsync(
                editor.OriginalRoot,
                document,
                diagnostic,
                variableDeclaratorSyntax,
                cancellationToken).ConfigureAwait(false);
            if (asSpanReplacements.IsDefault)
            {
                return;
            }

            // Rewrite this field's 'AsSpan' call sites. Validate all of them before changing any so a
            // stale diagnostic cannot leave either the call sites or the declaration partially fixed.
            foreach (var (original, replacement) in asSpanReplacements)
            {
                editor.ReplaceNode(original, replacement);
            }

            state.GetDeclarators(fieldDeclarationSyntax).Add(variableDeclaratorSyntax);
        }

        private static Document GetFixedDocument(Document document, SyntaxEditor editor, FixState state)
        {
            string newLine = GetNewLine(editor.OriginalRoot.SyntaxTree.GetText());
            foreach (var fieldDeclarationSyntax in state.OrderedFields)
            {
                FixFieldDeclaration(editor, fieldDeclarationSyntax, state.GetDeclarators(fieldDeclarationSyntax), newLine);
            }

            return document.WithSyntaxRoot(editor.GetChangedRoot());
        }

        private static void FixFieldDeclaration(
            SyntaxEditor editor,
            FieldDeclarationSyntax fieldDeclarationSyntax,
            List<VariableDeclaratorSyntax> diagnosedDeclarators,
            string newLine)
        {
            var arrayTypeSyntax = (ArrayTypeSyntax)fieldDeclarationSyntax.Declaration.Type;
            var rosNameSyntax = SyntaxFactory.QualifiedName(
                SyntaxFactory.AliasQualifiedName(
                    SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)),
                    SyntaxFactory.IdentifierName(nameof(System))),
                SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier(nameof(ReadOnlySpan<byte>)),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(arrayTypeSyntax.ElementType))))
                .WithAdditionalAnnotations(Simplifier.Annotation);
            var modifiersWithoutReadOnlyKeyword = fieldDeclarationSyntax.Modifiers.Remove(
                fieldDeclarationSyntax.Modifiers.First(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));

            var declaration = fieldDeclarationSyntax.Declaration;

            // Match the result of applying the individual code action repeatedly: each converted
            // declarator is inserted immediately after the surviving declaration.
            var propertiesInInsertionOrder = diagnosedDeclarators
                .OrderByDescending(declaration.Variables.IndexOf)
                .ToArray();
            var diagnosedIndices = propertiesInInsertionOrder
                .Select(declaration.Variables.IndexOf)
                .OrderBy(index => index)
                .ToArray();
            var remainingVariables = declaration.Variables;
            for (int i = diagnosedIndices.Length - 1; i >= 0; i--)
            {
                remainingVariables = remainingVariables.RemoveAt(diagnosedIndices[i]);
            }

            if (remainingVariables.Count > 0)
            {
                //  At least one field in the declaration is left as an array: keep the declaration
                //  with the remaining variables and insert the new properties after it.
                var insertedProperties = propertiesInInsertionOrder
                    .Select(declarator => CreateInsertedProperty(arrayTypeSyntax, rosNameSyntax, modifiersWithoutReadOnlyKeyword, declarator, declaration, fieldDeclarationSyntax.AttributeLists, newLine));
                editor.InsertAfter(fieldDeclarationSyntax, insertedProperties);
                editor.ReplaceNode(
                    fieldDeclarationSyntax,
                    (currentField, _) =>
                    {
                        var field = (FieldDeclarationSyntax)currentField;
                        var currentVariables = field.Declaration.Variables;
                        SyntaxTriviaList preservedLeadingTrivia = default;
                        for (int i = diagnosedIndices.Length - 1; i >= 0; i--)
                        {
                            int diagnosedIndex = diagnosedIndices[i];
                            if (diagnosedIndex < currentVariables.Count - 1)
                            {
                                var nextVariable = currentVariables[diagnosedIndex + 1];
                                var separator = currentVariables.GetSeparator(diagnosedIndex);
                                var nextVariableLeadingTrivia = separator.TrailingTrivia.AddRange(nextVariable.GetLeadingTrivia());
                                nextVariable = nextVariable.WithLeadingTrivia(default(SyntaxTriviaList));
                                currentVariables = currentVariables.Replace(currentVariables[diagnosedIndex + 1], nextVariable);

                                if (diagnosedIndex == 0)
                                {
                                    if (nextVariableLeadingTrivia.Any(trivia =>
                                        !trivia.IsKind(SyntaxKind.WhitespaceTrivia) &&
                                        !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                                    {
                                        preservedLeadingTrivia = preservedLeadingTrivia.AddRange(nextVariableLeadingTrivia);
                                    }
                                }
                                else
                                {
                                    nextVariable = nextVariable.WithLeadingTrivia(nextVariableLeadingTrivia);
                                    currentVariables = currentVariables.Replace(currentVariables[diagnosedIndex + 1], nextVariable);
                                }
                            }

                            if (diagnosedIndex > 0 && diagnosedIndex < currentVariables.Count - 1)
                            {
                                var previousSeparator = currentVariables.GetSeparator(diagnosedIndex - 1);
                                currentVariables = currentVariables.ReplaceSeparator(
                                    previousSeparator,
                                    previousSeparator.WithTrailingTrivia(default(SyntaxTriviaList)));
                            }

                            currentVariables = currentVariables.RemoveAt(diagnosedIndex);
                        }

                        return field
                            .WithDeclaration(field.Declaration.WithVariables(currentVariables))
                            .WithLeadingTrivia(field.GetLeadingTrivia().AddRange(preservedLeadingTrivia))
                            .WithAdditionalAnnotations(Formatter.Annotation);
                    });
            }
            else
            {
                //  Every field in the declaration is being converted: replace the declaration with
                //  the first property (carrying the declaration's trivia) and insert the rest after.
                var first = CreateReplacementProperty(arrayTypeSyntax, rosNameSyntax, modifiersWithoutReadOnlyKeyword, propertiesInInsertionOrder[0], fieldDeclarationSyntax);
                var rest = propertiesInInsertionOrder
                    .Skip(1)
                    .Select(declarator => CreateInsertedProperty(arrayTypeSyntax, rosNameSyntax, modifiersWithoutReadOnlyKeyword, declarator, declaration, fieldDeclarationSyntax.AttributeLists, newLine));
                editor.InsertAfter(fieldDeclarationSyntax, rest);
                editor.ReplaceNode(fieldDeclarationSyntax, first);
            }
        }

        private static PropertyDeclarationSyntax CreateInsertedProperty(
            ArrayTypeSyntax arrayTypeSyntax,
            NameSyntax rosNameSyntax,
            SyntaxTokenList modifiers,
            VariableDeclaratorSyntax variableDeclaratorSyntax,
            VariableDeclarationSyntax variableDeclarationSyntax,
            SyntaxList<AttributeListSyntax> attributeLists,
            string newLine)
        {
            return CreateProperty(
                arrayTypeSyntax,
                rosNameSyntax,
                modifiers,
                variableDeclaratorSyntax,
                attributeLists,
                SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine)))
                .WithIdentifier(variableDeclaratorSyntax.Identifier.WithLeadingTrivia(default(SyntaxTriviaList)))
                .WithLeadingTrivia(GetDeclaratorLeadingTrivia(variableDeclarationSyntax, variableDeclaratorSyntax))
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static PropertyDeclarationSyntax CreateReplacementProperty(
            ArrayTypeSyntax arrayTypeSyntax,
            NameSyntax rosNameSyntax,
            SyntaxTokenList modifiers,
            VariableDeclaratorSyntax variableDeclaratorSyntax,
            FieldDeclarationSyntax fieldDeclarationSyntax)
        {
            var property = CreateProperty(
                arrayTypeSyntax,
                rosNameSyntax,
                modifiers,
                variableDeclaratorSyntax,
                fieldDeclarationSyntax.AttributeLists,
                fieldDeclarationSyntax.SemicolonToken)
                .WithIdentifier(variableDeclaratorSyntax.Identifier.WithLeadingTrivia(default(SyntaxTriviaList)))
                .WithLeadingTrivia(
                    fieldDeclarationSyntax.GetLeadingTrivia().AddRange(
                        GetDeclaratorLeadingTrivia(fieldDeclarationSyntax.Declaration, variableDeclaratorSyntax)));

            return fieldDeclarationSyntax.Declaration.Variables.IndexOf(variableDeclaratorSyntax) == 0
                ? property
                : property.WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static SyntaxTriviaList GetDeclaratorLeadingTrivia(
            VariableDeclarationSyntax variableDeclarationSyntax,
            VariableDeclaratorSyntax variableDeclaratorSyntax)
        {
            int variableIndex = variableDeclarationSyntax.Variables.IndexOf(variableDeclaratorSyntax);
            if (variableIndex == 0)
            {
                return default;
            }

            var separator = variableDeclarationSyntax.Variables.GetSeparator(variableIndex - 1);
            return separator.TrailingTrivia
                .AddRange(variableDeclaratorSyntax.GetLeadingTrivia());
        }

        private static PropertyDeclarationSyntax CreateProperty(
            ArrayTypeSyntax arrayTypeSyntax,
            NameSyntax rosNameSyntax,
            SyntaxTokenList modifiers,
            VariableDeclaratorSyntax variableDeclaratorSyntax,
            SyntaxList<AttributeListSyntax> attributeLists,
            SyntaxToken semicolonToken)
        {
            return SyntaxFactory.PropertyDeclaration(
                attributeLists,
                modifiers,
                rosNameSyntax,
                explicitInterfaceSpecifier: null,
                variableDeclaratorSyntax.Identifier,
                accessorList: null,
                CreateArrowExpressionClause(arrayTypeSyntax, variableDeclaratorSyntax),
                initializer: null,
                semicolonToken);
        }

        private static ArrowExpressionClauseSyntax CreateArrowExpressionClause(
            ArrayTypeSyntax arrayTypeSyntax,
            VariableDeclaratorSyntax variableDeclaratorSyntax)
        {
            if (variableDeclaratorSyntax.Initializer is not EqualsValueClauseSyntax initializer)
            {
                throw new InvalidOperationException();
            }

            ExpressionSyntax value = initializer.Value is InitializerExpressionSyntax arrayInitializer
                ? SyntaxFactory.ArrayCreationExpression(
                    arrayTypeSyntax.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space),
                    arrayInitializer)
                : initializer.Value;
            return SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTriviaFrom(initializer.EqualsToken),
                value);
        }

        private static async Task<ImmutableArray<(SyntaxNode Original, SyntaxNode Replacement)>> GetAsSpanReplacementsAsync(
            SyntaxNode root,
            Document document,
            Diagnostic diagnostic,
            VariableDeclaratorSyntax variableDeclaratorSyntax,
            CancellationToken cancellationToken)
        {
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model is null ||
                model.GetDeclaredSymbol(variableDeclaratorSyntax, cancellationToken) is not IFieldSymbol expectedField ||
                variableDeclaratorSyntax.Initializer?.Value is not ExpressionSyntax initializerValue ||
                model.GetOperation(initializerValue, cancellationToken) is not IOperation initializerOperation ||
                !model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttributeUsageAttribute, out var attributeUsageAttributeType) ||
                !PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsAnalyzer.IsValidCandidate(expectedField, initializerOperation, attributeUsageAttributeType) ||
                !model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions, out var memoryExtensionsType) ||
                !model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1, out var readOnlySpanType))
            {
                return default;
            }

            var indexType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIndex);
            var rangeType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRange);
            var replacements = ImmutableArray.CreateBuilder<(SyntaxNode Original, SyntaxNode Replacement)>(diagnostic.AdditionalLocations.Count);
            var seenLocations = new HashSet<(SyntaxTree? SourceTree, TextSpan SourceSpan)>();
            foreach (var location in diagnostic.AdditionalLocations)
            {
                if (!seenLocations.Add((location.SourceTree, location.SourceSpan)))
                {
                    continue;
                }

                if (location.SourceTree != root.SyntaxTree ||
                    model.GetOperation(root.FindNode(location.SourceSpan, getInnermostNodeForTie: true), cancellationToken) is not IFieldReferenceOperation fieldReference)
                {
                    return default;
                }

                IOperation sourceOperation = fieldReference;
                while (sourceOperation.Parent is IArrayElementReferenceOperation arrayElement &&
                    ReferenceEquals(arrayElement.ArrayReference, sourceOperation))
                {
                    sourceOperation = arrayElement;
                }

                if (sourceOperation.Parent is not IArgumentOperation { Parent: IInvocationOperation invocation } sourceArgument ||
                    sourceOperation.Syntax is not ExpressionSyntax sourceExpression ||
                    !SymbolEqualityComparer.Default.Equals(fieldReference.Field.OriginalDefinition, expectedField.OriginalDefinition) ||
                    invocation.TargetMethod.Name != nameof(MemoryExtensions.AsSpan) ||
                    !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition.ContainingType, memoryExtensionsType) ||
                    invocation.Parent is not IConversionOperation { Type: { } conversionType } ||
                    !SymbolEqualityComparer.Default.Equals(conversionType.OriginalDefinition, readOnlySpanType) ||
                    invocation.Syntax is not InvocationExpressionSyntax invocationSyntax ||
                    invocationSyntax.ContainsDirectives)
                {
                    return default;
                }

                // AsSpan() becomes the property reference itself.
                if (invocation.TargetMethod.Parameters.Length == 1)
                {
                    replacements.Add((invocationSyntax, sourceExpression.WithTriviaFrom(invocationSyntax)));
                    continue;
                }

                if (invocation.Arguments.Length == 2)
                {
                    var slicingArgument = ReferenceEquals(invocation.Arguments[0], sourceArgument)
                        ? invocation.Arguments[1]
                        : invocation.Arguments[0];
                    if (slicingArgument.Parameter is IParameterSymbol slicingParameter &&
                        slicingArgument.Value.Syntax is ExpressionSyntax slicingExpression)
                    {
                        if ((SymbolEqualityComparer.Default.Equals(slicingParameter.Type, indexType) ||
                            SymbolEqualityComparer.Default.Equals(slicingParameter.Type, rangeType)) &&
                            root.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: < LanguageVersion.CSharp8 })
                        {
                            return default;
                        }

                        ExpressionSyntax? rangeExpression = null;
                        if (SymbolEqualityComparer.Default.Equals(slicingParameter.Type, indexType))
                        {
                            rangeExpression = SyntaxFactory.RangeExpression(slicingExpression.Parenthesize(), rightOperand: null);
                        }
                        else if (SymbolEqualityComparer.Default.Equals(slicingParameter.Type, rangeType))
                        {
                            rangeExpression = slicingExpression;
                        }

                        if (rangeExpression is not null)
                        {
                            var elementAccess = SyntaxFactory.ElementAccessExpression(
                                sourceExpression.WithoutTrivia(),
                                SyntaxFactory.BracketedArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(rangeExpression))))
                                .WithTriviaFrom(invocationSyntax);
                            replacements.Add((invocationSyntax, elementAccess));
                            continue;
                        }
                    }
                }

                InvocationExpressionSyntax replacement;
                if (invocationSyntax.ArgumentList.Arguments.Count == invocation.Arguments.Length)
                {
                    // A static call becomes a Slice call on the property. Locate the array argument by syntax
                    // so named and reordered arguments are handled correctly.
                    if (sourceArgument.Syntax is not ArgumentSyntax arrayArgumentSyntax)
                    {
                        return default;
                    }

                    int arrayArgumentIndex = invocationSyntax.ArgumentList.Arguments.IndexOf(arrayArgumentSyntax);
                    if (arrayArgumentIndex < 0)
                    {
                        return default;
                    }

                    var sliceExpression = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        sourceExpression.WithoutTrivia(),
                        SyntaxFactory.IdentifierName(nameof(ReadOnlySpan<byte>.Slice)))
                        .WithTriviaFrom(invocationSyntax.Expression);
                    replacement = invocationSyntax
                        .WithExpression(sliceExpression)
                        .WithArgumentList(invocationSyntax.ArgumentList.WithArguments(invocationSyntax.ArgumentList.Arguments.RemoveAt(arrayArgumentIndex)));
                }
                else if (invocationSyntax.Expression is MemberAccessExpressionSyntax memberAccessSyntax)
                {
                    // Any 'start'/'length' arguments are carried over verbatim, including name colons.
                    // MemoryExtensions.AsSpan and ReadOnlySpan<T>.Slice intentionally share those names.
                    replacement = invocationSyntax.WithExpression(
                        memberAccessSyntax.WithName(
                            SyntaxFactory.IdentifierName(nameof(ReadOnlySpan<byte>.Slice)).WithTriviaFrom(memberAccessSyntax.Name)));
                }
                else
                {
                    return default;
                }

                replacements.Add((invocationSyntax, replacement));
            }

            return replacements.ToImmutable();
        }

        private static string GetNewLine(SourceText sourceText)
        {
            if (sourceText.Lines.Count > 1)
            {
                TextLine firstLine = sourceText.Lines[0];
                return sourceText.ToString(TextSpan.FromBounds(firstLine.End, firstLine.EndIncludingLineBreak));
            }

            return Environment.NewLine;
        }
    }
}
