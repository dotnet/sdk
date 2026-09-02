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

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1064: Exceptions should be public
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class ExceptionsShouldBePublicFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(ExceptionsShouldBePublicAnalyzer.RuleId);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            RegisterCodeFix(context, MicrosoftCodeQualityAnalyzersResources.MakeExceptionPublic, nameof(ExceptionsShouldBePublicFixer));
            return Task.CompletedTask;
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode classDecl = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            editor.SetAccessibility(classDecl, Accessibility.Public);
            return Task.CompletedTask;
        }
    }
}