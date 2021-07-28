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
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    /// <summary>CA2247: Do not create TaskCompletionSource with wrong arguments.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class DoNotCreateTaskCompletionSourceWithWrongArgumentsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DoNotCreateTaskCompletionSourceWithWrongArguments.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            CancellationToken cancellationToken = context.CancellationToken;
            SyntaxNode root = await doc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SemanticModel model = await doc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // If we're able to make the desired substitution...
            var (targetNode, replacementField) = GetTaskCreationOptionsField(context, root, model, cancellationToken);
            if (replacementField != null)
            {
                // ...then offer it.
                string title = MicrosoftNetCoreAnalyzersResources.DoNotCreateTaskCompletionSourceWithWrongArgumentsFix;
                context.RegisterCodeFix(
                    new MyCodeAction(title,
                        async ct =>
                        {
                            // Replace "TaskContinuationOptions.Value" with "TaskCreationOptions.Value"
                            DocumentEditor editor = await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(false);
                            editor.ReplaceNode(targetNode,
                                editor.Generator.Argument(
                                    editor.Generator.MemberAccessExpression(
                                        editor.Generator.TypeExpressionForStaticMemberAccess(replacementField.ContainingType), replacementField.Name)));
                            return editor.GetChangedDocument();
                        },
                        equivalenceKey: title),
                    context.Diagnostics);
            }

            static (SyntaxNode Expression, IFieldSymbol? ReplacementField) GetTaskCreationOptionsField(
                CodeFixContext context, SyntaxNode root, SemanticModel model, CancellationToken cancellationToken)
            {
                if (// If we can get all the necessary types,
                    model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskCompletionSource1, out _) &&
                    model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskContinuationOptions, out var taskContinutationOptionsType) &&
                    model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskCreationOptions, out INamedTypeSymbol? taskCreationOptionsType) &&

                    // and the provided expression is an argument,
                    root.FindNode(context.Span) is SyntaxNode expression &&
                    model.GetOperationWalkingUpParentChain(expression, cancellationToken) is IArgumentOperation arg &&

                    // and it wraps a conversion from a TaskContinuationOptions member
                    arg.Value is IConversionOperation convert &&
                    convert.Operand is IFieldReferenceOperation field &&
                    field.Type.Equals(taskContinutationOptionsType) &&
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

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private sealed class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey) :
                base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}