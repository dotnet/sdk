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
        internal static DiagnosticDescriptor RuleDuplicatedValue = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                            s_localizableTitle,
                                                                                            s_localizableMessageRuleDuplicatedValue,
                                                                                            DiagnosticCategory.Design,
                                                                                            RuleLevel.IdeSuggestion,
                                                                                            description: null,
                                                                                            isPortedFxCopRule: false,
                                                                                            isDataflowRule: false);

        private static readonly LocalizableString s_localizableMessageRuleDuplicatedBitwiseValuePart = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.EnumShouldNotHaveDuplicatedValuesMessageDuplicatedBitwiseValuePart), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        internal static DiagnosticDescriptor RuleDuplicatedBitwiseValuePart = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                       s_localizableTitle,
                                                                                                       s_localizableMessageRuleDuplicatedBitwiseValuePart,
                                                                                                       DiagnosticCategory.Design,
                                                                                                       RuleLevel.IdeSuggestion,
                                                                                                       description: null,
                                                                                                       isPortedFxCopRule: false,
                                                                                                       isDataflowRule: false);

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

                var membersByValue = new ConcurrentDictionary<object, ConcurrentBag<(SyntaxNode fieldSyntax, string fieldName)>>();

                // Workaround to get the value of all enum fields that don't have an explicit initializer.
                // We will start with all of the enum fields and remove the ones with an explicit initializer.
                // See https://github.com/dotnet/roslyn/issues/40811
                var filteredFields = enumSymbol.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(f => !f.IsImplicitlyDeclared)
                    .Select(x => new KeyValuePair<IFieldSymbol, object>(x, x.ConstantValue));
                var enumFieldsWithImplicitValue = new ConcurrentDictionary<IFieldSymbol, object>(filteredFields);

                // Collect duplicated values...
                ssac.RegisterOperationAction(oc =>
                {
                    var fieldInitializerOperation = (IFieldInitializerOperation)oc.Operation;

                    if (fieldInitializerOperation.InitializedFields.Length != 1 ||
                        !fieldInitializerOperation.Value.ConstantValue.HasValue)
                    {
                        return;
                    }

                    var currentField = fieldInitializerOperation.InitializedFields[0];

                    // Remove the explicitly initialized field
                    enumFieldsWithImplicitValue.TryRemove(currentField, out _);

                    var onlyReferencesOneField = GetFilteredDescendants(fieldInitializerOperation, op => op.Kind != OperationKind.Binary)
                        .OfType<IFieldReferenceOperation>()
                        .Where(fro => fro.Field.ContainingType.Equals(enumSymbol))
                        .Any();
                    if (onlyReferencesOneField || fieldInitializerOperation.Value.ConstantValue.Value == null)
                    {
                        return;
                    }

                    membersByValue.AddOrUpdate(fieldInitializerOperation.Value.ConstantValue.Value,
                        new ConcurrentBag<(SyntaxNode, string)> { (fieldInitializerOperation.Syntax.Parent, currentField.Name) },
                        (key, value) =>
                        {
                            value.Add((fieldInitializerOperation.Syntax.Parent, currentField.Name));
                            return value;
                        });
                }, OperationKind.FieldInitializer);

                // ...and report at the end of the enum declaration
                ssac.RegisterSymbolEndAction(sac =>
                {
                    // Handle all enum fields without an explicit initializer
                    foreach ((var field, var value) in enumFieldsWithImplicitValue)
                    {
                        var fieldSyntax = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

                        if (fieldSyntax != null && value != null)
                        {
                            membersByValue.AddOrUpdate(value,
                                new ConcurrentBag<(SyntaxNode, string)> { (fieldSyntax, field.Name) },
                                (k, v) =>
                                {
                                    v.Add((fieldSyntax, field.Name));
                                    return v;
                                });
                        }
                    }

                    foreach (var kvp in membersByValue)
                    {
                        var orderedItems = kvp.Value.OrderBy(x => x.fieldSyntax.GetLocation().SourceSpan);
                        var duplicatedMemberName = orderedItems.FirstOrDefault().fieldName;

                        foreach ((var fieldSyntax, var fieldName) in orderedItems.Skip(1))
                        {
                            sac.ReportDiagnostic(fieldSyntax.CreateDiagnostic(RuleDuplicatedValue, fieldName, kvp.Key, duplicatedMemberName));
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
                            oc.ReportDiagnostic(item.CreateDiagnostic(RuleDuplicatedBitwiseValuePart, kvp.Key.Name));
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
