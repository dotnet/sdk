// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1418: <inheritdoc cref="UseValidPlatformStringTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseValidPlatformString : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1418";
        private static readonly ImmutableArray<SymbolKind> s_symbols = ImmutableArray.Create(SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event);
        private static readonly ImmutableArray<string> methodNames = ImmutableArray.Create("IsOSPlatform", "IsOSPlatformVersionAtLeast");
        private const string IsPrefix = "Is";
        private const string VersionSuffix = "VersionAtLeast";
        private const string macOS = nameof(macOS);
        private const string Browser = nameof(Browser);

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(UseValidPlatformStringTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(UseValidPlatformStringDescription));

        internal static readonly DiagnosticDescriptor UnknownPlatform = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              CreateLocalizableResourceString(nameof(UseValidPlatformStringUnknownPlatform)),
                                                                              DiagnosticCategory.Interoperability,
                                                                              RuleLevel.BuildWarning,
                                                                              description: s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);

        internal static readonly DiagnosticDescriptor InvalidVersion = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              CreateLocalizableResourceString(nameof(UseValidPlatformStringInvalidVersion)),
                                                                              DiagnosticCategory.Interoperability,
                                                                              RuleLevel.BuildWarning,
                                                                              description: s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);

        internal static readonly DiagnosticDescriptor NoVersion = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              CreateLocalizableResourceString(nameof(UseValidPlatformStringNoVersion)),
                                                                              DiagnosticCategory.Interoperability,
                                                                              RuleLevel.BuildWarning,
                                                                              description: s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(UnknownPlatform, InvalidVersion, NoVersion);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemOperatingSystem, out var operatingSystemType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeVersioningSupportedOSPlatformAttribute, out var supportedAttriubte) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeVersioningUnsupportedOSPlatformAttribute, out var unsupportedAttribute))
                {
                    return;
                }

                var knownPlatforms = PooledDictionary<string, int>.GetInstance(StringComparer.OrdinalIgnoreCase);
                AddPlatformsAndVersionCountFromGuardMethods(operatingSystemType, knownPlatforms);
                AddPlatformsFromMsBuildOptions(knownPlatforms, context.Options.GetMSBuildItemMetadataValues(
                    MSBuildItemOptionNames.SupportedPlatform, context.Compilation));

                if (knownPlatforms.TryGetValue(macOS, out var versions))
                {
                    knownPlatforms.Add("OSX", versions);
                }

                if (knownPlatforms.TryGetValue(Browser, out versions) && versions == 0)
                {
                    // Browser is a special case, we don't have a version guard method for it but we need to support a browser1.0 target platform
                    knownPlatforms[Browser] = 2;
                }

                context.RegisterOperationAction(context => AnalyzeOperation(context.Operation, context, knownPlatforms), OperationKind.Invocation);
                context.RegisterSymbolAction(context => AnalyzeSymbol(
                    static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                    context,
                    context.Symbol,
                    supportedAttriubte, unsupportedAttribute, knownPlatforms, context.CancellationToken), s_symbols);
                context.RegisterCompilationEndAction(context => AnalyzeSymbol(
                    static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                    context,
                    context.Compilation.Assembly,
                    supportedAttriubte, unsupportedAttribute, knownPlatforms, context.CancellationToken));
            });

            static void AddPlatformsAndVersionCountFromGuardMethods(INamedTypeSymbol operatingSystemType, PooledDictionary<string, int> knownPlatforms)
            {
                var methods = operatingSystemType.GetMembers().OfType<IMethodSymbol>();
                foreach (var m in methods)
                {
                    if (m.IsStatic &&
                        m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                        NameAndParametersValid(m))
                    {
                        var (platformName, versionPartsCount) = ExtractPlatformAndVersionCount(m);
                        if (!knownPlatforms.TryGetValue(platformName, out var count) ||
                            versionPartsCount > count)
                        {
                            knownPlatforms[platformName] = versionPartsCount; // only keep highest count
                        }
                    }
                }
            }

            static void AddPlatformsFromMsBuildOptions(PooledDictionary<string, int> knownPlatforms, ImmutableArray<string> msBuildPlatforms)
            {
                foreach (var platform in msBuildPlatforms)
                {
                    if (!knownPlatforms.ContainsKey(platform))
                    {
                        knownPlatforms.Add(platform, 4); // Default version count is 4
                    }
                }
            }

            static (string platformName, int versionPartsCount) ExtractPlatformAndVersionCount(IMethodSymbol method)
            {
                var name = method.Name;
                if (name.EndsWith(VersionSuffix, StringComparison.Ordinal))
                {
                    return (name[2..(name.Length - VersionSuffix.Length)], method.Parameters.Length);
                }

                return (name[2..], 0);
            }

            static bool NameAndParametersValid(IMethodSymbol method) =>
                method.Name.StartsWith(IsPrefix, StringComparison.Ordinal) &&
                (method.Parameters.Length == 0 || method.Name.EndsWith(VersionSuffix, StringComparison.Ordinal));
        }

        private static void AnalyzeOperation(IOperation operation, OperationAnalysisContext context, PooledDictionary<string, int> knownPlatforms)
        {
            if (operation is IInvocationOperation invocation &&
                methodNames.Contains(invocation.TargetMethod.Name) &&
                invocation.Arguments.Length > 0 &&
                invocation.Arguments[0].Value is { } argument &&
                argument.ConstantValue.HasValue &&
                argument.ConstantValue.Value is string platformName &&
                IsNotKnownPlatform(knownPlatforms, platformName))
            {
                context.ReportDiagnostic(argument.Syntax.CreateDiagnostic(UnknownPlatform, platformName));
            }
        }

        private static void AnalyzeSymbol<TContext>(Action<TContext, Diagnostic> reportDiagnostic, TContext context, ISymbol symbol, INamedTypeSymbol supportedAttribute,
            INamedTypeSymbol unsupportedAttribute, PooledDictionary<string, int> knownPlatforms, CancellationToken token)
        {
            foreach (var attribute in symbol.GetAttributes(supportedAttribute, unsupportedAttribute))
            {
                AnalyzeAttribute(reportDiagnostic, context, attribute, knownPlatforms, token);
            }
        }

        private static void AnalyzeAttribute<TContext>(Action<TContext, Diagnostic> reportDiagnostic, TContext context, AttributeData attributeData, PooledDictionary<string, int> knownPlatforms, CancellationToken token)
        {
            var constructorArguments = attributeData.ConstructorArguments;
            var syntaxReference = attributeData.ApplicationSyntaxReference;

            if (constructorArguments.Length == 1 && syntaxReference != null)
            {
                if (constructorArguments[0].Value is string value)
                {
                    AnalyzeStringParameter(reportDiagnostic, context, syntaxReference.GetSyntax(token), knownPlatforms, value);
                }
                else
                {
                    reportDiagnostic(context, syntaxReference.GetSyntax(token).CreateDiagnostic(UnknownPlatform, "null"));
                }
            }

            static void AnalyzeStringParameter(Action<TContext, Diagnostic> reportDiagnostic, TContext context, SyntaxNode syntax, PooledDictionary<string, int> knownPlatforms, string value)
            {
                if (TryParsePlatformNameAndVersion(value, out var platformName, out var versionPart, out var versionCount))
                {
                    if (!knownPlatforms.TryGetValue(platformName, out var count))
                    {
                        reportDiagnostic(context, syntax.CreateDiagnostic(UnknownPlatform, platformName));
                    }
                    else if (count == 0 && versionCount != 0)
                    {
                        reportDiagnostic(context, syntax.CreateDiagnostic(NoVersion, versionPart, platformName));
                    }
                    else if (count < versionCount)
                    {
                        var maxCount = count == 2 ? string.Empty : $"-{count}";
                        reportDiagnostic(context, syntax.CreateDiagnostic(InvalidVersion, versionPart, platformName, maxCount));
                    }
                }
                else
                {
                    // version were not parsable, check the platform name and version count
                    if (!knownPlatforms.TryGetValue(platformName, out var count))
                    {
                        reportDiagnostic(context, syntax.CreateDiagnostic(UnknownPlatform, platformName));
                    }
                    else if (count == 0 && versionPart.Length != 0)
                    {
                        reportDiagnostic(context, syntax.CreateDiagnostic(NoVersion, versionPart, platformName));
                    }
                    else
                    {
                        var maxCount = count == 2 ? string.Empty : $"-{count}";
                        reportDiagnostic(context, syntax.CreateDiagnostic(InvalidVersion, versionPart, platformName, maxCount));
                    }
                }
            }
        }

        private static bool IsNotKnownPlatform(PooledDictionary<string, int> knownPlatforms, string platformName) =>
            platformName.Length == 0 || !knownPlatforms.ContainsKey(platformName);

        private static bool TryParsePlatformNameAndVersion(string osString, out string osPlatformName, out string versionPart, out int versionCount)
        {
            versionCount = 0;
            versionPart = string.Empty;

            for (int i = 0; i < osString.Length; i++)
            {
                if (char.IsDigit(osString[i]))
                {
                    osPlatformName = osString[..i];
                    versionPart = osString[i..];
                    if (i > 0 && Version.TryParse(osString[i..], out Version _))
                    {
                        versionCount = osString.Count(ch => ch == '.') + 1;
                        return true;
                    }

                    return false;
                }
            }

            osPlatformName = osString;
            return true;
        }
    }
}
