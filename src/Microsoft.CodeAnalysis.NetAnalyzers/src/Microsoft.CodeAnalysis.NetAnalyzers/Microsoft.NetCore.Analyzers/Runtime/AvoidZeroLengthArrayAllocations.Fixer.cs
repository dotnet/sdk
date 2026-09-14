// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1825: Avoid zero-length array allocations.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AvoidZeroLengthArrayAllocationsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AvoidZeroLengthArrayAllocationsAnalyzer.RuleId);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            RegisterCodeFix(
                context,
                MicrosoftNetCoreAnalyzersResources.UseArrayEmpty,
                MicrosoftNetCoreAnalyzersResources.UseArrayEmpty);

            return Task.CompletedTask;
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            // In case the ArrayCreationExpressionSyntax is wrapped in an ArgumentSyntax or some other node with the same span,
            // get the innermost node for ties.
            SyntaxNode nodeToFix = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;

            INamedTypeSymbol? arrayTypeSymbol = semanticModel.Compilation.GetSpecialType(SpecialType.System_Array);
            if (arrayTypeSymbol is null)
            {
                return;
            }

            ITypeSymbol? elementType = GetArrayElementType(nodeToFix, semanticModel, cancellationToken);
            if (elementType is null)
            {
                return;
            }

            // A zero-length array creation has no operands, so the replacement carries nothing over from the
            // node it replaces and these diagnostics cannot nest.
            SyntaxNode arrayEmptyInvocation = GenerateArrayEmptyInvocation(generator, arrayTypeSymbol, elementType).WithTriviaFrom(nodeToFix);
            editor.ReplaceNode(nodeToFix, arrayEmptyInvocation);
        }

        private static ITypeSymbol? GetArrayElementType(SyntaxNode arrayCreationExpression, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            var typeInfo = semanticModel.GetTypeInfo(arrayCreationExpression, cancellationToken);
            // When Type is null in cases like 'T[] goo = { }', use ConvertedType instead (https://github.com/dotnet/roslyn/issues/23545).
            // When Type isn't null, do not use ConvertedType. For cases like `object[] goo = new string[0]`,
            // we want to return the string type symbol, not the object one.
            var arrayType = (typeInfo.Type ?? typeInfo.ConvertedType) as IArrayTypeSymbol;
            return arrayType?.ElementType;
        }

        private static SyntaxNode GenerateArrayEmptyInvocation(SyntaxGenerator generator, INamedTypeSymbol arrayTypeSymbol, ITypeSymbol elementType)
        {
            SyntaxNode arrayEmptyName = generator.MemberAccessExpression(
                generator.TypeExpressionForStaticMemberAccess(arrayTypeSymbol),
                generator.GenericName(AvoidZeroLengthArrayAllocationsAnalyzer.ArrayEmptyMethodName, elementType));
            return generator.InvocationExpression(arrayEmptyName);
        }
    }
}
