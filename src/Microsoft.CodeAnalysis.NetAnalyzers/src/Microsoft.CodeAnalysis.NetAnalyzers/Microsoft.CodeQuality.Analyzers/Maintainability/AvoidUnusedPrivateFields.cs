// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1823: Avoid unused private fields
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidUnusedPrivateFieldsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1823";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUnusedPrivateFieldsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUnusedPrivateFieldsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUnusedPrivateFieldsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Performance,
                                                                                      RuleLevel.Disabled,       // Need to figure out how to handle runtime only references. We also have an implementation in the IDE.
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: true,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // We need to analyze generated code, but don't intend to report diagnostics for generated code fields.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

            context.RegisterCompilationStartAction(
                (compilationContext) =>
                {
                    ConcurrentDictionary<IFieldSymbol, UnusedValue> maybeUnreferencedPrivateFields = new ConcurrentDictionary<IFieldSymbol, UnusedValue>();
                    ConcurrentDictionary<IFieldSymbol, UnusedValue> referencedPrivateFields = new ConcurrentDictionary<IFieldSymbol, UnusedValue>();

                    ImmutableHashSet<INamedTypeSymbol> specialAttributes = GetSpecialAttributes(compilationContext.Compilation);
                    var structLayoutAttribute = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesStructLayoutAttribute);

                    compilationContext.RegisterSymbolAction(
                        (symbolContext) =>
                        {
                            IFieldSymbol field = (IFieldSymbol)symbolContext.Symbol;

                            // Fields of types marked with StructLayoutAttribute with LayoutKind.Sequential should never be flagged as unused as their removal can change the runtime behavior.
                            if (structLayoutAttribute != null && field.ContainingType != null)
                            {
                                foreach (var attribute in field.ContainingType.GetAttributes())
                                {
                                    if (structLayoutAttribute.Equals(attribute.AttributeClass.OriginalDefinition) &&
                                        attribute.ConstructorArguments.Length == 1)
                                    {
                                        var argument = attribute.ConstructorArguments[0];
                                        if (argument.Type != null)
                                        {
                                            SpecialType specialType = argument.Type.TypeKind == TypeKind.Enum ?
                                                ((INamedTypeSymbol)argument.Type).EnumUnderlyingType.SpecialType :
                                                argument.Type.SpecialType;

                                            if (DiagnosticHelpers.TryConvertToUInt64(argument.Value, specialType, out ulong convertedLayoutKindValue) &&
                                                convertedLayoutKindValue == (ulong)System.Runtime.InteropServices.LayoutKind.Sequential)
                                            {
                                                return;
                                            }
                                        }
                                    }
                                }
                            }

                            if (field.DeclaredAccessibility == Accessibility.Private && !referencedPrivateFields.ContainsKey(field))
                            {
                                // Fields with certain special attributes should never be considered unused.
                                if (!specialAttributes.IsEmpty)
                                {
                                    foreach (var attribute in field.GetAttributes())
                                    {
                                        if (specialAttributes.Contains(attribute.AttributeClass.OriginalDefinition))
                                        {
                                            return;
                                        }
                                    }
                                }

                                maybeUnreferencedPrivateFields.TryAdd(field, default);
                            }
                        },
                        SymbolKind.Field);

                    compilationContext.RegisterOperationAction(
                        (operationContext) =>
                        {
                            IFieldSymbol field = ((IFieldReferenceOperation)operationContext.Operation).Field;
                            if (field.DeclaredAccessibility == Accessibility.Private)
                            {
                                referencedPrivateFields.TryAdd(field, default);
                                maybeUnreferencedPrivateFields.TryRemove(field, out _);
                            }
                        },
                        OperationKind.FieldReference);

                    // Private field reference information reaches a state of consistency as each type symbol completes
                    // analysis. Reporting information at the end of each named type provides incremental analysis
                    // support inside the IDE.
                    compilationContext.RegisterSymbolStartAction(
                        context =>
                        {
                            context.RegisterSymbolEndAction(context =>
                            {
                                var namedType = (INamedTypeSymbol)context.Symbol;
                                foreach (var member in namedType.GetMembers())
                                {
                                    if (member is not IFieldSymbol field)
                                    {
                                        continue;
                                    }

                                    if (!maybeUnreferencedPrivateFields.ContainsKey(field) || referencedPrivateFields.ContainsKey(field))
                                    {
                                        continue;
                                    }

                                    context.ReportDiagnostic(field.CreateDiagnostic(Rule, field.Name));
                                }
                            });
                        },
                        SymbolKind.NamedType);
                });
        }

        private static ImmutableHashSet<INamedTypeSymbol> GetSpecialAttributes(Compilation compilation)
        {
            var specialAttributes = PooledHashSet<INamedTypeSymbol>.GetInstance();

            var fieldOffsetAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesFieldOffsetAttribute);
            if (fieldOffsetAttribute != null)
            {
                specialAttributes.Add(fieldOffsetAttribute);
            }

            var mefV1Attribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemComponentModelCompositionExportAttribute);
            if (mefV1Attribute != null)
            {
                specialAttributes.Add(mefV1Attribute);
            }

            var mefV2Attribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCompositionExportAttribute);
            if (mefV2Attribute != null)
            {
                specialAttributes.Add(mefV2Attribute);
            }

            return specialAttributes.ToImmutableAndFree();
        }
    }
}