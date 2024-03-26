// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2022: <inheritdoc cref="AvoidUnreliableStreamReadTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidUnreliableStreamReadAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2022";

        private const string Read = nameof(Read);
        private const string ReadAsync = nameof(ReadAsync);

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidUnreliableStreamReadTitle)),
            CreateLocalizableResourceString(nameof(AvoidUnreliableStreamReadMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(AvoidUnreliableStreamReadDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
            {
                return;
            }

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

            void AnalyzeInvocation(OperationAnalysisContext context)
            {
                var invocation = (IInvocationOperation)context.Operation;

                if (symbols.IsKnownReliableStreamType(invocation.GetInstanceType()))
                {
                    return;
                }

                if (symbols.IsAnyStreamReadMethod(invocation.TargetMethod))
                {
                    if (invocation.Parent is not IExpressionStatementOperation)
                    {
                        return;
                    }
                }
                else if (symbols.IsAnyStreamReadAsyncMethod(invocation.TargetMethod))
                {
                    if (invocation.Parent is not IAwaitOperation awaitOperation ||
                        awaitOperation.Parent is not IExpressionStatementOperation)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }

                context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, invocation.TargetMethod.ToDisplayString()));
            }
        }

        internal sealed class RequiredSymbols
        {
            private RequiredSymbols(
                ImmutableArray<IMethodSymbol> streamReadMethods,
                ImmutableArray<IMethodSymbol> streamReadAsyncMethods,
                ImmutableHashSet<ITypeSymbol> knownReliableStreamTypes)
            {
                _streamReadMethods = streamReadMethods;
                _streamReadAsyncMethods = streamReadAsyncMethods;
                _knownReliableStreamTypes = knownReliableStreamTypes;
            }

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var streamType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOStream);

                if (streamType is null)
                {
                    return false;
                }

                var streamReadMethods = streamType.GetMembers(Read)
                    .OfType<IMethodSymbol>()
                    .ToImmutableArray();
                var streamReadAsyncMethods = streamType.GetMembers(ReadAsync)
                    .OfType<IMethodSymbol>()
                    .ToImmutableArray();

                if (streamReadMethods.IsEmpty && streamReadAsyncMethods.IsEmpty)
                {
                    return false;
                }

                var knownReliableStreamTypesBuilder = ImmutableHashSet.CreateBuilder<ITypeSymbol>();
                knownReliableStreamTypesBuilder.AddIfNotNull(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOMemoryStream));
                knownReliableStreamTypesBuilder.AddIfNotNull(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOUnmanagedMemoryStream));

                symbols = new RequiredSymbols(streamReadMethods, streamReadAsyncMethods, knownReliableStreamTypesBuilder.ToImmutable());

                return true;
            }

            public bool IsAnyStreamReadMethod(IMethodSymbol method)
            {
                return _streamReadMethods.Any(m =>
                    SymbolEqualityComparer.Default.Equals(method, m) || IsOverrideOf(method, m));
            }

            public bool IsAnyStreamReadAsyncMethod(IMethodSymbol method)
            {
                return _streamReadAsyncMethods.Any(m =>
                    SymbolEqualityComparer.Default.Equals(method, m) || IsOverrideOf(method, m));
            }

            public bool IsKnownReliableStreamType(ITypeSymbol? type)
            {
                return _knownReliableStreamTypes.Any(t => SymbolEqualityComparer.Default.Equals(type, t));
            }

            private static bool IsOverrideOf(IMethodSymbol method, IMethodSymbol baseMethod)
            {
                var overriddenMethod = method.OverriddenMethod;
                while (overriddenMethod is not null)
                {
                    if (SymbolEqualityComparer.Default.Equals(overriddenMethod, baseMethod))
                    {
                        return true;
                    }

                    overriddenMethod = overriddenMethod.OverriddenMethod;
                }

                return false;
            }

            private readonly ImmutableArray<IMethodSymbol> _streamReadMethods;
            private readonly ImmutableArray<IMethodSymbol> _streamReadAsyncMethods;
            private readonly ImmutableHashSet<ITypeSymbol> _knownReliableStreamTypes;
        }
    }
}
