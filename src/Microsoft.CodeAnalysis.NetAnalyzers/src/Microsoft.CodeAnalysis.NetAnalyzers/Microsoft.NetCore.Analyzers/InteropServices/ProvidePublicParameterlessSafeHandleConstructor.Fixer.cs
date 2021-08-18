// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class ProvidePublicParameterlessSafeHandleConstructorFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ProvidePublicParameterlessSafeHandleConstructorAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode enclosingNode = root.FindNode(context.Span);
            SyntaxNode declaration = generator.GetDeclaration(enclosingNode);
            if (declaration == null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic,
                        async ct => await MakeParameterlessConstructorPublicAsync(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic)),
                    diagnostic);
            }
        }

        private static async Task<Document> MakeParameterlessConstructorPublicAsync(SyntaxNode declaration, Document document, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            editor.SetAccessibility(declaration, Accessibility.Public);
            return editor.GetChangedDocument();
        }
    }
}
