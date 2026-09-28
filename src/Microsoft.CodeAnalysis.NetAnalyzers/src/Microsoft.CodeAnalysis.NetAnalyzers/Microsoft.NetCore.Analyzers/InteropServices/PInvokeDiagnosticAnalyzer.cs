// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1401: <inheritdoc cref="PInvokesShouldNotBeVisibleTitle"/>
    /// CA2101: <inheritdoc cref="SpecifyMarshalingForPInvokeStringArgumentsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PInvokeDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleCA1401Id = "CA1401";
        public const string RuleCA2101Id = "CA2101";

        internal static readonly DiagnosticDescriptor RuleCA1401 = DiagnosticDescriptorHelper.Create(
            RuleCA1401Id,
            CreateLocalizableResourceString(nameof(PInvokesShouldNotBeVisibleTitle)),
            CreateLocalizableResourceString(nameof(PInvokesShouldNotBeVisibleMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(PInvokesShouldNotBeVisibleDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        private static readonly LocalizableString s_localizableMessageAndTitleCA2101 = CreateLocalizableResourceString(nameof(SpecifyMarshalingForPInvokeStringArgumentsTitle));

        internal static readonly DiagnosticDescriptor RuleCA2101 = DiagnosticDescriptorHelper.Create(
            RuleCA2101Id,
            s_localizableMessageAndTitleCA2101,
            s_localizableMessageAndTitleCA2101,
            DiagnosticCategory.Globalization,
            RuleLevel.BuildWarningCandidate,
            description: CreateLocalizableResourceString(nameof(SpecifyMarshalingForPInvokeStringArgumentsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(RuleCA1401, RuleCA2101);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                (context) =>
                {
                    INamedTypeSymbol? dllImportType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesDllImportAttribute);
                    if (dllImportType == null)
                    {
                        return;
                    }

                    INamedTypeSymbol? marshalAsType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesMarshalAsAttribute);
                    if (marshalAsType == null)
                    {
                        return;
                    }

                    INamedTypeSymbol? stringBuilderType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextStringBuilder);
                    if (stringBuilderType == null)
                    {
                        return;
                    }

                    INamedTypeSymbol? unmanagedType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesUnmanagedType);
                    if (unmanagedType == null)
                    {
                        return;
                    }

                    context.RegisterSymbolAction(new SymbolAnalyzer(dllImportType, marshalAsType, stringBuilderType, unmanagedType).AnalyzeSymbol, SymbolKind.Method);
                });
        }

        private sealed class SymbolAnalyzer
        {
            private readonly INamedTypeSymbol _dllImportType;
            private readonly INamedTypeSymbol _marshalAsType;
            private readonly INamedTypeSymbol _stringBuilderType;
            private readonly INamedTypeSymbol _unmanagedType;

            public SymbolAnalyzer(
                INamedTypeSymbol dllImportType,
                INamedTypeSymbol marshalAsType,
                INamedTypeSymbol stringBuilderType,
                INamedTypeSymbol unmanagedType)
            {
                _dllImportType = dllImportType;
                _marshalAsType = marshalAsType;
                _stringBuilderType = stringBuilderType;
                _unmanagedType = unmanagedType;
            }

            public void AnalyzeSymbol(SymbolAnalysisContext context)
            {
                var methodSymbol = (IMethodSymbol)context.Symbol;
                if (methodSymbol == null)
                {
                    return;
                }

                DllImportData? dllImportData = methodSymbol.GetDllImportData();
                if (dllImportData == null)
                {
                    return;
                }

                AttributeData? dllAttribute = methodSymbol.GetAttribute(_dllImportType);
                Location? defaultLocation = dllAttribute == null ? methodSymbol.Locations.FirstOrDefault() : GetAttributeLocation(dllAttribute);

                // CA1401 - PInvoke methods should not be visible
                if (methodSymbol.IsExternallyVisible())
                {
                    context.ReportDiagnostic(context.Symbol.CreateDiagnostic(RuleCA1401, methodSymbol.Name));
                }

                // CA2101 - Specify marshalling for PInvoke string arguments
                if (dllImportData.BestFitMapping != false &&
                    context.Options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.InvariantGlobalization, context.Compilation) is not "true")
                {
                    bool appliedCA2101ToMethod = false;
                    foreach (IParameterSymbol parameter in methodSymbol.Parameters)
                    {
                        if (parameter.Type.SpecialType == SpecialType.System_String || parameter.Type.Equals(_stringBuilderType))
                        {
                            AttributeData? marshalAsAttribute = parameter.GetAttribute(_marshalAsType);
                            CharSet? charSet = marshalAsAttribute == null
                                ? dllImportData.CharacterSet
                                : MarshalingToCharSet(GetParameterMarshaling(marshalAsAttribute));

                            // only unicode marshaling is considered safe
                            if (charSet != CharSet.Unicode)
                            {
                                if (marshalAsAttribute != null)
                                {
                                    // track the diagnostic on the [MarshalAs] attribute
                                    Location? marshalAsLocation = GetAttributeLocation(marshalAsAttribute);
                                    context.ReportDiagnostic(Diagnostic.Create(RuleCA2101, marshalAsLocation));
                                }
                                else if (!appliedCA2101ToMethod)
                                {
                                    // track the diagnostic on the [DllImport] attribute
                                    appliedCA2101ToMethod = true;
                                    context.ReportDiagnostic(Diagnostic.Create(RuleCA2101, defaultLocation));
                                }
                            }
                        }
                    }

                    // only unicode marshaling is considered safe, but only check this if we haven't already flagged the attribute
                    if (!appliedCA2101ToMethod && dllImportData.CharacterSet != CharSet.Unicode &&
                        (methodSymbol.ReturnType.SpecialType == SpecialType.System_String || methodSymbol.ReturnType.Equals(_stringBuilderType)))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(RuleCA2101, defaultLocation));
                    }
                }
            }

            private UnmanagedType? GetParameterMarshaling(AttributeData attributeData)
            {
                if (!attributeData.ConstructorArguments.IsEmpty)
                {
                    TypedConstant argument = attributeData.ConstructorArguments.First();
                    if (SymbolEqualityComparer.Default.Equals(argument.Type, _unmanagedType))
                    {
                        return (UnmanagedType)argument.Value!;
                    }
                    else if (argument.Type?.SpecialType == SpecialType.System_Int16)
                    {
                        return (UnmanagedType)(short)argument.Value!;
                    }
                }

                return null;
            }

            private static CharSet? MarshalingToCharSet(UnmanagedType? type)
            {
                if (type == null)
                {
                    return null;
                }

#pragma warning disable CS0618 // Type or member is obsolete
                switch (type)
                {
                    case UnmanagedType.AnsiBStr:
                    case UnmanagedType.LPStr:
                    case UnmanagedType.VBByRefStr:
                        return CharSet.Ansi;
                    case UnmanagedType.BStr:
                    case UnmanagedType.LPWStr:
                        return CharSet.Unicode;
                    case UnmanagedType.ByValTStr:
                    case UnmanagedType.LPTStr:
                    case UnmanagedType.TBStr:
                    default:
                        // CharSet.Auto and CharSet.None are not available in the portable
                        // profiles. We are not interested in those values for our analysis and so simply
                        // return null
                        return null;
                }
#pragma warning restore CS0618 // Type or member is obsolete
            }

            private static Location? GetAttributeLocation(AttributeData attributeData)
            {
                return attributeData.ApplicationSyntaxReference?.SyntaxTree.GetLocation(attributeData.ApplicationSyntaxReference.Span);
            }
        }
    }
}
