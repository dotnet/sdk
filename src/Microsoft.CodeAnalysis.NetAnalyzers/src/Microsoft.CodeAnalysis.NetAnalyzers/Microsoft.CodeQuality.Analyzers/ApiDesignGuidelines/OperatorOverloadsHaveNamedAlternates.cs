// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext symbolContext)
        {
            var methodSymbol = (IMethodSymbol)symbolContext.Symbol;

            // FxCop compat: only analyze externally visible symbols by default.
            // Note all the descriptors/rules for this analyzer have the same ID and category and hence
            // will always have identical configured visibility.
            if (!symbolContext.Options.MatchesConfiguredVisibility(DefaultRule, methodSymbol, symbolContext.Compilation))
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
            return operatorName switch
            {
                OpTrueText or OpFalseText => true,
                _ => false,
            };
        }

        internal static ExpectedAlternateMethodGroup? GetExpectedAlternateMethodGroup(string operatorName, ITypeSymbol returnType, ITypeSymbol? parameterType)
        {
            // list of operator alternate names: https://docs.microsoft.com/visualstudio/code-quality/ca2225

            // the most common case; create a static method with the already specified types
            static ExpectedAlternateMethodGroup createSingle(string methodName) => new(methodName);
            return operatorName switch
            {
                "op_Addition"
                or "op_AdditonAssignment" => createSingle("Add"),
                "op_BitwiseAnd"
                or "op_BitwiseAndAssignment" => createSingle("BitwiseAnd"),
                "op_BitwiseOr"
                or "op_BitwiseOrAssignment" => createSingle("BitwiseOr"),
                "op_Decrement" => createSingle("Decrement"),
                "op_Division"
                or "op_DivisionAssignment" => createSingle("Divide"),
                "op_Equality"
                or "op_Inequality" => createSingle("Equals"),
                "op_ExclusiveOr"
                or "op_ExclusiveOrAssignment" => createSingle("Xor"),
                "op_GreaterThan"
                or "op_GreaterThanOrEqual" or "op_LessThan" or "op_LessThanOrEqual" => new ExpectedAlternateMethodGroup(alternateMethod1: "CompareTo", alternateMethod2: "Compare"),
                "op_Increment" => createSingle("Increment"),
                "op_LeftShift"
                or "op_LeftShiftAssignment" => createSingle("LeftShift"),
                "op_LogicalAnd" => createSingle("LogicalAnd"),
                "op_LogicalOr" => createSingle("LogicalOr"),
                "op_LogicalNot" => createSingle("LogicalNot"),
                "op_Modulus"
                or "op_ModulusAssignment" => new ExpectedAlternateMethodGroup(alternateMethod1: "Mod", alternateMethod2: "Remainder"),
                "op_MultiplicationAssignment"
                or "op_Multiply" => createSingle("Multiply"),
                "op_OnesComplement" => createSingle("OnesComplement"),
                "op_RightShift"
                or "op_RightShiftAssignment"
                or "op_SignedRightShift"
                or "op_UnsignedRightShift"
                or "op_UnsignedRightShiftAssignment" => createSingle("RightShift"),
                "op_Subtraction"
                or "op_SubtractionAssignment" => createSingle("Subtract"),
                "op_UnaryNegation" => createSingle("Negate"),
                "op_UnaryPlus" => createSingle("Plus"),
                "op_Implicit"
                or "op_Explicit" => new ExpectedAlternateMethodGroup(alternateMethod1: $"To{GetTypeName(returnType)}", alternateMethod2: parameterType != null ? $"From{GetTypeName(parameterType)}" : null),
                _ => null,
            };

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