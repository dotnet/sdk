// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpMakeTypesInternal : MakeTypesInternal<SyntaxKind>
    {
        protected override ImmutableArray<SyntaxKind> TypeKinds { get; } =
            ImmutableArray.Create(SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration, SyntaxKind.InterfaceDeclaration, SyntaxKind.RecordDeclaration);

        protected override SyntaxKind EnumKind { get; } = SyntaxKind.EnumDeclaration;

        protected override ImmutableArray<SyntaxKind> DelegateKinds { get; } = ImmutableArray.Create(SyntaxKind.DelegateDeclaration);

        protected override void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            var type = (TypeDeclarationSyntax)context.Node;
            ReportIfPublic(context, type.Modifiers, type.Identifier);
        }

        protected override void AnalyzeEnumDeclaration(SyntaxNodeAnalysisContext context)
        {
            var @enum = (EnumDeclarationSyntax)context.Node;
            ReportIfPublic(context, @enum.Modifiers, @enum.Identifier);
        }

        protected override void AnalyzeDelegateDeclaration(SyntaxNodeAnalysisContext context)
        {
            var @delegate = (DelegateDeclarationSyntax)context.Node;
            ReportIfPublic(context, @delegate.Modifiers, @delegate.Identifier);
        }

        private static void ReportIfPublic(SyntaxNodeAnalysisContext context, SyntaxTokenList modifiers, SyntaxToken identifier)
        {
            if (modifiers.Any(SyntaxKind.PublicKeyword))
            {
                context.ReportDiagnostic(identifier.CreateDiagnostic(Rule));
            }
        }
    }
}