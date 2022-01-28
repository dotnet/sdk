// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed class CSharpDisableRuntimeMarshallingFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DisableRuntimeMarshallingAnalyzer.MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            if (context.Document.Project.CompilationOptions is CSharpCompilationOptions { AllowUnsafe: false })
            {
                // We can't code fix if unsafe code isn't allowed.
                return;
            }
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode enclosingNode = root.FindNode(context.Span);

            foreach (var diagnostic in context.Diagnostics)
            {
                if (diagnostic.Properties[DisableRuntimeMarshallingAnalyzer.CanConvertToDisabledMarshallingEquivalentKey] is not null)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            MicrosoftNetCoreAnalyzersResources.UseDisabledMarshallingEquivalent,
                            async ct => await UseDisabledMarshallingEquivalentAsync(enclosingNode, context.Document, context.CancellationToken).ConfigureAwait(false),
                            equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.UseDisabledMarshallingEquivalent)),
                        diagnostic);
                }
            }
        }

        private static async Task<Document> UseDisabledMarshallingEquivalentAsync(SyntaxNode node, Document document, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            var operation = (IInvocationOperation)editor.SemanticModel.GetOperation(node, ct);
            InvocationExpressionSyntax syntax = (InvocationExpressionSyntax)operation.Syntax;
            var enclosingMethod = FindEnclosingMethod(syntax);
            bool addUnsafeToEnclosingMethod = false;

            if (operation.TargetMethod.Name == "SizeOf")
            {
                if (operation.TargetMethod.IsGenericMethod)
                {
                    if (operation.TargetMethod.TypeArguments[0].IsUnmanagedType)
                    {
                        addUnsafeToEnclosingMethod = true;
                        editor.ReplaceNode(syntax, SyntaxFactory.SizeOfExpression(GetGenericTypeParameterSyntax(syntax)));
                    }
                }
                else if (operation.Arguments[0].Value is ITypeOfOperation { TypeOperand.IsUnmanagedType: true } typeOf)
                {
                    addUnsafeToEnclosingMethod = true;
                    editor.ReplaceNode(syntax, SyntaxFactory.SizeOfExpression(GetTypeOfTypeSyntax((TypeOfExpressionSyntax)typeOf.Syntax)));
                }
            }
            if (operation.TargetMethod.Name == "StructureToPtr" && operation.Arguments[0].Value.Type.IsUnmanagedType)
            {
                addUnsafeToEnclosingMethod = true;
                editor.ReplaceNode(syntax,
                    editor.Generator.AssignmentStatement(
                        SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PointerIndirectionExpression,
                            (ExpressionSyntax)editor.Generator.CastExpression(editor.SemanticModel.Compilation.CreatePointerTypeSymbol(operation.Arguments[0].Value.Type),
                                operation.Arguments[1].Value.Syntax)),
                        operation.Arguments[0].Value.Syntax));
            }

            if (addUnsafeToEnclosingMethod)
            {
                editor.SetModifiers(enclosingMethod, editor.Generator.GetModifiers(enclosingMethod).WithIsUnsafe(true));
            }

            return editor.GetChangedDocument();

            static TypeSyntax GetGenericTypeParameterSyntax(InvocationExpressionSyntax syntax)
            {
                var memberAccess = (MemberAccessExpressionSyntax)syntax.Expression;
                var genericName = (GenericNameSyntax)memberAccess.Name;
                return genericName.TypeArgumentList.Arguments[0];
            }

            static TypeSyntax GetTypeOfTypeSyntax(TypeOfExpressionSyntax syntax)
            {
                return syntax.Type;
            }

            static BaseMethodDeclarationSyntax? FindEnclosingMethod(SyntaxNode syntax)
            {
                while (syntax.Parent is not (null or BaseMethodDeclarationSyntax))
                {
                    syntax = syntax.Parent;
                }

                return (BaseMethodDeclarationSyntax?)syntax.Parent;
            }
        }
    }
}
