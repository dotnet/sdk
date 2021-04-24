// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.Helpers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// Implements CA1027 and CA2217
    ///
    /// 1) CA1027: Mark enums with FlagsAttribute
    ///
    /// Cause:
    /// The values of a public enumeration are powers of two or are combinations of other values that are defined in the enumeration,
    /// and the System.FlagsAttribute attribute is not present.
    /// To reduce false positives, this rule does not report a violation for enumerations that have contiguous values.
    ///
    /// 2) CA2217: Do not mark enums with FlagsAttribute
    ///
    /// Cause:
    /// An externally visible enumeration is marked with FlagsAttribute and it has one or more values that are not powers of two or
    /// a combination of the other defined values on the enumeration.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class EnumWithFlagsAttributeAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleIdMarkEnumsWithFlags = "CA1027";
        internal const string RuleIdDoNotMarkEnumsWithFlags = "CA2217";
        internal const string RuleNameForExportAttribute = "EnumWithFlagsAttributeRules";

        private static readonly LocalizableString s_localizableTitleCA1027 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkEnumsWithFlagsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1027 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkEnumsWithFlagsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1027 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkEnumsWithFlagsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        internal static DiagnosticDescriptor Rule1027 = DiagnosticDescriptorHelper.Create(RuleIdMarkEnumsWithFlags,
                                                                             s_localizableTitleCA1027,
                                                                             s_localizableMessageCA1027,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescriptionCA1027,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        private static readonly LocalizableString s_localizableTitleCA2217 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotMarkEnumsWithFlagsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA2217 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotMarkEnumsWithFlagsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA2217 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotMarkEnumsWithFlagsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        internal static DiagnosticDescriptor Rule2217 = DiagnosticDescriptorHelper.Create(RuleIdDoNotMarkEnumsWithFlags,
                                                                             s_localizableTitleCA2217,
                                                                             s_localizableMessageCA2217,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescriptionCA2217,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule1027, Rule2217);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var flagsAttributeType = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFlagsAttribute);
                if (flagsAttributeType == null)
                {
                    return;
                }

                compilationStartContext.RegisterSymbolAction(symbolContext =>
                {
                    AnalyzeSymbol(symbolContext, flagsAttributeType);
                }, SymbolKind.NamedType);
            });

        }

        private static void AnalyzeSymbol(SymbolAnalysisContext symbolContext, INamedTypeSymbol flagsAttributeType)
        {
            var symbol = (INamedTypeSymbol)symbolContext.Symbol;
            if (symbol != null &&
                symbol.TypeKind == TypeKind.Enum)
            {
                var reportCA1027 = symbolContext.Options.MatchesConfiguredVisibility(Rule1027, symbol, symbolContext.Compilation);
                var reportCA2217 = symbolContext.Options.MatchesConfiguredVisibility(Rule2217, symbol, symbolContext.Compilation);
                if (!reportCA1027 && !reportCA2217)
                {
                    return;
                }

                if (EnumHelpers.TryGetEnumMemberValues(symbol, out IList<ulong> memberValues))
                {
                    if (symbol.HasAttribute(flagsAttributeType))
                    {
                        // Check "CA2217: Do not mark enums with FlagsAttribute"
                        if (reportCA2217 && !ShouldBeFlags(memberValues, out IEnumerable<ulong> missingValues))
                        {
                            Debug.Assert(missingValues != null);

                            string missingValuesString = missingValues.Select(v => v.ToString(CultureInfo.InvariantCulture)).Aggregate((i, j) => i + ", " + j);
                            symbolContext.ReportDiagnostic(symbol.CreateDiagnostic(Rule2217, symbol.Name, missingValuesString));
                        }
                    }
                    else
                    {
                        // Check "CA1027: Mark enums with FlagsAttribute"
                        // Ignore contiguous value enums to reduce noise.
                        if (reportCA1027 && !IsContiguous(memberValues) && ShouldBeFlags(memberValues))
                        {
                            symbolContext.ReportDiagnostic(symbol.CreateDiagnostic(Rule1027, symbol.Name));
                        }
                    }
                }
            }
        }

        private static bool IsContiguous(IList<ulong> list)
        {
            Debug.Assert(list != null);

            bool first = true;
            ulong previous = 0;
            foreach (ulong element in list.OrderBy(t => t))
            {
                if (first)
                {
                    first = false;
                }
                else if (element != previous + 1)
                {
                    return false;
                }

                previous = element;
            }

            return true;
        }

        // algorithm makes sure that any enum values that are not powers of two
        // are just combinations of the other powers of two
        private static bool ShouldBeFlags(IList<ulong> enumValues, out IEnumerable<ulong> missingValues)
        {
            ulong missingBits = GetMissingBitsInBinaryForm(enumValues);
            missingValues = GetIndividualBits(missingBits);
            return missingBits == 0;
        }

        // algorithm makes sure that any enum values that are not powers of two
        // are just combinations of the other powers of two
        private static bool ShouldBeFlags(IList<ulong> enumValues)
        {
            return GetMissingBitsInBinaryForm(enumValues) == 0;
        }

        private static ulong GetMissingBitsInBinaryForm(IList<ulong> values)
        {
            // all the powers of two that are individually represented
            ulong powersOfTwo = 0;
            bool foundNonPowerOfTwo = false;

            foreach (ulong value in values)
            {
                if (IsPowerOfTwo(value))
                {
                    powersOfTwo |= value;
                }
                else
                {
                    foundNonPowerOfTwo = true;
                }
            }

            if (foundNonPowerOfTwo)
            {
                // since this is not all powers of two, we need to make sure that each bit
                // is represented by an individual enum value
                ulong missingBits = 0;
                foreach (ulong value in values)
                {
                    if ((value & powersOfTwo) != value)
                    {
                        // we found a value where one of the bits is not represented individually in the enum
                        missingBits |= value & ~(value & powersOfTwo);
                    }
                }

                return missingBits;
            }

            return 0;
        }

        private static IEnumerable<ulong> GetIndividualBits(ulong value)
        {
            if (value != 0)
            {
                ulong current = value;
                int shifted = 0;

                while (current != 0)
                {
                    if ((current & 1) == 1)
                    {
                        yield return ((ulong)1) << shifted;
                    }

                    shifted++;
                    current >>= 1;
                }
            }
        }

        private static bool IsPowerOfTwo(ulong number)
        {
            return number == 0 || ((number & (number - 1)) == 0);
        }
    }
}
