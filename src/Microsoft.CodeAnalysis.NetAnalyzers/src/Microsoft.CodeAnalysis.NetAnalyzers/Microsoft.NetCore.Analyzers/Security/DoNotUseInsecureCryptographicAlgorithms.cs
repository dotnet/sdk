// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.NetCore.Analyzers.Security.Helpers;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5350: <inheritdoc cref="DoNotUseBrokenCryptographicAlgorithms"/>
    /// CA5351: <inheritdoc cref="DoNotUseWeakCryptographicAlgorithms"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseInsecureCryptographicAlgorithmsAnalyzer : DiagnosticAnalyzer
    {
        internal const string DoNotUseWeakCryptographyRuleId = "CA5350";
        internal const string DoNotUseBrokenCryptographyRuleId = "CA5351";

        internal static readonly DiagnosticDescriptor DoNotUseBrokenCryptographyRule =
            DiagnosticDescriptorHelper.Create(
                DoNotUseBrokenCryptographyRuleId,
                CreateLocalizableResourceString(nameof(DoNotUseBrokenCryptographicAlgorithms)),
                CreateLocalizableResourceString(nameof(DoNotUseBrokenCryptographicAlgorithmsMessage)),
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: CreateLocalizableResourceString(nameof(DoNotUseBrokenCryptographicAlgorithmsDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal static readonly DiagnosticDescriptor DoNotUseWeakCryptographyRule =
            DiagnosticDescriptorHelper.Create(
                DoNotUseWeakCryptographyRuleId,
                CreateLocalizableResourceString(nameof(DoNotUseWeakCryptographicAlgorithms)),
                CreateLocalizableResourceString(nameof(DoNotUseWeakCryptographicAlgorithmsMessage)),
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: CreateLocalizableResourceString(nameof(DoNotUseWeakCryptographicAlgorithmsDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DoNotUseBrokenCryptographyRule, DoNotUseWeakCryptographyRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext context) =>
                {
                    var cryptTypes = new CompilationSecurityTypes(context.Compilation);
                    if (!ReferencesAnyTargetType(cryptTypes))
                    {
                        return;
                    }

                    context.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IMethodSymbol method;

                            switch (operationAnalysisContext.Operation)
                            {
                                case IInvocationOperation invocationOperation:
                                    method = invocationOperation.TargetMethod;
                                    break;
                                case IObjectCreationOperation objectCreationOperation:
                                    if (objectCreationOperation.Constructor == null)
                                        return;
                                    method = objectCreationOperation.Constructor;
                                    break;
                                default:
                                    Debug.Fail($"Unhandled IOperation {operationAnalysisContext.Operation.Kind}");
                                    return;
                            }

                            INamedTypeSymbol type = method.ContainingType;
                            DiagnosticDescriptor rule;
                            string algorithmName;

                            if (type.DerivesFrom(cryptTypes.MD5))
                            {
                                rule = DoNotUseBrokenCryptographyRule;
                                algorithmName = cryptTypes.MD5.Name;
                            }
                            else if (type.DerivesFrom(cryptTypes.SHA1))
                            {
                                rule = DoNotUseWeakCryptographyRule;
                                algorithmName = cryptTypes.SHA1.Name;
                            }
                            else if (type.DerivesFrom(cryptTypes.HMACSHA1))
                            {
                                rule = DoNotUseWeakCryptographyRule;
                                algorithmName = cryptTypes.HMACSHA1.Name;
                            }
                            else if (type.DerivesFrom(cryptTypes.DES))
                            {
                                rule = DoNotUseBrokenCryptographyRule;
                                algorithmName = cryptTypes.DES.Name;
                            }
                            else if ((method.ContainingType.DerivesFrom(cryptTypes.DSA)
                                      && method.MetadataName == SecurityMemberNames.CreateSignature)
                                || (type.Equals(cryptTypes.DSASignatureFormatter)
                                    && method.ContainingType.DerivesFrom(cryptTypes.DSASignatureFormatter)
                                    && method.MetadataName == WellKnownMemberNames.InstanceConstructorName))
                            {
                                rule = DoNotUseBrokenCryptographyRule;
                                algorithmName = "DSA";
                            }
                            else if (type.DerivesFrom(cryptTypes.HMACMD5))
                            {
                                rule = DoNotUseBrokenCryptographyRule;
                                algorithmName = cryptTypes.HMACMD5.Name;
                            }
                            else if (type.DerivesFrom(cryptTypes.RC2))
                            {
                                rule = DoNotUseBrokenCryptographyRule;
                                algorithmName = cryptTypes.RC2.Name;
                            }
                            else if (type.DerivesFrom(cryptTypes.TripleDES))
                            {
                                rule = DoNotUseWeakCryptographyRule;
                                algorithmName = cryptTypes.TripleDES.Name;
                            }
                            else if (type.DerivesFrom(cryptTypes.RIPEMD160))
                            {
                                rule = DoNotUseWeakCryptographyRule;
                                algorithmName = cryptTypes.RIPEMD160.Name;
                            }
                            else if (type.DerivesFrom(cryptTypes.HMACRIPEMD160))
                            {
                                rule = DoNotUseWeakCryptographyRule;
                                algorithmName = cryptTypes.HMACRIPEMD160.Name;
                            }
                            else
                            {
                                return;
                            }

                            operationAnalysisContext.ReportDiagnostic(
                                Diagnostic.Create(
                                    rule,
                                    operationAnalysisContext.Operation.Syntax.GetLocation(),
                                    operationAnalysisContext.ContainingSymbol.Name,
                                    algorithmName));
                        },
                        OperationKind.Invocation,
                        OperationKind.ObjectCreation);
                });
        }

        private static bool ReferencesAnyTargetType(CompilationSecurityTypes types)
        {
            return types.MD5 != null
                || types.SHA1 != null
                || types.HMACSHA1 != null
                || types.DES != null
                || types.DSA != null
                || types.DSASignatureFormatter != null
                || types.HMACMD5 != null
                || types.RC2 != null
                || types.TripleDES != null
                || types.RIPEMD160 != null
                || types.HMACRIPEMD160 != null;
        }
    }
}

