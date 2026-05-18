// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    public abstract class SpecifyCultureForToLowerAndToUpperAnalyzer : AbstractGlobalizationDiagnosticAnalyzer
    {
        internal const string RuleId = "CA1311";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(SpecifyCultureForToLowerAndToUpperTitle)),
            CreateLocalizableResourceString(nameof(SpecifyCultureForToLowerAndToUpperMessage)),
            DiagnosticCategory.Globalization,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(SpecifyCultureForToLowerAndToUpperDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal const string ToLowerMethodName = "ToLower";
        internal const string ToUpperMethodName = "ToUpper";
        protected abstract Location GetMethodNameLocation(SyntaxNode node);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        protected override void InitializeWorker(CompilationStartAnalysisContext context)
        {
            var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
            var toLower = stringType.GetMembers(ToLowerMethodName).FirstOrDefault(m => !m.IsStatic && m is IMethodSymbol method && method.Parameters.IsEmpty);
            var toUpper = stringType.GetMembers(ToUpperMethodName).FirstOrDefault(m => !m.IsStatic && m is IMethodSymbol method && method.Parameters.IsEmpty);
            if (toLower is null && toUpper is null)
            {
                return;
            }

            context.RegisterOperationAction(operationContext =>
            {
                var operation = (IInvocationOperation)operationContext.Operation;
                IMethodSymbol methodSymbol = operation.TargetMethod;

                if (methodSymbol.Equals(toLower, SymbolEqualityComparer.Default) || methodSymbol.Equals(toUpper, SymbolEqualityComparer.Default))
                {
                    operationContext.ReportDiagnostic(Diagnostic.Create(Rule, GetMethodNameLocation(operation.Syntax)));
                }
            }, OperationKind.Invocation);
        }
    }
}
