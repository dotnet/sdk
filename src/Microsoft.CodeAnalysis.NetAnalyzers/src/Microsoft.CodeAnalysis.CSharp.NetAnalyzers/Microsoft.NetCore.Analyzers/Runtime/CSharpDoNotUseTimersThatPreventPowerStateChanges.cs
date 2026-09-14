// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// CA1601: Do not use timers that prevent power state changes
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotUseTimersThatPreventPowerStateChangesAnalyzer : DoNotUseTimersThatPreventPowerStateChangesAnalyzer
    {
    }
}