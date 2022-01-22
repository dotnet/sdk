// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
