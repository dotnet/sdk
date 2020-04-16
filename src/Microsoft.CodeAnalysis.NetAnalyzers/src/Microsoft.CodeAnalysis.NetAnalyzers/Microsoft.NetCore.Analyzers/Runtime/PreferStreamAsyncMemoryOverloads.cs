// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1834: Prefer Memory/ReadOnlyMemory overloads for Stream ReadAsync/WriteAsync methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PreferStreamAsyncMemoryOverloads : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1834";

        private static readonly LocalizableString s_localizableTitleRead = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamReadAsyncMemoryOverloadsTitle),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageRead = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamReadAsyncMemoryOverloadsMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableDescriptionRead = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamReadAsyncMemoryOverloadsDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleWrite = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamWriteAsyncMemoryOverloadsTitle),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageWrite = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamWriteAsyncMemoryOverloadsMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableDescriptionWrite = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamWriteAsyncMemoryOverloadsDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor PreferStreamReadAsyncMemoryOverloadsRule = DiagnosticDescriptorHelper.Create(
                                                                                        RuleId,
                                                                                        s_localizableTitleRead,
                                                                                        s_localizableMessageRead,
                                                                                        DiagnosticCategory.Performance,
                                                                                        RuleLevel.IdeSuggestion,
                                                                                        s_localizableDescriptionRead,
                                                                                        isPortedFxCopRule: false,
                                                                                        isDataflowRule: false);

        internal static DiagnosticDescriptor PreferStreamWriteAsyncMemoryOverloadsRule = DiagnosticDescriptorHelper.Create(
                                                                                        RuleId,
                                                                                        s_localizableTitleWrite,
                                                                                        s_localizableMessageWrite,
                                                                                        DiagnosticCategory.Performance,
                                                                                        RuleLevel.IdeSuggestion,
                                                                                        s_localizableDescriptionWrite,
                                                                                        isPortedFxCopRule: false,
                                                                                        isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(PreferStreamReadAsyncMemoryOverloadsRule, PreferStreamWriteAsyncMemoryOverloadsRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemory1, out _) ||
                !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOStream, out INamedTypeSymbol? streamType))
            {
                return;
            }

            context.RegisterOperationAction(context =>
            {
                IInvocationOperation invocation = (IInvocationOperation)context.Operation;
                IMethodSymbol method = invocation.TargetMethod;
                DiagnosticDescriptor rule;

                if (string.Equals(method.Name, "ReadAsync", StringComparison.Ordinal))
                {
                    rule = PreferStreamReadAsyncMemoryOverloadsRule;
                }
                else if (string.Equals(method.Name, "WriteAsync", StringComparison.Ordinal))
                {
                    rule = PreferStreamWriteAsyncMemoryOverloadsRule;
                }
                else
                {
                    return;
                }

                if (IsPredecessorAwaiting(invocation) && // Verify if the top invocation in this statement is an await
                    IsDefinedBy(method, streamType) && // Verify if the current method's type is or inherits from Stream
                    HasUndesiredArguments(method))
                {
                    context.ReportDiagnostic(invocation.CreateDiagnostic(rule));
                }
            }, OperationKind.Invocation);
        }

        private static bool IsPredecessorAwaiting(IInvocationOperation invocation)
        {
            IOperation current = invocation;
            while (current.Kind == OperationKind.Invocation)
            {
                current = current.Parent;
            }

            return current.Kind == OperationKind.Await;
        }

        private static bool IsDefinedBy(IMethodSymbol method, INamedTypeSymbol baseType)
        {
            while (method.OverriddenMethod != null)
            {
                method = method.OverriddenMethod;
            }

            return method.ContainingType.Equals(baseType);
        }

        private static bool HasUndesiredArguments(IMethodSymbol method)
        {
            // The undesired ReadAsync/WriteAsync overloads have the same 3 args and an optional 4th (CancellationToken)
            return method.Parameters.Length >= 3 &&
                method.Parameters[0].Type is IArrayTypeSymbol arrayTypeSymbol &&
                arrayTypeSymbol.ElementType.SpecialType == SpecialType.System_Byte &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                method.Parameters[2].Type.SpecialType == SpecialType.System_Int32;
        }
    }
}