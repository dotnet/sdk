// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class ForwardCancellationTokenToInvocationsFixer<TArgumentSyntax> : SyntaxEditorBasedCodeFixProvider
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

        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(ForwardCancellationTokenToInvocationsAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken ct = context.CancellationToken;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(ct).ConfigureAwait(false);
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(ct).ConfigureAwait(false);

            if (!TryGetFix(model, root, context.Diagnostics[0], ct, out _))
            {
                return;
            }

            RegisterCodeFix(context,
                MicrosoftNetCoreAnalyzersResources.ForwardCancellationTokenToInvocationsTitle,
                nameof(MicrosoftNetCoreAnalyzersResources.ForwardCancellationTokenToInvocationsTitle));
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (!TryGetFix(model, editor.OriginalRoot, diagnostic, cancellationToken, out Fix fix))
            {
                return;
            }

            editor.TrackNode(fix.Expression);
            foreach (TArgumentSyntax argument in fix.Arguments)
            {
                editor.TrackNode(argument);
            }

            // An argument can itself be a diagnosed invocation, so the invocation is rebuilt from the
            // arguments as the inner fixes left them rather than from the original tree.
            editor.ReplaceNode(fix.Invocation.Syntax, (currentNode, generator) =>
            {
                SyntaxNode expression = currentNode.GetCurrentNode(fix.Expression) ?? fix.Expression;

                ImmutableArray<TArgumentSyntax>.Builder currentArguments = ImmutableArray.CreateBuilder<TArgumentSyntax>(fix.Arguments.Length);
                foreach (TArgumentSyntax argument in fix.Arguments)
                {
                    currentArguments.Add((TArgumentSyntax)(currentNode.GetCurrentNode(argument) ?? argument));
                }

                return GenerateInvocation(generator, fix, expression, currentArguments.MoveToImmutable()).WithTriviaFrom(currentNode);
            });
        }

        private bool TryGetFix(SemanticModel model, SyntaxNode root, Diagnostic diagnostic, CancellationToken cancellationToken, out Fix fix)
        {
            fix = default;

            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not SyntaxNode node)
            {
                return false;
            }

            // The analyzer created the diagnostic on the IdentifierNameSyntax, and the parent is the actual invocation
            if (!TryGetInvocation(model, node, cancellationToken, out IInvocationOperation? invocation))
            {
                return false;
            }

            ImmutableDictionary<string, string?> properties = diagnostic.Properties;

            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ShouldFix, out var shouldFix) ||
                string.IsNullOrEmpty(shouldFix) ||
                shouldFix!.Equals("0", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            // The name that identifies the object that is to be passed
            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ArgumentName, out var argumentName) ||
                string.IsNullOrEmpty(argumentName))
            {
                return false;
            }

            // If the invocation requires the token to be passed with a name, use this
            if (!properties.TryGetValue(ForwardCancellationTokenToInvocationsAnalyzer.ParameterName, out var parameterName))
            {
                return false;
            }

            if (!TryGetExpressionAndArguments(invocation.Syntax, out SyntaxNode? expression, out ImmutableArray<TArgumentSyntax> arguments))
            {
                return false;
            }

            var paramsArrayType = invocation.Arguments.SingleOrDefault(a => a.ArgumentKind == ArgumentKind.ParamArray)?.Value.Type as IArrayTypeSymbol;

            fix = new Fix(invocation, expression, arguments, argumentName!, parameterName!, paramsArrayType);
            return true;
        }

        private SyntaxNode GenerateInvocation(SyntaxGenerator generator, in Fix fix, SyntaxNode expression, ImmutableArray<TArgumentSyntax> currentArguments)
        {
            ImmutableArray<SyntaxNode> newArguments;
            if (fix.ParamsArrayType is not null)
            {
                // current callsite is a params array, we need to wrap all these arguments to preserve semantics
                var typeSyntax = GetTypeSyntaxForArray(fix.ParamsArrayType);
                var expressions = GetExpressions(currentArguments);
                newArguments = ImmutableArray.Create(GetArrayCreationExpression(generator, typeSyntax, expressions));
            }
            else
            {
                // not a params array just pass the existing arguments along
                newArguments = currentArguments.CastArray<SyntaxNode>();
            }

            SyntaxNode identifier = generator.IdentifierName(fix.ArgumentName);
            SyntaxNode cancellationTokenArgument;
            if (!string.IsNullOrEmpty(fix.ParameterName))
            {
                cancellationTokenArgument = generator.Argument(fix.ParameterName, RefKind.None, identifier);
            }
            else
            {
                cancellationTokenArgument = generator.Argument(identifier);
            }

            newArguments = newArguments.Add(cancellationTokenArgument);

            return generator.InvocationExpression(expression, newArguments);
        }

        private readonly struct Fix
        {
            public Fix(IInvocationOperation invocation, SyntaxNode expression, ImmutableArray<TArgumentSyntax> arguments,
                string argumentName, string parameterName, IArrayTypeSymbol? paramsArrayType)
            {
                Invocation = invocation;
                Expression = expression;
                Arguments = arguments;
                ArgumentName = argumentName;
                ParameterName = parameterName;
                ParamsArrayType = paramsArrayType;
            }

            public IInvocationOperation Invocation { get; }

            public SyntaxNode Expression { get; }

            public ImmutableArray<TArgumentSyntax> Arguments { get; }

            /// <summary>The name of the token to forward.</summary>
            public string ArgumentName { get; }

            /// <summary>The parameter to name the forwarded token after, or empty to pass it positionally.</summary>
            public string ParameterName { get; }

            public IArrayTypeSymbol? ParamsArrayType { get; }
        }
    }
}
