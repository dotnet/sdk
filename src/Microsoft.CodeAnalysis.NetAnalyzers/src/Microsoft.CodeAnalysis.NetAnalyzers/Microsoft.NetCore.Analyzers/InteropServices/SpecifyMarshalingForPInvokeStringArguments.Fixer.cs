// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public abstract class SpecifyMarshalingForPInvokeStringArgumentsFixer : CodeFixProvider
    {
        protected const string CharSetText = "CharSet";
        protected const string LPWStrText = "LPWStr";
        protected const string UnicodeText = "Unicode";

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PInvokeDiagnosticAnalyzer.RuleCA2101Id);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);
            if (node == null)
            {
                return;
            }

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            INamedTypeSymbol? charSetType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesCharSet);
            INamedTypeSymbol? dllImportType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesDllImportAttribute);
            INamedTypeSymbol? marshalAsType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesMarshalAsAttribute);
            INamedTypeSymbol? unmanagedType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesUnmanagedType);
            if (charSetType == null || dllImportType == null || marshalAsType == null || unmanagedType == null)
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.SpecifyMarshalingForPInvokeStringArgumentsTitle;

            if (IsAttribute(node))
            {
                context.RegisterCodeFix(new MyCodeAction(title,
                                                         async ct => await FixAttributeArguments(context.Document, node, charSetType, dllImportType, marshalAsType, unmanagedType, ct).ConfigureAwait(false),
                                                         equivalenceKey: title),
                                        context.Diagnostics);
            }
            else if (IsDeclareStatement(node))
            {
                context.RegisterCodeFix(new MyCodeAction(title,
                                                         async ct => await FixDeclareStatement(context.Document, node, ct).ConfigureAwait(false),
                                                         equivalenceKey: title),
                                        context.Diagnostics);
            }
        }

        protected abstract bool IsAttribute(SyntaxNode node);
        protected abstract bool IsDeclareStatement(SyntaxNode node);
        protected abstract Task<Document> FixDeclareStatement(Document document, SyntaxNode node, CancellationToken cancellationToken);
        protected abstract SyntaxNode FindNamedArgument(IReadOnlyList<SyntaxNode> arguments, string argumentName);

        private async Task<Document> FixAttributeArguments(Document document, SyntaxNode attributeDeclaration,
            INamedTypeSymbol charSetType, INamedTypeSymbol dllImportType, INamedTypeSymbol marshalAsType, INamedTypeSymbol unmanagedType, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;
            SemanticModel model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // could be either a [DllImport] or [MarshalAs] attribute
            ISymbol attributeType = model.GetSymbolInfo(attributeDeclaration, cancellationToken).Symbol;
            IReadOnlyList<SyntaxNode> arguments = generator.GetAttributeArguments(attributeDeclaration);

            if (dllImportType.Equals(attributeType.ContainingType))
            {
                // [DllImport] attribute, add or replace CharSet named parameter
                SyntaxNode argumentValue = generator.MemberAccessExpression(
                                        generator.TypeExpression(charSetType),
                                        generator.IdentifierName(UnicodeText));
                SyntaxNode newCharSetArgument = generator.AttributeArgument(CharSetText, argumentValue);

                SyntaxNode charSetArgument = FindNamedArgument(arguments, CharSetText);
                if (charSetArgument == null)
                {
                    // add the parameter
                    editor.AddAttributeArgument(attributeDeclaration, newCharSetArgument);
                }
                else
                {
                    // replace the parameter
                    editor.ReplaceNode(charSetArgument, newCharSetArgument);
                }
            }
            else if (marshalAsType.Equals(attributeType.ContainingType) && arguments.Count == 1)
            {
                // [MarshalAs] attribute, replace the only argument
                SyntaxNode newArgument = generator.AttributeArgument(
                                        generator.MemberAccessExpression(
                                            generator.TypeExpression(unmanagedType),
                                            generator.IdentifierName(LPWStrText)));

                editor.ReplaceNode(arguments[0], newArgument);
            }

            return editor.GetChangedDocument();
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
