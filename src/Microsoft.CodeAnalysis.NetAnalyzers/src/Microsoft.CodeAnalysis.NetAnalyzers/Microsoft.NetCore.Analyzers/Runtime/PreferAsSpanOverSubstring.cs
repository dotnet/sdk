// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
        internal const string RuleId = "CA1846";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(Resx.PreferAsSpanOverSubstringTitle), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(Resx.PreferAsSpanOverSubstringMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(Resx.PreferAsSpanOverSubstringDescription), Resx.ResourceManager, typeof(Resx));

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
            return;

            //  Local functions

            void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
            {
                var invocations = PooledConcurrentSet<IInvocationOperation>.GetInstance();

                context.RegisterOperationAction(context =>
                {
                    var argument = (IArgumentOperation)context.Operation;
                    if (symbols.IsAnySubstringInvocation(argument.Value.WalkDownConversion(c => c.IsImplicit)) && argument.Parent is IInvocationOperation invocation)
                    {
                        invocations.Add(invocation);
                    }
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
                if (symbols.IsAnySubstringInvocation(argument.Value.WalkDownConversion(c => c.IsImplicit)))
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
            var candidates = GetAllAccessibleOverloadsAtInvocationCallSite(invocation, cancellationToken);
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
                    if (isSubstringLookup[parameter.Ordinal] && SymbolEqualityComparer.Default.Equals(parameter.Type, symbols.ReadOnlySpanOfCharType))
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

        private static IEnumerable<IMethodSymbol> GetAllAccessibleOverloadsAtInvocationCallSite(IInvocationOperation invocation, CancellationToken cancellationToken)
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
                //  Ensure protected members can only be invoked on instances that are known to be instances of the accessing class.
                var enclosingType = GetEnclosingType(model, location, cancellationToken);
                allOverloads = model.LookupSymbols(location, instance.Type, method.Name).OfType<IMethodSymbol>();
                if (instance.Type.DerivesFrom(enclosingType, baseTypesOnly: true) || instance is IInstanceReferenceOperation)
                {
                    allOverloads = allOverloads.Union(model.LookupBaseMembers(location, method.Name).OfType<IMethodSymbol>());
                }
            }
            else
            {
                //  This can happen when compiling invalid code.
                return Enumerable.Empty<IMethodSymbol>();
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
                ReadOnlySpanOfCharType = roscharType;
                MemoryExtensionsType = memoryExtensionsType;
                SubstringStart = substring1;
                SubstringStartLength = substring2;
                AsSpanStart = asSpan1;
                AsSpanStartLength = asSpan2;
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

                var readOnlySpanOfCharType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1)?.Construct(charType);
                var memoryExtensionsType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions);

                if (readOnlySpanOfCharType is null || memoryExtensionsType is null)
                {
                    symbols = default;
                    return false;
                }

                var int32ParamInfo = ParameterInfo.GetParameterInfo(int32Type);
                var stringParamInfo = ParameterInfo.GetParameterInfo(stringType);

                var substringMembers = stringType.GetMembers(nameof(string.Substring)).OfType<IMethodSymbol>();
                var substringStart = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo);
                var substringStartLength = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(int32ParamInfo, int32ParamInfo);

                var asSpanMembers = memoryExtensionsType.GetMembers(nameof(MemoryExtensions.AsSpan)).OfType<IMethodSymbol>();
                var asSpanStart = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, int32ParamInfo);
                var asSpanStartLength = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, int32ParamInfo, int32ParamInfo);

                if (substringStart is null || substringStartLength is null || asSpanStart is null || asSpanStartLength is null)
                {
                    symbols = default;
                    return false;
                }

                symbols = new RequiredSymbols(
                    stringType, readOnlySpanOfCharType,
                    memoryExtensionsType,
                    substringStart, substringStartLength,
                    asSpanStart, asSpanStartLength);
                return true;
            }

            public INamedTypeSymbol StringType { get; }
            public INamedTypeSymbol ReadOnlySpanOfCharType { get; }
            public INamedTypeSymbol MemoryExtensionsType { get; }
            public IMethodSymbol SubstringStart { get; }
            public IMethodSymbol SubstringStartLength { get; }
            public IMethodSymbol AsSpanStart { get; }
            public IMethodSymbol AsSpanStartLength { get; }

            public bool IsAnySubstringInvocation(IOperation operation)
            {
                if (operation is not IInvocationOperation invocation)
                    return false;
                return SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, SubstringStart) ||
                    SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, SubstringStartLength);
            }
        }
    }
}
