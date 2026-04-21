// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public sealed partial class CSharpDisableRuntimeMarshallingFixer
    {
        private class CustomFixAllProvider : DocumentBasedFixAllProvider
        {
            public static readonly CustomFixAllProvider Instance = new();

            protected override string GetFixAllTitle(FixAllContext fixAllContext)
                => MicrosoftNetCoreAnalyzersResources.UseDisabledMarshallingEquivalentCodeFix;

            protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
            {
                if (document.Project.CompilationOptions is CSharpCompilationOptions { AllowUnsafe: false })
                {
                    // We can't code fix if unsafe code isn't allowed.
                    return document;
                }

                var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
                SyntaxNode root = await document.GetRequiredSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);

                Dictionary<IBlockOperation, IdentifierGenerator> scopeMap = new();
                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Properties[DisableRuntimeMarshallingAnalyzer.CanConvertToDisabledMarshallingEquivalentKey] is not null)
                    {
                        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
                        IBlockOperation? block = editor.SemanticModel.GetOperation(node, fixAllContext.CancellationToken).GetFirstParentBlock();
                        IdentifierGenerator identifierGenerator;
                        if (block is null)
                        {
                            identifierGenerator = new IdentifierGenerator(editor.SemanticModel, node.SpanStart);
                        }
                        else if (!scopeMap.TryGetValue(block, out identifierGenerator))
                        {
                            identifierGenerator = scopeMap[block] = new IdentifierGenerator(editor.SemanticModel, block);
                        }

                        if (TryRewriteMethodCall(node, editor, identifierGenerator, addRenameAnnotation: false, fixAllContext.CancellationToken))
                        {
                            AddUnsafeModifierToEnclosingMethod(editor, node);
                        }
                    }
                }

                return editor.GetChangedDocument();
            }
        }
    }
}
