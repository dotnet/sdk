// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
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

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1872: <inheritdoc cref="PreferConvertToHexStringOverBitConverterTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferConvertToHexStringOverBitConverterFixer : SyntaxEditorBasedCodeFixProvider
    {
        private static readonly SyntaxAnnotation s_asSpanSymbolAnnotation = new("SymbolId", WellKnownTypeNames.SystemMemoryExtensions);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferConvertToHexStringOverBitConverterAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();

            if (diagnostic is null ||
                GetReplacementMethodName(diagnostic) is not string convertToHexStringName)
            {
                return;
            }

            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (GetInvocation(root, semanticModel, diagnostic.AdditionalLocations[0].SourceSpan, context.CancellationToken) is null ||
                GetInvocation(root, semanticModel, context.Span, context.CancellationToken) is null)
            {
                return;
            }

            RegisterCodeFix(
                context,
                string.Format(CultureInfo.CurrentCulture, PreferConvertToHexStringOverBitConverterCodeFixTitle, convertToHexStringName),
                nameof(PreferConvertToHexStringOverBitConverterCodeFixTitle));
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            if (GetReplacementMethodName(diagnostic) is not string convertToHexStringName)
            {
                return;
            }

            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var root = editor.OriginalRoot;

            if (GetInvocation(root, semanticModel, diagnostic.AdditionalLocations[0].SourceSpan, cancellationToken) is not IInvocationOperation bitConverterInvocation ||
                GetInvocation(root, semanticModel, diagnostic.Location.SourceSpan, cancellationToken) is not IInvocationOperation outerInvocation)
            {
                return;
            }

            var toLowerInvocation = diagnostic.AdditionalLocations.Count == 2
                ? GetInvocation(root, semanticModel, diagnostic.AdditionalLocations[1].SourceSpan, cancellationToken)
                : null;

            var bitConverterArgumentsInParameterOrder = bitConverterInvocation.Arguments.GetArgumentsInParameterOrder();
            var carriedOver = bitConverterArgumentsInParameterOrder.Select(a => a.Value.Syntax)
                .Concat(toLowerInvocation?.Arguments.Select(a => a.Value.Syntax) ?? Enumerable.Empty<SyntaxNode>())
                .ToImmutableArray();

            // The replacement carries over syntax from inside the invocation it replaces, so that syntax has to
            // be read as the fixes nested inside it left it rather than off the original tree.
            foreach (var node in carriedOver)
            {
                editor.TrackNode(node);
            }

            editor.ReplaceNode(outerInvocation.Syntax, (currentOuterInvocation, generator) =>
            {
                SyntaxNode Current(SyntaxNode original) => currentOuterInvocation.GetCurrentNode(original) ?? original;

                var typeExpression = generator.DottedName(WellKnownTypeNames.SystemConvert);
                var methodExpression = generator.MemberAccessExpression(typeExpression, convertToHexStringName);
                var methodInvocation = bitConverterArgumentsInParameterOrder.Length switch
                {
                    // BitConverter.ToString(data).Replace("-", "") => Convert.ToHexString(data)
                    1 => generator.InvocationExpression(methodExpression, Current(bitConverterArgumentsInParameterOrder[0].Value.Syntax)),
                    // BitConverter.ToString(data, start).Replace("-", "") => Convert.ToHexString(data.AsSpan().Slice(start))
                    2 => generator.InvocationExpression(
                        methodExpression,
                        generator.InvocationExpression(generator.MemberAccessExpression(
                            generator.InvocationExpression(generator.MemberAccessExpression(
                                Current(bitConverterArgumentsInParameterOrder[0].Value.Syntax),
                                nameof(MemoryExtensions.AsSpan))),
                            WellKnownMemberNames.SliceMethodName),
                        Current(bitConverterArgumentsInParameterOrder[1].Value.Syntax)))
                            .WithAddImportsAnnotation()
                            .WithAdditionalAnnotations(s_asSpanSymbolAnnotation),
                    // BitConverter.ToString(data, start, length).Replace("-", "") => Convert.ToHexString(data, start, length)
                    3 => generator.InvocationExpression(methodExpression, bitConverterArgumentsInParameterOrder.Select(a => Current(a.Value.Syntax)).ToArray()),
                    _ => throw new NotImplementedException()
                };

                // This branch is hit when string.ToLower* is used and Convert.ToHexStringLower is not available.
                if (toLowerInvocation is not null)
                {
                    methodInvocation = generator.InvocationExpression(
                        generator.MemberAccessExpression(methodInvocation, toLowerInvocation.TargetMethod.Name),
                        toLowerInvocation.Arguments.Select(a => Current(a.Value.Syntax)).ToArray());
                }

                return methodInvocation.WithTriviaFrom(currentOuterInvocation);
            });
        }

        private static string? GetReplacementMethodName(Diagnostic diagnostic)
        {
            return diagnostic is { AdditionalLocations.Count: > 0, Properties.Count: 1 } &&
                diagnostic.Properties.TryGetValue(PreferConvertToHexStringOverBitConverterAnalyzer.ReplacementPropertiesKey, out var name)
                ? name
                : null;
        }

        private static IInvocationOperation? GetInvocation(SyntaxNode root, SemanticModel semanticModel, TextSpan span, CancellationToken cancellationToken)
        {
            var node = root.FindNode(span, getInnermostNodeForTie: true);

            return node is null ? null : semanticModel.GetOperation(node, cancellationToken) as IInvocationOperation;
        }
    }
}
