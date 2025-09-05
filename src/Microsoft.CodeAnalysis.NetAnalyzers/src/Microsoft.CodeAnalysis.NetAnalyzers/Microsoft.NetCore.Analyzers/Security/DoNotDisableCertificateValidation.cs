// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5359: <inheritdoc cref="DoNotDisableCertificateValidation"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDisableCertificateValidation : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5359";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(DoNotDisableCertificateValidation)),
            CreateLocalizableResourceString(nameof(DoNotDisableCertificateValidationMessage)),
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(DoNotDisableCertificateValidationDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    var compilation = compilationStartAnalysisContext.Compilation;
                    var systemNetSecurityRemoteCertificateValidationCallbackTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNetSecurityRemoteCertificateValidationCallback);
                    var obj = compilation.GetSpecialType(SpecialType.System_Object);
                    var x509Certificate = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyX509CertificatesX509Certificate);
                    var x509Chain = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyX509CertificatesX509Chain);
                    var sslPolicyErrors = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNetSecuritySslPolicyErrors);

                    if (systemNetSecurityRemoteCertificateValidationCallbackTypeSymbol == null
                        || obj == null
                        || x509Certificate == null
                        || x509Chain == null
                        || sslPolicyErrors == null)
                    {
                        return;
                    }

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            var delegateCreationOperation =
                                (IDelegateCreationOperation)operationAnalysisContext.Operation;

                            if (systemNetSecurityRemoteCertificateValidationCallbackTypeSymbol.Equals(delegateCreationOperation.Type))
                            {
                                var alwaysReturnTrue = false;

                                switch (delegateCreationOperation.Target.Kind)
                                {
                                    case OperationKind.AnonymousFunction:
                                        if (!IsCertificateValidationFunction(
                                            ((IAnonymousFunctionOperation)delegateCreationOperation.Target).Symbol,
                                            obj,
                                            x509Certificate,
                                            x509Chain,
                                            sslPolicyErrors))
                                        {
                                            return;
                                        }

                                        alwaysReturnTrue = AlwaysReturnTrue(delegateCreationOperation.Target.Descendants());
                                        break;

                                    case OperationKind.MethodReference:
                                        var methodReferenceOperation = (IMethodReferenceOperation)delegateCreationOperation.Target;
                                        var methodSymbol = methodReferenceOperation.Method;

                                        if (!IsCertificateValidationFunction(
                                            methodSymbol,
                                            obj,
                                            x509Certificate,
                                            x509Chain,
                                            sslPolicyErrors))
                                        {
                                            return;
                                        }

                                        var blockOperation = methodSymbol.GetTopmostOperationBlock(compilation);

                                        if (blockOperation == null)
                                        {
                                            return;
                                        }

                                        var targetOperations = blockOperation.Descendants().ToImmutableArray().WithoutFullyImplicitOperations();
                                        alwaysReturnTrue = AlwaysReturnTrue(targetOperations);
                                        break;
                                }

                                if (alwaysReturnTrue)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        delegateCreationOperation.CreateDiagnostic(
                                            Rule));
                                }
                            }
                        },
                        OperationKind.DelegateCreation);
                });
        }

        private static bool IsCertificateValidationFunction(IMethodSymbol methodSymbol, INamedTypeSymbol obj, INamedTypeSymbol x509Certificate, INamedTypeSymbol x509Chain, INamedTypeSymbol sslPolicyErrors)
        {
            if (methodSymbol.ReturnType.SpecialType != SpecialType.System_Boolean)
            {
                return false;
            }

            var parameters = methodSymbol.Parameters;

            if (parameters.Length != 4)
            {
                return false;
            }

            if (!parameters[0].Type.Equals(obj)
                || !parameters[1].Type.Equals(x509Certificate)
                || !parameters[2].Type.Equals(x509Chain)
                || !parameters[3].Type.Equals(sslPolicyErrors))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Find every IReturnOperation in the method and get the value of return statement to determine if the method always return true.
        /// </summary>
        /// <param name="operations">A method body in the form of explicit IOperations</param>
        private static bool AlwaysReturnTrue(IEnumerable<IOperation> operations)
        {
            var hasReturnStatement = false;

            foreach (var descendant in operations)
            {
                if (descendant.Kind == OperationKind.Return)
                {
                    var returnOperation = (IReturnOperation)descendant;

                    if (returnOperation.ReturnedValue == null)
                    {
                        return false;
                    }

                    var constantValue = returnOperation.ReturnedValue.ConstantValue;
                    hasReturnStatement = true;

                    // Check if the value being returned is a compile time constant 'true'
                    if (!constantValue.HasValue ||
                        constantValue.Value is not bool value ||
                        !value)
                    {
                        return false;
                    }
                }
            }

            return hasReturnStatement;
        }
    }
}
