// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1835: Prefer Memory/ReadOnlyMemory overloads for Stream ReadAsync/WriteAsync methods.
    ///
    /// Undesired methods (available since .NET Framework 4.5):
    ///
    /// - Stream.WriteAsync(Byte[], Int32, Int32)
    /// - Stream.WriteAsync(Byte[], Int32, Int32, CancellationToken)
    /// - Stream.ReadAsync(Byte[], Int32, Int32)
    /// - Stream.ReadAsync(Byte[], Int32, Int32, CancellationToken)
    ///
    /// Preferred methods (available since .NET Standard 2.1 and .NET Core 2.1):
    ///
    /// - Stream.WriteAsync(ReadOnlyMemory{Byte}, CancellationToken)
    /// - Stream.ReadAsync(Memory{Byte}, CancellationToken)
    ///
    /// </summary>
    public abstract class PreferStreamAsyncMemoryOverloadsFixer : CodeFixProvider
    {
        private static readonly SyntaxAnnotation s_asMemorySymbolAnnotation = new("SymbolId", "System.MemoryExtensions");

        // Checks if the argument in the specified index has a name. If it doesn't, returns that arguments. If it does, then looks for the argument using the specified name, and returns it, or null if not found.
        protected abstract SyntaxNode? GetArgumentByPositionOrName(IInvocationOperation invocation, int index, string name, out bool isNamed);

        // Verifies if the user passed `0` as the 1st argument (`offset`) and `buffer.Length` as the 2nd argument (`count`),
        // where `buffer` is the name of the variable passed as the 0th argument.
        protected abstract bool IsPassingZeroAndBufferLength(SemanticModel model, SyntaxNode bufferValueNode, SyntaxNode offsetValueNode, SyntaxNode countValueNode);

        // Ensures the invocation node is returned with nullability.
        protected abstract SyntaxNode GetNodeWithNullability(IInvocationOperation invocation);

        // Ensures the argument is retrieved with the name and nullability.
        protected abstract SyntaxNode GetNamedArgument(SyntaxGenerator generator, SyntaxNode node, bool isNamed, string newName);

        // Ensures the member invocation is retrieved with the name and nullability.
        protected abstract SyntaxNode GetNamedMemberInvocation(SyntaxGenerator generator, SyntaxNode node, string memberName);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferStreamAsyncMemoryOverloads.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(context => context.CodeActionEquivalenceKey, ApplyFixAsync);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken ct = context.CancellationToken;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(ct).ConfigureAwait(false);
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(ct).ConfigureAwait(false);

            if (!TryGetFix(model, root, context.Diagnostics[0], ct, out Fix fix))
            {
                return;
            }

            string equivalenceKey = fix.EquivalenceKey;

            context.RegisterCodeFix(
                CodeAction.Create(
                    MicrosoftNetCoreAnalyzersResources.PreferStreamAsyncMemoryOverloadsTitle,
                    cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(doc, context.Diagnostics,
                        (document, diagnostic, editor, token) => ApplyFixAsync(document, diagnostic, editor, equivalenceKey, token), cancellationToken),
                    equivalenceKey),
                context.Diagnostics);
        }

        private async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (!TryGetFix(model, editor.OriginalRoot, diagnostic, cancellationToken, out Fix fix) ||
                (equivalenceKey is not null && equivalenceKey != fix.EquivalenceKey))
            {
                return;
            }

            SyntaxNode streamInstanceNode = GetNodeWithNullability(fix.Invocation);

            editor.TrackNode(streamInstanceNode);
            editor.TrackNode(fix.Buffer.Node!);
            editor.TrackNode(fix.Offset.Node!);
            editor.TrackNode(fix.Count.Node!);
            if (fix.CancellationToken.Node is SyntaxNode cancellationTokenNode)
            {
                editor.TrackNode(cancellationTokenNode);
            }

            //  An argument can itself be a diagnosed invocation, so the rewritten call is built from the
            //  arguments as the inner fixes left them rather than from the original tree.
            editor.ReplaceNode(fix.Invocation.Syntax, (currentNode, generator) =>
            {
                SyntaxNode Current(SyntaxNode original) => currentNode.GetCurrentNode(original) ?? original;

                SyntaxNode bufferNode = Current(fix.Buffer.Node!);
                SyntaxNode offsetNode = Current(fix.Offset.Node!);
                SyntaxNode countNode = Current(fix.Count.Node!);

                // Depending on the arguments being passed to Read/WriteAsync, it's the substitution we will make
                SyntaxNode replacedInvocationNode;

                if (IsPassingZeroAndBufferLength(model, fix.Buffer.Node!, fix.Offset.Node!, fix.Count.Node!))
                {
                    // Remove 0 and buffer.length
                    replacedInvocationNode =
                        GetNamedArgument(generator, bufferNode, fix.Buffer.IsNamed, "buffer")
                        .WithTriviaFrom(bufferNode);
                }
                else
                {
                    // buffer.AsMemory(int start, int length)
                    // offset should become start
                    // count should become length
                    SyntaxNode namedStartNode = GetNamedArgument(generator, offsetNode, fix.Offset.IsNamed, "start");
                    SyntaxNode namedLengthNode = GetNamedArgument(generator, countNode, fix.Count.IsNamed, "length");

                    // Generate an invocation of the AsMemory() method from the byte array object, using the correct named arguments
                    SyntaxNode asMemoryExpressionNode = GetNamedMemberInvocation(generator, bufferNode, "AsMemory");
                    SyntaxNode asMemoryInvocationNode = generator.InvocationExpression(
                        asMemoryExpressionNode,
                        namedStartNode.WithTriviaFrom(offsetNode),
                        namedLengthNode.WithTriviaFrom(countNode)).WithAddImportsAnnotation().WithAdditionalAnnotations(s_asMemorySymbolAnnotation);

                    // Generate the new buffer argument, ensuring we include the buffer argument name if the user originally indicated one
                    replacedInvocationNode = GetNamedArgument(generator, asMemoryInvocationNode, fix.Buffer.IsNamed, "buffer")
                        .WithTriviaFrom(bufferNode);
                }

                // Create an async method call for the stream object with no arguments
                SyntaxNode currentStreamInstanceNode = Current(streamInstanceNode);
                SyntaxNode asyncMethodNode = generator.MemberAccessExpression(currentStreamInstanceNode, fix.Invocation.TargetMethod.Name);

                // Add the arguments to the async method call, with or without CancellationToken
                SyntaxNode[] nodeArguments;
                if (fix.CancellationToken.Node is SyntaxNode originalCancellationTokenNode)
                {
                    SyntaxNode currentCancellationTokenNode = Current(originalCancellationTokenNode);
                    SyntaxNode namedCancellationTokenNode = GetNamedArgument(generator, currentCancellationTokenNode, fix.CancellationToken.IsNamed, "cancellationToken");
                    nodeArguments = new SyntaxNode[] { replacedInvocationNode, namedCancellationTokenNode.WithTriviaFrom(currentCancellationTokenNode) };
                }
                else
                {
                    nodeArguments = new SyntaxNode[] { replacedInvocationNode };
                }

                return generator.InvocationExpression(asyncMethodNode, nodeArguments).WithTriviaFrom(currentNode);
            });
        }

        private bool TryGetFix(SemanticModel model, SyntaxNode root, Diagnostic diagnostic, CancellationToken cancellationToken, out Fix fix)
        {
            fix = default;

            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not SyntaxNode node ||
                model.GetOperation(node, cancellationToken) is not IInvocationOperation invocation)
            {
                return false;
            }

            // Defensive check to ensure the fix is only attempted on one of the 4 specific undesired overloads
            if (invocation.Arguments.Length is not (3 or 4))
            {
                return false;
            }

            SyntaxNode? bufferNode = GetArgumentByPositionOrName(invocation, 0, "buffer", out bool isBufferNamed);
            if (bufferNode is null)
            {
                return false;
            }

            SyntaxNode? offsetNode = GetArgumentByPositionOrName(invocation, 1, "offset", out bool isOffsetNamed);
            if (offsetNode is null)
            {
                return false;
            }

            SyntaxNode? countNode = GetArgumentByPositionOrName(invocation, 2, "count", out bool isCountNamed);
            if (countNode is null)
            {
                return false;
            }

            // No nullcheck for this, because there is an overload that may not contain it
            SyntaxNode? cancellationTokenNode = GetArgumentByPositionOrName(invocation, 3, "cancellationToken", out bool isCancellationTokenNamed);

            fix = new Fix(invocation,
                new Argument(bufferNode, isBufferNamed),
                new Argument(offsetNode, isOffsetNamed),
                new Argument(countNode, isCountNamed),
                new Argument(cancellationTokenNode, isCancellationTokenNamed));
            return true;
        }

        private readonly struct Argument
        {
            public Argument(SyntaxNode? node, bool isNamed)
            {
                Node = node;
                IsNamed = isNamed;
            }

            public SyntaxNode? Node { get; }

            public bool IsNamed { get; }
        }

        private readonly struct Fix
        {
            public Fix(IInvocationOperation invocation, Argument buffer, Argument offset, Argument count, Argument cancellationToken)
            {
                Invocation = invocation;
                Buffer = buffer;
                Offset = offset;
                Count = count;
                CancellationToken = cancellationToken;
            }

            public IInvocationOperation Invocation { get; }

            public Argument Buffer { get; }

            public Argument Offset { get; }

            public Argument Count { get; }

            public Argument CancellationToken { get; }

            /// <summary>
            /// Read and write get their own key, so that fixing all of one does not silently rewrite the other.
            /// </summary>
            public string EquivalenceKey => nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamAsyncMemoryOverloadsTitle) + Invocation.TargetMethod.Name;
        }
    }
}
