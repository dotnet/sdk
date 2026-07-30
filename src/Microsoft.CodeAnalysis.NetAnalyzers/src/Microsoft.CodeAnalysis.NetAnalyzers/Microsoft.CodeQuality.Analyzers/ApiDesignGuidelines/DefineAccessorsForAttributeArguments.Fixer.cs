// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class DefineAccessorsForAttributeArgumentsFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DefineAccessorsForAttributeArgumentsAnalyzer.RuleId);

        // The rule reports three different problems and offers a different action for each, so the fix-all
        // pass has to be told which one the user picked - DocumentBasedFixAllProvider hands over every
        // diagnostic it collected without filtering by the equivalence key.
        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                ApplyFixAsync);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document document = context.Document;
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
                string? title = GetTitle(diagnostic);

                // Offer nothing where the fix cannot reach the declaration the diagnostic named, rather than
                // registering an action that produces an unchanged document.
                if (title == null || GetNodeToFix(generator, node, diagnostic) == null)
                {
                    continue;
                }

                ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(diagnostic);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                            document,
                            diagnostics,
                            (doc, d, editor, token) => ApplyFixAsync(doc, d, editor, title, token),
                            cancellationToken),
                        equivalenceKey: title),
                    diagnostic);
            }
        }

        private static string? GetTitle(Diagnostic diagnostic)
        {
            if (!diagnostic.Properties.TryGetValue("case", out string? fixCase))
            {
                return null;
            }

            return fixCase switch
            {
                DefineAccessorsForAttributeArgumentsAnalyzer.AddAccessorCase => MicrosoftCodeQualityAnalyzersResources.CreatePropertyAccessorForParameter,
                DefineAccessorsForAttributeArgumentsAnalyzer.MakePublicCase => MicrosoftCodeQualityAnalyzersResources.MakeGetterPublic,
                DefineAccessorsForAttributeArgumentsAnalyzer.RemoveSetterCase => MicrosoftCodeQualityAnalyzersResources.MakeSetterNonPublic,
                _ => null,
            };
        }

        private static SyntaxNode? GetNodeToFix(SyntaxGenerator generator, SyntaxNode node, Diagnostic diagnostic)
        {
            if (!diagnostic.Properties.TryGetValue("case", out string? fixCase))
            {
                return null;
            }

            return fixCase switch
            {
                DefineAccessorsForAttributeArgumentsAnalyzer.AddAccessorCase => generator.GetDeclaration(node, DeclarationKind.Parameter),
                DefineAccessorsForAttributeArgumentsAnalyzer.MakePublicCase => generator.GetDeclaration(node, DeclarationKind.Property),
                DefineAccessorsForAttributeArgumentsAnalyzer.RemoveSetterCase => node,
                _ => null,
            };
        }

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            if (GetTitle(diagnostic) != equivalenceKey)
            {
                return;
            }

            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            SyntaxNode? nodeToFix = GetNodeToFix(editor.Generator, node, diagnostic);
            if (nodeToFix == null)
            {
                return;
            }

            switch (diagnostic.Properties["case"])
            {
                case DefineAccessorsForAttributeArgumentsAnalyzer.AddAccessorCase:
                    await AddAccessorAsync(document, nodeToFix, editor, cancellationToken).ConfigureAwait(false);
                    break;

                case DefineAccessorsForAttributeArgumentsAnalyzer.MakePublicCase:
                    MakePublic(node, nodeToFix, editor);
                    break;

                case DefineAccessorsForAttributeArgumentsAnalyzer.RemoveSetterCase:
                    editor.SetAccessibility(nodeToFix, Accessibility.Internal);
                    break;
            }
        }

        private static async Task AddAccessorAsync(Document document, SyntaxNode parameter, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (model.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol parameterSymbol)
            {
                return;
            }

            // Make the first character uppercase since we are generating a property.
            string propName = char.ToUpper(parameterSymbol.Name[0], CultureInfo.InvariantCulture).ToString() + parameterSymbol.Name[1..];

            INamedTypeSymbol typeSymbol = parameterSymbol.ContainingType;
            ISymbol? propertySymbol = typeSymbol.GetMembers(propName).FirstOrDefault(m => m.Kind == SymbolKind.Property);

            // Add a new property
            if (propertySymbol == null)
            {
                // Add it to the declaration that has this parameter, since a partial type can be declared
                // across several documents and the editor only edits this one.
                SyntaxNode typeDeclaration = editor.Generator.GetDeclaration(parameter, DeclarationKind.Class);
                if (typeDeclaration == null)
                {
                    return;
                }

                SyntaxNode newProperty = editor.Generator.PropertyDeclaration(propName,
                                                                              editor.Generator.TypeExpression(parameterSymbol.Type),
                                                                              Accessibility.Public,
                                                                              DeclarationModifiers.ReadOnly);
                newProperty = editor.Generator.WithGetAccessorStatements(newProperty, null);
                editor.AddMember(typeDeclaration, newProperty);
            }
            else
            {
                SyntaxReference? reference = propertySymbol.DeclaringSyntaxReferences.FirstOrDefault(r => r.SyntaxTree == editor.OriginalRoot.SyntaxTree);
                if (reference == null)
                {
                    return;
                }

                SyntaxNode propertyDeclaration = editor.Generator.GetDeclaration(await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false), DeclarationKind.Property);
                if (propertyDeclaration == null)
                {
                    return;
                }

                editor.SetGetAccessorStatements(propertyDeclaration, editor.Generator.DefaultMethodBody(model.Compilation));
                editor.SetModifiers(propertyDeclaration, editor.Generator.GetModifiers(propertyDeclaration) - DeclarationModifiers.WriteOnly);
            }
        }

        private static void MakePublic(SyntaxNode getMethod, SyntaxNode property, SyntaxEditor editor)
        {
            // Clear the accessibility on the getter.
            editor.SetAccessibility(getMethod, Accessibility.NotApplicable);

            // If the containing property is not public, make it so
            Accessibility propertyAccessibility = editor.Generator.GetAccessibility(property);
            if (propertyAccessibility != Accessibility.Public)
            {
                editor.SetAccessibility(property, Accessibility.Public);

                // Having just made the property public, if it has a setter with no Accessibility set, then we've just made the setter public.
                // Instead restore the setter's original accessibility so that we don't fire a violation with the generated code.
                SyntaxNode setter = editor.Generator.GetAccessor(property, DeclarationKind.SetAccessor);
                if (setter != null)
                {
                    Accessibility setterAccessibility = editor.Generator.GetAccessibility(setter);
                    if (setterAccessibility == Accessibility.NotApplicable)
                    {
                        editor.SetAccessibility(setter, propertyAccessibility);
                    }
                }
            }
        }
    }
}
