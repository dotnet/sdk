// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class DoNotUseStackallocInLoopsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2014";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseStackallocInLoopsTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseStackallocInLoopsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseStackallocInLoopsDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);
    }
}
