// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
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
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
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
                    var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                    var symbol = (INamedTypeSymbol)model.GetDeclaredSymbol(declaration, context.CancellationToken);
                    if (symbol.AllInterfaces.SelectMany(m => m.GetMembers()).Any(m => m.IsStatic && m.IsAbstract))
                    {
                        // Cannot offer a fix for static abstracts.
                        return;
                    }

                    context.RegisterCodeFix(
                        CodeAction.Create(
                            MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation,
                            _ => Task.FromResult(ImplementInterfacesOnDynamicCastableImplementation(root, declaration, symbol, context.Document, generator, model.Compilation)),
                            equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation)),
                        diagnostic);
                }
                else if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.MembersDeclaredOnImplementationTypeMustBeStaticRuleId
                    && diagnostic.Properties.ContainsKey(DynamicInterfaceCastableImplementationAnalyzer.NonStaticMemberIsMethodKey))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            MicrosoftNetCoreAnalyzersResources.MakeMethodDeclaredOnImplementationTypeStatic,
                            async ct => await MakeMemberDeclaredOnImplementationTypeStaticAsync(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
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

        protected abstract Document ImplementInterfacesOnDynamicCastableImplementation(
            SyntaxNode root,
            SyntaxNode declaration,
            INamedTypeSymbol type,
            Document document,
            SyntaxGenerator generator,
            Compilation compilation);

        protected abstract Task<Document> MakeMemberDeclaredOnImplementationTypeStaticAsync(
            SyntaxNode declaration,
            Document document,
            CancellationToken cancellationToken);
    }
}
