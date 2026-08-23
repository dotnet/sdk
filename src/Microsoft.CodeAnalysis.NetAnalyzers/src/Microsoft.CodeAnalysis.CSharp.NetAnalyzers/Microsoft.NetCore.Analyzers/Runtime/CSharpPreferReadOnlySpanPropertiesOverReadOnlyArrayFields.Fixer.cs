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
using Microsoft.CodeAnalysis.Options;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsFixer : PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsFixer
    {
        /// <summary>
        /// Carries the declarators to convert, grouped by their containing field declaration, plus the
        /// values every fix in the document shares.
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

            public INamedTypeSymbol? ReadOnlySpanType { get; set; }

            public OptionSet? Options { get; set; }

            public CancellationToken CancellationToken { get; set; }

            public string NewLine { get; set; } = Environment.NewLine;

            public bool Initialized { get; set; }

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

        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<FixState>(
                static _ => new FixState(),
                ApplyFixAsync,
                fixAllTitle: null,
                getFixedDocument: GetFixedDocument);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root is null ||
                root.FindNode(context.Diagnostics[0].Location.SourceSpan) is not VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax { Declaration.Type: ArrayTypeSyntax } })
            {
                return;
            }

            var document = context.Document;
            var diagnostics = context.Diagnostics;
            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsCodeFixTitle,
                cancellationToken => FixAllInDocumentAsync(document, diagnostics, cancellationToken),
                nameof(MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsCodeFixTitle));
            context.RegisterCodeFix(codeAction, diagnostics);
        }

        //  This mirrors 'SyntaxEditorFixAllProvider.ApplyFixesAsync' rather than calling it, because that
        //  overload has no 'getFixedDocument' hook and the field declarations cannot be rewritten until
        //  every diagnostic has been seen.
        private static async Task<Document> FixAllInDocumentAsync(Document document, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
        {
            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = new SyntaxEditor(root, document.Project.Solution.Workspace.Services);
            var state = new FixState();

            foreach (var diagnostic in diagnostics.Distinct().OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start))
            {
                await ApplyFixAsync(document, diagnostic, editor, state, cancellationToken).ConfigureAwait(false);
            }

            return GetFixedDocument(document, editor, state);
        }

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, FixState state, CancellationToken cancellationToken)
        {
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.CancellationToken = cancellationToken;
                state.NewLine = GetNewLine(editor.OriginalRoot);
                state.Options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

                var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (model is not null &&
                    model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1, out var readOnlySpanType))
                {
                    state.ReadOnlySpanType = readOnlySpanType;
                }
            }

            if (state.ReadOnlySpanType is null)
            {
                return;
            }

            //  Rewrite this field's 'AsSpan' call sites. Those edits touch nodes that are independent of
            //  the declaration rewrites, so their order relative to them does not matter.
            await FixAsSpanInvocationsAsync(editor, document, diagnostic, cancellationToken).ConfigureAwait(false);

            if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not VariableDeclaratorSyntax variableDeclaratorSyntax ||
                variableDeclaratorSyntax.Parent?.Parent is not FieldDeclarationSyntax fieldDeclarationSyntax ||
                fieldDeclarationSyntax.Declaration.Type is not ArrayTypeSyntax)
            {
                return;
            }

            state.GetDeclarators(fieldDeclarationSyntax).Add(variableDeclaratorSyntax);
        }

        private static Document GetFixedDocument(Document document, SyntaxEditor editor, FixState state)
        {
            if (state.ReadOnlySpanType is null)
            {
                return document;
            }

            foreach (var fieldDeclarationSyntax in state.OrderedFields)
            {
                FixFieldDeclaration(editor, state.ReadOnlySpanType, fieldDeclarationSyntax, state.GetDeclarators(fieldDeclarationSyntax), state.NewLine);
            }

            var options = state.Options?.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, state.NewLine);
            var formattedRoot = Formatter.Format(
                editor.GetChangedRoot(),
                Formatter.Annotation,
                document.Project.Solution.Workspace,
                options,
                state.CancellationToken);
            return document.WithSyntaxRoot(formattedRoot);
        }

        private static void FixFieldDeclaration(
            SyntaxEditor editor,
            INamedTypeSymbol readOnlySpanType,
            FieldDeclarationSyntax fieldDeclarationSyntax,
            List<VariableDeclaratorSyntax> diagnosedDeclarators,
            string newLine)
        {
            var arrayTypeSyntax = (ArrayTypeSyntax)fieldDeclarationSyntax.Declaration.Type;
            var rosNameSyntax = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier(readOnlySpanType.Name),
                SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(arrayTypeSyntax.ElementType)));
            var modifiersWithoutReadOnlyKeyword = SyntaxFactory.TokenList(fieldDeclarationSyntax.Modifiers.Where(x => !x.IsKind(SyntaxKind.ReadOnlyKeyword)));

            var declaration = fieldDeclarationSyntax.Declaration;

            //  Remove the diagnosed declarators by descending index rather than by node reference:
            //  'SeparatedSyntaxList.Remove' re-creates the surviving nodes in the returned list, so
            //  a second 'Remove' called with an original node reference would no longer find it and
            //  would silently leave that declarator in place.
            var indicesToRemove = diagnosedDeclarators
                .Select(declaration.Variables.IndexOf)
                .OrderByDescending(index => index)
                .ToArray();
            var remainingVariables = declaration.Variables;
            foreach (var index in indicesToRemove)
            {
                remainingVariables = remainingVariables.RemoveAt(index);
            }

            //  Emit the properties in reverse declaration order: each is inserted after the field
            //  declaration, so reversing lines them back up in source order beneath it.
            var propertiesInInsertionOrder = diagnosedDeclarators
                .OrderByDescending(declaration.Variables.IndexOf)
                .ToArray();

            if (remainingVariables.Count > 0)
            {
                //  At least one field in the declaration is left as an array: keep the declaration
                //  with the remaining variables and insert the new properties after it.
                var insertedProperties = propertiesInInsertionOrder
                    .Select(declarator => CreateInsertedProperty(rosNameSyntax, modifiersWithoutReadOnlyKeyword, declarator, fieldDeclarationSyntax.AttributeLists, newLine))
                    .ToArray();
                editor.InsertAfter(fieldDeclarationSyntax, insertedProperties);
                editor.ReplaceNode(declaration, declaration.WithVariables(remainingVariables));
            }
            else
            {
                //  Every field in the declaration is being converted: replace the declaration with
                //  the first property (carrying the declaration's trivia) and insert the rest after.
                var first = CreateReplacementProperty(rosNameSyntax, modifiersWithoutReadOnlyKeyword, propertiesInInsertionOrder[0], fieldDeclarationSyntax);
                var rest = propertiesInInsertionOrder
                    .Skip(1)
                    .Select(declarator => CreateInsertedProperty(rosNameSyntax, modifiersWithoutReadOnlyKeyword, declarator, fieldDeclarationSyntax.AttributeLists, newLine))
                    .ToArray();
                editor.InsertAfter(fieldDeclarationSyntax, rest);
                editor.ReplaceNode(fieldDeclarationSyntax, first);
            }
        }

        private static PropertyDeclarationSyntax CreateInsertedProperty(
            GenericNameSyntax rosNameSyntax,
            SyntaxTokenList modifiers,
            VariableDeclaratorSyntax variableDeclaratorSyntax,
            SyntaxList<AttributeListSyntax> attributeLists,
            string newLine)
        {
            return SyntaxFactory.PropertyDeclaration(rosNameSyntax, variableDeclaratorSyntax.Identifier)
                .WithExpressionBody(CreateArrowExpressionClause(rosNameSyntax, variableDeclaratorSyntax))
                .WithAttributeLists(attributeLists)
                .WithModifiers(modifiers)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine)))
                .WithoutLeadingTrivia()
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static PropertyDeclarationSyntax CreateReplacementProperty(
            GenericNameSyntax rosNameSyntax,
            SyntaxTokenList modifiers,
            VariableDeclaratorSyntax variableDeclaratorSyntax,
            FieldDeclarationSyntax fieldDeclarationSyntax)
        {
            return SyntaxFactory.PropertyDeclaration(rosNameSyntax, variableDeclaratorSyntax.Identifier)
                .WithExpressionBody(CreateArrowExpressionClause(rosNameSyntax, variableDeclaratorSyntax))
                .WithAttributeLists(fieldDeclarationSyntax.AttributeLists)
                .WithModifiers(modifiers)
                .WithSemicolonToken(fieldDeclarationSyntax.SemicolonToken)
                .WithTriviaFrom(fieldDeclarationSyntax);
        }

        private static ArrowExpressionClauseSyntax CreateArrowExpressionClause(GenericNameSyntax rosNameSyntax, VariableDeclaratorSyntax variableDeclaratorSyntax)
        {
            return variableDeclaratorSyntax.Initializer is not null ?
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTriviaFrom(variableDeclaratorSyntax.Initializer.EqualsToken),
                    variableDeclaratorSyntax.Initializer.Value) :
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken),
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        rosNameSyntax,
                        SyntaxFactory.IdentifierName(nameof(ReadOnlySpan<byte>.Empty))));
        }

        //  Update calls to 'AsSpan'
        private static async Task FixAsSpanInvocationsAsync(SyntaxEditor editor, Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var savedOperations = await GetFieldReferenceOperationsRequiringUpdateAsync(document, diagnostic, cancellationToken).ConfigureAwait(false);
            foreach (var fieldReference in savedOperations)
            {
                //  Walk up to the AsSpan invocation operation. The analyzer only saves field references
                //  that are the array argument of an 'AsSpan' call, so this shape is expected; skip this
                //  reference rather than throwing if a future analyzer change ever saves one that is not.
                if (fieldReference.Parent is not IArgumentOperation { Parent: IInvocationOperation invocation })
                {
                    continue;
                }

                //  If we called 'AsSpan(int)' or 'AsSpan(int, int)', replace with call to appropriate Slice overload.
                //  Otherwise simply replace the 'AsSpan()' call with the field reference itself.
                if (invocation.TargetMethod.Parameters.Length > 1)
                {
                    var invocationSyntax = (InvocationExpressionSyntax)invocation.Syntax;
                    var memberAccessSyntax = (MemberAccessExpressionSyntax)invocationSyntax.Expression;

                    //  If 'AsSpan' was not called via extension method, then memberAccessSyntax.Expression will be the type name
                    //  expression 'MemoryExtensions'. We need to replace it with the array field reference and remove the
                    //  array argument from the argument list. The array argument is located by its own syntax rather than
                    //  by position, so a field passed as a named or reordered argument is handled correctly.
                    if (invocationSyntax.ArgumentList.Arguments.Count == invocation.Arguments.Length &&
                        fieldReference.Syntax.FirstAncestorOrSelf<ArgumentSyntax>() is ArgumentSyntax arrayArgumentSyntax)
                    {
                        var newArgumentList = invocationSyntax.ArgumentList.WithArguments(invocationSyntax.ArgumentList.Arguments.Remove(arrayArgumentSyntax));
                        editor.ReplaceNode(invocationSyntax.ArgumentList, newArgumentList);
                        var newExpressionSyntax = fieldReference.Syntax.WithTriviaFrom(memberAccessSyntax.Expression);
                        editor.ReplaceNode(memberAccessSyntax.Expression, newExpressionSyntax);
                    }

                    //  Rename 'AsSpan' to 'Slice'. Any 'start'/'length' arguments the caller wrote are
                    //  carried over verbatim, including name colons for named arguments. This is only
                    //  valid because 'MemoryExtensions.AsSpan(T[], int start, int length)' and
                    //  'ReadOnlySpan<T>.Slice(int start, int length)' share those parameter names; the
                    //  carried-over named arguments would not bind otherwise.
                    var sliceMemberNameSyntax = SyntaxFactory.IdentifierName(nameof(ReadOnlySpan<byte>.Slice)).WithTriviaFrom(memberAccessSyntax.Name);
                    editor.ReplaceNode(memberAccessSyntax.Name, sliceMemberNameSyntax);
                }
                else
                {
                    editor.ReplaceNode(invocation.Syntax, fieldReference.Syntax.WithTriviaFrom(invocation.Syntax));
                }
            }
        }

        private static async Task<ImmutableArray<IOperation>> GetFieldReferenceOperationsRequiringUpdateAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var savedSpans = PreferReadOnlySpanPropertiesOverReadOnlyArrayFields.SavedSpanLocation.Deserialize(
                diagnostic.Properties[PreferReadOnlySpanPropertiesOverReadOnlyArrayFields.FixerDataPropertyName]!);
            var documentLookup = document.Project.Documents.ToImmutableDictionary(x => x.FilePath!);
            var builder = ImmutableArray.CreateBuilder<IOperation>(savedSpans.Length);

            for (int i = 0; i < savedSpans.Length; ++i)
            {
                var referenceDocument = documentLookup[savedSpans[i].SourceFilePath];
                var root = await referenceDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var model = await referenceDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (root is null || model is null)
                {
                    continue;
                }

                //  A diagnostic can be stale relative to the document the fixer runs against, so the
                //  saved span may have drifted onto a node with no operation. Skip it rather than
                //  dereferencing a null operation and throwing in the IDE.
                var node = root.FindNode(savedSpans[i].Span, getInnermostNodeForTie: true);
                if (model.GetOperation(node, cancellationToken) is IOperation operation)
                {
                    builder.Add(operation);
                }
            }

            return builder.ToImmutable();
        }

        private static string GetNewLine(SyntaxNode root)
        {
            foreach (var trivia in root.DescendantTrivia())
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    return trivia.ToString();
                }
            }

            return Environment.NewLine;
        }
    }
}
