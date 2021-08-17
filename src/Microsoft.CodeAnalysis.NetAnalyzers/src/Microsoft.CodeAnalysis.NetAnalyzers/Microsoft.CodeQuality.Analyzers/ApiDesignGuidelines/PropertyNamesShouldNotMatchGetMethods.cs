// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1721: Property names should not match get methods
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PropertyNamesShouldNotMatchGetMethodsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1721";

        private const string Get = "Get";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PropertyNamesShouldNotMatchGetMethodsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Naming,
                                                                             RuleLevel.Disabled,    // Heuristic based naming rule.
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var obsoleteAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);

                // Analyze properties, methods
                context.RegisterSymbolAction(ctx => AnalyzeSymbol(ctx, obsoleteAttributeType), SymbolKind.Property, SymbolKind.Method);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol? obsoleteAttributeType)
        {
            string identifier;
            var symbol = context.Symbol;

            // We only want to report an issue when the user is free to update the member.
            // This method will be called for both the property and the method so we can bail out
            // when the member (symbol) is an override.
            // Note that in the case of an override + a local declaration, the issue will be raised from
            // the local declaration.
            if (symbol.IsOverride)
            {
                return;
            }

            // Bail out if the method/property is not exposed (public, protected, or protected internal) by default
            var configuredVisibilities = context.Options.GetSymbolVisibilityGroupOption(Rule, context.Symbol, context.Compilation, SymbolVisibilityGroup.Public);
            if (!configuredVisibilities.Contains(symbol.GetResultantVisibility()))
            {
                return;
            }

            // If either the property or method is marked as obsolete, bail out
            // see https://github.com/dotnet/roslyn-analyzers/issues/2956
            if (symbol.HasAttribute(obsoleteAttributeType))
            {
                return;
            }

            if (symbol.Kind == SymbolKind.Property)
            {
                // Want to look for methods named the same as the property but with a 'Get' prefix
                identifier = Get + symbol.Name;
            }
            else if (symbol.Kind == SymbolKind.Method && symbol.Name.StartsWith(Get, StringComparison.Ordinal))
            {
                // Want to look for properties named the same as the method sans 'Get'
                identifier = symbol.Name[3..];
            }
            else
            {
                // Exit if the method name doesn't start with 'Get'
                return;
            }

            // Iterate through all declared types, including base
            foreach (INamedTypeSymbol type in symbol.ContainingType.GetBaseTypesAndThis())
            {
                Diagnostic? diagnostic = null;

                var exposedMembers = type.GetMembers(identifier).Where(member => configuredVisibilities.Contains(member.GetResultantVisibility()));
                foreach (var member in exposedMembers)
                {
                    // Ignore Object.GetType, as it's commonly seen and Type is a commonly-used property name.
                    if (member.ContainingType.SpecialType == SpecialType.System_Object &&
                        member.Name == nameof(GetType))
                    {
                        continue;
                    }

                    // Ignore members whose IsStatic does not match with the symbol's IsStatic
                    if (symbol.IsStatic != member.IsStatic)
                    {
                        continue;
                    }

                    // If either the property or method is marked as obsolete, bail out
                    // see https://github.com/dotnet/roslyn-analyzers/issues/2956
                    if (member.HasAttribute(obsoleteAttributeType))
                    {
                        continue;
                    }

                    // If the declared type is a property, was a matching method found?
                    if (symbol.Kind == SymbolKind.Property && member.Kind == SymbolKind.Method)
                    {
                        diagnostic = symbol.CreateDiagnostic(Rule, symbol.Name, identifier);
                        break;
                    }

                    // If the declared type is a method, was a matching property found?
                    if (symbol.Kind == SymbolKind.Method
                        && member.Kind == SymbolKind.Property
                        && !symbol.ContainingType.Equals(type)) // prevent reporting duplicate diagnostics
                    {
                        diagnostic = symbol.CreateDiagnostic(Rule, identifier, symbol.Name);
                        break;
                    }
                }

                if (diagnostic != null)
                {
                    // Once a match is found, exit the outer for loop
                    context.ReportDiagnostic(diagnostic);
                    break;
                }
            }
        }
    }
}