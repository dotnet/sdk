// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
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
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed partial class CSharpDisableRuntimeMarshallingFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DisableRuntimeMarshallingAnalyzer.MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return CustomFixAllProvider.Instance;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            if (context.Document.Project.CompilationOptions is CSharpCompilationOptions { AllowUnsafe: false })
            {
                // We can't code fix if unsafe code isn't allowed.
                return;
            }

            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode enclosingNode = root.FindNode(context.Span);

            foreach (var diagnostic in context.Diagnostics)
            {
                if (diagnostic.Properties[DisableRuntimeMarshallingAnalyzer.CanConvertToDisabledMarshallingEquivalentKey] is not null)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            MicrosoftNetCoreAnalyzersResources.UseDisabledMarshallingEquivalentCodeFix,
                            async ct => await UseDisabledMarshallingEquivalentAsync(enclosingNode, context.Document, context.CancellationToken).ConfigureAwait(false),
                            equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.UseDisabledMarshallingEquivalentCodeFix)),
                        diagnostic);
                }
            }
        }

        private static int? FindFirstUnusedIdentifierIndex(SemanticModel model, int docOffset, string baseName)
        {
            if (model.GetSpeculativeSymbolInfo(docOffset, SyntaxFactory.IdentifierName(baseName), SpeculativeBindingOption.BindAsExpression).Symbol is null)
            {
                return 0;
            }

            for (int i = 1; i < int.MaxValue; i++)
            {
                if (model.GetSpeculativeSymbolInfo(docOffset, SyntaxFactory.IdentifierName($"{baseName}{i}"), SpeculativeBindingOption.BindAsExpression).Symbol is null)
                {
                    return i;
                }
            }

            return 0;
        }

        private static async Task<Document> UseDisabledMarshallingEquivalentAsync(SyntaxNode node, Document document, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            var identifierGenerator = new IdentifierGenerator(editor.SemanticModel, node.SpanStart);
            var addUnsafeToEnclosingMethod = TryRewriteMethodCall(node, editor, identifierGenerator, addRenameAnnotation: true, ct);

            if (addUnsafeToEnclosingMethod)
            {
                AddUnsafeModifierToEnclosingMethod(editor, node);
            }

            return editor.GetChangedDocument();
        }

        private static bool TryRewriteMethodCall(SyntaxNode node, DocumentEditor editor, IdentifierGenerator pointerIdentifierGenerator, bool addRenameAnnotation, CancellationToken ct)
        {
            var operation = (IInvocationOperation)editor.SemanticModel.GetOperation(node, ct)!;
            InvocationExpressionSyntax syntax = (InvocationExpressionSyntax)operation.Syntax;

            if (operation.TargetMethod.Name == "SizeOf")
            {
                if (operation.TargetMethod.IsGenericMethod)
                {
                    if (operation.TargetMethod.TypeArguments[0].IsUnmanagedType)
                    {
                        editor.ReplaceNode(syntax, SyntaxFactory.SizeOfExpression((TypeSyntax)editor.Generator.TypeExpression(operation.TargetMethod.TypeArguments[0])));
                        return true;
                    }
                }
                else if (operation.Arguments[0].Value is ITypeOfOperation { TypeOperand.IsUnmanagedType: true } typeOf)
                {
                    editor.ReplaceNode(syntax, SyntaxFactory.SizeOfExpression(GetTypeOfTypeSyntax((TypeOfExpressionSyntax)typeOf.Syntax)));
                    return true;
                }
            }

            if (operation.TargetMethod.Name == "StructureToPtr" && operation.Arguments[0].Value.Type!.IsUnmanagedType)
            {
                editor.ReplaceNode(syntax,
                    editor.Generator.AssignmentStatement(
                        SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PointerIndirectionExpression,
                            (ExpressionSyntax)editor.Generator.CastExpression(editor.SemanticModel.Compilation.CreatePointerTypeSymbol(operation.Arguments[0].Value.Type!),
                                operation.Arguments[1].Value.Syntax)),
                        operation.Arguments[0].Value.Syntax));
                return true;
            }

            if (operation.TargetMethod.Name == "PtrToStructure")
            {
                ITypeSymbol type;
                if (operation.TargetMethod.IsGenericMethod && operation.Arguments.Length == 1)
                {
                    type = operation.TargetMethod.TypeArguments[0];
                }
                else if (operation.TargetMethod.ReturnType.SpecialType == SpecialType.System_Object
                        && operation.Arguments.Length == 2
                        && operation.Arguments[1].Value is ITypeOfOperation typeOf)
                {
                    type = typeOf.TypeOperand;
                }
                else
                {
                    return false;
                }

                if (operation.Arguments.Length > 0)
                {
                    SyntaxNode replacementNode;
                    IOperation pointer = operation.Arguments[0].Value;
                    if (type.IsNullableValueType() && type.GetNullableValueTypeUnderlyingType() is ITypeSymbol { IsUnmanagedType: true } underlyingType)
                    {
                        var nonNullPtrIdentifier = pointerIdentifierGenerator.NextIdentifier();
                        if (nonNullPtrIdentifier is null)
                        {
                            // We couldn't generate an identifier to use, so don't update the call
                            return false;
                        }

                        SyntaxAnnotation renameIdentiferAnnotation = RenameAnnotation.Create();

                        IdentifierNameSyntax nonNullPtrIdentifierNode = SyntaxFactory.IdentifierName(nonNullPtrIdentifier);

                        if (addRenameAnnotation)
                        {
                            nonNullPtrIdentifierNode = nonNullPtrIdentifierNode.WithAdditionalAnnotations(renameIdentiferAnnotation);
                        }

                        var pointerCast = editor.Generator.CastExpression(
                                editor.SemanticModel.Compilation.CreatePointerTypeSymbol(underlyingType),
                                pointer.Syntax);

                        // Parse from a string since we're limited in SyntaxFactory methods due to the Roslyn version we build against.
                        // Use a dummy identifier for the expression since we want to replace it with `pointerCast` from above anyway
                        // to preserve the annotations that SyntaxGenerator provides.
                        var nullCheckAndDecl = (IsPatternExpressionSyntax)SyntaxFactory.ParseExpression($"x is not null and var {nonNullPtrIdentifier}");
                        nullCheckAndDecl = nullCheckAndDecl.WithExpression((ExpressionSyntax)pointerCast);
                        replacementNode = editor.Generator.ConditionalExpression(
                            nullCheckAndDecl,
                            SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PointerIndirectionExpression, nonNullPtrIdentifierNode),
                            editor.Generator.CastExpression(operation.TargetMethod.ReturnType, editor.Generator.NullLiteralExpression()));
                    }
                    else if (type is { IsUnmanagedType: true })
                    {
                        replacementNode = editor.Generator.CastExpression(operation.TargetMethod.ReturnType,
                                SyntaxFactory.ParenthesizedExpression(SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PointerIndirectionExpression,
                                (ExpressionSyntax)editor.Generator.CastExpression(
                                    editor.SemanticModel.Compilation.CreatePointerTypeSymbol(type),
                                    pointer.Syntax))));
                    }
                    else
                    {
                        return false;
                    }

                    editor.ReplaceNode(syntax, replacementNode);
                    return true;
                }
            }

            return false;

            static TypeSyntax GetTypeOfTypeSyntax(TypeOfExpressionSyntax syntax)
            {
                return syntax.Type;
            }
        }

        private static void AddUnsafeModifierToEnclosingMethod(DocumentEditor editor, SyntaxNode syntax)
        {
            var enclosingMethod = FindEnclosingMethod(syntax);

            static BaseMethodDeclarationSyntax? FindEnclosingMethod(SyntaxNode syntax)
            {
                while (syntax.Parent is not (null or BaseMethodDeclarationSyntax))
                {
                    syntax = syntax.Parent;
                }

                return (BaseMethodDeclarationSyntax?)syntax.Parent;
            }

            editor.SetModifiers(enclosingMethod, editor.Generator.GetModifiers(enclosingMethod).WithIsUnsafe(true));
        }
    }
}
