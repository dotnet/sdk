// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseDSA : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5384";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseDSA),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseDSAMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseDSADescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_Description,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly ImmutableHashSet<string> s_DSAAlgorithmNames =
            ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                "DSA",
                "System.Security.Cryptography.DSA");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemSecurityCryptographyDSA,
                    out var dsaTypeSymbol);
                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemSecurityCryptographyAsymmetricAlgorithm,
                    out var asymmetricAlgorithmTypeSymbol);
                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemSecurityCryptographyCryptoConfig,
                    out var cryptoConfigTypeSymbol);

                if (dsaTypeSymbol == null &&
                    asymmetricAlgorithmTypeSymbol == null &&
                    cryptoConfigTypeSymbol == null)
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var objectCreationOperation = (IObjectCreationOperation)operationAnalysisContext.Operation;
                    var typeSymbol = objectCreationOperation.Constructor.ContainingType;

                    if (typeSymbol == null)
                    {
                        return;
                    }

                    var baseTypesAndThis = typeSymbol.GetBaseTypesAndThis();

                    if (dsaTypeSymbol != null && baseTypesAndThis.Contains(dsaTypeSymbol))
                    {
                        operationAnalysisContext.ReportDiagnostic(
                            objectCreationOperation.CreateDiagnostic(
                                Rule,
                                typeSymbol.Name));
                    }
                }, OperationKind.ObjectCreation);

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var returnOperation = (IReturnOperation)operationAnalysisContext.Operation;
                    var typeSymbol = returnOperation.ReturnedValue?.Type;

                    if (typeSymbol == null)
                    {
                        return;
                    }

                    var baseTypesAndThis = typeSymbol.GetBaseTypesAndThis();

                    if (dsaTypeSymbol != null && baseTypesAndThis.Contains(dsaTypeSymbol))
                    {
                        operationAnalysisContext.ReportDiagnostic(
                            returnOperation.CreateDiagnostic(
                                Rule,
                                typeSymbol.Name));
                    }
                }, OperationKind.Return);

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var methodSymbol = invocationOperation.TargetMethod;
                    var typeSymbol = methodSymbol.ContainingType;

                    if (typeSymbol == null)
                    {
                        return;
                    }

                    var methodName = methodSymbol.Name;
                    var arguments = invocationOperation.Arguments;

                    if (methodName == "Create" &&
                        typeSymbol.Equals(asymmetricAlgorithmTypeSymbol) &&
                        !arguments.IsEmpty &&
                        arguments[0].Parameter.Type.SpecialType == SpecialType.System_String &&
                        arguments[0].Value.ConstantValue.HasValue)
                    {
                        var argValue = arguments[0].Value.ConstantValue.Value;

                        if (s_DSAAlgorithmNames.Contains(argValue.ToString()))
                        {
                            operationAnalysisContext.ReportDiagnostic(
                                invocationOperation.CreateDiagnostic(
                                    Rule,
                                    argValue));
                        }
                    }
                    else if (methodName == "CreateFromName" &&
                            typeSymbol.Equals(cryptoConfigTypeSymbol) &&
                            !arguments.IsEmpty &&
                            arguments[0].Parameter.Type.SpecialType == SpecialType.System_String &&
                            arguments[0].Value.ConstantValue.HasValue)
                    {
                        var argValue = arguments[0].Value.ConstantValue.Value;

                        if (s_DSAAlgorithmNames.Contains(argValue.ToString()))
                        {
                            operationAnalysisContext.ReportDiagnostic(
                                invocationOperation.CreateDiagnostic(
                                    Rule,
                                    argValue));
                        }
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
