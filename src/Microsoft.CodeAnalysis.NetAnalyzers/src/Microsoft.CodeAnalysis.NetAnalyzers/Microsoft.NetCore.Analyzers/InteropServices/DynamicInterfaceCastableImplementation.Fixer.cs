// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public abstract class DynamicInterfaceCastableImplementationFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(
                DynamicInterfaceCastableImplementationAnalyzer.InterfaceMembersMissingImplementationRuleId,
                DynamicInterfaceCastableImplementationAnalyzer.MembersDeclaredOnImplementationTypeMustBeStaticRuleId);

        public override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode enclosingNode = root.FindNode(context.Span, getInnermostNodeForTie: true);
            SyntaxNode declaration = generator.GetDeclaration(enclosingNode);
            if (declaration == null || !CodeFixSupportsDeclaration(declaration))
            {
                return;
            }
            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.InterfaceMembersMissingImplementationRuleId)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation,
                            async ct => await ImplementInterfacesOnDynamicCastableImplementation(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                            equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation)),
                        diagnostic);
                }
                else if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.MembersDeclaredOnImplementationTypeMustBeStaticRuleId
                    && diagnostic.Properties.ContainsKey(DynamicInterfaceCastableImplementationAnalyzer.NonStaticMemberIsMethodKey))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            MicrosoftNetCoreAnalyzersResources.MakeMethodDeclaredOnImplementationTypeStatic,
                            async ct => await MakeMemberDeclaredOnImplementationTypeStatic(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                            equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.MakeMethodDeclaredOnImplementationTypeStatic)),
                        diagnostic);
                }
            }
        }

        protected static SyntaxAnnotation CreatePossibleInvalidCodeWarning()
        {
            return WarningAnnotation.Create(MicrosoftNetCoreAnalyzersResources.MakeMethodDeclaredOnImplementationTypeStaticMayProduceInvalidCode);
        }

        protected abstract bool CodeFixSupportsDeclaration(SyntaxNode declaration);

        protected abstract Task<Document> ImplementInterfacesOnDynamicCastableImplementation(
            SyntaxNode declaration,
            Document document,
            CancellationToken cancellationToken);

        protected abstract Task<Document> MakeMemberDeclaredOnImplementationTypeStatic(
            SyntaxNode declaration,
            Document document,
            CancellationToken cancellationToken);
    }
}
