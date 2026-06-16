// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1834: <inheritdoc cref="PreferConstCharOverConstUnitStringInStringBuilderTitle"/>
    /// Test for single character strings passed in to StringBuilder.Append
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferConstCharOverConstUnitStringAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1834";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferConstCharOverConstUnitStringInStringBuilderTitle)),
            CreateLocalizableResourceString(nameof(PreferConstCharOverConstUnitStringInStringBuilderMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferConstCharOverConstUnitStringInStringBuilderDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Check that StringBuilder is defined in this compilation
                if (!compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextStringBuilder, out INamedTypeSymbol? stringBuilderType))
                {
                    return;
                }

                IMethodSymbol appendStringMethod = stringBuilderType
                    .GetMembers("Append")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(s =>
                        s.Parameters.Length == 1 &&
                        s.Parameters[0].Type.SpecialType == SpecialType.System_String);
                if (appendStringMethod is null)
                {
                    return;
                }

                compilationContext.RegisterOperationAction(context =>
                {
                    var invocationOperation = (IInvocationOperation)context.Operation;
                    if (invocationOperation.Arguments.IsEmpty)
                    {
                        return;
                    }

                    if (!invocationOperation.TargetMethod.Equals(appendStringMethod))
                    {
                        return;
                    }

                    ImmutableArray<IArgumentOperation> arguments = invocationOperation.Arguments;
                    IArgumentOperation firstArgument = arguments[0];

                    if (firstArgument.Value.ConstantValue.HasValue && firstArgument.Value.ConstantValue.Value is string unitString && unitString.Length == 1)
                    {
                        context.ReportDiagnostic(firstArgument.Value.CreateDiagnostic(Rule));
                    }
                },
                OperationKind.Invocation);
            });
        }
    }
}
