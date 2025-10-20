// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1863: <inheritdoc cref="UseCompositeFormatTitle"/>
    /// </summary>
    /// <remarks>
    /// Roslyn already provides a refactoring for finding string.Format calls with literal string formats
    /// and converting them to use string interpolation. This analyzer instead focuses on non-literal / const
    /// arguments.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseCompositeFormatAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor UseCompositeFormatRule = DiagnosticDescriptorHelper.Create("CA1863",
            CreateLocalizableResourceString(nameof(UseCompositeFormatTitle)),
            CreateLocalizableResourceString(nameof(UseCompositeFormatMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeHidden_BulkConfigurable,
            CreateLocalizableResourceString(nameof(UseCompositeFormatDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal const string StringIndexPropertyName = "StringIndex";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(UseCompositeFormatRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol stringType = compilationContext.Compilation.GetSpecialType(SpecialType.System_String);

                // Get the types for CompositeFormat, IFormatProvider, and StringBuilder. If we can't, bail.
                if (!compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextCompositeFormat, out INamedTypeSymbol? compositeFormatType) ||
                    !compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIFormatProvider, out INamedTypeSymbol? formatProviderType) ||
                    !compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextStringBuilder, out INamedTypeSymbol? stringBuilderType))
                {
                    return;
                }

                // Process all calls to string.Format, assuming we can find all the members we'd use as replacements.
                IMethodSymbol[] formatCompositeMethods = stringType.GetMembers("Format").OfType<IMethodSymbol>()
                    .Where(m => m.IsStatic &&
                                m.Parameters.Length >= 3 &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, formatProviderType) &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, compositeFormatType)).ToArray();
                if (HasAllCompositeFormatMethods(formatCompositeMethods))
                {
                    compilationContext.RegisterOperationAction(
                        CreateAnalysisAction(isStatic: true, stringType, "Format", formatProviderType), OperationKind.Invocation);
                }

                // Process all calls to StringBuilder.AppendFormat, assuming we can find all the members we'd use as replacements.
                IMethodSymbol[] appendFormatCompositeMethods = stringBuilderType.GetMembers("AppendFormat").OfType<IMethodSymbol>()
                    .Where(m => !m.IsStatic &&
                                m.Parameters.Length >= 3 &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, formatProviderType) &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, compositeFormatType)).ToArray();
                if (HasAllCompositeFormatMethods(appendFormatCompositeMethods))
                {
                    compilationContext.RegisterOperationAction(
                        CreateAnalysisAction(isStatic: false, stringBuilderType, "AppendFormat", formatProviderType), OperationKind.Invocation);
                }
            });
        }

        /// <summary>Creates a delegate to register with RegisterOperationAction and that flags all of the relevant format string parameters that warrant replacing.</summary>
        /// <param name="isStatic">Whether the target methods are static; true for string.Format, false for StringBuilder.AppendFormat.</param>
        /// <param name="containingType">The symbol for the containing type, either for string or StringBuilder.</param>
        /// <param name="methodName">The name of the target method, either "Format" or "AppendFormat".</param>
        /// <param name="formatProviderType">The symbol for IFormatProvider.</param>
        /// <returns></returns>
        private static Action<OperationAnalysisContext> CreateAnalysisAction(bool isStatic, ITypeSymbol containingType, string methodName, ITypeSymbol formatProviderType)
        {
            return operationContext =>
            {
                IInvocationOperation invocation = (IInvocationOperation)operationContext.Operation;
                IMethodSymbol targetMethod = invocation.TargetMethod;

                // Much match the specified method shape
                if (targetMethod.IsStatic != isStatic ||
                    !SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, containingType) ||
                    targetMethod.Name != methodName)
                {
                    return;
                }

                // Must accept a string format rather than CompositFormat format
                int stringIndex;
                ImmutableArray<IParameterSymbol> parameters = targetMethod.Parameters;
                if (parameters.Length >= 1 && parameters[0].Type.SpecialType == SpecialType.System_String)
                {
                    stringIndex = 0;
                }
                else if (parameters.Length >= 2 &&
                         parameters[1].Type.SpecialType == SpecialType.System_String &&
                         SymbolEqualityComparer.Default.Equals(parameters[0].Type, formatProviderType))
                {
                    stringIndex = 1;
                }
                else
                {
                    return;
                }

                // Get the argument for the format string
                if (!invocation.Arguments.TryGetArgumentForParameterAtIndex(stringIndex, out IArgumentOperation? arg))
                {
                    return;
                }

                // If the argument contains anything that references local state, we can't recommend extracting that out
                // into a statically-cached CompositeFormat.  We instead stick to the easy cases which should also be the
                // most common, e.g. literals, static references, etc.
                IOperation stringArg = arg.Value.WalkDownConversion();
                if (IsStringLiteralOrStaticReference(stringArg))
                {
                    // If the expression is a static reference, we can replace just the format string argument
                    // with a CompositeFormat, so report the diagnostic on just that argument.
                    operationContext.ReportDiagnostic(stringArg.CreateDiagnostic(
                        UseCompositeFormatRule,
                        properties: ImmutableDictionary<string, string?>.Empty.Add(StringIndexPropertyName, stringIndex.ToString())));
                }
            };
        }

        /// <summary>Determines whether the expression is something trivially lifted out of the member body.</summary>
        private static bool IsStringLiteralOrStaticReference(IOperation operation, bool allowLiteral = false)
        {
            if (operation.Type?.SpecialType != SpecialType.System_String)
            {
                return false;
            }

            if (operation.Kind == OperationKind.Literal)
            {
                return allowLiteral;
            }

            if (operation.Kind == OperationKind.FieldReference)
            {
                return ((IFieldReferenceOperation)operation).Field.IsStatic;
            }

            if (operation.Kind == OperationKind.PropertyReference)
            {
                return ((IPropertyReferenceOperation)operation).Property.IsStatic;
            }

            if (operation.Kind == OperationKind.Invocation)
            {
                IInvocationOperation invocation = (IInvocationOperation)operation;
                if (invocation.TargetMethod.IsStatic)
                {
                    foreach (IArgumentOperation? arg in invocation.Arguments)
                    {
                        if (!arg.ConstantValue.HasValue && !IsStringLiteralOrStaticReference(arg.Value, allowLiteral: true))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>Validates that all of the required CompositeFormat-based methods exist in the specified set.</summary>
        private static bool HasAllCompositeFormatMethods(IMethodSymbol[] methods)
        {
            // (IFormatProvider, CompositeFormat, T1)
            if (!methods.Any(m => m.IsGenericMethod &&
                                  m.Parameters.Length == 3 &&
                                  m.TypeParameters.Length == 1 &&
                                  SymbolEqualityComparer.Default.Equals(m.TypeParameters[0], m.Parameters[2].Type)))
            {
                return false;
            }

            // (IFormatProvider, CompositeFormat, T1, T2)
            if (!methods.Any(m => m.IsGenericMethod &&
                                  m.Parameters.Length == 4 &&
                                  m.TypeParameters.Length == 2 &&
                                  SymbolEqualityComparer.Default.Equals(m.TypeParameters[0], m.Parameters[2].Type) &&
                                  SymbolEqualityComparer.Default.Equals(m.TypeParameters[1], m.Parameters[3].Type)))
            {
                return false;
            }

            // (IFormatProvider, CompositeFormat, T1, T2, T3)
            if (!methods.Any(m => m.IsGenericMethod &&
                                  m.Parameters.Length == 5 &&
                                  m.TypeParameters.Length == 3 &&
                                  SymbolEqualityComparer.Default.Equals(m.TypeParameters[0], m.Parameters[2].Type) &&
                                  SymbolEqualityComparer.Default.Equals(m.TypeParameters[1], m.Parameters[3].Type) &&
                                  SymbolEqualityComparer.Default.Equals(m.TypeParameters[2], m.Parameters[4].Type)))
            {
                return false;
            }

            // (IFormatProvider, CompositeFormat, object[])
            if (!methods.Any(m => m.Parameters.Length == 3 &&
                                  m.Parameters[2].Type.Kind == SymbolKind.ArrayType))
            {
                return false;
            }

            // All relevant methods exist.
            return true;
        }
    }
}