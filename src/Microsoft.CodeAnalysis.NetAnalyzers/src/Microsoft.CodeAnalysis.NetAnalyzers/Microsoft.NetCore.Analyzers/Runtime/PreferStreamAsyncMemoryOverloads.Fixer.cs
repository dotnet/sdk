// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
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
        // Checks if the argument in the specified index has a name. If it doesn't, returns that arguments. If it does, then looks for the argument using the specified name, and returns it, or null if not found.
        protected abstract SyntaxNode? GetArgumentByPositionOrName(IInvocationOperation invocation, int index, string name, out bool isNamed);

        // Verifies if a namespace has already been added to the usings/imports list.
        protected abstract bool IsSystemNamespaceImported(IReadOnlyList<SyntaxNode> importList);

        // Verifies if the user passed `0` as the 1st argument (`offset`) and `buffer.Length` as the 2nd argument (`count`),
        // where `buffer` is the name of the variable passed as the 0th argument.
        protected abstract bool IsPassingZeroAndBufferLength(SemanticModel model, SyntaxNode bufferValueNode, SyntaxNode offsetValueNode, SyntaxNode countValueNode);

        // Ensures the invocation node is returned with nullability.
        protected abstract SyntaxNode GetNodeWithNullability(IInvocationOperation invocation);

        // Ensures the argument is retrieved with the name and nullability.
        protected abstract SyntaxNode GetNamedArgument(SyntaxGenerator generator, SyntaxNode node, bool isNamed, string newName);

        // Ensures the member invocation is retrieved with the name and nullability.
        protected abstract SyntaxNode GetNamedMemberInvocation(SyntaxGenerator generator, SyntaxNode node, string memberName);

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(PreferStreamAsyncMemoryOverloads.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken ct = context.CancellationToken;
            SyntaxNode root = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);

            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not SyntaxNode node)
            {
                return;
            }

            SemanticModel model = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);

            if (model.GetOperation(node, ct) is not IInvocationOperation invocation)
            {
                return;
            }

            // Defensive check to ensure the fix is only attempted on one of the 4 specific undesired overloads
            if (invocation.Arguments.Length is not (3 or 4))
            {
                return;
            }

            SyntaxNode? bufferNode = GetArgumentByPositionOrName(invocation, 0, "buffer", out bool isBufferNamed);
            if (bufferNode == null)
            {
                return;
            }

            SyntaxNode? offsetNode = GetArgumentByPositionOrName(invocation, 1, "offset", out bool isOffsetNamed);
            if (offsetNode == null)
            {
                return;
            }

            SyntaxNode? countNode = GetArgumentByPositionOrName(invocation, 2, "count", out bool isCountNamed);
            if (countNode == null)
            {
                return;
            }

            // No nullcheck for this, because there is an overload that may not contain it
            SyntaxNode? cancellationTokenNode = GetArgumentByPositionOrName(invocation, 3, "cancellationToken", out bool isCancellationTokenNamed);

            string title = MicrosoftNetCoreAnalyzersResources.PreferStreamAsyncMemoryOverloadsTitle;

            Task<Document> createChangedDocument(CancellationToken _) => FixInvocationAsync(model, doc, root,
                                                         invocation, invocation.TargetMethod.Name,
                                                         bufferNode, isBufferNamed,
                                                         offsetNode, isOffsetNamed,
                                                         countNode, isCountNamed,
                                                         cancellationTokenNode, isCancellationTokenNamed);

            context.RegisterCodeFix(
                new MyCodeAction(
                    title: title,
                    createChangedDocument,
                    equivalenceKey: title + invocation.TargetMethod.Name),
                context.Diagnostics);
        }

        private Task<Document> FixInvocationAsync(SemanticModel model, Document doc, SyntaxNode root,
            IInvocationOperation invocation, string methodName,
            SyntaxNode bufferNode, bool isBufferNamed,
            SyntaxNode offsetNode, bool isOffsetNamed,
            SyntaxNode countNode, bool isCountNamed,
            SyntaxNode? cancellationTokenNode, bool isCancellationTokenNamed)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(doc);

            // The stream-derived instance
            SyntaxNode streamInstanceNode = GetNodeWithNullability(invocation);

            // Depending on the arguments being passed to Read/WriteAsync, it's the substitution we will make
            SyntaxNode replacedInvocationNode;

            if (IsPassingZeroAndBufferLength(model, bufferNode, offsetNode, countNode))
            {
                // Remove 0 and buffer.length
                replacedInvocationNode =
                    GetNamedArgument(generator, bufferNode, isBufferNamed, "buffer")
                    .WithTriviaFrom(bufferNode);
            }
            else
            {
                // buffer.AsMemory(int start, int length)
                // offset should become start
                // count should become length
                SyntaxNode namedStartNode = GetNamedArgument(generator, offsetNode, isOffsetNamed, "start");
                SyntaxNode namedLengthNode = GetNamedArgument(generator, countNode, isCountNamed, "length");

                // Generate an invocation of the AsMemory() method from the byte array object, using the correct named arguments
                SyntaxNode asMemoryExpressionNode = GetNamedMemberInvocation(generator, bufferNode, "AsMemory");
                SyntaxNode asMemoryInvocationNode = generator.InvocationExpression(
                    asMemoryExpressionNode,
                    namedStartNode.WithTriviaFrom(offsetNode),
                    namedLengthNode.WithTriviaFrom(countNode));

                // Generate the new buffer argument, ensuring we include the buffer argument name if the user originally indicated one
                replacedInvocationNode = GetNamedArgument(generator, asMemoryInvocationNode, isBufferNamed, "buffer")
                    .WithTriviaFrom(bufferNode);
            }

            // Create an async method call for the stream object with no arguments
            SyntaxNode asyncMethodNode = generator.MemberAccessExpression(streamInstanceNode, methodName);

            // Add the arguments to the async method call, with or without CancellationToken
            SyntaxNode[] nodeArguments;
            if (cancellationTokenNode != null)
            {
                SyntaxNode namedCancellationTokenNode = GetNamedArgument(generator, cancellationTokenNode, isCancellationTokenNamed, "cancellationToken");
                nodeArguments = new SyntaxNode[] { replacedInvocationNode, namedCancellationTokenNode.WithTriviaFrom(cancellationTokenNode) };
            }
            else
            {
                nodeArguments = new SyntaxNode[] { replacedInvocationNode };
            }
            SyntaxNode newInvocationExpression = generator.InvocationExpression(asyncMethodNode, nodeArguments).WithTriviaFrom(streamInstanceNode);

            bool containsSystemImport = IsSystemNamespaceImported(generator.GetNamespaceImports(root));

            // The invocation needs to be replaced before adding the import/using, it won't work the other way around
            SyntaxNode newRoot = generator.ReplaceNode(root, invocation.Syntax, newInvocationExpression.WithTriviaFrom(invocation.Syntax));
            SyntaxNode newRootWithImports = containsSystemImport ? newRoot : generator.AddNamespaceImports(newRoot, generator.NamespaceImportDeclaration(nameof(System)));

            return Task.FromResult(doc.WithSyntaxRoot(newRootWithImports));
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
