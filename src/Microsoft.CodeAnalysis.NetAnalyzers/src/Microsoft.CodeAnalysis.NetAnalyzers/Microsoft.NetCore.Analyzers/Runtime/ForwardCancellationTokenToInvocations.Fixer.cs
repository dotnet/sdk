// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class ForwardCancellationTokenToInvocationsFixer<TArgumentSyntax> : CodeFixProvider
        where TArgumentSyntax : SyntaxNode
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
            out ImmutableArray<TArgumentSyntax> arguments);

        // Verifies if the specified argument was passed with an explicit name.
        protected abstract bool IsArgumentNamed(IArgumentOperation argumentOperation);

        // Retrieves the invocation expression for a conditional operation, which consists of the dot and the method name.
        protected abstract SyntaxNode GetConditionalOperationInvocationExpression(SyntaxNode invocationNode);

        protected abstract SyntaxNode GetTypeSyntaxForArray(IArrayTypeSymbol type);
        protected abstract IEnumerable<SyntaxNode> GetExpressions(ImmutableArray<TArgumentSyntax> newArguments);
        protected abstract SyntaxNode GetArrayCreationExpression(SyntaxGenerator generator, SyntaxNode typeSyntax, IEnumerable<SyntaxNode> expressions);

        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(ForwardCancellationTokenToInvocationsAnalyzer.RuleId);

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

            // The analyzer created the diagnostic on the IdentifierNameSyntax, and the parent is the actual invocation
            if (!TryGetInvocation(model, node, ct, out IInvocationOperation? invocation))
            {
                return;
            }

            ImmutableDictionary<string, string>? properties = context.Diagnostics[0].Properties;

            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ShouldFix, out string shouldFix) ||
                string.IsNullOrEmpty(shouldFix) ||
                shouldFix.Equals("0", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            // The name that identifies the object that is to be passed
            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ArgumentName, out string argumentName) ||
                string.IsNullOrEmpty(argumentName))
            {
                return;
            }

            // If the invocation requires the token to be passed with a name, use this
            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ParameterName, out string parameterName))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.ForwardCancellationTokenToInvocationsTitle;

            if (!TryGetExpressionAndArguments(invocation.Syntax, out SyntaxNode? expression, out ImmutableArray<TArgumentSyntax> newArguments))
            {
                return;
            }

            var paramsArrayType = invocation.Arguments.SingleOrDefault(a => a.ArgumentKind == ArgumentKind.ParamArray)?.Value.Type as IArrayTypeSymbol;
            Task<Document> CreateChangedDocumentAsync(CancellationToken _)
            {
                SyntaxNode newRoot = TryGenerateNewDocumentRoot(doc, root, invocation, argumentName, parameterName, expression, newArguments, paramsArrayType);
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

        private static SyntaxNode TryGenerateNewDocumentRoot(
            Document doc,
            SyntaxNode root,
            IInvocationOperation invocation,
            string invocationTokenArgumentName,
            string ancestorTokenParameterName,
            SyntaxNode expression,
            ImmutableArray<TArgumentSyntax> currentArguments,
            IArrayTypeSymbol? paramsArrayType)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(doc);

            ImmutableArray<SyntaxNode> newArguments;
            if (paramsArrayType is not null)
            {
                // current callsite is a params array, we need to wrap all these arguments to preserve semantics
                var typeSyntax = GetTypeSyntaxForArray(paramsArrayType);
                var expressions = GetExpressions(currentArguments);
                newArguments = ImmutableArray.Create(GetArrayCreationExpression(generator, typeSyntax, expressions));
            }
            else
            {
                // not a params array just pass the existing arguments along
                newArguments = currentArguments.CastArray<SyntaxNode>();
            }

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

            newArguments = newArguments.Add(cancellationTokenArgument);

            // Insert the new arguments to the new invocation
            SyntaxNode newInvocationWithArguments = generator.InvocationExpression(expression, newArguments).WithTriviaFrom(invocation.Syntax);

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
