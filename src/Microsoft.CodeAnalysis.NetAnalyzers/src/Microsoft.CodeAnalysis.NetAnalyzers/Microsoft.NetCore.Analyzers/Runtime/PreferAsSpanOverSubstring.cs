// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1842: Prefer 'AsSpan' over 'Substring'.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferAsSpanOverSubstring : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1842";

        private static readonly LocalizableString s_localizableTitle = CreateResource(nameof(Resx.PreferAsSpanOverSubstringTitle));
        private static readonly LocalizableString s_localizableMessage = CreateResource(nameof(Resx.PreferAsSpanOverSubstringMessage));
        private static readonly LocalizableString s_localizableDescription = CreateResource(nameof(Resx.PreferAsSpanOverSubstringDescription));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out RequiredSymbols symbols))
                return;

            context.RegisterOperationBlockStartAction(OnOperationBlockStart);

            void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
            {
                var invocations = PooledConcurrentSet<IInvocationOperation>.GetInstance();

                context.RegisterOperationAction(context =>
                {
                    var argument = (IArgumentOperation)context.Operation;
                    if (!symbols.IsAnySubstringInvocation(WalkDownImplicitConversions(argument.Value)))
                        return;
                    if (argument.Parent is not IInvocationOperation invocation)
                        return;
                    invocations.Add(invocation);
                }, OperationKind.Argument);

                context.RegisterOperationBlockEndAction(context =>
                {
                    foreach (var invocation in invocations)
                    {
                        //  We search for an overload of the invoked member whose signature matches the signature of
                        //  the invoked member, except with ReadOnlySpan<char> substituted in for some of the 
                        //  arguments that are Substring invocations.
                        if (!GetBestSpanBasedOverloads(symbols, invocation, context.CancellationToken).IsEmpty)
                        {
                            Diagnostic diagnostic = invocation.CreateDiagnostic(Rule);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                    invocations.Free(context.CancellationToken);
                });
            }
        }

        internal static IOperation WalkDownImplicitConversions(IOperation operation)
        {
            while (operation is IConversionOperation conversion && conversion.IsImplicit)
                operation = conversion.Operand;
            return operation;
        }

        /// <summary>
        /// Gets all the overloads that are tied for being the "best" span-based overload for the specified <see cref="IInvocationOperation"/>.
        /// An overload is considered "better" if it allows more Substring invocations to be replaced with AsSpan invocations.
        /// 
        /// If there are no overloads that replace any Substring calls, or none of the arguments in the invocation are
        /// Substring calls, an empty array is returned.
        /// </summary>
        internal static ImmutableArray<IMethodSymbol> GetBestSpanBasedOverloads(in RequiredSymbols symbols, IInvocationOperation invocation, CancellationToken cancellationToken)
        {
            var method = invocation.TargetMethod;

            //  Whether an argument at a particular parameter ordinal is a Substring call.
            Span<bool> isSubstringLookup = stackalloc bool[method.Parameters.Length];
            int substringCalls = 0;
            foreach (var argument in invocation.Arguments)
            {
                IOperation value = WalkDownImplicitConversions(argument.Value);
                if (symbols.IsAnySubstringInvocation(value))
                {
                    isSubstringLookup[argument.Parameter.Ordinal] = true;
                    ++substringCalls;
                }
            }

            if (substringCalls == 0)
                return ImmutableArray<IMethodSymbol>.Empty;

            //  Find all overloads that are tied for being the "best" overload. An overload is considered
            //  "better" if it allows more Substring calls to be replaced with AsSpan calls.
            var bestCandidates = ImmutableArray.CreateBuilder<IMethodSymbol>();
            int resultQuality = 0;
            var candidates = GetAllAccessibleOverloadsIncludingSelf(invocation, cancellationToken);
            foreach (var candidate in candidates)
            {
                int quality = EvaluateCandidateQuality(symbols, isSubstringLookup, invocation, candidate);

                //  Reject candidates that do not replace at least one Substring call.
                if (quality < 1)
                {
                    continue;
                }
                else if (quality == resultQuality)
                {
                    bestCandidates.Add(candidate);
                }
                else if (quality > resultQuality)
                {
                    resultQuality = quality;
                    bestCandidates.Clear();
                    bestCandidates.Add(candidate);
                }
            }

            return bestCandidates.ToImmutable();

            //  Returns a number indicating how good the candidate method is. 
            //  If the candidate is valid, the number of Substring calls that can be replaced with AsSpan calls is returned.
            //  If the candidate is invalid, -1 is returned.
            static int EvaluateCandidateQuality(in RequiredSymbols symbols, ReadOnlySpan<bool> isSubstringLookup, IInvocationOperation invocation, IMethodSymbol candidate)
            {
                var method = invocation.TargetMethod;

                if (candidate.Parameters.Length != method.Parameters.Length)
                    return -1;

                int replacementCount = 0;
                foreach (var parameter in candidate.Parameters)
                {
                    if (isSubstringLookup[parameter.Ordinal] && SymbolEqualityComparer.Default.Equals(parameter.Type, symbols.RoscharType))
                    {
                        ++replacementCount;
                        continue;
                    }

                    var oldParameter = method.Parameters[parameter.Ordinal];
                    if (!SymbolEqualityComparer.Default.Equals(parameter.Type, oldParameter.Type))
                    {
                        return -1;
                    }
                }

                return replacementCount;
            }
        }

        private static IEnumerable<IMethodSymbol> GetAllAccessibleOverloadsIncludingSelf(IInvocationOperation invocation, CancellationToken cancellationToken)
        {
            var method = invocation.TargetMethod;
            var model = invocation.SemanticModel;
            int location = invocation.Syntax.SpanStart;
            var instance = invocation.Instance;

            IEnumerable<IMethodSymbol> allOverloads;
            if (method.IsStatic)
            {
                allOverloads = model.LookupStaticMembers(location, method.ContainingType, method.Name).OfType<IMethodSymbol>();
            }
            else if (instance is not null)
            {
                var enclosingType = GetEnclosingType(model, location, cancellationToken);
                allOverloads = model.LookupSymbols(location, instance.Type, method.Name).OfType<IMethodSymbol>();
                if (DerivesFromOrEqualTo(instance.Type, enclosingType) || instance is IInstanceReferenceOperation)
                {
                    allOverloads = allOverloads.Union(model.LookupBaseMembers(location, method.Name).OfType<IMethodSymbol>());
                }
            }
            //  This can happen when compiling invalid code.
            else
            {
                allOverloads = Enumerable.Empty<IMethodSymbol>();
            }

            return allOverloads.Where(x => x.IsStatic == method.IsStatic && SymbolEqualityComparer.Default.Equals(x.ReturnType, method.ReturnType));

            static INamedTypeSymbol GetEnclosingType(SemanticModel model, int location, CancellationToken cancellationToken)
            {
                ISymbol symbol = model.GetEnclosingSymbol(location, cancellationToken);
                if (symbol is not INamedTypeSymbol type)
                    type = symbol.ContainingType;

                return type;
            }
        }

        private static bool DerivesFromOrEqualTo(ITypeSymbol derived, ITypeSymbol candidateBase)
        {
            return derived.DerivesFrom(candidateBase, baseTypesOnly: true) || SymbolEqualityComparer.Default.Equals(derived, candidateBase);
        }

        //  Use struct to avoid allocations.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct RequiredSymbols
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            private RequiredSymbols(
                INamedTypeSymbol stringType, INamedTypeSymbol roscharType,
                INamedTypeSymbol memoryExtensionsType,
                IMethodSymbol substring1, IMethodSymbol substring2,
                IMethodSymbol asSpan1, IMethodSymbol asSpan2)
            {
                StringType = stringType;
                RoscharType = roscharType;
                MemoryExtensionsType = memoryExtensionsType;
                Substring1 = substring1;
                Substring2 = substring2;
                AsSpan1 = asSpan1;
                AsSpan2 = asSpan2;
            }

            public static bool TryGetSymbols(Compilation compilation, out RequiredSymbols symbols)
            {
                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var charType = compilation.GetSpecialType(SpecialType.System_Char);
                var int32Type = compilation.GetSpecialType(SpecialType.System_Int32);

                if (stringType is null || charType is null || int32Type is null)
                {
                    symbols = default;
                    return false;
                }

                var roscharType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1)?.Construct(charType);
                var memoryExtensionsType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions);

                if (roscharType is null || memoryExtensionsType is null)
                {
                    symbols = default;
                    return false;
                }

                var int32ParamInfo = ParameterInfo.GetParameterInfo(int32Type);
                var stringParamInfo = ParameterInfo.GetParameterInfo(stringType);

                var substringMembers = stringType.GetMembers(nameof(string.Substring)).OfType<IMethodSymbol>();
                var substring1 = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo);
                var substring2 = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo, int32ParamInfo);

                var asSpanMembers = memoryExtensionsType.GetMembers(nameof(MemoryExtensions.AsSpan)).OfType<IMethodSymbol>();
                var asSpan1 = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, int32ParamInfo);
                var asSpan2 = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, int32ParamInfo, int32ParamInfo);

                if (substring1 is null || substring2 is null || asSpan1 is null || asSpan2 is null)
                {
                    symbols = default;
                    return false;
                }

                symbols = new RequiredSymbols(
                    stringType, roscharType,
                    memoryExtensionsType,
                    substring1, substring2,
                    asSpan1, asSpan2);
                return true;
            }

            public INamedTypeSymbol StringType { get; }
            public INamedTypeSymbol RoscharType { get; }
            public INamedTypeSymbol MemoryExtensionsType { get; }
            public IMethodSymbol Substring1 { get; }
            public IMethodSymbol Substring2 { get; }
            public IMethodSymbol AsSpan1 { get; }
            public IMethodSymbol AsSpan2 { get; }

            public bool IsAnySubstringInvocation(IOperation operation)
            {
                if (operation is not IInvocationOperation invocation)
                    return false;
                return SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, Substring1) ||
                    SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, Substring2);
            }
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resx.ResourceManager, typeof(Resx));
        }
    }
}
