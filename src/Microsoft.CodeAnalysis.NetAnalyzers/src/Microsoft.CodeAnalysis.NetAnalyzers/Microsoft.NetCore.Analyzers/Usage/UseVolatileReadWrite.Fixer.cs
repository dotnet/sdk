// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Usage
{
    public abstract class UseVolatileReadWriteFixer : CodeFixProvider
    {
        private const string ThreadVolatileReadMethodName = nameof(Thread.VolatileRead);
        private const string ThreadVolatileWriteMethodName = nameof(Thread.VolatileWrite);
        private const string VolatileReadMethodName = nameof(Volatile.Read);
        private const string VolatileWriteMethodName = nameof(Volatile.Write);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var typeProvider = WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation);
            var operation = semanticModel.GetOperation(node);
            if (typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread) is not INamedTypeSymbol threadType
                || typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingVolatile) is not INamedTypeSymbol volatileType
                || operation is not IInvocationOperation invocationOperation)
            {
                return;
            }

            var obsoleteMethodsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
            obsoleteMethodsBuilder.AddRange(threadType.GetMembers(ThreadVolatileReadMethodName).OfType<IMethodSymbol>());
            obsoleteMethodsBuilder.AddRange(threadType.GetMembers(ThreadVolatileWriteMethodName).OfType<IMethodSymbol>());
            var obsoleteMethods = obsoleteMethodsBuilder.ToImmutable();

            var volatileReadMethod = volatileType.GetMembers(VolatileReadMethodName).OfType<IMethodSymbol>().FirstOrDefault();
            var volatileWriteMethod = volatileType.GetMembers(VolatileWriteMethodName).OfType<IMethodSymbol>().FirstOrDefault();

            if (!SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod.ContainingType, threadType)
                || !obsoleteMethods.Any(SymbolEqualityComparer.Default.Equals, invocationOperation.TargetMethod)
                || volatileReadMethod is null
                || volatileWriteMethod is null)
            {
                return;
            }

            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.DoNotUseThreadVolatileReadWriteCodeFixTitle,
                ReplaceObsoleteCall,
                equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseThreadVolatileReadWriteCodeFixTitle));

            context.RegisterCodeFix(codeAction, context.Diagnostics);

            return;

            async Task<Document> ReplaceObsoleteCall(CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(context.Document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;

                string methodName;
                IEnumerable<SyntaxNode> arguments;
                if (invocationOperation.TargetMethod.Name.Equals(ThreadVolatileReadMethodName, StringComparison.Ordinal))
                {
                    methodName = VolatileReadMethodName;
                    arguments = [GetArgumentForVolatileReadCall(invocationOperation.Arguments[0], volatileReadMethod.Parameters[0])];
                }
                else
                {
                    methodName = VolatileWriteMethodName;
                    arguments = GetArgumentForVolatileWriteCall(invocationOperation.Arguments, volatileWriteMethod.Parameters);
                }

                var methodExpression = generator.MemberAccessExpression(
                    generator.TypeExpressionForStaticMemberAccess(volatileType),
                    methodName);
                var methodInvocation = generator.InvocationExpression(methodExpression, arguments);

                editor.ReplaceNode(invocationOperation.Syntax, methodInvocation.WithTriviaFrom(invocationOperation.Syntax));

                return context.Document.WithSyntaxRoot(editor.GetChangedRoot());
            }
        }

        protected abstract SyntaxNode GetArgumentForVolatileReadCall(IArgumentOperation argument, IParameterSymbol volatileReadParameter);

        protected abstract IEnumerable<SyntaxNode> GetArgumentForVolatileWriteCall(ImmutableArray<IArgumentOperation> arguments, ImmutableArray<IParameterSymbol> volatileWriteParameters);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("SYSLIB0054");
    }
}