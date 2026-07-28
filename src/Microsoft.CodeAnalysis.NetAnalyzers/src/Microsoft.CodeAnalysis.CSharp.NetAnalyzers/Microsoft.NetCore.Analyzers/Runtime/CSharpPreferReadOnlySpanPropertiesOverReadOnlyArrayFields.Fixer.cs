// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsFixer : PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsFixer
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var doc = context.Document;
            var root = await doc.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root is null ||
                root.FindNode(context.Diagnostics.First().Location.SourceSpan) is not VariableDeclaratorSyntax variableDeclaratorSyntax ||
                variableDeclaratorSyntax?.Parent?.Parent is not FieldDeclarationSyntax fieldDeclarationSyntax ||
                fieldDeclarationSyntax.Declaration.Type is not ArrayTypeSyntax arrayTypeSyntax)
            {
                return;
            }

            var model = await doc.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (model is null || !model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1, out var readOnlySpanType))
                return;

            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsCodeFixTitle,
                GetChangedDocumentAsync,
                nameof(MicrosoftNetCoreAnalyzersResources.PreferReadOnlySpanPropertiesOverReadOnlyArrayFieldsCodeFixTitle));
            context.RegisterCodeFix(codeAction, context.Diagnostics);

            return;

            //  Local functions

            async Task<Document> GetChangedDocumentAsync(CancellationToken token)
            {
                //  Replace readonly array field declaration with ReadOnlySpan<> property declaration
                var editor = await DocumentEditor.CreateAsync(doc, token).ConfigureAwait(false);
                FixDeclaration(editor);
                await FixAsSpanInvocationsAsync(editor, token).ConfigureAwait(false);
                return await Formatter.FormatAsync(editor.GetChangedDocument(), Formatter.Annotation, cancellationToken: token).ConfigureAwait(false);
            }

            async Task<ImmutableArray<IOperation>> GetFieldReferenceOperationsRequiringUpdateAsync(CancellationToken token)
            {
                var savedSpans = PreferReadOnlySpanPropertiesOverReadOnlyArrayFields.SavedSpanLocation.Deserialize(
                    context.Diagnostics[0].Properties[PreferReadOnlySpanPropertiesOverReadOnlyArrayFields.FixerDataPropertyName]!);
                var documentLookup = context.Document.Project.Documents.ToImmutableDictionary(x => x.FilePath!);
                var builder = ImmutableArray.CreateBuilder<IOperation>(savedSpans.Length);
                builder.Count = savedSpans.Length;

                for (int i = 0; i < savedSpans.Length; ++i)
                {
                    var doc = documentLookup[savedSpans[i].SourceFilePath];
                    var root = await doc.GetSyntaxRootAsync(token).ConfigureAwait(false);
                    var model = await doc.GetSemanticModelAsync(token).ConfigureAwait(false);
                    var node = root!.FindNode(savedSpans[i].Span, getInnermostNodeForTie: true);
                    builder[i] = model!.GetOperation(node, token)!;
                }

                return builder.MoveToImmutable();
            }

            void FixDeclaration(DocumentEditor editor)
            {
                var rosNameSyntax = SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier(readOnlySpanType.Name),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(arrayTypeSyntax.ElementType)));
                var arrowExpressionClauseSyntax = variableDeclaratorSyntax.Initializer is not null ?
                    SyntaxFactory.ArrowExpressionClause(
                        SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTriviaFrom(variableDeclaratorSyntax.Initializer.EqualsToken),
                        variableDeclaratorSyntax.Initializer.Value) :
                    SyntaxFactory.ArrowExpressionClause(
                        SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken),
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            rosNameSyntax,
                            SyntaxFactory.IdentifierName(nameof(ReadOnlySpan<byte>.Empty))));

                var declaration = fieldDeclarationSyntax.Declaration;
                var modifiersWithoutReadOnlyKeyword = SyntaxFactory.TokenList(fieldDeclarationSyntax.Modifiers.Where(x => !x.IsKind(SyntaxKind.ReadOnlyKeyword)));

                //  If multiple fields were declared in a single field declaration, then we need to
                //  insert the newly-created property declaration after the field declaration, and then remove
                //  the reported field variable declarator from the field declaration. In other words:
                //      private static readonly byte[] {|a|} = new[] { ... }, b = new[] { ... };
                //  becomes
                //      private static readonly byte[] b = new[] { ... };
                //      private static ReadOnlySpan<byte> a => new[] { ... };
                //  where {|...|} indicates the location of the diagnostic.
                if (declaration.Variables.Count > 1)
                {
                    var propertyDeclarationSyntax = SyntaxFactory.PropertyDeclaration(rosNameSyntax, variableDeclaratorSyntax.Identifier)
                        .WithExpressionBody(arrowExpressionClauseSyntax)
                        .WithModifiers(modifiersWithoutReadOnlyKeyword)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)))
                        .WithoutLeadingTrivia()
                        .WithAdditionalAnnotations(Formatter.Annotation);
                    editor.InsertAfter(fieldDeclarationSyntax, propertyDeclarationSyntax);

                    var newDeclaration = declaration.WithVariables(declaration.Variables.Remove(variableDeclaratorSyntax));
                    editor.ReplaceNode(declaration, newDeclaration);
                }
                else
                {
                    var propertyDeclarationSyntax = SyntaxFactory.PropertyDeclaration(rosNameSyntax, variableDeclaratorSyntax.Identifier)
                        .WithExpressionBody(arrowExpressionClauseSyntax)
                        .WithModifiers(modifiersWithoutReadOnlyKeyword)
                        .WithSemicolonToken(fieldDeclarationSyntax.SemicolonToken)
                        .WithTriviaFrom(fieldDeclarationSyntax);
                    editor.ReplaceNode(fieldDeclarationSyntax, propertyDeclarationSyntax);
                }
            }

            //  Update calls to 'AsSpan'
            async Task FixAsSpanInvocationsAsync(DocumentEditor editor, CancellationToken token)
            {

                var savedOperations = await GetFieldReferenceOperationsRequiringUpdateAsync(token).ConfigureAwait(false);
                foreach (var fieldReference in savedOperations)
                {
                    //  Walk up to the AsSpan invocation operation.
                    var invocation = (IInvocationOperation)fieldReference.Parent!.Parent!;

                    //  If we called 'AsSpan(int)' or 'AsSpan(int, int)', replace with call to appropriate Slice overload.
                    //  Otherwise simply replace the 'AsSpan()' call with the field reference itself.
                    if (invocation.TargetMethod.Parameters.Length > 1)
                    {
                        var invocationSyntax = (InvocationExpressionSyntax)invocation.Syntax;
                        var memberAccessSyntax = (MemberAccessExpressionSyntax)invocationSyntax.Expression;

                        //  If 'AsSpan' was not called via extension method, then memberAccessSyntax.Expression will be the type name
                        //  expression 'MemoryExtensions'. We need to replace it with the first argument in the argument list (which will be the
                        //  reference to the array field) and then remove the first argument from the argument list.
                        if (invocationSyntax.ArgumentList.Arguments.Count == invocation.Arguments.Length)
                        {
                            var newArgumentList = invocationSyntax.ArgumentList.WithArguments(invocationSyntax.ArgumentList.Arguments.RemoveAt(0));
                            editor.ReplaceNode(invocationSyntax.ArgumentList, newArgumentList);
                            var newExpressionSyntax = fieldReference.Syntax.WithTriviaFrom(memberAccessSyntax.Expression);
                            editor.ReplaceNode(memberAccessSyntax.Expression, newExpressionSyntax);
                        }

                        var sliceMemberNameSyntax = SyntaxFactory.IdentifierName(nameof(ReadOnlySpan<byte>.Slice)).WithTriviaFrom(memberAccessSyntax.Name);
                        editor.ReplaceNode(memberAccessSyntax.Name, sliceMemberNameSyntax);
                    }
                    else
                    {
                        editor.ReplaceNode(invocation.Syntax, fieldReference.Syntax.WithTriviaFrom(invocation.Syntax));
                    }
                }
            }
        }
    }
}
