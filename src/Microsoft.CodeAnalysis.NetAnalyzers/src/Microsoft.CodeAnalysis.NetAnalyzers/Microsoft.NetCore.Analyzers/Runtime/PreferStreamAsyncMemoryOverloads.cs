// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1835: Prefer Memory/ReadOnlyMemory overloads for Stream ReadAsync/WriteAsync methods.
    ///
    /// Undesired methods (available since .NET Framework 4.5):
    ///
    /// - Stream.WriteAsync(Byte[], Int32, Int32)
    /// - Stream.WriteAsync(Byte[], Int32, Int32, CancellationToken)
    /// - Stream.ReadAsync(Byte[], Int32, Int32)
    /// - Stream.ReadAsync(Byte[], Int32, Int32, CancellationToken)
    ///
    /// Preferred methods (available since .NET Standard 2.1 and .NET Core 2.1):
    ///
    /// - Stream.WriteAsync(ReadOnlyMemory{Byte}, CancellationToken)
    /// - Stream.ReadAsync(Memory{Byte}, CancellationToken)
    ///
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferStreamAsyncMemoryOverloads : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1835";

        private static readonly LocalizableString s_localizableTitleRead = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamReadAsyncMemoryOverloadsTitle),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamAsyncMemoryOverloadsMessage),
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

        private static readonly LocalizableString s_localizableDescriptionWrite = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PreferStreamWriteAsyncMemoryOverloadsDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor PreferStreamReadAsyncMemoryOverloadsRule = DiagnosticDescriptorHelper.Create(
                                                                                        RuleId,
                                                                                        s_localizableTitleRead,
                                                                                        s_localizableMessage,
                                                                                        DiagnosticCategory.Performance,
                                                                                        RuleLevel.IdeSuggestion,
                                                                                        s_localizableDescriptionRead,
                                                                                        isPortedFxCopRule: false,
                                                                                        isDataflowRule: false);

        internal static DiagnosticDescriptor PreferStreamWriteAsyncMemoryOverloadsRule = DiagnosticDescriptorHelper.Create(
                                                                                        RuleId,
                                                                                        s_localizableTitleWrite,
                                                                                        s_localizableMessage,
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
            // Find the essential type for this analysis
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOStream, out INamedTypeSymbol? streamType))
            {
                return;
            }

            // Find the types for the rule message, available since .NET Standard 2.1
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlyMemory1, out INamedTypeSymbol? readOnlyMemoryType))
            {
                return;
            }
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemory1, out INamedTypeSymbol? memoryType))
            {
                return;
            }

            // Find the additional types for this analysis
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken, out INamedTypeSymbol? cancellationTokenType))
            {
                return;
            }

            INamedTypeSymbol byteType = context.Compilation.GetSpecialType(SpecialType.System_Byte);
            if (byteType == null)
            {
                return;
            }

            INamedTypeSymbol int32Type = context.Compilation.GetSpecialType(SpecialType.System_Int32);
            if (int32Type == null)
            {
                return;
            }

            // Create the arrays with the exact parameter order of the undesired methods
            var undesiredParameters = new[]
            {
                ParameterInfo.GetParameterInfo(byteType, isArray: true, arrayRank: 1), // byte[] buffer
                ParameterInfo.GetParameterInfo(int32Type),                             // int offset
                ParameterInfo.GetParameterInfo(int32Type),                             // int count
            };

            var undesiredParametersWithCancellationToken = new[]
            {
                ParameterInfo.GetParameterInfo(byteType, isArray: true, arrayRank: 1),
                ParameterInfo.GetParameterInfo(int32Type),
                ParameterInfo.GetParameterInfo(int32Type),
                ParameterInfo.GetParameterInfo(cancellationTokenType)
            };

            // Create the arrays with the exact parameter order of the undesired methods
            var preferredReadAsyncParameters = new[]
            {
                ParameterInfo.GetParameterInfo(readOnlyMemoryType),  // ReadOnlyMemory<byte> buffer
                ParameterInfo.GetParameterInfo(cancellationTokenType), // CancellationToken
            };

            var preferredWriteAsyncParameters = new[]
            {
                ParameterInfo.GetParameterInfo(memoryType),  // ReadOnlyMemory<byte> buffer
                ParameterInfo.GetParameterInfo(cancellationTokenType), // CancellationToken
            };

            // Retrieve the ReadAsync/WriteSync methods available in Stream
            // If we don't find them all, the Memory based overloads are not supported in this .NET version
            IEnumerable<IMethodSymbol> readAsyncMethodGroup = streamType.GetMembers("ReadAsync").OfType<IMethodSymbol>();
            IEnumerable<IMethodSymbol> writeAsyncMethodGroup = streamType.GetMembers("WriteAsync").OfType<IMethodSymbol>();

            // Retrieve the undesired methods
            IMethodSymbol? undesiredReadAsyncMethod = readAsyncMethodGroup.GetFirstOrDefaultMemberWithParameterInfos(undesiredParameters);
            if (undesiredReadAsyncMethod == null)
            {
                return;
            }

            IMethodSymbol? undesiredWriteAsyncMethod = writeAsyncMethodGroup.GetFirstOrDefaultMemberWithParameterInfos(undesiredParameters);
            if (undesiredWriteAsyncMethod == null)
            {
                return;
            }

            IMethodSymbol? undesiredReadAsyncMethodWithCancellationToken = readAsyncMethodGroup.GetFirstOrDefaultMemberWithParameterInfos(undesiredParametersWithCancellationToken);
            if (undesiredReadAsyncMethodWithCancellationToken == null)
            {
                return;
            }

            IMethodSymbol? undesiredWriteAsyncMethodWithCancellationToken = writeAsyncMethodGroup.GetFirstOrDefaultMemberWithParameterInfos(undesiredParametersWithCancellationToken);
            if (undesiredWriteAsyncMethodWithCancellationToken == null)
            {
                return;
            }

            // Retrieve the preferred methods, which are used for constructing the rule message
            IMethodSymbol? preferredReadAsyncMethod = readAsyncMethodGroup.FirstOrDefault(x =>
                x.Parameters.Count() == 2 && x.Parameters[0].Type is INamedTypeSymbol type && type.ConstructedFrom.Equals(memoryType));
            if (preferredReadAsyncMethod == null)
            {
                return;
            }

            IMethodSymbol? preferredWriteAsyncMethod = writeAsyncMethodGroup.FirstOrDefault(x =>
                x.Parameters.Count() == 2 && x.Parameters[0].Type is INamedTypeSymbol type && type.ConstructedFrom.Equals(readOnlyMemoryType));
            if (preferredWriteAsyncMethod == null)
            {
                return;
            }

            // Retrieve the ConfigureAwait methods that could also be detected:
            // - The undesired WriteAsync methods return a Task
            // - The preferred WriteAsync method returns a ValueTask
            // - The undesired ReadAsync methods return a Task<int>
            // - The preferred ReadAsync method returns a ValueTask<int>
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask, out INamedTypeSymbol? taskType))
            {
                return;
            }

            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksGenericTask, out INamedTypeSymbol? genericTaskType))
            {
                return;
            }

            IMethodSymbol? configureAwaitMethod = taskType.GetMembers("ConfigureAwait").OfType<IMethodSymbol>().FirstOrDefault();
            if (configureAwaitMethod == null)
            {
                return;
            }

            IMethodSymbol? genericConfigureAwaitMethod = genericTaskType.GetMembers("ConfigureAwait").OfType<IMethodSymbol>().FirstOrDefault();
            if (genericConfigureAwaitMethod == null)
            {
                return;
            }

            context.RegisterOperationAction(context =>
            {
                IAwaitOperation awaitOperation = (IAwaitOperation)context.Operation;
                if (ShouldAnalyze(awaitOperation, configureAwaitMethod, genericConfigureAwaitMethod, streamType, out IMethodSymbol? method) && method != null)
                {
                    DiagnosticDescriptor rule;
                    string ruleMessageMethod;
                    string ruleMessagePreferredMethod;

                    // Verify if the method is an undesired Async overload
                    if (method.Equals(undesiredReadAsyncMethod) || method.Equals(undesiredReadAsyncMethodWithCancellationToken))
                    {
                        rule = PreferStreamReadAsyncMemoryOverloadsRule;
                        ruleMessageMethod = undesiredReadAsyncMethod.Name;
                        ruleMessagePreferredMethod = preferredReadAsyncMethod.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                    }
                    else if (method.Equals(undesiredWriteAsyncMethod) || method.Equals(undesiredWriteAsyncMethodWithCancellationToken))
                    {
                        rule = PreferStreamWriteAsyncMemoryOverloadsRule;
                        ruleMessageMethod = undesiredWriteAsyncMethod.Name;
                        ruleMessagePreferredMethod = preferredWriteAsyncMethod.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                    }
                    else
                    {
                        // Prevent use of unassigned variables error
                        return;
                    }

                    context.ReportDiagnostic(awaitOperation.CreateDiagnostic(rule, ruleMessageMethod, ruleMessagePreferredMethod));
                }
            },
            OperationKind.Await);
        }

        private static bool ShouldAnalyze(
            IAwaitOperation awaitOperation,
            IMethodSymbol configureAwaitMethod,
            IMethodSymbol genericConfigureAwaitMethod,
            INamedTypeSymbol streamType,
            out IMethodSymbol? actualMethod)
        {
            actualMethod = null;

            if (!awaitOperation.Children.Any())
            {
                return false;
            }

            if (!(awaitOperation.Children.FirstOrDefault() is IInvocationOperation invocation))
            {
                return false;
            }

            IMethodSymbol method = invocation.TargetMethod;

            if (method.OriginalDefinition.Equals(configureAwaitMethod) ||
                method.OriginalDefinition.Equals(genericConfigureAwaitMethod))
            {
                if (invocation.Instance is IInvocationOperation instanceInvocation)
                {
                    method = instanceInvocation.TargetMethod;
                }
                else
                {
                    return false;
                }
            }

            // Verify if the current method's type is or inherits from Stream
            return IsDefinedBy(method, streamType, out actualMethod);
        }

        private static bool IsDefinedBy(IMethodSymbol method, INamedTypeSymbol baseType, out IMethodSymbol actualMethod)
        {
            actualMethod = method;

            while (actualMethod.OverriddenMethod != null)
            {
                actualMethod = actualMethod.OverriddenMethod;
            }

            return actualMethod.ContainingType.Equals(baseType);
        }
    }
}