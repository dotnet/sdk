// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseCancellationTokenThrowIfCancellationRequested : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2250";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedTitle), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedDescription), Resx.ResourceManager, typeof(Resx));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
                return;

            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Conditional);
            return;

            void AnalyzeOperation(OperationAnalysisContext context)
            {
                var conditional = (IConditionalOperation)context.Operation;

                if (symbols.IsSimpleAffirmativeCheck(conditional, out _) || symbols.IsNegatedCheckWithThrowingElseClause(conditional, out _))
                {
                    context.ReportDiagnostic(conditional.CreateDiagnostic(Rule));
                }
            }
        }

        /// <summary>
        /// If <paramref name="singleOrBlock"/> is a block operation with one child, returns that child.
        /// If <paramref name="singleOrBlock"/> is a block operation with more than one child, returns <see langword="null"/>.
        /// If <paramref name="singleOrBlock"/> is not a block operation, returns <paramref name="singleOrBlock"/>.
        /// </summary>
        /// <param name="singleOrBlock">The operation to unwrap.</param>
        internal static IOperation? GetSingleStatementOrDefault(IOperation? singleOrBlock)
        {
            if (singleOrBlock is IBlockOperation blockOperation)
            {
                return blockOperation.Operations.Length is 1 ? blockOperation.Operations[0] : default;
            }

            return singleOrBlock;
        }

        //  Use readonly struct to avoid allocations.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct RequiredSymbols
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public static bool TryGetSymbols(Compilation compilation, out RequiredSymbols symbols)
            {
                symbols = default;
                INamedTypeSymbol boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
                if (boolType is null)
                    return false;

                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken, out INamedTypeSymbol? cancellationTokenType))
                    return false;

                IMethodSymbol? throwIfCancellationRequestedMethod = cancellationTokenType.GetMembers(nameof(CancellationToken.ThrowIfCancellationRequested))
                    .OfType<IMethodSymbol>()
                    .GetFirstOrDefaultMemberWithParameterInfos();
                IPropertySymbol? isCancellationRequestedProperty = cancellationTokenType.GetMembers(nameof(CancellationToken.IsCancellationRequested))
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault();

                if (throwIfCancellationRequestedMethod is null || isCancellationRequestedProperty is null)
                    return false;

                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemOperationCanceledException, out INamedTypeSymbol? operationCanceledExceptionType))
                    return false;

                IMethodSymbol? operationCanceledExceptionDefaultCtor = operationCanceledExceptionType.InstanceConstructors
                    .GetFirstOrDefaultMemberWithParameterInfos();
                IMethodSymbol? operationCanceledExceptionTokenCtor = operationCanceledExceptionType.InstanceConstructors
                    .GetFirstOrDefaultMemberWithParameterInfos(ParameterInfo.GetParameterInfo(cancellationTokenType));

                if (operationCanceledExceptionDefaultCtor is null || operationCanceledExceptionTokenCtor is null)
                    return false;

                symbols = new RequiredSymbols
                {
                    IsCancellationRequestedProperty = isCancellationRequestedProperty,
                    OperationCanceledExceptionDefaultCtor = operationCanceledExceptionDefaultCtor,
                    OperationCanceledExceptionTokenCtor = operationCanceledExceptionTokenCtor
                };

                return true;
            }

            public IPropertySymbol IsCancellationRequestedProperty { get; init; }
            public IMethodSymbol OperationCanceledExceptionDefaultCtor { get; init; }
            public IMethodSymbol OperationCanceledExceptionTokenCtor { get; init; }

            /// <summary>
            /// Indicates whether the specified operation is a conditional statement of the form
            /// <code>
            /// if (token.IsCancellationRequested)
            ///     throw new OperationCanceledException();
            /// </code>
            /// </summary>
            public bool IsSimpleAffirmativeCheck(IConditionalOperation conditional, [NotNullWhen(true)] out IPropertyReferenceOperation? isCancellationRequestedPropertyReference)
            {
                IOperation? whenTrueUnwrapped = GetSingleStatementOrDefault(conditional.WhenTrue);

                if (conditional.Condition is IPropertyReferenceOperation propertyReference &&
                    SymbolEqualityComparer.Default.Equals(propertyReference.Property, IsCancellationRequestedProperty) &&
                    whenTrueUnwrapped is IThrowOperation @throw &&
                    @throw.Exception is IObjectCreationOperation objectCreation &&
                    IsDefaultOrTokenOperationCanceledExceptionCtor(objectCreation.Constructor))
                {
                    isCancellationRequestedPropertyReference = propertyReference;
                    return true;
                }

                isCancellationRequestedPropertyReference = default;
                return false;
            }

            /// <summary>
            /// Indicates whether the specified operation is a conditional statement of the form
            /// <code>
            /// if (!token.IsCancellationRequested)
            /// {
            ///     // statements
            /// }
            /// else
            /// {
            ///     throw new OperationCanceledException();
            /// }
            /// </code>
            /// </summary>
            public bool IsNegatedCheckWithThrowingElseClause(IConditionalOperation conditional, [NotNullWhen(true)] out IPropertyReferenceOperation? isCancellationRequestedPropertyReference)
            {
                IOperation? whenFalseUnwrapped = GetSingleStatementOrDefault(conditional.WhenFalse);

                if (conditional.Condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary &&
                    unary.Operand is IPropertyReferenceOperation propertyReference &&
                    SymbolEqualityComparer.Default.Equals(propertyReference.Property, IsCancellationRequestedProperty) &&
                    whenFalseUnwrapped is IThrowOperation @throw &&
                    @throw.Exception is IObjectCreationOperation objectCreation &&
                    IsDefaultOrTokenOperationCanceledExceptionCtor(objectCreation.Constructor))
                {
                    isCancellationRequestedPropertyReference = propertyReference;
                    return true;
                }

                isCancellationRequestedPropertyReference = default;
                return false;
            }

            private bool IsDefaultOrTokenOperationCanceledExceptionCtor(IMethodSymbol method)
            {
                return SymbolEqualityComparer.Default.Equals(method, OperationCanceledExceptionDefaultCtor) ||
                    SymbolEqualityComparer.Default.Equals(method, OperationCanceledExceptionTokenCtor);
            }
        }
    }
}
