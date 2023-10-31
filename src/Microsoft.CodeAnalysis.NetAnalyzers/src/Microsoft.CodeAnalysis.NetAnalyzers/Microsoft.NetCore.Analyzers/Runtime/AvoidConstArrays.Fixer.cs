// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1861: Avoid constant arrays as arguments. Replace with static readonly arrays.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AvoidConstArraysFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AvoidConstArraysAnalyzer.RuleId);

        private static readonly ImmutableArray<string> s_collectionMemberEndings = ImmutableArray.Create("array", "collection", "enumerable", "list");

        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document document = context.Document;
            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            context.RegisterCodeFix(CodeAction.Create(
                    MicrosoftNetCoreAnalyzersResources.AvoidConstArraysCodeFixTitle,
                    async ct => await ExtractConstArrayAsync(document, root, node, context.Diagnostics[0].Properties, ct).ConfigureAwait(false),
                    equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.AvoidConstArraysCodeFixTitle)),
                context.Diagnostics);
        }

        private static async Task<Document> ExtractConstArrayAsync(Document document, SyntaxNode root, SyntaxNode node,
            ImmutableDictionary<string, string?> properties, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;
            IArrayCreationOperation arrayArgument = GetArrayCreationOperation(node, model, cancellationToken, out bool isInvoked);
            ISymbol enclosingSymbol = model.GetEnclosingSymbol(node.SpanStart, cancellationToken)!;
            INamedTypeSymbol containingType = enclosingSymbol.ContainingType;
            HashSet<string> identifiers = new(containingType.MemberNames);
            if (enclosingSymbol is IMethodSymbol method)
            {
                identifiers.AddRange(method.Parameters.Select(p => p.Name));
            }

            IMethodBodyOperation? containingMethod = arrayArgument.GetAncestor<IMethodBodyOperation>(OperationKind.MethodBody);
            if (containingMethod?.BlockBody is not null)
            {
                identifiers.AddRange(containingMethod.BlockBody.Locals.Select(l => l.Name));
            }

            // Get a valid member name for the extracted constant
            string newMemberName = GetExtractedMemberName(identifiers, properties["paramName"] ?? GetMemberNameFromType(arrayArgument));

            // Get method containing the symbol that is being diagnosed
            IOperation? methodContext = arrayArgument.GetAncestor<IMethodBodyOperation>(OperationKind.MethodBody);
            methodContext ??= arrayArgument.GetAncestor<IBlockOperation>(OperationKind.Block); // VB methods have a different structure than CS methods

            // Create the new member
            SyntaxNode newMember = generator.FieldDeclaration(
                newMemberName,
                generator.TypeExpression(arrayArgument.Type),
                GetAccessibility(methodContext is null ? null : model.GetEnclosingSymbol(methodContext.Syntax.SpanStart, cancellationToken)),
                DeclarationModifiers.Static | DeclarationModifiers.ReadOnly,
                arrayArgument.Syntax.WithoutTrailingTrivia() // don't include extra trivia before the end of the declaration
            );

            // Add any additional formatting
            if (methodContext is not null)
            {
                newMember = newMember.FormatForExtraction(methodContext.Syntax);
            }

            ISymbol? lastFieldOrPropertySymbol = containingType.GetMembers().LastOrDefault(x => x is IFieldSymbol or IPropertySymbol);
            if (lastFieldOrPropertySymbol is not null)
            {
                var span = lastFieldOrPropertySymbol.Locations.First().SourceSpan;
                if (root.FullSpan.Contains(span))
                {
                    // Insert after fields or properties
                    SyntaxNode lastFieldOrPropertyNode = root.FindNode(span);
                    editor.InsertAfter(generator.GetDeclaration(lastFieldOrPropertyNode), newMember);
                }
                else if (methodContext != null)
                {
                    // Span not found
                    editor.InsertBefore(methodContext.Syntax, newMember);
                }
            }
            else if (methodContext != null)
            {
                // No fields or properties, insert right before the containing method for simplicity
                editor.InsertBefore(methodContext.Syntax, newMember);
            }

            // Replace argument with a reference to our new member
            SyntaxNode identifier = generator.IdentifierName(newMemberName);
            if (isInvoked)
            {
                editor.ReplaceNode(node, generator.WithExpression(identifier, node));
            }
            else
            {
                // add any extra trivia that was after the original argument
                editor.ReplaceNode(node, generator.Argument(identifier).WithTriviaFrom(arrayArgument.Syntax));
            }

            // Return changed document
            return editor.GetChangedDocument();
        }

        private static IArrayCreationOperation GetArrayCreationOperation(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken, out bool isInvoked)
        {
            // The analyzer only passes a diagnostic for two scenarios, each having an IArrayCreationOperation:
            //      1. The node is an IArgumentOperation that is a direct parent of an IArrayCreationOperation
            //      2. The node is an IArrayCreationOperation already, as it was pulled from an
            //         invocation, like with LINQ extension methods

            // If this is a LINQ invocation, the node is already an IArrayCreationOperation
            if (model.GetOperation(node, cancellationToken) is IArrayCreationOperation arrayCreation)
            {
                isInvoked = true;
                return arrayCreation;
            }
            // Otherwise, we'll get the IArrayCreationOperation from the argument node's child
            isInvoked = false;
            return (IArrayCreationOperation)model.GetOperation(node.ChildNodes().First(), cancellationToken)!;
        }

        private static string GetExtractedMemberName(ISet<string> identifierNames, string parameterName)
        {
            bool hasCollectionEnding = s_collectionMemberEndings.Any(x => parameterName.EndsWith(x, true, CultureInfo.InvariantCulture));

            if (parameterName == "source" // for LINQ, "sourceArray" is clearer than "source"
                || (identifierNames.Contains(parameterName) && !hasCollectionEnding))
            {
                parameterName += "Array";
            }

            if (identifierNames.Contains(parameterName))
            {
                int suffix = 0;
                while (identifierNames.Contains(parameterName + suffix))
                {
                    suffix++;
                }

                return parameterName + suffix;
            }

            return parameterName;
        }

        private static string GetMemberNameFromType(IArrayCreationOperation arrayCreationOperation)
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            return ((IArrayTypeSymbol)arrayCreationOperation.Type!).ElementType.OriginalDefinition.Name.ToLowerInvariant() + "Array";
#pragma warning restore CA1308 // Normalize strings to uppercase
        }

        private static Accessibility GetAccessibility(ISymbol? methodSymbol)
        {
            // If private or public, return private since public accessibility not wanted for fields by default
            return methodSymbol?.GetResultantVisibility() switch
            {
                // Return internal if internal
                SymbolVisibility.Internal => Accessibility.Internal,
                _ => Accessibility.Private
            };
        }
    }

    internal static class SyntaxNodeExtensions
    {
        internal static SyntaxNode FormatForExtraction(this SyntaxNode node, SyntaxNode previouslyContainingNode)
        {
            return node.HasTrailingTrivia ? node : node.WithTrailingTrivia(previouslyContainingNode.GetTrailingTrivia());
        }
    }
}
