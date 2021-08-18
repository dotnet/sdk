// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseRSAWithSufficientKeySize : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5385";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseRSAWithSufficientKeySize),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseRSAWithSufficientKeySizeMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseRSAWithSufficientKeySizeDescription),
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

        private static readonly ImmutableHashSet<string> s_RSAAlgorithmNames =
            ImmutableHashSet.Create(
                StringComparer.OrdinalIgnoreCase,
                "RSA",
                "System.Security.Cryptography.RSA",
                "System.Security.Cryptography.AsymmetricAlgorithm");

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
                    WellKnownTypeNames.SystemSecurityCryptographyRSA,
                    out var rsaTypeSymbol);
                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemSecurityCryptographyAsymmetricAlgorithm,
                    out var asymmetricAlgorithmTypeSymbol);
                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemSecurityCryptographyCryptoConfig,
                    out var cryptoConfigTypeSymbol);

                if (rsaTypeSymbol == null &&
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

                    if (rsaTypeSymbol != null && baseTypesAndThis.Contains(rsaTypeSymbol))
                    {
                        var arguments = objectCreationOperation.Arguments;

                        if (arguments.Length == 1 &&
                            arguments[0].Parameter.Type.SpecialType == SpecialType.System_Int32 &&
                            arguments[0].Value.ConstantValue.HasValue &&
                            Convert.ToInt32(arguments[0].Value.ConstantValue.Value, CultureInfo.InvariantCulture) < 2048)
                        {
                            operationAnalysisContext.ReportDiagnostic(
                                objectCreationOperation.CreateDiagnostic(
                                    Rule,
                                    typeSymbol.Name));
                        }
                    }
                }, OperationKind.ObjectCreation);

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
                        arguments.IsEmpty)
                    {
                        // Use AsymmetricAlgorithm.Create() to create RSA and the default key size is 1024.
                        operationAnalysisContext.ReportDiagnostic(
                                invocationOperation.CreateDiagnostic(
                                    Rule,
                                    "RSA"));
                    }
                    else if (methodName == "Create" &&
                            typeSymbol.Equals(asymmetricAlgorithmTypeSymbol) &&
                            !arguments.IsEmpty &&
                            arguments[0].Parameter.Type.SpecialType == SpecialType.System_String &&
                            arguments[0].Value.ConstantValue.HasValue)
                    {
                        var argValue = arguments[0].Value.ConstantValue.Value;

                        if (s_RSAAlgorithmNames.Contains(argValue.ToString()))
                        {
                            // Use AsymmetricAlgorithm.Create(string) to create RSA and the default key size is 1024.
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
                        // Use CryptoConfig.CreateFromName(string, ...).
                        var argValue = arguments[0].Value.ConstantValue.Value;

                        if (s_RSAAlgorithmNames.Contains(argValue.ToString()))
                        {
                            // Create RSA.
                            if (arguments.Length == 1 /* The default key size is 1024 */ ||
                                arguments[1].Value is IArrayCreationOperation arrayCreationOperation /* Use CryptoConfig.CreateFromName(string, object[]) to create RSA */&&
                                arrayCreationOperation.DimensionSizes[0].ConstantValue.Value.Equals(1) &&
                                arrayCreationOperation.Initializer.ElementValues.Any(
                                    s => s is IConversionOperation conversionOperation &&
                                        conversionOperation.Operand.ConstantValue.HasValue &&
                                        Convert.ToInt32(conversionOperation.Operand.ConstantValue.Value, CultureInfo.InvariantCulture) < 2048) /* Specify the key size is smaller than 2048 explicitly */ )
                            {
                                operationAnalysisContext.ReportDiagnostic(
                                invocationOperation.CreateDiagnostic(
                                    Rule,
                                    argValue));
                            }
                        }
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
