// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2022: <inheritdoc cref="AvoidUnreliableStreamReadTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AvoidUnreliableStreamReadFixer : SyntaxEditorBasedCodeFixProvider
    {
        private const string Async = nameof(Async);
        private const string ReadExactly = nameof(ReadExactly);
        private const string ReadExactlyAsync = nameof(ReadExactlyAsync);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(AvoidUnreliableStreamReadAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (GetReadInvocation(semanticModel, root.FindNode(context.Span, getInnermostNodeForTie: true), context.CancellationToken) is null)
            {
                return;
            }

            RegisterCodeFix(context, AvoidUnreliableStreamReadCodeFixTitle, nameof(AvoidUnreliableStreamReadCodeFixTitle));
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (GetReadInvocation(semanticModel, node, cancellationToken) is not IInvocationOperation invocation)
            {
                return;
            }

            var arguments = invocation.Arguments.GetArgumentsInParameterOrder();
            var isAsyncInvocation = invocation.TargetMethod.Name.EndsWith(Async, StringComparison.Ordinal);

            var instance = invocation.Instance!.Syntax;
            ImmutableArray<SyntaxNode> replacementArguments = CanUseSpanOverload()
                ? isAsyncInvocation && arguments.Length == 4
                    // Stream.ReadExactlyAsync(buffer, ct)
                    ? ImmutableArray.Create(arguments[0].Syntax, arguments[3].Syntax)
                    // Stream.ReadExactly(buffer) and Stream.ReadExactlyAsync(buffer)
                    : ImmutableArray.Create(arguments[0].Syntax)
                : invocation.Arguments.Where(a => !a.IsImplicit).Select(a => a.Syntax).ToImmutableArray();

            editor.TrackNode(instance);

            foreach (SyntaxNode argument in replacementArguments)
            {
                editor.TrackNode(argument);
            }

            // The receiver and the arguments are carried over from inside the node being replaced, so they have
            // to be read back off the current node rather than off the original tree.
            editor.ReplaceNode(invocation.Syntax, (currentNode, generator) =>
            {
                var methodExpression = generator.MemberAccessExpression(
                    currentNode.GetCurrentNode(instance) ?? instance,
                    isAsyncInvocation ? ReadExactlyAsync : ReadExactly);

                return generator.InvocationExpression(methodExpression, replacementArguments.Select(argument => currentNode.GetCurrentNode(argument) ?? argument))
                    .WithTriviaFrom(currentNode);
            });

            bool CanUseSpanOverload()
            {
                return arguments.Length >= 3 &&
                    arguments[2].Value is IPropertyReferenceOperation propertyRef &&
                    propertyRef.Property.Name.Equals(WellKnownMemberNames.LengthPropertyName, StringComparison.Ordinal) &&
                    AreSameInstance(arguments[0].Value, propertyRef.Instance);
            }

            static bool AreSameInstance(IOperation? operation1, IOperation? operation2)
            {
                return (operation1, operation2) switch
                {
                    (IFieldReferenceOperation fieldRef1, IFieldReferenceOperation fieldRef2) => fieldRef1.Member == fieldRef2.Member,
                    (IPropertyReferenceOperation propRef1, IPropertyReferenceOperation propRef2) => propRef1.Member == propRef2.Member,
                    (IParameterReferenceOperation paramRef1, IParameterReferenceOperation paramRef2) => paramRef1.Parameter == paramRef2.Parameter,
                    (ILocalReferenceOperation localRef1, ILocalReferenceOperation localRef2) => localRef1.Local == localRef2.Local,
                    _ => false,
                };
            }
        }

        private static IInvocationOperation? GetReadInvocation(SemanticModel semanticModel, SyntaxNode? node, CancellationToken cancellationToken)
        {
            if (node is null ||
                semanticModel.GetOperation(node, cancellationToken) is not IInvocationOperation invocation ||
                invocation.Instance is null)
            {
                return null;
            }

            var streamType = semanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOStream);

            return streamType is not null && streamType.GetMembers(ReadExactly).OfType<IMethodSymbol>().Any()
                ? invocation
                : null;
        }
    }
}
