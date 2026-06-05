// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ParameterValidationAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1062: <inheritdoc cref="ValidateArgumentsOfPublicMethodsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ValidateArgumentsOfPublicMethods : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1062";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ValidateArgumentsOfPublicMethodsTitle)),
            CreateLocalizableResourceString(nameof(ValidateArgumentsOfPublicMethodsMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(ValidateArgumentsOfPublicMethodsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                compilationContext.RegisterOperationBlockAction(operationBlockContext =>
                {
                    // Analyze externally visible methods with reference type parameters.
                    if (operationBlockContext.OwningSymbol is not IMethodSymbol containingMethod ||
                        !containingMethod.Parameters.Any(p => p.Type.IsReferenceType) ||
                        !operationBlockContext.Options.MatchesConfiguredVisibility(Rule, containingMethod, operationBlockContext.Compilation) ||
                        operationBlockContext.Options.IsConfiguredToSkipAnalysis(Rule, containingMethod, operationBlockContext.Compilation))
                    {
                        return;
                    }

                    // Bail out for protected members of sealed classes if the entire overridden method chain
                    // is defined in the same assembly.
                    if (containingMethod.IsOverride &&
                        containingMethod.ContainingType.IsSealed)
                    {
                        var overriddenMethod = containingMethod.OverriddenMethod;
                        var hasAssemblyMismatch = false;
                        while (overriddenMethod != null)
                        {
                            if (!Equals(overriddenMethod.ContainingAssembly, containingMethod.ContainingAssembly))
                            {
                                hasAssemblyMismatch = true;
                                break;
                            }

                            overriddenMethod = overriddenMethod.OverriddenMethod;
                        }

                        if (!hasAssemblyMismatch)
                        {
                            return;
                        }
                    }

                    // Bail out early if we have no parameter references in the method body.
                    if (!operationBlockContext.OperationBlocks.HasAnyOperationDescendant(OperationKind.ParameterReference))
                    {
                        return;
                    }

                    // Perform analysis of all direct/indirect parameter usages in the method to get all non-validated usages that can cause a null dereference.
                    ImmutableDictionary<IParameterSymbol, SyntaxNode>? hazardousParameterUsages = null;
                    foreach (var operationBlock in operationBlockContext.OperationBlocks)
                    {
                        if (operationBlock is IBlockOperation topmostBlock)
                        {
                            hazardousParameterUsages = ParameterValidationAnalysis.GetOrComputeHazardousParameterUsages(
                                topmostBlock, operationBlockContext.Compilation, containingMethod,
                                operationBlockContext.Options, Rule);
                            break;
                        }
                    }

                    if (hazardousParameterUsages != null)
                    {
                        foreach (var kvp in hazardousParameterUsages)
                        {
                            IParameterSymbol parameter = kvp.Key;
                            SyntaxNode node = kvp.Value;

                            // Check if user has configured to skip extension method 'this' parameter analysis.
                            if (containingMethod.IsExtensionMethod &&
                                Equals(containingMethod.Parameters[0], parameter))
                            {
                                bool excludeThisParameterOption = operationBlockContext.Options.GetBoolOptionValue(
                                    optionName: EditorConfigOptionNames.ExcludeExtensionMethodThisParameter,
                                    rule: Rule,
                                    containingMethod,
                                    operationBlockContext.Compilation,
                                    defaultValue: false);
                                if (excludeThisParameterOption)
                                {
                                    continue;
                                }
                            }

                            // In externally visible method '{0}', validate parameter '{1}' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                            var arg1 = containingMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                            var arg2 = parameter.Name;
                            var diagnostic = node.CreateDiagnostic(Rule, arg1, arg2);
                            operationBlockContext.ReportDiagnostic(diagnostic);
                        }
                    }
                });

            });
        }
    }
}
