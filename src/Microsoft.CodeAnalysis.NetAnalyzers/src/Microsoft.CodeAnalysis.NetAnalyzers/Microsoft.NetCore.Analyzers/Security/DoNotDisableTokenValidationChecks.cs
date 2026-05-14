// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5404: <inheritdoc cref="DoNotDisableTokenValidationChecksTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDisableTokenValidationChecks : DiagnosticAnalyzer
    {
        // Set of properties on Microsoft.IdentityModel.Tokens.TokenValidationParameters which shouldn't be set to false.
        private ImmutableArray<string> PropertiesWhichShouldNotBeFalse = ImmutableArray.Create(
            "RequireExpirationTime",
            "ValidateAudience",
            "ValidateIssuer",
            "ValidateLifetime");

        internal const string DiagnosticId = "CA5404";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                CreateLocalizableResourceString(nameof(DoNotDisableTokenValidationChecksTitle)),
                CreateLocalizableResourceString(nameof(DoNotDisableTokenValidationChecksMessage)),
                DiagnosticCategory.Security,
                RuleLevel.Disabled,
                description: CreateLocalizableResourceString(nameof(DoNotDisableTokenValidationChecksDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;

                var tokenValidationParamsTypeSymbol = compilation.GetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.MicrosoftIdentityModelTokensTokenValidationParameters);

                if (tokenValidationParamsTypeSymbol == null)
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var simpleAssignmentOperation = (ISimpleAssignmentOperation)context.Operation;

                    if (simpleAssignmentOperation.Target is IPropertyReferenceOperation propertyReferenceOperation)
                    {
                        var property = propertyReferenceOperation.Property;

                        if (property.ContainingType.Equals(tokenValidationParamsTypeSymbol) &&
                            simpleAssignmentOperation.Value.ConstantValue.HasValue &&
                            simpleAssignmentOperation.Value.ConstantValue.Value != null &&
                            simpleAssignmentOperation.Value.ConstantValue.Value.Equals(false) &&
                            PropertiesWhichShouldNotBeFalse.Contains(property.Name))
                        {
                            context.ReportDiagnostic(
                                simpleAssignmentOperation.CreateDiagnostic(
                                    Rule, property.Name));
                        }
                    }
                }, OperationKind.SimpleAssignment);
            });
        }
    }
}
