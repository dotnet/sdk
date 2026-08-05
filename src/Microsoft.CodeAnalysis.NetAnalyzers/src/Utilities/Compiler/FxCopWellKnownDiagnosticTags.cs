// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities
{
    internal static class FxCopWellKnownDiagnosticTags
    {
        public const string PortedFromFxCop = nameof(PortedFromFxCop);

        public static readonly string[] PortedFxCopRule = new string[] { PortedFromFxCop, WellKnownDiagnosticTags.Telemetry };
        public static readonly string[] PortedFxCopRuleEnabledInAggressiveMode = new string[] { PortedFromFxCop, WellKnownDiagnosticTags.Telemetry, WellKnownDiagnosticTagsExtensions.EnabledRuleInAggressiveMode };

        public static readonly string[] PortedFxCopDataflowRule = new string[] { PortedFromFxCop, WellKnownDiagnosticTagsExtensions.Dataflow, WellKnownDiagnosticTags.Telemetry };
        public static readonly string[] PortedFxCopDataflowRuleEnabledInAggressiveMode = new string[] { PortedFromFxCop, WellKnownDiagnosticTagsExtensions.Dataflow, WellKnownDiagnosticTags.Telemetry, WellKnownDiagnosticTagsExtensions.EnabledRuleInAggressiveMode };
    }
}