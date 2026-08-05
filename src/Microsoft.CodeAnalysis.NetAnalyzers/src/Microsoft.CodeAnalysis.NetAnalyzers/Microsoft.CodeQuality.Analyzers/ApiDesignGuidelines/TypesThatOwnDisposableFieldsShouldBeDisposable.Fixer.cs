// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            SyntaxNode? declaration = root.FindNode(context.Span);
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

            if (!model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable, out INamedTypeSymbol? iDisposableType))
            {
                return document;
            }

            // Add the interface to the baselist.
            SyntaxNode interfaceType = generator.TypeExpression(iDisposableType);
            editor.AddInterfaceType(declaration, interfaceType);

            // Find a Dispose method. If one exists make that implement IDisposable, else generate a new method.
            var typeSymbol = model.GetDeclaredSymbol(declaration, cancellationToken) as INamedTypeSymbol;
            IMethodSymbol? disposeMethod = (typeSymbol?.GetMembers("Dispose"))?.OfType<IMethodSymbol>()?.Where(m => m.Parameters.IsEmpty).FirstOrDefault();
            if (disposeMethod != null && disposeMethod.DeclaringSyntaxReferences.Length == 1)
            {
                SyntaxNode memberPartNode = await disposeMethod.DeclaringSyntaxReferences.Single().GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                if (generator.GetDeclaration(memberPartNode) is not SyntaxNode disposeDeclaration ||
                    generator.AsPublicInterfaceImplementation(disposeDeclaration, interfaceType) is not SyntaxNode disposeImplementation)
                {
                    return document;
                }

                editor.ReplaceNode(disposeDeclaration, disposeImplementation);
            }
            else
            {
                if (!model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotImplementedException, out INamedTypeSymbol? notImplementedExceptionType))
                {
                    return document;
                }

                SyntaxNode throwStatement = generator.ThrowStatement(generator.ObjectCreationExpression(generator.TypeExpression(notImplementedExceptionType)));
                SyntaxNode member = generator.MethodDeclaration(TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer.Dispose, statements: new[] { throwStatement });
                if (generator.AsPublicInterfaceImplementation(member, interfaceType) is not SyntaxNode memberImplementation)
                {
                    return document;
                }

                editor.AddMember(declaration, memberImplementation);
            }

            return editor.GetChangedDocument();
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
