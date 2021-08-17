// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>CA1837, CA1839, CA1840: Use Environment.ProcessId / ProcessPath / CurrentManagedThreadId</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseEnvironmentMembersFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            UseEnvironmentMembers.EnvironmentProcessIdRuleId,
            UseEnvironmentMembers.EnvironmentProcessPathRuleId,
            UseEnvironmentMembers.EnvironmentCurrentManagedThreadIdRuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SemanticModel model = await doc.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await doc.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is SyntaxNode node &&
                model.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEnvironment, out var environmentType) &&
                model.GetOperation(node, context.CancellationToken) is IPropertyReferenceOperation operation)
            {
                string title, memberName;
                switch (context.Diagnostics[0].Id)
                {
                    case UseEnvironmentMembers.EnvironmentProcessIdRuleId:
                        title = MicrosoftNetCoreAnalyzersResources.UseEnvironmentProcessIdFix;
                        memberName = "ProcessId";
                        break;

                    case UseEnvironmentMembers.EnvironmentProcessPathRuleId:
                        title = MicrosoftNetCoreAnalyzersResources.UseEnvironmentProcessPathFix;
                        memberName = "ProcessPath";
                        break;

                    default:
                        RoslynDebug.Assert(context.Diagnostics[0].Id == UseEnvironmentMembers.EnvironmentCurrentManagedThreadIdRuleId);
                        title = MicrosoftNetCoreAnalyzersResources.UseEnvironmentCurrentManagedThreadIdFix;
                        memberName = "CurrentManagedThreadId";
                        break;
                }

                context.RegisterCodeFix(
                    new MyCodeAction(title,
                    async cancellationToken =>
                    {
                        DocumentEditor editor = await DocumentEditor.CreateAsync(doc, cancellationToken).ConfigureAwait(false);
                        var replacement = editor.Generator.MemberAccessExpression(editor.Generator.TypeExpressionForStaticMemberAccess(environmentType), memberName);
                        editor.ReplaceNode(node, replacement.WithTriviaFrom(node));
                        return editor.GetChangedDocument();
                    },
                    equivalenceKey: title),
                    context.Diagnostics);
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