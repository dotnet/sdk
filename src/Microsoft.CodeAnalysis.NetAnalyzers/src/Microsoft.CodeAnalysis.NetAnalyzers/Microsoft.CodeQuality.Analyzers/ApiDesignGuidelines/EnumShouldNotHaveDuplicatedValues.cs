﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1069: <inheritdoc cref="EnumShouldNotHaveDuplicatedValuesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class EnumShouldNotHaveDuplicatedValues : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1069";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(EnumShouldNotHaveDuplicatedValuesTitle));

        internal static readonly DiagnosticDescriptor RuleDuplicatedValue = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(EnumShouldNotHaveDuplicatedValuesMessageDuplicatedValue)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleDuplicatedBitwiseValuePart = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(EnumShouldNotHaveDuplicatedValuesMessageDuplicatedBitwiseValuePart)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(RuleDuplicatedValue, RuleDuplicatedBitwiseValuePart);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolStartAction(visitEnumSymbol, SymbolKind.NamedType);

            void visitEnumSymbol(SymbolStartAnalysisContext context)
            {
                var enumSymbol = (INamedTypeSymbol)context.Symbol;
                if (enumSymbol.TypeKind != TypeKind.Enum)
                {
                    return;
                }

                // This dictionary is populated by this thread and then read concurrently.
                // https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2?view=net-5.0#thread-safety
                var membersByValue = PooledDictionary<object, IFieldSymbol>.GetInstance();
                var duplicates = PooledConcurrentSet<IFieldSymbol>.GetInstance(SymbolEqualityComparer.Default);
                foreach (var member in enumSymbol.GetMembers())
                {
                    if (member is not IFieldSymbol { IsImplicitlyDeclared: false, HasConstantValue: true } field ||
                        field.ConstantValue is null)
                    {
                        continue;
                    }

                    var constantValue = field.ConstantValue;
                    if (membersByValue.ContainsKey(constantValue))
                    {
                        // This field is a duplicate. We need to first check
                        // if its initializer is another field on this enum,
                        // and if not give a diagnostic on it.
                        var added = duplicates.Add(field);
                        Debug.Assert(added);
                    }
                    else
                    {
                        membersByValue[constantValue] = field;
                    }
                }

                context.RegisterOperationAction(visitFieldInitializer, OperationKind.FieldInitializer);
                context.RegisterSymbolEndAction(endVisitEnumSymbol);

                void visitFieldInitializer(OperationAnalysisContext context)
                {
                    var initializer = (IFieldInitializerOperation)context.Operation;
                    if (initializer.InitializedFields.Length != 1)
                    {
                        return;
                    }

                    var field = initializer.InitializedFields[0];
                    if (duplicates.Remove(field))
                    {
                        var duplicatedField = membersByValue[field.ConstantValue!];
                        if (initializer.Value is not IConversionOperation { Operand: IFieldReferenceOperation { Field: IFieldSymbol referencedField } }
                            || !SymbolEqualityComparer.Default.Equals(referencedField, duplicatedField))
                        {
                            context.ReportDiagnostic(field.CreateDiagnostic(RuleDuplicatedValue, field.Name, field.ConstantValue!, duplicatedField.Name));
                        }
                    }

                    // Check for duplicate usages of an enum field in an initializer consisting of '|' expressions
                    var referencedSymbols = PooledHashSet<IFieldSymbol>.GetInstance(SymbolEqualityComparer.Default);
                    var containingType = field.ContainingType;
                    visitInitializerValue(initializer.Value);
                    referencedSymbols.Free(context.CancellationToken);

                    void visitInitializerValue(IOperation operation)
                    {
                        switch (operation)
                        {
                            case IBinaryOperation { OperatorKind: not BinaryOperatorKind.Or }:
                                // only descend into '|' binary operators, not into '&', '+', ...
                                break;
                            case IFieldReferenceOperation { Field: var referencedField } fieldOperation:
                                if (SymbolEqualityComparer.Default.Equals(referencedField.ContainingType, containingType)
                                    && !referencedSymbols.Add(referencedField))
                                {
                                    context.ReportDiagnostic(fieldOperation.CreateDiagnostic(RuleDuplicatedBitwiseValuePart, referencedField.Name));
                                }

                                break;
                            default:
                                foreach (var childOperation in operation.Children)
                                {
                                    visitInitializerValue(childOperation);
                                }

                                break;
                        }
                    }
                }

                void endVisitEnumSymbol(SymbolAnalysisContext context)
                {
                    // visit any duplicates which didn't have an initializer
                    foreach (var field in duplicates)
                    {
                        var duplicatedField = membersByValue[field.ConstantValue!];
                        context.ReportDiagnostic(field.CreateDiagnostic(RuleDuplicatedValue, field.Name, field.ConstantValue!, duplicatedField.Name));
                    }

                    duplicates.Free(context.CancellationToken);
                    membersByValue.Free(context.CancellationToken);
                }
            }
        }
    }
}
