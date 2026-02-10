// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
