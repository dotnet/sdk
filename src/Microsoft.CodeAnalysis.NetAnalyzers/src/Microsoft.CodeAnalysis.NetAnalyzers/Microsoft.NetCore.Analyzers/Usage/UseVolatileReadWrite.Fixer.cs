// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
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
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NetCore.Analyzers.Usage
{
    public abstract class UseVolatileReadWriteFixer : SyntaxEditorBasedCodeFixProvider
    {
        private const string ThreadVolatileReadMethodName = nameof(Thread.VolatileRead);
        private const string ThreadVolatileWriteMethodName = nameof(Thread.VolatileWrite);
        private const string VolatileReadMethodName = nameof(Volatile.Read);
        private const string VolatileWriteMethodName = nameof(Volatile.Write);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("SYSLIB0054");

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (TryGetObsoleteCall(semanticModel, root, context.Span, context.CancellationToken).Invocation is null)
            {
                return;
            }

            RegisterCodeFix(context,
                MicrosoftNetCoreAnalyzersResources.DoNotUseThreadVolatileReadWriteCodeFixTitle,
                nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseThreadVolatileReadWriteCodeFixTitle));
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var (invocation, volatileType) = TryGetObsoleteCall(semanticModel, editor.OriginalRoot, diagnostic.Location.SourceSpan, cancellationToken);
            if (invocation is null || volatileType is null)
            {
                return;
            }

            var generator = editor.Generator;

            string methodName;
            ImmutableArray<IParameterSymbol> parameters;
            if (invocation.TargetMethod.Name.Equals(ThreadVolatileReadMethodName, StringComparison.Ordinal))
            {
                methodName = VolatileReadMethodName;
                parameters = volatileType.GetMembers(VolatileReadMethodName).OfType<IMethodSymbol>().First().Parameters;
            }
            else
            {
                methodName = VolatileWriteMethodName;
                parameters = volatileType.GetMembers(VolatileWriteMethodName).OfType<IMethodSymbol>().First().Parameters;
            }

            // IInvocationOperation.Arguments is in parameter order, which is the order the rewritten
            // call is emitted in, but the arguments themselves have to be taken from the syntax. Map
            // each one back to where it appears in source so the two can be zipped up below.
            var originalArguments = GetArguments(invocation.Syntax);
            var sourceIndices = invocation.Arguments.Select(argument => originalArguments.IndexOf(argument.Syntax)).ToImmutableArray();
            var parameterNames = invocation.Arguments.Select(argument => parameters[argument.Parameter!.Ordinal].Name).ToImmutableArray();

            var methodExpression = generator.MemberAccessExpression(
                generator.TypeExpressionForStaticMemberAccess(volatileType),
                methodName);

            // Thread.VolatileWrite takes its value by value, so one diagnosed call can sit inside
            // another's argument. The arguments are therefore read off the node as already rewritten
            // rather than off SyntaxEditor.OriginalRoot, which would re-emit the inner call from its
            // pre-fix syntax and drop the annotation the fix-all provider tracks it by.
            editor.ReplaceNode(invocation.Syntax, (currentNode, currentGenerator) =>
            {
                var currentArguments = GetArguments(currentNode);
                var arguments = sourceIndices.Select((sourceIndex, i) => WithParameterName(currentArguments[sourceIndex], parameterNames[i]));

                return currentGenerator.InvocationExpression(methodExpression, arguments).WithTriviaFrom(currentNode);
            });
        }

        protected abstract ImmutableArray<SyntaxNode> GetArguments(SyntaxNode invocationSyntax);

        /// <summary>
        /// Renames an explicitly named argument to <paramref name="parameterName"/>, returning
        /// <paramref name="argumentSyntax"/> unchanged when the argument is positional.
        /// </summary>
        protected abstract SyntaxNode WithParameterName(SyntaxNode argumentSyntax, string parameterName);

        private static (IInvocationOperation? Invocation, INamedTypeSymbol? VolatileType) TryGetObsoleteCall(
            SemanticModel semanticModel, SyntaxNode root, TextSpan span, CancellationToken cancellationToken)
        {
            var node = root.FindNode(span, getInnermostNodeForTie: true);
            var typeProvider = WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation);
            if (typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread) is not INamedTypeSymbol threadType
                || typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingVolatile) is not INamedTypeSymbol volatileType
                || semanticModel.GetOperation(node, cancellationToken) is not IInvocationOperation invocationOperation)
            {
                return default;
            }

            var obsoleteMethodsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
            obsoleteMethodsBuilder.AddRange(threadType.GetMembers(ThreadVolatileReadMethodName).OfType<IMethodSymbol>());
            obsoleteMethodsBuilder.AddRange(threadType.GetMembers(ThreadVolatileWriteMethodName).OfType<IMethodSymbol>());
            var obsoleteMethods = obsoleteMethodsBuilder.ToImmutable();

            if (!SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod.ContainingType, threadType)
                || !obsoleteMethods.Any(SymbolEqualityComparer.Default.Equals, invocationOperation.TargetMethod)
                || volatileType.GetMembers(VolatileReadMethodName).OfType<IMethodSymbol>().FirstOrDefault() is null
                || volatileType.GetMembers(VolatileWriteMethodName).OfType<IMethodSymbol>().FirstOrDefault() is null)
            {
                return default;
            }

            return (invocationOperation, volatileType);
        }
    }
}