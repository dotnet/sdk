// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpConstantExpectedAnalyzer : ConstantExpectedAnalyzer
    {
        private static readonly CSharpDiagnosticHelper s_diagnosticHelper = new();
        private static readonly IdentifierNameSyntax s_constantExpectedIdentifier = (IdentifierNameSyntax)SyntaxFactory.ParseName(ConstantExpected);
        private static readonly IdentifierNameSyntax s_constantExpectedAttributeIdentifier = (IdentifierNameSyntax)SyntaxFactory.ParseName(ConstantExpectedAttribute);

        protected override DiagnosticHelper Helper => s_diagnosticHelper;

        protected override void RegisterAttributeSyntax(CompilationStartAnalysisContext context, ConstantExpectedContext constantExpectedContext)
        {
            context.RegisterSyntaxNodeAction(context => OnAttributeNode(context, constantExpectedContext), SyntaxKind.Attribute);
        }

        private void OnAttributeNode(SyntaxNodeAnalysisContext context, ConstantExpectedContext constantExpectedContext)
        {
            var attributeSyntax = (AttributeSyntax)context.Node;
            var attributeName = attributeSyntax.Name;
            if (!attributeName.IsEquivalentTo(s_constantExpectedIdentifier) && !attributeName.IsEquivalentTo(s_constantExpectedAttributeIdentifier))
            {
                return;
            }

            if (attributeSyntax.Parent?.Parent is ParameterSyntax parameter)
            {
                var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter)!;
                OnParameterWithConstantExpectedAttribute(parameterSymbol, constantExpectedContext, context.ReportDiagnostic);
            }
        }

        private sealed class CSharpDiagnosticHelper : DiagnosticHelper
        {
            private readonly IdentifierNameSyntax _constantExpectedMinIdentifier = (IdentifierNameSyntax)SyntaxFactory.ParseName(ConstantExpectedMin);
            private readonly IdentifierNameSyntax _constantExpectedMaxIdentifier = (IdentifierNameSyntax)SyntaxFactory.ParseName(ConstantExpectedMax);

            public override Location? GetMaxLocation(SyntaxNode attributeSyntax) => GetArgumentLocation(attributeSyntax, _constantExpectedMaxIdentifier);

            public override Location? GetMinLocation(SyntaxNode attributeSyntax) => GetArgumentLocation(attributeSyntax, _constantExpectedMinIdentifier);

            private static Location? GetArgumentLocation(SyntaxNode attributeNode, IdentifierNameSyntax targetNameSyntax)
            {
                var attributeSyntax = (AttributeSyntax)attributeNode;
                if (attributeSyntax.ArgumentList is null)
                {
                    return null;
                }

                var targetArg = attributeSyntax.ArgumentList.Arguments.FirstOrDefault(arg => arg.NameEquals?.Name.IsEquivalentTo(targetNameSyntax, true) == true);
                return targetArg?.GetLocation();
            }
        }
    }
}
;