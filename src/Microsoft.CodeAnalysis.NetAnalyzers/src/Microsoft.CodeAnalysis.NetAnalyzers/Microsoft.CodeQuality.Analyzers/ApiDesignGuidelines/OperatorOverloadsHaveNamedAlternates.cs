// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2225: Operator overloads have named alternates
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class OperatorOverloadsHaveNamedAlternatesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2225";
        internal const string DiagnosticKindText = "DiagnosticKind";
        internal const string AddAlternateText = "AddAlternate";
        internal const string FixVisibilityText = "FixVisibility";
        internal const string IsTrueText = "IsTrue";
        private const string OpTrueText = "op_True";
        private const string OpFalseText = "op_False";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageProperty = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageProperty), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageMultiple = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageMultiple), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageVisibility = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesMessageVisibility), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OperatorOverloadsHaveNamedAlternatesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor PropertyRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageProperty,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor MultipleRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageMultiple,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor VisibilityRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageVisibility,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, PropertyRule, MultipleRule, VisibilityRule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext symbolContext)
        {
            var methodSymbol = (IMethodSymbol)symbolContext.Symbol;

            // FxCop compat: only analyze externally visible symbols by default.
            // Note all the descriptors/rules for this analyzer have the same ID and category and hence
            // will always have identical configured visibility.
            if (!symbolContext.Options.MatchesConfiguredVisibility(DefaultRule, methodSymbol, symbolContext.Compilation, symbolContext.CancellationToken))
            {
                return;
            }

            if (methodSymbol.ContainingSymbol is ITypeSymbol typeSymbol && (methodSymbol.MethodKind == MethodKind.UserDefinedOperator || methodSymbol.MethodKind == MethodKind.Conversion))
            {
                string operatorName = methodSymbol.Name;
                if (IsPropertyExpected(operatorName) && operatorName != OpFalseText)
                {
                    // don't report a diagnostic on the `op_False` method because then the user would see two diagnostics for what is really one error
                    // special-case looking for `IsTrue` instance property
                    // named properties can't be overloaded so there will only ever be 0 or 1
                    IPropertySymbol property = typeSymbol.GetMembers(IsTrueText).OfType<IPropertySymbol>().FirstOrDefault();
                    if (property == null || property.Type.SpecialType != SpecialType.System_Boolean)
                    {
                        symbolContext.ReportDiagnostic(CreateDiagnostic(PropertyRule, GetSymbolLocation(methodSymbol), AddAlternateText, IsTrueText, operatorName));
                    }
                    else if (!property.IsPublic())
                    {
                        symbolContext.ReportDiagnostic(CreateDiagnostic(VisibilityRule, GetSymbolLocation(property), FixVisibilityText, IsTrueText, operatorName));
                    }
                }
                else
                {
                    ExpectedAlternateMethodGroup? expectedGroup = GetExpectedAlternateMethodGroup(operatorName, methodSymbol.ReturnType, methodSymbol.Parameters.FirstOrDefault()?.Type);
                    if (expectedGroup == null)
                    {
                        // no alternate methods required
                        return;
                    }

                    var matchedMethods = new List<IMethodSymbol>();
                    var unmatchedMethods = new HashSet<string>() { expectedGroup.AlternateMethod1 };
                    if (expectedGroup.AlternateMethod2 != null)
                    {
                        unmatchedMethods.Add(expectedGroup.AlternateMethod2);
                    }

                    foreach (IMethodSymbol candidateMethod in typeSymbol.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (candidateMethod.Name == expectedGroup.AlternateMethod1 || candidateMethod.Name == expectedGroup.AlternateMethod2)
                        {
                            // found an appropriately-named method
                            matchedMethods.Add(candidateMethod);
                            unmatchedMethods.Remove(candidateMethod.Name);
                        }
                    }

                    // only one public method match is required
                    if (matchedMethods.Any(m => m.IsPublic()))
                    {
                        // at least one public alternate method was found, do nothing
                    }
                    else
                    {
                        // either we found at least one method that should be public or we didn't find anything
                        IMethodSymbol notPublicMethod = matchedMethods.FirstOrDefault(m => !m.IsPublic());
                        if (notPublicMethod != null)
                        {
                            // report error for improper visibility directly on the method itself
                            symbolContext.ReportDiagnostic(CreateDiagnostic(VisibilityRule, GetSymbolLocation(notPublicMethod), FixVisibilityText, notPublicMethod.Name, operatorName));
                        }
                        else
                        {
                            // report error for missing methods on the operator overload
                            if (expectedGroup.AlternateMethod2 == null)
                            {
                                // only one alternate expected
                                symbolContext.ReportDiagnostic(CreateDiagnostic(DefaultRule, GetSymbolLocation(methodSymbol), AddAlternateText, expectedGroup.AlternateMethod1, operatorName));
                            }
                            else
                            {
                                // one of two alternates expected
                                symbolContext.ReportDiagnostic(CreateDiagnostic(MultipleRule, GetSymbolLocation(methodSymbol), AddAlternateText, expectedGroup.AlternateMethod1, expectedGroup.AlternateMethod2, operatorName));
                            }
                        }
                    }
                }
            }
        }

        private static Location GetSymbolLocation(ISymbol symbol)
        {
            return symbol.OriginalDefinition.Locations.First();
        }

        private static Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, Location location, string kind, params string[] messageArgs)
        {
            return Diagnostic.Create(descriptor, location, ImmutableDictionary.Create<string, string>().Add(DiagnosticKindText, kind), messageArgs);
        }

        internal static bool IsPropertyExpected(string operatorName)
        {
            switch (operatorName)
            {
                case OpTrueText:
                case OpFalseText:
                    return true;
                default:
                    return false;
            }
        }

        internal static ExpectedAlternateMethodGroup? GetExpectedAlternateMethodGroup(string operatorName, ITypeSymbol returnType, ITypeSymbol? parameterType)
        {
            // list of operator alternate names: https://docs.microsoft.com/visualstudio/code-quality/ca2225

            // the most common case; create a static method with the already specified types
            static ExpectedAlternateMethodGroup createSingle(string methodName) => new ExpectedAlternateMethodGroup(methodName);
            switch (operatorName)
            {
                case "op_Addition":
                case "op_AdditonAssignment":
                    return createSingle("Add");
                case "op_BitwiseAnd":
                case "op_BitwiseAndAssignment":
                    return createSingle("BitwiseAnd");
                case "op_BitwiseOr":
                case "op_BitwiseOrAssignment":
                    return createSingle("BitwiseOr");
                case "op_Decrement":
                    return createSingle("Decrement");
                case "op_Division":
                case "op_DivisionAssignment":
                    return createSingle("Divide");
                case "op_Equality":
                case "op_Inequality":
                    return createSingle("Equals");
                case "op_ExclusiveOr":
                case "op_ExclusiveOrAssignment":
                    return createSingle("Xor");
                case "op_GreaterThan":
                case "op_GreaterThanOrEqual":
                case "op_LessThan":
                case "op_LessThanOrEqual":
                    return new ExpectedAlternateMethodGroup(alternateMethod1: "CompareTo", alternateMethod2: "Compare");
                case "op_Increment":
                    return createSingle("Increment");
                case "op_LeftShift":
                case "op_LeftShiftAssignment":
                    return createSingle("LeftShift");
                case "op_LogicalAnd":
                    return createSingle("LogicalAnd");
                case "op_LogicalOr":
                    return createSingle("LogicalOr");
                case "op_LogicalNot":
                    return createSingle("LogicalNot");
                case "op_Modulus":
                case "op_ModulusAssignment":
                    return new ExpectedAlternateMethodGroup(alternateMethod1: "Mod", alternateMethod2: "Remainder");
                case "op_MultiplicationAssignment":
                case "op_Multiply":
                    return createSingle("Multiply");
                case "op_OnesComplement":
                    return createSingle("OnesComplement");
                case "op_RightShift":
                case "op_RightShiftAssignment":
                case "op_SignedRightShift":
                case "op_UnsignedRightShift":
                case "op_UnsignedRightShiftAssignment":
                    return createSingle("RightShift");
                case "op_Subtraction":
                case "op_SubtractionAssignment":
                    return createSingle("Subtract");
                case "op_UnaryNegation":
                    return createSingle("Negate");
                case "op_UnaryPlus":
                    return createSingle("Plus");
                case "op_Implicit":
                case "op_Explicit":
                    return new ExpectedAlternateMethodGroup(alternateMethod1: $"To{GetTypeName(returnType)}", alternateMethod2: parameterType != null ? $"From{GetTypeName(parameterType)}" : null);
                default:
                    return null;
            }

            static string GetTypeName(ITypeSymbol typeSymbol)
            {
                if (typeSymbol.TypeKind != TypeKind.Array)
                {
                    return typeSymbol.Name;
                }

                var elementType = typeSymbol;
                do
                {
                    elementType = ((IArrayTypeSymbol)elementType).ElementType;
                }
                while (elementType.TypeKind == TypeKind.Array);

                return elementType.Name + "Array";
            }
        }

        internal class ExpectedAlternateMethodGroup
        {
            public string AlternateMethod1 { get; }
            public string? AlternateMethod2 { get; }

            public ExpectedAlternateMethodGroup(string alternateMethod1, string? alternateMethod2 = null)
            {
                AlternateMethod1 = alternateMethod1;
                AlternateMethod2 = alternateMethod2;
            }
        }
    }
}