// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class ProvidePublicParameterlessSafeHandleConstructorFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(ProvidePublicParameterlessSafeHandleConstructorAnalyzer.RuleId);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            RegisterCodeFix(
                context,
                MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic,
                nameof(MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic));
            return Task.CompletedTask;
        }

        protected override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode enclosingNode = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            SyntaxNode declaration = editor.Generator.GetDeclaration(enclosingNode);

            if (declaration != null)
            {
                editor.SetAccessibility(declaration, Accessibility.Public);
            }

            return Task.CompletedTask;
        }
    }
}
