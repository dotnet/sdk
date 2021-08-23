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
    /// <summary>
    /// Test for single character strings passed in to StringBuilder.Append
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferConstCharOverConstUnitStringAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1834";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferConstCharOverConstUnitStringInStringBuilderTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferConstCharOverConstUnitStringInStringBuilderMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PreferConstCharOverConstUnitStringInStringBuilderDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Performance,
                                                                                      RuleLevel.IdeSuggestion,
                                                                                      s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
