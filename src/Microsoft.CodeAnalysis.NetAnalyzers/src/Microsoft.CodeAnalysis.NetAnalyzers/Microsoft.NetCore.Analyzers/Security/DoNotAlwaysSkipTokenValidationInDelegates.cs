// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5405: <inheritdoc cref="DoNotAlwaysSkipTokenValidationInDelegatesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotAlwaysSkipTokenValidationInDelegates : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5405";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                CreateLocalizableResourceString(nameof(DoNotAlwaysSkipTokenValidationInDelegatesTitle)),
                CreateLocalizableResourceString(nameof(DoNotAlwaysSkipTokenValidationInDelegatesMessage)),
                DiagnosticCategory.Security,
                RuleLevel.Disabled,
                description: CreateLocalizableResourceString(nameof(DoNotAlwaysSkipTokenValidationInDelegatesDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;
                var microsoftIdentityModelTokensAudienceValidatorTypeSymbol =
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftIdentityModelTokensAudienceValidator);
                var microsoftIdentityModelTokensLifetimeValidatorTypeSymbol =
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftIdentityModelTokensLifetimeValidator);
                var nullableDateTime = compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(compilation.GetSpecialType(SpecialType.System_DateTime));
                var stringSymbol = compilation.GetSpecialType(SpecialType.System_String);
                var ienumString = compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T).Construct(stringSymbol);
                var securityToken = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftIdentityModelTokensSecurityToken);
                var tokenValidationParameters = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftIdentityModelTokensTokenValidationParameters);

                if ((microsoftIdentityModelTokensAudienceValidatorTypeSymbol == null
                    || securityToken == null
                    || tokenValidationParameters == null) &&
                    (microsoftIdentityModelTokensLifetimeValidatorTypeSymbol == null
                    || securityToken == null
                    || tokenValidationParameters == null))
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var delegateCreationOperation = (IDelegateCreationOperation)context.Operation;
                    Func<IMethodSymbol, bool> isCorrectFunction;

                    var alwaysReturnTrue = false;

                    if (microsoftIdentityModelTokensAudienceValidatorTypeSymbol != null &&
                        microsoftIdentityModelTokensAudienceValidatorTypeSymbol.Equals(delegateCreationOperation.Type))
                    {
                        isCorrectFunction = (method) => IsAudienceValidatorFunction(method, ienumString, securityToken, tokenValidationParameters);
                    }
                    else if (microsoftIdentityModelTokensLifetimeValidatorTypeSymbol != null &&
                                microsoftIdentityModelTokensLifetimeValidatorTypeSymbol.Equals(delegateCreationOperation.Type))
                    {
                        isCorrectFunction = (method) => IsLifetimeValidatorFunction(method, nullableDateTime, securityToken, tokenValidationParameters);
                    }
                    else
                    {
                        return;
                    }

                    switch (delegateCreationOperation.Target.Kind)
                    {
                        case OperationKind.AnonymousFunction:
                            if (!isCorrectFunction(((IAnonymousFunctionOperation)delegateCreationOperation.Target).Symbol))
                            {
                                return;
                            }

                            alwaysReturnTrue = AlwaysReturnTrue(delegateCreationOperation.Target.Descendants());
                            break;

                        case OperationKind.MethodReference:
                            var methodReferenceOperation = (IMethodReferenceOperation)delegateCreationOperation.Target;
                            var methodSymbol = methodReferenceOperation.Method;

                            if (!isCorrectFunction(methodSymbol))
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

                        default:
                            Debug.Fail("Unhandled OperationKind " + delegateCreationOperation.Target.Kind);
                            break;
                    }

                    if (alwaysReturnTrue)
                    {
                        context.ReportDiagnostic(
                            delegateCreationOperation.CreateDiagnostic(
                                Rule,
                                delegateCreationOperation.Type.Name));
                    }
                },
                OperationKind.DelegateCreation);
            });
        }

        private static bool IsAudienceValidatorFunction(
            IMethodSymbol methodSymbol,
            INamedTypeSymbol iEnumerableString,
            INamedTypeSymbol securityToken,
            INamedTypeSymbol tokenValidationParameters)
        {
            if (methodSymbol.ReturnType.SpecialType != SpecialType.System_Boolean)
            {
                return false;
            }

            var parameters = methodSymbol.Parameters;

            if (parameters.Length != 3)
            {
                return false;
            }

            if (!parameters[0].Type.Equals(iEnumerableString)
                || !parameters[1].Type.Equals(securityToken)
                || !parameters[2].Type.Equals(tokenValidationParameters))
            {
                return false;
            }

            return true;
        }

        private static bool IsLifetimeValidatorFunction(
            IMethodSymbol methodSymbol,
            INamedTypeSymbol nullableDateTime,
            INamedTypeSymbol securityToken,
            INamedTypeSymbol tokenValidationParameters)
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

            if (!parameters[0].Type.Equals(nullableDateTime)
                || !parameters[1].Type.Equals(nullableDateTime)
                || !parameters[2].Type.Equals(securityToken)
                || !parameters[3].Type.Equals(tokenValidationParameters))
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
