// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeQuality.Analyzers;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpStaticHolderTypesFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(StaticHolderTypesAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root.FindToken(context.Span.Start).Parent?.FirstAncestorOrSelf<ClassDeclarationSyntax>() is not null)
            {
                string title = MicrosoftCodeQualityAnalyzersResources.MakeClassStatic;
                RegisterCodeFix(context, title, title);
            }
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            if (editor.OriginalRoot.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<ClassDeclarationSyntax>() is not ClassDeclarationSyntax classDeclaration)
            {
                return Task.CompletedTask;
            }

            DeclarationModifiers modifiers = editor.Generator.GetModifiers(classDeclaration);
            editor.SetModifiers(classDeclaration, modifiers - DeclarationModifiers.Sealed + DeclarationModifiers.Static);

            MemberDeclarationSyntax defaultConstructor = classDeclaration.Members.FirstOrDefault(m => m.IsDefaultConstructor());
            if (defaultConstructor != null)
            {
                editor.RemoveNode(defaultConstructor);
            }

            return Task.CompletedTask;
        }
    }

    internal static class CA1052CSharpCodeFixProviderExtensions
    {
        internal static bool IsDefaultConstructor(this MemberDeclarationSyntax member)
        {
            if (member.Kind() != SyntaxKind.ConstructorDeclaration)
            {
                return false;
            }

            var constructor = (ConstructorDeclarationSyntax)member;
            if (constructor.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            {
                return false;
            }

            return constructor.ParameterList.Parameters.Count == 0;
        }
    }
}
