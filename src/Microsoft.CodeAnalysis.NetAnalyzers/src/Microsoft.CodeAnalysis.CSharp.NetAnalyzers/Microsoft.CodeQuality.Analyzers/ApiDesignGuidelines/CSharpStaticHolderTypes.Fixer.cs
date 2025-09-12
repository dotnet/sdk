﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeQuality.Analyzers;
using Microsoft.CodeAnalysis.CodeActions;
using Analyzer.Utilities;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpStaticHolderTypesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(StaticHolderTypesAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document document = context.Document;
            CodeAnalysis.Text.TextSpan span = context.Span;
            CancellationToken cancellationToken = context.CancellationToken;

            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            ClassDeclarationSyntax? classDeclaration = root.FindToken(span.Start).Parent?.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDeclaration != null)
            {
                string title = MicrosoftCodeQualityAnalyzersResources.MakeClassStatic;
                var codeAction = CodeAction.Create(title,
                                                  async ct => await MakeClassStaticAsync(document, classDeclaration, ct).ConfigureAwait(false),
                                                  equivalenceKey: title);
                context.RegisterCodeFix(codeAction, context.Diagnostics);
            }
        }

        private static async Task<Document> MakeClassStaticAsync(Document document, ClassDeclarationSyntax classDeclaration, CancellationToken ct)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            DeclarationModifiers modifiers = editor.Generator.GetModifiers(classDeclaration);
            editor.SetModifiers(classDeclaration, modifiers - DeclarationModifiers.Sealed + DeclarationModifiers.Static);

            SyntaxList<MemberDeclarationSyntax> members = classDeclaration.Members;
            MemberDeclarationSyntax defaultConstructor = members.FirstOrDefault(m => m.IsDefaultConstructor());
            if (defaultConstructor != null)
            {
                editor.RemoveNode(defaultConstructor);
            }

            return editor.GetChangedDocument();
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
