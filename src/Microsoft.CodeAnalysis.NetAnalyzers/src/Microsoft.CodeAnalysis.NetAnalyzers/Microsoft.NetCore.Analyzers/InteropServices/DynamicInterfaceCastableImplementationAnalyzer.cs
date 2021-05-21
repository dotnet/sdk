// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class DynamicInterfaceCastableImplementationAnalyzer : DiagnosticAnalyzer
    {
        internal const string InterfaceMethodsMissingImplementationRuleId = "CA2250";

        private static readonly DiagnosticDescriptor InterfaceMethodsMissingImplementation =
            DiagnosticDescriptorHelper.Create(
                InterfaceMethodsMissingImplementationRuleId,
                "",
                "",
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                "",
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string MethodsDeclaredOnImplementationTypeMustBeVirtualRuleId = "CA2251";

        private static readonly DiagnosticDescriptor MethodsDeclaredOnImplementationTypeMustBeVirtual =
            DiagnosticDescriptorHelper.Create(
                MethodsDeclaredOnImplementationTypeMustBeVirtualRuleId,
                "",
                "",
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                "",
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => throw new NotImplementedException();

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSymbolAction(context => AnalyzeType(context), SymbolKind.NamedType);
        }

        private const string DynamicCastableImplementationAttributeTypeName = "System.Runtime.InteropServices.DynamicInterfaceCastableImplementationAttribute";

        private static void AnalyzeType(SymbolAnalysisContext context)
        {
            INamedTypeSymbol targetType = (INamedTypeSymbol)context.Symbol;

            if (targetType.TypeKind != TypeKind.Interface)
            {
                return;
            }

            bool isDynamicInterfaceImplementation = false;
            foreach (var attribute in targetType.GetAttributes())
            {
                if (attribute.AttributeClass.ToDisplayString(SymbolDisplayFormats.QualifiedTypeAndNamespaceSymbolDisplayFormat) == DynamicCastableImplementationAttributeTypeName)
                {
                    isDynamicInterfaceImplementation = true;
                    break;
                }
            }

            if (!isDynamicInterfaceImplementation)
            {
                return;
            }

            bool missingMethodImplementations = false;
            foreach (var iface in targetType.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (!member.IsStatic && targetType.FindImplementationForInterfaceMember(member) is null)
                    {
                        missingMethodImplementations = true;
                        break;
                    }
                }
            }

            if (missingMethodImplementations)
            {
                context.ReportDiagnostic(targetType.CreateDiagnostic(InterfaceMethodsMissingImplementation));
            }

            foreach (var member in targetType.GetMembers())
            {
                if (member.IsVirtual)
                {
                    // Emit diagnostic for non-concrete method on implementation interface
                    context.ReportDiagnostic(member.CreateDiagnostic(MethodsDeclaredOnImplementationTypeMustBeVirtual, member.ToDisplayString()));
                }
            }
        }
    }
}
