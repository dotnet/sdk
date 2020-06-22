// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class ForwardCancellationTokenToInvocationsFixer : CodeFixProvider
    {
        // Attempts to retrieve the invocation from the current operation.
        protected abstract bool TryGetInvocation(
            SemanticModel model,
            SyntaxNode node,
            CancellationToken ct,
            [NotNullWhen(returnValue: true)] out IInvocationOperation? invocation);

        // Retrieves the invocation expression node and the invocation argument list
        protected abstract bool TryGetExpressionAndArguments(
            SyntaxNode invocationNode,
            [NotNullWhen(returnValue: true)] out SyntaxNode? expression,
            [NotNullWhen(returnValue: true)] out List<SyntaxNode>? arguments);

        // Verifies if the specified argument was passed with an explicit name.
        protected abstract bool IsArgumentNamed(IArgumentOperation argumentOperation);

        // Retrieves the invocation expression for a conditional operation, which consists of the dot and the method name.
        protected abstract SyntaxNode GetConditionalOperationInvocationExpression(SyntaxNode invocationNode);

        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(ForwardCancellationTokenToInvocationsAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken ct = context.CancellationToken;
            SyntaxNode root = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);

            if (!(root.FindNode(context.Span, getInnermostNodeForTie: true) is SyntaxNode node))
            {
                return;
            }

            SemanticModel model = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);

            // The analyzer created the diagnostic on the IdentifierNameSyntax, and the parent is the actual invocation
            if (!TryGetInvocation(model, node, ct, out IInvocationOperation? invocation))
            {
                return;
            }

            ImmutableDictionary<string, string>? properties = context.Diagnostics[0].Properties;

            // The name that identifies the object that is to be passed
            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ArgumentName, out string argumentName) || string.IsNullOrEmpty(argumentName))
            {
                return;
            }

            // If the invocation requires the token to be passed with a name, use this
            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ParameterName, out string parameterName))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.ForwardCancellationTokenToInvocationsTitle;

            if (!TryGetExpressionAndArguments(invocation.Syntax, out SyntaxNode? expression, out List<SyntaxNode>? newArguments))
            {
                return;
            }

            Task<Document> CreateChangedDocumentAsync(CancellationToken _)
            {
                SyntaxNode newRoot = TryGenerateNewDocumentRoot(doc, root, invocation, argumentName, parameterName, expression, newArguments);
                Document newDocument = doc.WithSyntaxRoot(newRoot);
                return Task.FromResult(newDocument);
            }

            context.RegisterCodeFix(
                new MyCodeAction(
                    title: title,
                    CreateChangedDocumentAsync,
                    equivalenceKey: title),
                context.Diagnostics);
        }

        private SyntaxNode TryGenerateNewDocumentRoot(
            Document doc,
            SyntaxNode root,
            IInvocationOperation invocation,
            string invocationTokenArgumentName,
            string ancestorTokenParameterName,
            SyntaxNode expression,
            List<SyntaxNode> newArguments)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(doc);

            SyntaxNode identifier = generator.IdentifierName(invocationTokenArgumentName);
            SyntaxNode cancellationTokenArgument;
            if (!string.IsNullOrEmpty(ancestorTokenParameterName))
            {
                cancellationTokenArgument = generator.Argument(ancestorTokenParameterName, RefKind.None, identifier);
            }
            else
            {
                cancellationTokenArgument = generator.Argument(identifier);
            }

            newArguments.Add(cancellationTokenArgument);

            SyntaxNode newInstance;
            // The instance is null when calling a static method from another type
            switch (invocation.Instance)
            {
                case null:
                    newInstance = expression;
                    break;
                case IConditionalAccessInstanceOperation _:
                    newInstance = GetConditionalOperationInvocationExpression(invocation.Syntax);
                    break;
                default:
                    if (invocation.Instance.IsImplicit)
                    {
                        newInstance = invocation.GetInstanceSyntax()!;
                    }
                    // Calling a method from an object, we must include the instance variable name
                    else
                    {
                        newInstance = generator.MemberAccessExpression(invocation.GetInstanceSyntax(), invocation.TargetMethod.Name);
                    }
                    break;
            }
            // Insert the new arguments to the new invocation
            SyntaxNode newInvocationWithArguments = generator.InvocationExpression(newInstance, newArguments).WithTriviaFrom(invocation.Syntax);

            return generator.ReplaceNode(root, invocation.Syntax, newInvocationWithArguments);
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