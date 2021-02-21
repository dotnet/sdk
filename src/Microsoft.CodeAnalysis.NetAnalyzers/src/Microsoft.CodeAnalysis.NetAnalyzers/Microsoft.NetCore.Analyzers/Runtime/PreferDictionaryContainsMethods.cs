// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferDictionaryContainsMethods : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1839";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(Resx.PreferDictionaryContainsMethodsTitle), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableContainsKeyMessage = new LocalizableResourceString(nameof(Resx.PreferDictionaryContainsKeyMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableContainsKeyDescription = new LocalizableResourceString(nameof(Resx.PreferDictionaryContainsKeyDescription), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableContainsValueMessage = new LocalizableResourceString(nameof(Resx.PreferDictionaryContainsValueMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableContainsValueDescription = new LocalizableResourceString(nameof(Resx.PreferDictionaryContainsValueDescription), Resx.ResourceManager, typeof(Resx));

        internal static readonly DiagnosticDescriptor ContainsKeyRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableContainsKeyMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableContainsKeyDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ContainsValueRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableContainsValueMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableContainsValueDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

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
                //  We report a diagnostic if we find an invocation of an applicable Contains method,
                //  and the contains method is being invoked on an applicable property.

                //  A property is applicable if:
                //      1) It belongs to a type that implements IDictionary`2
                //      2) It's name is either "Keys" or "Values"
                //  A Contains method is applicable if:
                //      1) It has a boolean return type.
                //      2) It has one argument, not counting the first argument of an extension method.
                //      3) The argument must match the applicable type argument.
                //          - If the property's name is "Keys", it must match the type substituted for TKey in the IDictionary`2 instance.
                //          - If the property's name is "Values", it must match the type substituted for TValue in the IDictionary`2 instance.

                //  Once we have an applicable Contains method and an applicable property reference, we search for an applicable ContainsXXX
                //  method on the IDictionary`2 receiver. 

                //  A ContainsXXX method is applicable if:
                //      1) It has a boolean return type
                //      2) It is publically accessible
                //      3) Its name is "ContainsKey" if property is "Keys", or "ContainsValue" if property is "Values". 
                //      4) It has exactly one parameter of the correct type:
                //          - If the method is a "ContainsKey" method, its type must be TKey. 
                //          - If the method is a "ContainsValue" method, its type must be TValue.

                var invocation = (IInvocationOperation)context.Operation;
                IMethodSymbol containsMethod = invocation.TargetMethod;

                if (containsMethod.Name != ContainsMethodName
                    || containsMethod.ReturnType.SpecialType != SpecialType.System_Boolean
                    || !TryGetPropertyReferenceOperation(invocation, out IPropertySymbol? property)
                    || !TryGetConstructedDictionaryType(property.ContainingType, out INamedTypeSymbol? constructedDictionaryType))
                {
                    return;
                }

                //  At this point, we know that the method being invoked is a method called "Contains" that has a boolean return type.
                //  We also know that the method is being invoked on a property belonging to a type that implements IDictionary`2. We will
                //  compare the types used to construct IDictionary`2 to the parameter type of the Contains method. 

                ITypeSymbol keyType = constructedDictionaryType.TypeArguments[0];
                ITypeSymbol valueType = constructedDictionaryType.TypeArguments[1];

                //  We use Parameters.Last() because the first argument could be the key/value collection, depending
                //  on whether the method is an extension method and whether the language is C# or Visual Basic. 

                ITypeSymbol containsParameterType = containsMethod.Parameters[^1].Type;

                if (property.Name == KeysPropertyName)
                {
                    if (!containsParameterType.Equals(keyType, SymbolEqualityComparer.Default))
                        return;

                    //  Now we search for an accessible ContainsKey method that returns boolean and accepts a single
                    //  parameter of the type that was substituted for TKey.

                    IMethodSymbol? containsKeyMethod = property.ContainingType.GetMembers(ContainsKeyMethodName)
                        .OfType<IMethodSymbol>()
                        .WhereAsArray(x => x.ReturnType.SpecialType == SpecialType.System_Boolean &&
                            x.IsPublic() &&
                            x.Parameters.Length == 1 &&
                            x.Parameters[0].Type.Equals(keyType, SymbolEqualityComparer.Default))
                        .FirstOrDefault();

                    if (containsKeyMethod is null)
                        return;

                    var diagnostic = invocation.CreateDiagnostic(ContainsKeyRule, property.ContainingType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
                else if (property.Name == ValuesPropertyName)
                {
                    if (!containsParameterType.Equals(valueType, SymbolEqualityComparer.Default))
                        return;

                    //  Now we search for an accessible ContainsValue method that returns boolean and accepts a single
                    //  parameter of the type that was substituted for TValue.

                    IMethodSymbol? containsValueMethod = property.ContainingType.GetMembers(ContainsValueMethodName)
                        .OfType<IMethodSymbol>()
                        .WhereAsArray(x => x.ReturnType.SpecialType == SpecialType.System_Boolean &&
                            x.IsPublic() &&
                            x.Parameters.Length == 1 &&
                            x.Parameters[0].Type.Equals(valueType, SymbolEqualityComparer.Default))
                        .FirstOrDefault();

                    if (containsValueMethod is null)
                        return;

                    var diagnostic = invocation.CreateDiagnostic(ContainsValueRule, property.ContainingType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            static bool TryGetPropertyReferenceOperation(IInvocationOperation containsInvocation, [NotNullWhen(true)] out IPropertySymbol? property)
            {
                IMethodSymbol method = containsInvocation.TargetMethod;
                IOperation? receiver = null;

                //  C# and VB handle extension methods differently. In C#, the "this" argument is included in the argument list. In VB, it's not.
                if (method.IsExtensionMethod && method.Language == LanguageNames.CSharp && method.Parameters.Length == 2)
                {
                    receiver = containsInvocation.Arguments[0].Value;
                }
                else if (method.Parameters.Length == 1)
                {
                    receiver = containsInvocation.Instance;
                }

                //  The receiver may be a conversion operation if the invocation is an extension method.
                receiver = receiver is IConversionOperation conversion ? conversion.Operand : receiver;

                property = (receiver as IPropertyReferenceOperation)?.Property;
                return property is not null;
            }

            bool TryGetConstructedDictionaryType(INamedTypeSymbol derived, [NotNullWhen(true)] out INamedTypeSymbol? constructedDictionaryType)
            {
                constructedDictionaryType = derived.GetBaseTypesAndThis()
                    .WhereAsArray(x => x.OriginalDefinition.Equals(idictionaryType, SymbolEqualityComparer.Default))
                    .FirstOrDefault();

                if (constructedDictionaryType is null)
                {
                    constructedDictionaryType = derived.AllInterfaces
                        .WhereAsArray(x => x.OriginalDefinition.Equals(idictionaryType, SymbolEqualityComparer.Default))
                        .FirstOrDefault();
                }

                return constructedDictionaryType is not null;
            }
        }
    }
}
