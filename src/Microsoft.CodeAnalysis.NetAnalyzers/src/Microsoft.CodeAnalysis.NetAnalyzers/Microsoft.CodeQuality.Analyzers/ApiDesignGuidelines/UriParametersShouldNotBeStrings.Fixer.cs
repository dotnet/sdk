// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1054: Uri parameters should not be strings
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public class UriParametersShouldNotBeStringsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UriParametersShouldNotBeStringsAnalyzer.RuleId);

        // Two diagnostics can call for the same overload - Method(string, string) needs Method(Uri, Uri)
        // whether it is reached from the first parameter or the second - so the signatures already added
        // have to be carried across the fixes applied to one document.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<HashSet<string>>(
                static _ => new HashSet<string>(StringComparer.Ordinal),
                AddOverloadAsync);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var title = MicrosoftCodeQualityAnalyzersResources.UriParametersShouldNotBeStringsCodeFixTitle;

            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken =>
                    {
                        HashSet<string> addedOverloads = new(StringComparer.Ordinal);
                        return SyntaxEditorFixAllProvider.ApplyFixesAsync(
                            document,
                            diagnostics,
                            (doc, diagnostic, editor, token) => AddOverloadAsync(doc, diagnostic, editor, addedOverloads, token),
                            cancellationToken);
                    },
                    title),
                diagnostics);

            return Task.CompletedTask;
        }

        private static async Task AddOverloadAsync(
            Document document,
            Diagnostic diagnostic,
            SyntaxEditor editor,
            HashSet<string> addedOverloads,
            CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            INamedTypeSymbol? uriType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemUri);
            if (uriType == null)
            {
                return;
            }

            var generator = editor.Generator;

            TextSpan span = diagnostic.Location.SourceSpan;
            var parameter = editor.OriginalRoot.FindNode(span, getInnermostNodeForTie: true);
            if (parameter == null)
            {
                // this diagnostic is not something we can deal with
                return;
            }

            var methodNode = generator.GetDeclaration(parameter, DeclarationKind.Method);
            if (methodNode == null)
            {
                // this diagnostic is not something we can deal with
                return;
            }

            var targetNode = generator.GetDeclaration(parameter, DeclarationKind.Class) ?? generator.GetDeclaration(parameter, DeclarationKind.Struct);
            if (targetNode == null)
            {
                // this diagnostic is not something we can deal with
                return;
            }

            if (model.GetDeclaredSymbol(methodNode, cancellationToken) is not IMethodSymbol methodSymbol)
            {
                return;
            }

            var parameterIndex = GetParameterIndex(methodSymbol, model.SyntaxTree, span);
            if (parameterIndex < 0)
            {
                // this is not something we can handle
                return;
            }

            if (!addedOverloads.Add(GetOverloadKey(methodSymbol, parameterIndex, uriType)))
            {
                return;
            }

            var newMethod = CreateNewMethod(generator, methodSymbol, parameterIndex, model.Compilation, uriType);
            editor.AddMember(targetNode, newMethod);
        }

        private static string GetOverloadKey(IMethodSymbol methodSymbol, int parameterIndex, INamedTypeSymbol uriType)
        {
            var builder = new StringBuilder();
            builder.Append(methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('.').Append(methodSymbol.Name).Append('(');

            for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append((i == parameterIndex ? uriType : methodSymbol.Parameters[i].Type).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            return builder.Append(')').ToString();
        }

        private static SyntaxNode CreateNewMethod(
            SyntaxGenerator generator, IMethodSymbol methodSymbol, int parameterIndex, Compilation compilation, INamedTypeSymbol uriType)
        {
            // create original parameter decl
            var originalParameter = generator.ParameterDeclaration(methodSymbol.Parameters[parameterIndex]);

            // replace original parameter type to System.Uri
            var newParameter = generator.ReplaceNode(originalParameter, generator.GetType(originalParameter), generator.TypeExpression(uriType));

            // create original method decl
            var original = generator.MethodDeclaration(methodSymbol, generator.DefaultMethodBody(compilation));

            // get parameters from original method decl
            var originalParameters = generator.GetParameters(original);

            // replace one of parameter to new one
            return generator.ReplaceNode(original, originalParameters[parameterIndex], newParameter);
        }

        private static int GetParameterIndex(IMethodSymbol methodSymbol, SyntaxTree tree, TextSpan span)
        {
            for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                var parameter = methodSymbol.Parameters[i];
                if (parameter.Locations.Any(l => l.IsInSource && l.SourceTree == tree && l.SourceSpan.IntersectsWith(span)))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}