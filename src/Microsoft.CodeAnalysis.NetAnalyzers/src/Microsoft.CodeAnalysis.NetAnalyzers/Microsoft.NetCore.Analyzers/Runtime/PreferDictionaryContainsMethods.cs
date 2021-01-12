// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferDictionaryContainsMethods : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1839";

        private static readonly LocalizableString s_localizableTitle = CreateResource(nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsMethodsTitle));
        private static readonly LocalizableString s_localizableContainsKeyMessage = CreateResource(nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyMessage));
        private static readonly LocalizableString s_localizableContainsKeyDescription = CreateResource(nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyDescription));
        private static readonly LocalizableString s_localizableContainsValueMessage = CreateResource(nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueMessage));
        private static readonly LocalizableString s_localizableContainsValueDescription = CreateResource(nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueDescription));

#pragma warning disable RS2000
        internal static readonly DiagnosticDescriptor ContainsKeyRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableContainsKeyMessage,
            DiagnosticCategory.Performance,
            RuleLevel.BuildWarning,
            s_localizableContainsKeyDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ContainsValueRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableContainsValueMessage,
            DiagnosticCategory.Performance,
            RuleLevel.BuildWarning,
            s_localizableContainsValueDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);
#pragma warning restore RS2000

        private const string ContainsMethodName = "Contains";
        internal const string ContainsKeyMethodName = "ContainsKey";
        internal const string ContainsValueMethodName = "ContainsValue";
        internal const string KeysPropertyName = "Keys";
        internal const string ValuesPropertyName = "Values";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ContainsKeyRule, ContainsValueRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var compilation = compilationContext.Compilation;

            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1, out var icollectionType))
                return;
            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIDictionary2, out var idictionaryType))
                return;
            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1, out var ienumerableType))
                return;

            compilationContext.RegisterOperationAction(OnOperationAction, OperationKind.Invocation);

            void OnOperationAction(OperationAnalysisContext context)
            {
                var invocation = (IInvocationOperation)context.Operation;
                IMethodSymbol containsMethod = invocation.TargetMethod;

                if (containsMethod.Name != ContainsMethodName)
                    return;
                if (containsMethod.ReturnType.SpecialType != SpecialType.System_Boolean)
                    return;
                if (!TryGetPropertyReferenceOperation(invocation, out IPropertySymbol? property))
                    return;
                if (!DoesContainsCandidateHaveCorrectArgumentCount(containsMethod))
                    return;
                if (!TryGetConstructedDictionaryType(property.ContainingType, out INamedTypeSymbol? constructedDictionaryType))
                    return;

                ITypeSymbol keyType = constructedDictionaryType.TypeArguments[0];
                ITypeSymbol valueType = constructedDictionaryType.TypeArguments[1];
                ITypeSymbol containsParameterType = containsMethod.Parameters.Last().Type;

                if (property.Name == KeysPropertyName)
                {
                    if (!containsParameterType.Equals(keyType, SymbolEqualityComparer.Default))
                        return;

                    IMethodSymbol? containsKeyMethod = property.ContainingType.GetMembers(ContainsKeyMethodName)
                        .OfType<IMethodSymbol>()
                        .WhereAsArray(x => x.ReturnType.SpecialType == SpecialType.System_Boolean &&
                            x.IsPublic() &&
                            x.Parameters.Length == 1 &&
                            x.Parameters[0].Type.Equals(keyType, SymbolEqualityComparer.Default))
                        .SingleOrDefault();

                    if (containsKeyMethod is null)
                        return;

                    var diagnostic = Diagnostic.Create(ContainsKeyRule, invocation.Syntax.GetLocation(), property.ContainingType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
                else if (property.Name == ValuesPropertyName)
                {
                    if (!containsParameterType.Equals(valueType, SymbolEqualityComparer.Default))
                        return;

                    IMethodSymbol? containsValueMethod = property.ContainingType.GetMembers(ContainsValueMethodName)
                        .OfType<IMethodSymbol>()
                        .WhereAsArray(x => x.ReturnType.SpecialType == SpecialType.System_Boolean &&
                            x.Parameters.Length == 1 &&
                            x.Parameters[0].Type.Equals(valueType, SymbolEqualityComparer.Default))
                        .SingleOrDefault();

                    if (containsValueMethod is null)
                        return;

                    var diagnostic = Diagnostic.Create(ContainsValueRule, invocation.Syntax.GetLocation(), property.ContainingType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            static bool TryGetPropertyReferenceOperation(IInvocationOperation containsInvocation, [NotNullWhen(true)] out IPropertySymbol? property)
            {
                IOperation current = containsInvocation;

                if (containsInvocation.TargetMethod.IsExtensionMethod)
                {
                    if (current.Children.FirstOrDefault() is IArgumentOperation argumentOperation)
                        current = argumentOperation;
                    if (current.Children.FirstOrDefault() is IConversionOperation conversionOperation)
                        current = conversionOperation;
                }

                property = (current.Children.FirstOrDefault() as IPropertyReferenceOperation)?.Property;
                return property is not null;
            }

            bool TryGetConstructedDictionaryType(INamedTypeSymbol derived, [NotNullWhen(true)] out INamedTypeSymbol? constructedDictionaryType)
            {
                constructedDictionaryType = derived.GetBaseTypesAndThis()
                    .WhereAsArray(x => x.OriginalDefinition.Equals(idictionaryType, SymbolEqualityComparer.Default))
                    .SingleOrDefault();

                if (constructedDictionaryType is null)
                {
                    constructedDictionaryType = derived.AllInterfaces
                        .WhereAsArray(x => x.OriginalDefinition.Equals(idictionaryType, SymbolEqualityComparer.Default))
                        .SingleOrDefault();
                }

                return constructedDictionaryType is not null;
            }

            static bool DoesContainsCandidateHaveCorrectArgumentCount(IMethodSymbol containsCandidate)
            {
                if (containsCandidate.Language == LanguageNames.CSharp && containsCandidate.IsExtensionMethod)
                    return containsCandidate.Parameters.Length == 2;
                else
                    return containsCandidate.Parameters.Length == 1;
            }
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        }
    }
}
