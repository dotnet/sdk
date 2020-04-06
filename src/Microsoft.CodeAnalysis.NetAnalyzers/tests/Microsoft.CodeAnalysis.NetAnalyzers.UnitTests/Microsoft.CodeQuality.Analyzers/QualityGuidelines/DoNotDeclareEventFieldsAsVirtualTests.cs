// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotDeclareEventFieldsAsVirtual,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public class DoNotDeclareEventFieldsAsVirtualTests
    {
        [Fact]
        public async Task EventFieldVirtual_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C
{
    public virtual event EventHandler ThresholdReached;
}",
                VerifyCS.Diagnostic().WithLocation(5, 39));
        }

        [Fact]
        public async Task EventPropertyVirtual_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C
{
    public virtual event EventHandler ThresholdReached
    {
        add
        {
        }
        remove
        {
        }
    }
}");
        }

        [Fact]
        public async Task EventFieldVirtualAllAccessibilities_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C
{
    public virtual event EventHandler [|Event1|];
    protected virtual event EventHandler [|Event2|];
    internal virtual event EventHandler [|Event3|];
    protected internal virtual event EventHandler [|Event4|];
}");
        }
    }
}
