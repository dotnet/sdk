// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotDirectlyAwaitATask : DoNotDirectlyAwaitATaskAnalyzer
    {
        protected override void RegisterLanguageSpecificChecks(OperationBlockStartAnalysisContext context, INamedTypeSymbol configuredAsyncEnumerable)
        {
            context.RegisterOperationAction(ctx => AnalyzeAwaitForEachLoopOperation(ctx, configuredAsyncEnumerable), OperationKind.Loop);
        }

        private static void AnalyzeAwaitForEachLoopOperation(OperationAnalysisContext context, INamedTypeSymbol configuredAsyncEnumerable)
        {
            if (context.Operation is IForEachLoopOperation {Syntax: ForEachStatementSyntax {AwaitKeyword.RawKind: not (int)SyntaxKind.None}} forEachOperation
                && !forEachOperation.Collection.Type.OriginalDefinition.Equals(configuredAsyncEnumerable, SymbolEqualityComparer.Default))
            {
                context.ReportDiagnostic(forEachOperation.Collection.CreateDiagnostic(Rule));
            }
        }
    }
}