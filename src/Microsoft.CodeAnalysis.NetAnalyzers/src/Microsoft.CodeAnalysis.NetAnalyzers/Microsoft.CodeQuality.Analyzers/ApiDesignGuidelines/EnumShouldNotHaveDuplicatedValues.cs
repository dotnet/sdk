// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class EnumShouldNotHaveDuplicatedValues : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1069";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EnumShouldNotHaveDuplicatedValuesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageRuleDuplicatedValue = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EnumShouldNotHaveDuplicatedValuesMessageDuplicatedValue), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        internal static DiagnosticDescriptor RuleDuplicatedValue = new DiagnosticDescriptor(RuleId,
                                                                                            s_localizableTitle,
                                                                                            s_localizableMessageRuleDuplicatedValue,
                                                                                            DiagnosticCategory.Design,
                                                                                            DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                                            isEnabledByDefault: false,
                                                                                            helpLinkUri: null,
                                                                                            customTags: WellKnownDiagnosticTags.Telemetry);

        private static readonly LocalizableString s_localizableMessageRuleDuplicatedBitwiseValuePart = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EnumShouldNotHaveDuplicatedValuesMessageDuplicatedBitwiseValuePart), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        internal static DiagnosticDescriptor RuleDuplicatedBitwiseValuePart = new DiagnosticDescriptor(RuleId,
                                                                                                       s_localizableTitle,
                                                                                                       s_localizableMessageRuleDuplicatedBitwiseValuePart,
                                                                                                       DiagnosticCategory.Design,
                                                                                                       DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                                                       isEnabledByDefault: false,
                                                                                                       helpLinkUri: null,
                                                                                                       customTags: WellKnownDiagnosticTags.Telemetry);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleDuplicatedValue, RuleDuplicatedBitwiseValuePart);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolStartAction(ssac =>
            {
                var enumSymbol = (INamedTypeSymbol)ssac.Symbol;
                if (enumSymbol.TypeKind != TypeKind.Enum)
                {
                    return;
                }

                var invalidMembersByValue = new ConcurrentDictionary<object, List<SyntaxNode>>();

                // Collect duplicated values...
                ssac.RegisterOperationAction(oc =>
                {
                    var fieldInitializerOperation = (IFieldInitializerOperation)oc.Operation;
                    if (fieldInitializerOperation.InitializedFields.Length != 1 ||
                        !fieldInitializerOperation.Value.ConstantValue.HasValue)
                    {
                        return;
                    }

                    var onlyReferencesOneField = GetFilteredDescendants(fieldInitializerOperation, op => op.Kind != OperationKind.Binary)
                        .OfType<IFieldReferenceOperation>()
                        .Any();
                    if (onlyReferencesOneField)
                    {
                        return;
                    }

                    invalidMembersByValue.AddOrUpdate(fieldInitializerOperation.Value.ConstantValue.Value,
                        new List<SyntaxNode> { fieldInitializerOperation.Syntax.Parent },
                        (key, value) =>
                        {
                            value.Add(fieldInitializerOperation.Syntax.Parent);
                            return value;
                        });
                }, OperationKind.FieldInitializer);

                // ...and report at the end of the enum declaration
                ssac.RegisterSymbolEndAction(sac =>
                {
                    foreach (var kvp in invalidMembersByValue)
                    {
                        foreach (var item in kvp.Value.OrderBy(x => x.GetLocation().SourceSpan).Skip(1))
                        {
                            sac.ReportDiagnostic(item.CreateDiagnostic(RuleDuplicatedValue));
                        }
                    }
                });

                // Collect and report on duplicated bitwise parts
                ssac.RegisterOperationAction(oc =>
                {
                    var binaryOperation = (IBinaryOperation)oc.Operation;
                    if (binaryOperation.OperatorKind != BinaryOperatorKind.Or)
                    {
                        return;
                    }

                    var bitwiseRefFieldsByField = GetFilteredDescendants(binaryOperation,
                            op => op.Kind != OperationKind.Binary || (op is IBinaryOperation binaryOp && binaryOp.OperatorKind == BinaryOperatorKind.Or))
                        .OfType<IFieldReferenceOperation>()
                        .GroupBy(fro => fro.Field, fro => fro.Syntax)
                        .ToDictionary(x => x.Key, x => x.ToList());

                    foreach (var kvp in bitwiseRefFieldsByField)
                    {
                        foreach (var item in kvp.Value.OrderBy(x => x.GetLocation().SourceSpan).Skip(1))
                        {
                            oc.ReportDiagnostic(item.CreateDiagnostic(RuleDuplicatedBitwiseValuePart));
                        }
                    }
                }, OperationKind.Binary);
            }, SymbolKind.NamedType);
        }

        private static IEnumerable<IOperation> GetFilteredDescendants(IOperation operation, Func<IOperation, bool> descendIntoOperation)
        {
            var stack = ArrayBuilder<IEnumerator<IOperation>>.GetInstance();
            stack.Add(operation.Children.GetEnumerator());

            while (stack.Any())
            {
                var enumerator = stack.Last();
                stack.RemoveLast();
                if (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    stack.Add(enumerator);
                    if (current != null && descendIntoOperation(current))
                    {
                        yield return current;
                        stack.Add(current.Children.GetEnumerator());
                    }
                }
            }

            stack.Free();
        }
    }
}
