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
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1001: Types that own disposable fields should be disposable
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class TypesThatOwnDisposableFieldsShouldBeDisposableFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (generator.GetDeclaration(root.FindNode(context.Span)) is null)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.ImplementIDisposableInterface;
            RegisterCodeFix(context, title, title);
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxGenerator generator = editor.Generator;
            SyntaxNode declaration = generator.GetDeclaration(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan));
            if (declaration is null)
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

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
                editor.ReplaceNode(memberPartNode, (currentNode, g) => g.AsPublicInterfaceImplementation(currentNode, interfaceType));
            }
            else
            {
                SyntaxNode throwStatement = generator.ThrowStatement(generator.ObjectCreationExpression(model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotImplementedException)));
                SyntaxNode member = generator.MethodDeclaration(TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer.Dispose, statements: new[] { throwStatement });
                member = generator.AsPublicInterfaceImplementation(member, interfaceType);
                editor.AddMember(declaration, member);
            }
        }
    }
}
