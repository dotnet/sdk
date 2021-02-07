// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
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
            var compilation = context.Compilation;
            if (!RequiredSymbols.TryGetSymbols(compilation, out RequiredSymbols symbols))
                return;

            context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);

            void AnalyzeInvocationOperation(OperationAnalysisContext context)
            {
                var invocation = (IInvocationOperation)context.Operation;
                var substringInvocationArguments = invocation.Arguments
                    .WhereAsArray(x => symbols.IsAnySubstringInvocation(x.Value));

                if (substringInvocationArguments.IsEmpty)
                    return;

                ITypeSymbol[] equivalentSpanBasedSignature = invocation.TargetMethod.Parameters.Select(x => x.Type).ToArray();
                foreach (var argument in substringInvocationArguments)
                {
                    equivalentSpanBasedSignature[argument.Parameter.Ordinal] = symbols.RoscharType;
                }

                IMethodSymbol? spanBasedOverload = invocation.TargetMethod.ContainingType.GetMembers(invocation.TargetMethod.Name)
                    .OfType<IMethodSymbol>()
                    .GetFirstOrDefaultMemberWithParameterTypes(equivalentSpanBasedSignature);

                if (spanBasedOverload is not null)
                {
                    Diagnostic diagnostic = invocation.CreateDiagnostic(Rule);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        //  Use struct to avoid allocations, as this won't be passed by value.
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
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resx.ResourceManager, typeof(Resx));
        }
    }
}
