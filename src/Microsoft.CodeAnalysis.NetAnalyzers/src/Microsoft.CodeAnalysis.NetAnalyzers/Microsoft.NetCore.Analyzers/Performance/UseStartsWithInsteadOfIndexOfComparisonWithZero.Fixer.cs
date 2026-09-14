// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseStartsWithInsteadOfIndexOfComparisonWithZero.RuleId);

        public override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var diagnostic = context.Diagnostics[0];
            var root = await document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            context.RegisterCodeFix(
                CodeAction.Create(MicrosoftNetCoreAnalyzersResources.UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFixTitle,
                createChangedDocument: cancellationToken =>
                {
                    var instance = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan);
                    var arguments = new SyntaxNode[diagnostic.AdditionalLocations.Count - 1];
                    for (int i = 1; i < diagnostic.AdditionalLocations.Count; i++)
                    {
                        arguments[i - 1] = root.FindNode(diagnostic.AdditionalLocations[i].SourceSpan);
                    }

                    var generator = SyntaxGenerator.GetGenerator(document);
                    var shouldNegate = diagnostic.Properties.TryGetValue(UseStartsWithInsteadOfIndexOfComparisonWithZero.ShouldNegateKey, out _);
                    var compilationHasStartsWithCharOverload = diagnostic.Properties.TryGetKey(UseStartsWithInsteadOfIndexOfComparisonWithZero.CompilationHasStartsWithCharOverloadKey, out _);
                    _ = diagnostic.Properties.TryGetValue(UseStartsWithInsteadOfIndexOfComparisonWithZero.ExistingOverloadKey, out var overloadValue);
                    switch (overloadValue)
                    {
                        // For 'IndexOf(string)' and 'IndexOf(string, stringComparison)', we replace with StartsWith(same arguments)
                        case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadString:
                        case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadString_StringComparison:
                            return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, CreateStartsWithInvocationFromArguments(generator, instance, arguments, shouldNegate))));

                        // For 'a.IndexOf(ch, stringComparison)':
                        // C#: Use 'a.AsSpan().StartsWith(stackalloc char[1] { ch }, stringComparison)'
                        // https://learn.microsoft.com/dotnet/api/system.memoryextensions.startswith?view=net-7.0#system-memoryextensions-startswith(system-readonlyspan((system-char))-system-readonlyspan((system-char))-system-stringcomparison)
                        // VB: Use a.StartsWith(c.ToString(), stringComparison)
                        case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadChar_StringComparison:
                            return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, HandleCharStringComparisonOverload(generator, instance, arguments, shouldNegate))));

                        // If 'StartsWith(char)' is available, use it. Otherwise check '.Length > 0 && [0] == ch'
                        // For negation, we use '.Length == 0 || [0] != ch'
                        case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadChar:
                            if (compilationHasStartsWithCharOverload)
                            {
                                return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, CreateStartsWithInvocationFromArguments(generator, instance, arguments, shouldNegate))));
                            }

                            var lengthAccess = generator.MemberAccessExpression(instance, "Length");
                            var zeroLiteral = generator.LiteralExpression(0);

                            var indexed = generator.ElementAccessExpression(instance, zeroLiteral);
                            var ch = root.FindNode(arguments[0].Span, getInnermostNodeForTie: true);

                            var replacement = shouldNegate
                                ? generator.LogicalOrExpression(
                                    generator.ValueEqualsExpression(lengthAccess, zeroLiteral),
                                    generator.ValueNotEqualsExpression(indexed, ch))
                                : generator.LogicalAndExpression(
                                    generator.GreaterThanExpression(lengthAccess, zeroLiteral),
                                    generator.ValueEqualsExpression(indexed, ch));

                            return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, AppendElasticMarker(replacement))));

                        default:
                            Debug.Fail("This should never happen.");
                            return Task.FromResult(document);
                    }
                },
                equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFixTitle)),
                context.Diagnostics);
        }

        protected abstract SyntaxNode HandleCharStringComparisonOverload(SyntaxGenerator generator, SyntaxNode instance, SyntaxNode[] arguments, bool shouldNegate);
        protected abstract SyntaxNode AppendElasticMarker(SyntaxNode replacement);

        protected static SyntaxNode CreateStartsWithInvocationFromArguments(SyntaxGenerator generator, SyntaxNode instance, SyntaxNode[] arguments, bool shouldNegate)
        {
            var expression = generator.InvocationExpression(generator.MemberAccessExpression(instance, "StartsWith"), arguments);
            return shouldNegate ? generator.LogicalNotExpression(expression) : expression;
        }
    }
}
