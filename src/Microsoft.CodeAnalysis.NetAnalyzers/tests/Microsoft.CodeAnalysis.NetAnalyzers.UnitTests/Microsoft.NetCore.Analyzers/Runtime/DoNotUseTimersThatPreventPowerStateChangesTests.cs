// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDoNotUseTimersThatPreventPowerStateChangesAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDoNotUseTimersThatPreventPowerStateChangesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDoNotUseTimersThatPreventPowerStateChangesAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDoNotUseTimersThatPreventPowerStateChangesFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotUseTimersThatPreventPowerStateChangesTests
    {
    }
}