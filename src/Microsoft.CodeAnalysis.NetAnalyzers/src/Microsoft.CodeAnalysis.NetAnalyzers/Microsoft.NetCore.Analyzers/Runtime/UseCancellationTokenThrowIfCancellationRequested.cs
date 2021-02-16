// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseCancellationTokenThrowIfCancellationRequested : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2250";

        private static readonly LocalizableString s_localizableTitle = CreateResource(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedTitle));
        private static readonly LocalizableString s_localizableMessage = CreateResource(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedMessage));
        private static readonly LocalizableString s_localizableDescription = CreateResource(nameof(Resx.UseCancellationTokenThrowIfCancellationRequestedDescription));

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
                    BoolType = boolType,
                    CancellationTokenType = cancellationTokenType,
                    OperationCanceledExceptionType = operationCanceledExceptionType,
                    ThrowIfCancellationRequestedMethod = throwIfCancellationRequestedMethod,
                    IsCancellationRequestedProperty = isCancellationRequestedProperty,
                    OperationCanceledExceptionDefaultCtor = operationCanceledExceptionDefaultCtor,
                    OperationCanceledExceptionTokenCtor = operationCanceledExceptionTokenCtor
                };
                return true;
            }

            public INamedTypeSymbol BoolType { get; init; }
            public INamedTypeSymbol CancellationTokenType { get; init; }
            public INamedTypeSymbol OperationCanceledExceptionType { get; init; }
            public IMethodSymbol ThrowIfCancellationRequestedMethod { get; init; }
            public IPropertySymbol IsCancellationRequestedProperty { get; init; }
            public IMethodSymbol OperationCanceledExceptionDefaultCtor { get; init; }
            public IMethodSymbol OperationCanceledExceptionTokenCtor { get; init; }
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resx.ResourceManager, typeof(Resx));
        }
    }
}
