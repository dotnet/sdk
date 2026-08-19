// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseStartsWithInsteadOfIndexOfComparisonWithZero.RuleId);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            RegisterCodeFix(
                context,
                MicrosoftNetCoreAnalyzersResources.UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFixTitle,
                nameof(MicrosoftNetCoreAnalyzersResources.UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFixTitle));

            return Task.CompletedTask;
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            bool shouldNegate = diagnostic.Properties.ContainsKey(UseStartsWithInsteadOfIndexOfComparisonWithZero.ShouldNegateKey);
            bool compilationHasStartsWithCharOverload = diagnostic.Properties.ContainsKey(UseStartsWithInsteadOfIndexOfComparisonWithZero.CompilationHasStartsWithCharOverloadKey);
            _ = diagnostic.Properties.TryGetValue(UseStartsWithInsteadOfIndexOfComparisonWithZero.ExistingOverloadKey, out string? overloadValue);

            // The replacement re-emits the instance and the arguments, and `IndexOf(...) == 0` comparisons nest,
            // so those are read off the node as an inner fix has already rewritten it rather than off the
            // original tree.
            editor.ReplaceNode(node, (currentNode, generator) =>
            {
                if (GetIndexOfInvocation(currentNode) is not SyntaxNode invocation)
                {
                    return currentNode;
                }

                SyntaxNode instance = GetInstance(invocation);
                SyntaxNode[] arguments = GetArguments(invocation);

                switch (overloadValue)
                {
                    // For 'IndexOf(string)' and 'IndexOf(string, stringComparison)', we replace with StartsWith(same arguments)
                    case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadString:
                    case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadString_StringComparison:
                        return CreateStartsWithInvocationFromArguments(generator, instance, arguments, shouldNegate);

                    // For 'a.IndexOf(ch, stringComparison)':
                    // C#: Use 'a.AsSpan().StartsWith(stackalloc char[1] { ch }, stringComparison)'
                    // https://learn.microsoft.com/dotnet/api/system.memoryextensions.startswith?view=net-7.0#system-memoryextensions-startswith(system-readonlyspan((system-char))-system-readonlyspan((system-char))-system-stringcomparison)
                    // VB: Use a.StartsWith(c.ToString(), stringComparison)
                    case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadChar_StringComparison:
                        return HandleCharStringComparisonOverload(generator, instance, arguments, shouldNegate);

                    // If 'StartsWith(char)' is available, use it. Otherwise check '.Length > 0 && [0] == ch'
                    // For negation, we use '.Length == 0 || [0] != ch'
                    case UseStartsWithInsteadOfIndexOfComparisonWithZero.OverloadChar:
                        if (compilationHasStartsWithCharOverload)
                        {
                            return CreateStartsWithInvocationFromArguments(generator, instance, arguments, shouldNegate);
                        }

                        SyntaxNode lengthAccess = generator.MemberAccessExpression(instance, "Length");
                        SyntaxNode zeroLiteral = generator.LiteralExpression(0);

                        SyntaxNode indexed = generator.ElementAccessExpression(instance, zeroLiteral);
                        SyntaxNode ch = GetArgumentExpression(arguments[0]);

                        SyntaxNode replacement = shouldNegate
                            ? generator.LogicalOrExpression(
                                generator.ValueEqualsExpression(lengthAccess, zeroLiteral),
                                generator.ValueNotEqualsExpression(indexed, ch))
                            : generator.LogicalAndExpression(
                                generator.GreaterThanExpression(lengthAccess, zeroLiteral),
                                generator.ValueEqualsExpression(indexed, ch));

                        return AppendElasticMarker(replacement);

                    default:
                        Debug.Fail("This should never happen.");

                        return currentNode;
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns the <c>IndexOf</c> invocation <paramref name="comparison"/> compares with zero, or
        /// <see langword="null"/> when it is not a shape the fix handles.
        /// </summary>
        protected abstract SyntaxNode? GetIndexOfInvocation(SyntaxNode comparison);

        /// <summary>
        /// Returns the instance <paramref name="invocation"/> is called on.
        /// </summary>
        protected abstract SyntaxNode GetInstance(SyntaxNode invocation);

        /// <summary>
        /// Returns <paramref name="invocation"/>'s arguments, in source order.
        /// </summary>
        protected abstract SyntaxNode[] GetArguments(SyntaxNode invocation);

        /// <summary>
        /// Returns the expression <paramref name="argument"/> passes, without any name prefix.
        /// </summary>
        protected abstract SyntaxNode GetArgumentExpression(SyntaxNode argument);

        protected abstract SyntaxNode HandleCharStringComparisonOverload(SyntaxGenerator generator, SyntaxNode instance, SyntaxNode[] arguments, bool shouldNegate);
        protected abstract SyntaxNode AppendElasticMarker(SyntaxNode replacement);

        protected static SyntaxNode CreateStartsWithInvocationFromArguments(SyntaxGenerator generator, SyntaxNode instance, SyntaxNode[] arguments, bool shouldNegate)
        {
            var expression = generator.InvocationExpression(generator.MemberAccessExpression(instance, "StartsWith"), arguments);
            return shouldNegate ? generator.LogicalNotExpression(expression) : expression;
        }
    }
}
