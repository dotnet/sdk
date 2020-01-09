// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.UseLiteralsWhereAppropriateAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpUseLiteralsWhereAppropriateFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.UseLiteralsWhereAppropriateAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicUseLiteralsWhereAppropriateFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class UseLiteralsWhereAppropriateFixerTests
    {
        [Fact]
        public async Task CSharp_CodeFixForEmptyString()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    public /*leading*/ static /*intermediate*/ readonly /*trailing*/ string f1 = """";
}
",
                VerifyCS.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.EmptyStringRule).WithSpan(4, 77, 4, 79).WithArguments("f1"),
                @"
class C
{
    public /*leading*/ const /*intermediate*/  /*trailing*/ string f1 = """";
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Class C
    Public Shared ReadOnly f1 As String = """"
End Class
",
                VerifyVB.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.EmptyStringRule).WithSpan(3, 28, 3, 30).WithArguments("f1"),
@"
Class C
    Public Const f1 As String = """"
End Class
");
        }

        [Fact]
        public async Task CSharp_CodeFixForNonEmptyString()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    /*leading*/
    readonly /*intermediate*/ static /*trailing*/ string f1 = ""Nothing"";
}
",
                VerifyCS.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule).WithSpan(5, 58, 5, 60).WithArguments("f1"),
                @"
class C
{
    /*leading*/
    const /*intermediate*/  /*trailing*/ string f1 = ""Nothing"";
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Class C
    'leading
    ReadOnly Shared f1 As String = ""Nothing""
End Class
",
                VerifyVB.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule).WithSpan(4, 21, 4, 23).WithArguments("f1"),
@"
Class C
    'leading
    Const f1 As String = ""Nothing""
End Class
");
        }

        [Fact]
        public async Task CSharp_CodeFixForMultiDeclaration()
        {
            // Fixers are disabled on multiple fields, because it may introduce compile error.

            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    /*leading*/
    readonly /*intermediate*/ static /*trailing*/ string f3, f4 = ""Message is shown only for f4"";
}
",
                VerifyCS.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule).WithSpan(5, 62, 5, 64).WithArguments("f4"),
                @"
class C
{
    /*leading*/
    readonly /*intermediate*/ static /*trailing*/ string f3, f4 = ""Message is shown only for f4"";
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Class C
    Shared ReadOnly f3 As String, f4 As String = ""Message is shown only for f4""
End Class
",
                VerifyVB.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule).WithSpan(3, 35, 3, 37).WithArguments("f4"),
@"
Class C
    Shared ReadOnly f3 As String, f4 As String = ""Message is shown only for f4""
End Class
");
        }

        [Fact]
        public async Task CSharp_CodeFixForInt32()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    const int f6 = 3;
    static readonly int f7 = 8 + f6;
}
",
                VerifyCS.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule).WithSpan(5, 25, 5, 27).WithArguments("f7"),
                @"
class C
{
    const int f6 = 3;
    const int f7 = 8 + f6;
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Class C
    Const f6 As Integer = 3
    Friend Shared ReadOnly f7 As Integer = 8 + f6
End Class
",
                VerifyVB.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule).WithSpan(4, 28, 4, 30).WithArguments("f7"),
@"
Class C
    Const f6 As Integer = 3
    Friend Const f7 As Integer = 8 + f6
End Class
");
        }
    }
}