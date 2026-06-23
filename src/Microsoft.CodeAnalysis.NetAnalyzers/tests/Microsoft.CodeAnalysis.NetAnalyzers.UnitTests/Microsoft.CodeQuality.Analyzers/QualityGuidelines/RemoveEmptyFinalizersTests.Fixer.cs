// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class RemoveEmptyFinalizersFixerTests
    {
        [Fact]
        public async Task CA1821CSharpCodeFixTestRemoveEmptyFinalizersAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class Class1
{
    // Violation occurs because the finalizer is empty.
    ~[|Class1|]()
    {
    }
}
",
@"
public class Class1
{
}
");
        }

        [Fact]
        public async Task CA1821BasicCodeFixTestRemoveEmptyFinalizersAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.Diagnostics

Public Class Class1
    '  Violation occurs because the finalizer is empty.
    Protected Overrides Sub [|Finalize|]()

    End Sub
End Class
",
@"
Imports System.Diagnostics

Public Class Class1
End Class
");
        }
    }
}
