// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1872: <inheritdoc cref="PreferConvertToHexStringOverBitConverterTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class PreferConvertToHexStringOverBitConverterFixer : CodeFixProvider
    {
        private static readonly SyntaxAnnotation s_asSpanSymbolAnnotation = new("SymbolId", WellKnownTypeNames.SystemMemoryExtensions);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(PreferConvertToHexStringOverBitConverterAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();

            if (diagnostic is not { AdditionalLocations.Count: > 0, Properties.Count: 1 } ||
                !diagnostic.Properties.TryGetValue(PreferConvertToHexStringOverBitConverterAnalyzer.ReplacementPropertiesKey, out var convertToHexStringName) ||
                convertToHexStringName is null)
            {
                return;
            }

            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var bitConverterInvocation = GetInvocationFromTextSpan(diagnostic.AdditionalLocations[0].SourceSpan);
            var outerInvocation = GetInvocationFromTextSpan(context.Span);

            if (bitConverterInvocation is null || outerInvocation is null)
            {
                return;
            }

            var toLowerInvocation = diagnostic.AdditionalLocations.Count == 2
                ? GetInvocationFromTextSpan(diagnostic.AdditionalLocations[1].SourceSpan)
                : null;

            var codeAction = CodeAction.Create(
                string.Format(CultureInfo.CurrentCulture, PreferConvertToHexStringOverBitConverterCodeFixTitle, convertToHexStringName),
                ReplaceWithConvertToHexStringCall,
                nameof(PreferConvertToHexStringOverBitConverterCodeFixTitle));

            context.RegisterCodeFix(codeAction, context.Diagnostics);

            IInvocationOperation? GetInvocationFromTextSpan(TextSpan span)
            {
                var node = root.FindNode(span, getInnermostNodeForTie: true);

                if (node is null)
                {
                    return null;
                }

                return semanticModel.GetOperation(node, context.CancellationToken) as IInvocationOperation;
            }

            async Task<Document> ReplaceWithConvertToHexStringCall(CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(context.Document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;
                var bitConverterArgumentsInParameterOrder = bitConverterInvocation.Arguments.GetArgumentsInParameterOrder();

                var typeExpression = generator.DottedName(WellKnownTypeNames.SystemConvert);
                var methodExpression = generator.MemberAccessExpression(typeExpression, convertToHexStringName);
                var methodInvocation = bitConverterArgumentsInParameterOrder.Length switch
                {
                    // BitConverter.ToString(data).Replace("-", "") => Convert.ToHexString(data)
                    1 => generator.InvocationExpression(methodExpression, bitConverterArgumentsInParameterOrder[0].Value.Syntax),
                    // BitConverter.ToString(data, start).Replace("-", "") => Convert.ToHexString(data.AsSpan().Slice(start))
                    2 => generator.InvocationExpression(
                        methodExpression,
                        generator.InvocationExpression(generator.MemberAccessExpression(
                            generator.InvocationExpression(generator.MemberAccessExpression(
                                bitConverterArgumentsInParameterOrder[0].Value.Syntax,
                                nameof(MemoryExtensions.AsSpan))),
                            WellKnownMemberNames.SliceMethodName),
                        bitConverterArgumentsInParameterOrder[1].Value.Syntax))
                            .WithAddImportsAnnotation()
                            .WithAdditionalAnnotations(s_asSpanSymbolAnnotation),
                    // BitConverter.ToString(data, start, length).Replace("-", "") => Convert.ToHexString(data, start, length)
                    3 => generator.InvocationExpression(methodExpression, bitConverterArgumentsInParameterOrder.Select(a => a.Value.Syntax).ToArray()),
                    _ => throw new NotImplementedException()
                };

                // This branch is hit when string.ToLower* is used and Convert.ToHexStringLower is not available.
                if (toLowerInvocation is not null)
                {
                    methodInvocation = generator.InvocationExpression(
                        generator.MemberAccessExpression(methodInvocation, toLowerInvocation.TargetMethod.Name),
                        toLowerInvocation.Arguments.Select(a => a.Value.Syntax).ToArray());
                }

                editor.ReplaceNode(outerInvocation.Syntax, methodInvocation.WithTriviaFrom(outerInvocation.Syntax));

                return context.Document.WithSyntaxRoot(editor.GetChangedRoot());
            }
        }
    }
}
