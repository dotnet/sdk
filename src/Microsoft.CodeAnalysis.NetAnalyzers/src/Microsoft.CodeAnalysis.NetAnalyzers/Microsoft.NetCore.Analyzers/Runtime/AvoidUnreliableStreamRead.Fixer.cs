// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
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
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2022: <inheritdoc cref="AvoidUnreliableStreamReadTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AvoidUnreliableStreamReadFixer : CodeFixProvider
    {
        private const string Async = nameof(Async);
        private const string ReadExactly = nameof(ReadExactly);
        private const string ReadExactlyAsync = nameof(ReadExactlyAsync);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(AvoidUnreliableStreamReadAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is null)
            {
                return;
            }

            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var operation = semanticModel.GetOperation(node, context.CancellationToken);

            if (operation is not IInvocationOperation invocation ||
                invocation.Instance is null)
            {
                return;
            }

            var compilation = semanticModel.Compilation;
            var streamType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOStream);

            if (streamType is null)
            {
                return;
            }

            var readExactlyMethods = streamType.GetMembers(ReadExactly)
                .OfType<IMethodSymbol>()
                .ToImmutableArray();

            if (readExactlyMethods.IsEmpty)
            {
                return;
            }

            var codeAction = CodeAction.Create(
                AvoidUnreliableStreamReadCodeFixTitle,
                ct => ReplaceWithReadExactlyCall(context.Document, ct),
                nameof(AvoidUnreliableStreamReadCodeFixTitle));

            context.RegisterCodeFix(codeAction, context.Diagnostics);

            async Task<Document> ReplaceWithReadExactlyCall(Document document, CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;
                var arguments = invocation.Arguments.GetArgumentsInParameterOrder();

                var isAsyncInvocation = invocation.TargetMethod.Name.EndsWith(Async, StringComparison.Ordinal);
                var methodExpression = generator.MemberAccessExpression(
                    invocation.Instance.Syntax,
                    isAsyncInvocation ? ReadExactlyAsync : ReadExactly);
                var methodInvocation = CanUseSpanOverload()
                    ? generator.InvocationExpression(
                        methodExpression,
                        isAsyncInvocation && arguments.Length == 4
                            // Stream.ReadExactlyAsync(buffer, ct)
                            ?[arguments[0].Syntax, arguments[3].Syntax]
                            // Stream.ReadExactly(buffer) and Stream.ReadExactlyAsync(buffer)
                            :[arguments[0].Syntax])
                    : generator.InvocationExpression(
                        methodExpression,
                        invocation.Arguments.Where(a => !a.IsImplicit).Select(a => a.Syntax));

                editor.ReplaceNode(invocation.Syntax, methodInvocation.WithTriviaFrom(invocation.Syntax));

                return document.WithSyntaxRoot(editor.GetChangedRoot());

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
        }
    }
}
