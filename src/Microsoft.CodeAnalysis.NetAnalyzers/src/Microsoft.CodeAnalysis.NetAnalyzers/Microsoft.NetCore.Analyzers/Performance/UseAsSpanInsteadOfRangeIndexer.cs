// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1831, CA1832, CA1833: Use AsSpan or AsMemory instead of Range-based indexers when appropriate.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseAsSpanInsteadOfRangeIndexerAnalyzer : DiagnosticAnalyzer
    {
        internal const string StringRuleId = "CA1831";
        internal const string ArrayReadOnlyRuleId = "CA1832";
        internal const string ArrayReadWriteRuleId = "CA1833";
        internal const string TargetMethodName = nameof(TargetMethodName);

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfRangeIndexerTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfRangeIndexerMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableStringDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfStringRangeIndexerDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableArrayReadDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsSpanReadOnlyInsteadOfArrayRangeIndexerDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableArrayWriteDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfArrayRangeIndexerDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static readonly DiagnosticDescriptor StringRule = DiagnosticDescriptorHelper.Create(
            StringRuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.BuildWarning,
            description: s_localizableStringDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ArrayReadOnlyRule = DiagnosticDescriptorHelper.Create(
            ArrayReadOnlyRuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableArrayReadDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ArrayReadWriteRule = DiagnosticDescriptorHelper.Create(
            ArrayReadWriteRuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: s_localizableArrayWriteDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(StringRule, ArrayReadOnlyRule, ArrayReadWriteRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var compilation = context.Compilation;
            var stringType = compilation.GetSpecialType(SpecialType.System_String);
            var span = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1);
            var readOnlySpan = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1);
            var memory = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemory1);
            var readOnlyMemory = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlyMemory1);
            var range = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRange);

            if (stringType == null || span == null || readOnlySpan == null || memory == null || readOnlyMemory == null || range == null)
            {
                return;
            }

            var spanTypes = ImmutableArray.Create(span, readOnlySpan);
            var readOnlyTypes = ImmutableArray.Create(readOnlySpan, readOnlyMemory);
            var targetTypes = ImmutableArray.Create(span, readOnlySpan, memory, readOnlyMemory);

            context.RegisterOperationAction(
                operationContext =>
                {
                    IOperation indexerArgument;
                    ITypeSymbol containingType;

                    if (operationContext.Operation is IPropertyReferenceOperation propertyReference)
                    {
                        if (!propertyReference.Property.IsIndexer || propertyReference.Arguments.Length != 1)
                        {
                            return;
                        }

                        indexerArgument = propertyReference.Arguments[0].Value;
                        containingType = propertyReference.Property.ContainingType;
                    }
                    else if (operationContext.Operation is IArrayElementReferenceOperation elementReference)
                    {
                        if (elementReference.Indices.Length != 1)
                        {
                            return;
                        }

                        indexerArgument = elementReference.Indices[0];
                        containingType = elementReference.ArrayReference.Type;
                    }
                    else if (operationContext.Operation.Kind == OperationKind.None)
                    {
                        // The forward support via the "None" operation kind is only available for C#.
                        if (operationContext.Compilation.Language != LanguageNames.CSharp)
                        {
                            return;
                        }

                        // 8635 is (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.ElementAccessExpression
                        if (operationContext.Operation.Syntax.RawKind != 8635)
                        {
                            return;
                        }

                        IEnumerator<IOperation> enumerator = operationContext.Operation.Children.GetEnumerator();

                        if (!enumerator.MoveNext())
                        {
                            return;
                        }

                        containingType = enumerator.Current.Type;

                        if (!enumerator.MoveNext())
                        {
                            return;
                        }

                        indexerArgument = enumerator.Current;

                        // Too many children, don't know what this is.
                        if (enumerator.MoveNext())
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }

                    if (indexerArgument is not IRangeOperation rangeOperation)
                    {
                        return;
                    }

                    if (operationContext.Operation.Parent is not IConversionOperation conversionOperation)
                    {
                        return;
                    }

                    if (!conversionOperation.IsImplicit)
                    {
                        return;
                    }

                    var targetType = conversionOperation.Type?.OriginalDefinition;

                    if (!targetTypes.Contains(targetType))
                    {
                        return;
                    }

                    DiagnosticDescriptor rule;

                    if (stringType.Equals(containingType))
                    {
                        rule = StringRule;
                    }
                    else if (containingType.TypeKind == TypeKind.Array)
                    {
                        rule = readOnlyTypes.Contains(targetType) ? ArrayReadOnlyRule : ArrayReadWriteRule;
                    }
                    else
                    {
                        return;
                    }

                    var methodToUse = spanTypes.Contains(targetType) ? nameof(MemoryExtensions.AsSpan) : nameof(MemoryExtensions.AsMemory);
                    var dictBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
                    dictBuilder.Add(TargetMethodName, methodToUse);

                    operationContext.ReportDiagnostic(
                        operationContext.Operation.CreateDiagnostic(
                            rule,
                            dictBuilder.ToImmutable(),
                            methodToUse,
                            rangeOperation.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                            containingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                },
                OperationKind.PropertyReference,
                OperationKind.ArrayElementReference,
                OperationKind.None);
        }
    }
}
