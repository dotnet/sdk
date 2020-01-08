// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpRemoveEmptyFinalizersAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicRemoveEmptyFinalizersAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class RemoveEmptyFinalizersFixerTests
    {
        [Fact]
        public async Task CA1821CSharpCodeFixTestRemoveEmptyFinalizers()
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
        public async Task CA1821BasicCodeFixTestRemoveEmptyFinalizers()
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
