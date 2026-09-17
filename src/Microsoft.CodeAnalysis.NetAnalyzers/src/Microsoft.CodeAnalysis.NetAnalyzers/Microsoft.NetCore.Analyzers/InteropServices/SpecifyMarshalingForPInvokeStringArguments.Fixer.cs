// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public abstract class SpecifyMarshalingForPInvokeStringArgumentsFixer : SyntaxEditorBasedCodeFixProvider
    {
        protected const string CharSetText = "CharSet";
        protected const string LPWStrText = "LPWStr";
        protected const string UnicodeText = "Unicode";

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PInvokeDiagnosticAnalyzer.RuleCA2101Id);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);
            if (node is null || (!IsAttribute(node) && !IsDeclareStatement(node)))
            {
                return;
            }

            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (!TryGetInteropTypes(model.Compilation, out _))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.SpecifyMarshalingForPInvokeStringArgumentsTitle;
            RegisterCodeFix(context, title, title);
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            if (node is null)
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (!TryGetInteropTypes(model.Compilation, out InteropTypes types))
            {
                return;
            }

            if (IsAttribute(node))
            {
                FixAttributeArguments(editor, model, node, types, cancellationToken);
            }
            else if (IsDeclareStatement(node))
            {
                FixDeclareStatement(editor, node);
            }
        }

        protected abstract bool IsAttribute(SyntaxNode node);
        protected abstract bool IsDeclareStatement(SyntaxNode node);
        protected abstract void FixDeclareStatement(SyntaxEditor editor, SyntaxNode node);
        protected abstract SyntaxNode FindNamedArgument(IReadOnlyList<SyntaxNode> arguments, string argumentName);

        private void FixAttributeArguments(SyntaxEditor editor, SemanticModel model, SyntaxNode attributeDeclaration, InteropTypes types, CancellationToken cancellationToken)
        {
            SyntaxGenerator generator = editor.Generator;

            // could be either a [DllImport] or [MarshalAs] attribute
            ISymbol? attributeType = model.GetSymbolInfo(attributeDeclaration, cancellationToken).Symbol;
            IReadOnlyList<SyntaxNode> arguments = generator.GetAttributeArguments(attributeDeclaration);

            if (types.DllImport.Equals(attributeType?.ContainingType))
            {
                // [DllImport] attribute, add or replace CharSet named parameter
                SyntaxNode argumentValue = generator.MemberAccessExpression(
                                        generator.TypeExpression(types.CharSet),
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
            else if (types.MarshalAs.Equals(attributeType?.ContainingType) && arguments.Count == 1)
            {
                // [MarshalAs] attribute, replace the only argument
                SyntaxNode newArgument = generator.AttributeArgument(
                                        generator.MemberAccessExpression(
                                            generator.TypeExpression(types.Unmanaged),
                                            generator.IdentifierName(LPWStrText)));

                editor.ReplaceNode(arguments[0], newArgument);
            }
        }

        private static bool TryGetInteropTypes(Compilation compilation, out InteropTypes types)
        {
            types = default;

            if (compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesCharSet) is not INamedTypeSymbol charSetType ||
                compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesDllImportAttribute) is not INamedTypeSymbol dllImportType ||
                compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesMarshalAsAttribute) is not INamedTypeSymbol marshalAsType ||
                compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesUnmanagedType) is not INamedTypeSymbol unmanagedType)
            {
                return false;
            }

            types = new InteropTypes(charSetType, dllImportType, marshalAsType, unmanagedType);
            return true;
        }

        private readonly struct InteropTypes
        {
            public InteropTypes(INamedTypeSymbol charSet, INamedTypeSymbol dllImport, INamedTypeSymbol marshalAs, INamedTypeSymbol unmanaged)
            {
                CharSet = charSet;
                DllImport = dllImport;
                MarshalAs = marshalAs;
                Unmanaged = unmanaged;
            }

            public INamedTypeSymbol CharSet { get; }
            public INamedTypeSymbol DllImport { get; }
            public INamedTypeSymbol MarshalAs { get; }
            public INamedTypeSymbol Unmanaged { get; }
        }
    }
}
