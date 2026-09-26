// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    /// <summary>CA2247: Do not create TaskCompletionSource with wrong arguments.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class DoNotCreateTaskCompletionSourceWithWrongArgumentsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotCreateTaskCompletionSourceWithWrongArguments.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // If we're able to make the desired substitution...
            if (GetTaskCreationOptionsField(root, model, context.Span, cancellationToken).ReplacementField is not null)
            {
                // ...then offer it.
                string title = MicrosoftNetCoreAnalyzersResources.DoNotCreateTaskCompletionSourceWithWrongArgumentsFix;
                RegisterCodeFix(context, title, title);
            }
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var (targetNode, replacementField) = GetTaskCreationOptionsField(editor.OriginalRoot, model, diagnostic.Location.SourceSpan, cancellationToken);
            if (replacementField is null)
            {
                return;
            }

            // Replace "TaskContinuationOptions.Value" with "TaskCreationOptions.Value"
            editor.ReplaceNode(targetNode,
                editor.Generator.Argument(
                    editor.Generator.MemberAccessExpression(
                        editor.Generator.TypeExpressionForStaticMemberAccess(replacementField.ContainingType), replacementField.Name)));
        }

        private static (SyntaxNode Expression, IFieldSymbol? ReplacementField) GetTaskCreationOptionsField(
            SyntaxNode root, SemanticModel model, TextSpan span, CancellationToken cancellationToken)
        {
            if (// If we can get all the necessary types,
                model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskCompletionSource1, out _) &&
                model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskContinuationOptions, out var taskContinutationOptionsType) &&
                model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskCreationOptions, out INamedTypeSymbol? taskCreationOptionsType) &&

                // and the provided expression is an argument,
                root.FindNode(span) is SyntaxNode expression &&
                model.GetOperationWalkingUpParentChain(expression, cancellationToken) is IArgumentOperation arg &&

                // and it wraps a conversion from a TaskContinuationOptions member
                arg.Value is IConversionOperation convert &&
                convert.Operand is IFieldReferenceOperation field &&
                taskContinutationOptionsType.Equals(field.Type) &&
                taskContinutationOptionsType.Equals(field.Field.ContainingType) &&

                // and that option also exists on TaskCreationOptions,
                taskCreationOptionsType.GetMembers(field.Field.Name).FirstOrDefault() is IFieldSymbol taskCreationOptionsField)
            {
                // then hand back the found SyntaxNode and desired TaskCreationOptions field to be substituted.
                return (expression, taskCreationOptionsField);
            }

            // Otherwise, nothing to fix.
            return default;
        }
    }
}