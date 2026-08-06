// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpAvoidDeadConditionalCode : AvoidDeadConditionalCode
    {
        protected override bool IsSwitchArmExpressionWithWhenClause(SyntaxNode node)
            => node is SwitchExpressionArmSyntax switchArm && switchArm.WhenClause != null;
    }
}
