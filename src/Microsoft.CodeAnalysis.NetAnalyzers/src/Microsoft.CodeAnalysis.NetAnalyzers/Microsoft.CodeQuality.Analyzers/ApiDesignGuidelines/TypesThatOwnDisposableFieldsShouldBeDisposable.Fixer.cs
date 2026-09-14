// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1001: Types that own disposable fields should be disposable
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class TypesThatOwnDisposableFieldsShouldBeDisposableFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = root.FindNode(context.Span);
            declaration = generator.GetDeclaration(declaration);

            if (declaration == null)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableInterface;
            context.RegisterCodeFix(CodeAction.Create(title,
                                                     async ct => await ImplementIDisposableAsync(context.Document, declaration, ct).ConfigureAwait(false),
                                                     equivalenceKey: title),
                                    context.Diagnostics);
        }

        private static async Task<Document> ImplementIDisposableAsync(Document document, SyntaxNode declaration, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;
            SemanticModel model = editor.SemanticModel;

            // Add the interface to the baselist.
            SyntaxNode interfaceType = generator.TypeExpression(model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable));
            editor.AddInterfaceType(declaration, interfaceType);

            // Find a Dispose method. If one exists make that implement IDisposable, else generate a new method.
            var typeSymbol = model.GetDeclaredSymbol(declaration, cancellationToken) as INamedTypeSymbol;
            IMethodSymbol? disposeMethod = (typeSymbol?.GetMembers("Dispose"))?.OfType<IMethodSymbol>()?.Where(m => m.Parameters.IsEmpty).FirstOrDefault();
            if (disposeMethod != null && disposeMethod.DeclaringSyntaxReferences.Length == 1)
            {
                SyntaxNode memberPartNode = await disposeMethod.DeclaringSyntaxReferences.Single().GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                memberPartNode = generator.GetDeclaration(memberPartNode);
                editor.ReplaceNode(memberPartNode, generator.AsPublicInterfaceImplementation(memberPartNode, interfaceType));
            }
            else
            {
                SyntaxNode throwStatement = generator.ThrowStatement(generator.ObjectCreationExpression(model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotImplementedException)));
                SyntaxNode member = generator.MethodDeclaration(TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer.Dispose, statements: new[] { throwStatement });
                member = generator.AsPublicInterfaceImplementation(member, interfaceType);
                editor.AddMember(declaration, member);
            }

            return editor.GetChangedDocument();
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
