// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.Documentation;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Documentation
{
    /// <summary>
    /// CA1200: Avoid using cref tags with a prefix
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpAvoidUsingCrefTagsWithAPrefixAnalyzer : AvoidUsingCrefTagsWithAPrefixAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeXmlAttribute, SyntaxKind.XmlTextAttribute);
        }

        private static void AnalyzeXmlAttribute(SyntaxNodeAnalysisContext context)
        {
            var textAttribute = (XmlTextAttributeSyntax)context.Node;

            if (textAttribute.Name.LocalName.Text == "cref")
            {
                ProcessAttribute(context, textAttribute.TextTokens);
            }
        }
    }
}