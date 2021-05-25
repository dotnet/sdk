// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public abstract class DynamicInterfaceCastableImplementationFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(
                DynamicInterfaceCastableImplementationAnalyzer.InterfaceMembersMissingImplementationRuleId,
                DynamicInterfaceCastableImplementationAnalyzer.MeembersDeclaredOnImplementationTypeMustBeSealedRuleId);

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
            Diagnostic diagnostic = context.Diagnostics.First();
            if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.InterfaceMembersMissingImplementationRuleId)
            {
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation,
                        async ct => await ImplementInterfacesOnDynamicCastableImplementation(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation)),
                    diagnostic);
            }
            else if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.MeembersDeclaredOnImplementationTypeMustBeSealedRuleId)
            {
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftNetCoreAnalyzersResources.SealMethodDeclaredOnImplementationType,
                        async ct => await SealMemberDeclaredOnImplementationType(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.SealMethodDeclaredOnImplementationType)),
                    diagnostic);
            }
        }

        protected abstract bool CodeFixSupportsDeclaration(SyntaxNode declaration);

        protected abstract Task<Document> ImplementInterfacesOnDynamicCastableImplementation(
            SyntaxNode declaration,
            Document document,
            CancellationToken ct);

        protected abstract Task<Document> SealMemberDeclaredOnImplementationType(
            SyntaxNode declaration,
            Document document,
            CancellationToken ct);

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
