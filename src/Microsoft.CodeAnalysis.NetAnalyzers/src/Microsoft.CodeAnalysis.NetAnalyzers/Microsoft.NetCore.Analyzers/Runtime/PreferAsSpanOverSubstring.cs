// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
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

            context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);

            void AnalyzeInvocationOperation(OperationAnalysisContext context)
            {
                var invocation = (IInvocationOperation)context.Operation;

                //  Bail if none of the arguments are Substring invocations.
                if (!invocation.Arguments.Any(x => symbols.IsAnySubstringInvocation(WalkDownImplicitConversions(x.Value))))
                    return;

                //  We search for an overload of the invoked member whose signature matches the signature of
                //  the invoked member, except with ReadOnlySpan<char> substituted in for all arguments that 
                //  are Substring invocations.
                if (symbols.TryGetEquivalentSpanBasedOverload(invocation, out _))
                {
                    Diagnostic diagnostic = invocation.CreateDiagnostic(Rule);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        internal static IOperation WalkDownImplicitConversions(IOperation operation)
        {
            while (operation is IConversionOperation conversion && conversion.IsImplicit)
                operation = conversion.Operand;
            return operation;
        }

        //  Use struct to avoid allocations.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct RequiredSymbols
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            private RequiredSymbols(
                INamedTypeSymbol stringType, INamedTypeSymbol roscharType,
                IMethodSymbol substring1, IMethodSymbol substring2,
                IMethodSymbol asSpan1, IMethodSymbol asSpan2)
            {
                StringType = stringType;
                RoscharType = roscharType;
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
                    substring1, substring2,
                    asSpan1, asSpan2);
                return true;
            }

            public INamedTypeSymbol StringType { get; }
            public INamedTypeSymbol RoscharType { get; }
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

            /// <summary>
            /// Attempts to find an overload of the invoked method that has the same signature, but with
            /// ReadOnlySpan{char} in all positions that were Substring invocations in the invoked member.
            /// </summary>
            public bool TryGetEquivalentSpanBasedOverload(IInvocationOperation invocation, [NotNullWhen(true)] out IMethodSymbol? spanBasedOverload)
            {
                spanBasedOverload = null;
                var method = invocation.TargetMethod;
                if (method.Parameters.IsEmpty)
                    return false;
                ITypeSymbol[] expectedSignature = new ITypeSymbol[method.Parameters.Length];
                for (int index = 0; index < method.Parameters.Length; index++)
                    expectedSignature[index] = method.Parameters[index].Type;

                foreach (var argument in invocation.Arguments)
                {
                    if (IsAnySubstringInvocation(argument.Value))
                        expectedSignature[argument.Parameter.Ordinal] = RoscharType;
                }

                var comparer = SymbolEqualityComparer.Default;
                spanBasedOverload = method.ContainingType.GetMembers(method.Name)
                    .OfType<IMethodSymbol>()
                    .Where(x =>
                    {
                        return comparer.Equals(x.ReturnType, method.ReturnType) &&
                        x.IsStatic == method.IsStatic;
                    })
                    .GetFirstOrDefaultMemberWithParameterTypes(expectedSignature);
                return spanBasedOverload is not null;
            }
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resx.ResourceManager, typeof(Resx));
        }
    }
}
