// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2243: Attribute string literals should parse correctly
    /// Unlike FxCop, this rule does not fire diagnostics for ill-formed versions
    /// Reason: There is wide usage of semantic versioning which does not follow traditional versioning grammar.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AttributeStringLiteralsShouldParseCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2243";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AttributeStringLiteralsShouldParseCorrectlyTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AttributeStringLiteralsShouldParseCorrectlyMessageDefault), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageEmpty = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AttributeStringLiteralsShouldParseCorrectlyMessageEmpty), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AttributeStringLiteralsShouldParseCorrectlyDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Usage,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca2243-attribute-string-literals-should-parse-correctly",
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor EmptyRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageEmpty,
                                                                             DiagnosticCategory.Usage,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca2243-attribute-string-literals-should-parse-correctly",
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, EmptyRule);

        private static readonly List<ValueValidator> s_tokensToValueValidator =
            new List<ValueValidator>(
                new[] { new ValueValidator(ImmutableArray.Create("guid"), "Guid", GuidValueValidator),
                        new ValueValidator(ImmutableArray.Create("url", "uri", "urn"), "Uri", UrlValueValidator, "UriTemplate")});

        private static bool GuidValueValidator(string value)
        {
            try
            {
                var unused = new Guid(value);
                return true;
            }
            catch (OverflowException)
            {
            }
            catch (FormatException)
            {
            }

            return false;
        }

        private static bool UrlValueValidator(string value)
        {
            return Uri.IsWellFormedUriString(value, System.UriKind.RelativeOrAbsolute);
        }

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterSymbolAction(saContext =>
            {
                var symbol = saContext.Symbol;
                AnalyzeSymbol(saContext.ReportDiagnostic, symbol);
                switch (symbol.Kind)
                {
                    case SymbolKind.NamedType:
                        {
                            var namedType = (INamedTypeSymbol)symbol;

                            AnalyzeSymbols(saContext.ReportDiagnostic, namedType.TypeParameters);

                            if (namedType.TypeKind == TypeKind.Delegate && namedType.DelegateInvokeMethod != null)
                            {
                                AnalyzeSymbols(saContext.ReportDiagnostic, namedType.DelegateInvokeMethod.Parameters);
                            }

                            return;
                        }

                    case SymbolKind.Method:
                        {
                            var methodSymbol = (IMethodSymbol)symbol;
                            if (!methodSymbol.IsAccessorMethod())
                            {
                                AnalyzeSymbols(saContext.ReportDiagnostic, methodSymbol.Parameters);
                                AnalyzeSymbols(saContext.ReportDiagnostic, methodSymbol.TypeParameters);
                            }

                            return;
                        }

                    case SymbolKind.Property:
                        {
                            var propertySymbol = (IPropertySymbol)symbol;
                            AnalyzeSymbols(saContext.ReportDiagnostic, propertySymbol.Parameters);
                            return;
                        }
                }
            },
            SymbolKind.NamedType,
            SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event);

            analysisContext.RegisterCompilationAction(caContext =>
            {
                var compilation = caContext.Compilation;
                AnalyzeSymbol(caContext.ReportDiagnostic, compilation.Assembly);
            });
        }

        private static void AnalyzeSymbols(Action<Diagnostic> reportDiagnostic, IEnumerable<ISymbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                AnalyzeSymbol(reportDiagnostic, symbol);
            }
        }

        private static void AnalyzeSymbol(Action<Diagnostic> reportDiagnostic, ISymbol symbol)
        {
            var attributes = symbol.GetAttributes();

            foreach (var attribute in attributes)
            {
                Analyze(reportDiagnostic, attribute);
            }
        }

        private static void Analyze(Action<Diagnostic> reportDiagnostic, AttributeData attributeData)
        {
            var attributeConstructor = attributeData.AttributeConstructor;
            var constructorArguments = attributeData.ConstructorArguments;

            if (attributeConstructor == null || !attributeConstructor.Parameters.HasExactly(constructorArguments.Count()))
            {
                return;
            }

            var syntax = attributeData.ApplicationSyntaxReference.GetSyntax();

            for (int i = 0; i < attributeConstructor.Parameters.Count(); i++)
            {
                var parameter = attributeConstructor.Parameters[i];
                if (parameter.Type.SpecialType != SpecialType.System_String)
                {
                    continue;
                }

                // If the name of the parameter is not something which requires the value-passed
                // to the parameter to be validated then we don't have to do anything
                var valueValidator = GetValueValidator(parameter.Name);
                if (valueValidator != null && !valueValidator.IsIgnoredName(parameter.Name))
                {
                    if (constructorArguments[i].Value != null)
                    {
                        var value = (string)constructorArguments[i].Value;
                        string classDisplayString = attributeData.AttributeClass.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat);
                        if (value.Length == 0)
                        {
                            reportDiagnostic(syntax.CreateDiagnostic(EmptyRule,
                                classDisplayString,
                                parameter.Name,
                                valueValidator.TypeName));
                        }
                        else if (!valueValidator.IsValidValue(value))
                        {
                            reportDiagnostic(syntax.CreateDiagnostic(DefaultRule,
                                classDisplayString,
                                parameter.Name,
                                value,
                                valueValidator.TypeName));
                        }
                    }
                }
            }

            foreach (var namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument.Value.IsNull ||
                    namedArgument.Value.Type.SpecialType != SpecialType.System_String)
                {
                    return;
                }

                var valueValidator = GetValueValidator(namedArgument.Key);
                if (valueValidator != null && !valueValidator.IsIgnoredName(namedArgument.Key))
                {
                    var value = (string)(namedArgument.Value.Value);
                    string classDisplayString = attributeData.AttributeClass.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat);
                    if (value.Length == 0)
                    {
                        reportDiagnostic(syntax.CreateDiagnostic(EmptyRule,
                            classDisplayString,
                            $"{classDisplayString}.{namedArgument.Key}",
                            valueValidator.TypeName));
                    }
                    else if (!valueValidator.IsValidValue(value))
                    {
                        reportDiagnostic(syntax.CreateDiagnostic(DefaultRule,
                            classDisplayString,
                            $"{classDisplayString}.{namedArgument.Key}",
                            value,
                            valueValidator.TypeName));
                    }
                }
            }
        }

        private static ValueValidator? GetValueValidator(string name)
        {
            foreach (var valueValidator in s_tokensToValueValidator)
            {
                if (WordParser.ContainsWord(name, WordParserOptions.SplitCompoundWords, valueValidator.AcceptedTokens))
                {
                    return valueValidator;
                }
            }

            return null;
        }
    }

    internal class ValueValidator
    {
        private readonly string? _ignoredName;

        public ImmutableArray<string> AcceptedTokens { get; }
        public string TypeName { get; }
        public Func<string, bool> IsValidValue { get; }

        public bool IsIgnoredName(string name)
        {
            return _ignoredName != null && string.Equals(_ignoredName, name, StringComparison.OrdinalIgnoreCase);
        }

        public ValueValidator(ImmutableArray<string> acceptedTokens, string typeName, Func<string, bool> isValidValue, string? ignoredName = null)
        {
            _ignoredName = ignoredName;

            AcceptedTokens = acceptedTokens;
            TypeName = typeName;
            IsValidValue = isValidValue;
        }
    }
}