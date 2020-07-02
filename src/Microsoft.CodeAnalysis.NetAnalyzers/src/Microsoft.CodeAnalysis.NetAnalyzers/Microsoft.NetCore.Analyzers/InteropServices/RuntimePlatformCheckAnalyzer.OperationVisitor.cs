// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public sealed partial class RuntimePlatformCheckAnalyzer
    {
        private sealed class OperationVisitor : GlobalFlowStateDataFlowOperationVisitor
        {
            private readonly ImmutableArray<IMethodSymbol> _platformCheckMethods;
            private readonly INamedTypeSymbol _osPlatformType;

            public OperationVisitor(
                ImmutableArray<IMethodSymbol> platformCheckMethods,
                INamedTypeSymbol osPlatformType,
                GlobalFlowStateAnalysisContext analysisContext)
                : base(analysisContext, hasPredicatedGlobalState: true)
            {
                _platformCheckMethods = platformCheckMethods;
                _osPlatformType = osPlatformType;
            }

            public override GlobalFlowStateAnalysisValueSet VisitInvocation_NonLambdaOrDelegateOrLocalFunction(
                IMethodSymbol method,
                IOperation? visitedInstance,
                ImmutableArray<IArgumentOperation> visitedArguments,
                bool invokedAsDelegate,
                IOperation originalOperation,
                GlobalFlowStateAnalysisValueSet defaultValue)
            {
                var value = base.VisitInvocation_NonLambdaOrDelegateOrLocalFunction(method, visitedInstance, visitedArguments, invokedAsDelegate, originalOperation, defaultValue);

                if (_platformCheckMethods.Contains(method.OriginalDefinition) &&
                    !visitedArguments.IsEmpty)
                {
                    return RuntimeOSPlatformInfo.TryDecode(method, visitedArguments, DataFlowAnalysisContext.ValueContentAnalysisResultOpt, _osPlatformType, out var platformInfo) ?
                        new GlobalFlowStateAnalysisValueSet(platformInfo) :
                        GlobalFlowStateAnalysisValueSet.Unknown;
                }

                return GetValueOrDefault(value);
            }
        }
    }
}
