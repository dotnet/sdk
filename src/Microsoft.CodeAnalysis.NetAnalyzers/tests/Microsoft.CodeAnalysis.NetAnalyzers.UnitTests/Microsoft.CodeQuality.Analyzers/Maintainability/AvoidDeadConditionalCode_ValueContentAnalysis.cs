// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using CSharpLanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpAvoidDeadConditionalCode,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
    public partial class AvoidDeadConditionalCodeTests
    {
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task SimpleStringCompare_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        if (param == """")
        {
        }

        if ("""" == param)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        If param = """" Then
        End If

        If """" = param Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task SimpleValueCompare_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int param)
    {
        if (param == 0)
        {
        }

        if (0 == param)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As Integer)
        If param = 0 Then
        End If

        If 0 = param Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ValueCompareWithAdd_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int param, int param2, int param3)
    {
        param2 = 2;
        if (param == 1 && (param3 == param + param2))
        {
            if (param3 == 3)
            {
            }
        }
    }
}
",
            // Test0.cs(9,17): warning CA1508: 'param3 == 3' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(9, 17, "param3 == 3", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As Integer, param2 As Integer, param3 As Integer)
        param2 = 2
        If param = 1 AndAlso (param3 = param + param2) Then
            If param3 = 3 Then
            End If
        End If
    End Sub
End Module",
            // Test0.vb(6,16): warning CA1508: 'param3 = 3' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(6, 16, "param3 = 3", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ValueCompareWithSubtract_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int param, int param2, int param3)
    {
        param2 = 2;
        if (param3 == 3 && (param3 - param2 == param))
        {
            if (param == 1)
            {
            }
        }
    }
}
",
            // Test0.cs(9,17): warning CA1508: 'param == 1' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(9, 17, "param == 1", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As Integer, param2 As Integer, param3 As Integer)
        param2 = 2
        If param3 = 3 AndAlso (param3 - param2 = param) Then
            If param = 1 Then
            End If
        End If
    End Sub
End Module",
            // Test0.vb(6,16): warning CA1508: 'param = 1' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(6, 16, "param = 1", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task SimpleStringCompare_AfterAssignment_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        param = """";
        if (param == """")
        {
        }

        if ("""" != param)
        {
        }
    }
}
",
            // Test0.cs(7,13): warning CA1508: 'param == ""' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 13, @"param == """"", "true"),
            // Test0.cs(11,13): warning CA1508: '"" != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 13, @""""" != param", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        param = """"
        If param = """" Then
        End If

        If """" <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(5,12): warning CA1508: 'param = ""' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 12, @"param = """"", "True"),
            // Test0.vb(8,12): warning CA1508: '"" <> param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 12, @""""" <> param", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task SimpleValueCompare_AfterAssignment_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int param)
    {
        param = 0;
        if (param == 0)
        {
        }

        if (0 != param)
        {
        }
    }
}
",
            // Test0.cs(7,13): warning CA1508: 'param == 0' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 13, @"param == 0", "true"),
            // Test0.cs(11,13): warning CA1508: '0 != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 13, @"0 != param", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As Integer)
        param = 0
        If param = 0 Then
        End If

        If 0 <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(5,12): warning CA1508: 'param = 0' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 12, @"param = 0", "True"),
            // Test0.vb(8,12): warning CA1508: '0 <> param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 12, @"0 <> param", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_StringCompare_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        string str = """";
        if (param != """")
        {
        }
        else if (param == str)
        {
        }

        if ("""" == param)
        {
            if (param != str)
            {
            }
        }
    }
}
",
            // Test0.cs(10,18): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 18, "param == str", "true"),
            // Test0.cs(16,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 17, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        Dim str = """"
        If param <> """" Then
        Else If param = str Then
        End If

        If """" = param Then
            If param <> str Then
            End If
        End If
    End Sub
End Module",
            // Test0.vb(6,17): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(6, 17, "param = str", "True"),
            // Test0.vb(10,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(10, 16, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ConditionaAndOrStringCompare_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        string str = """";
        if (param != """" || param == str)
        {
        }

        if ("""" == param && param != str)
        {
        }
    }
}
",
            // Test0.cs(7,28): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 28, "param == str", "true"),
            // Test0.cs(11,28): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 28, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        Dim str = """"
        If param <> """" OrElse param = str Then
        End If

        If """" = param AndAlso param <> str Then
        End If
    End Sub
End Module",
            // Test0.vb(5,31): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 31, "param = str", "True"),
            // Test0.vb(8,31): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 31, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_StringCompare_DifferentLiteral_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        string str = ""a"";
        if (param != """")
        {
        }
        else if (param == str)
        {
        }

        if ("""" == param)
        {
            if (param != str)
            {
            }
        }
    }
}
",
            // Test0.cs(10,18): warning CA1508: 'param == str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 18, "param == str", "false"),
            // Test0.cs(16,17): warning CA1508: 'param != str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 17, "param != str", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        Dim str = ""a""
        If param <> """" Then
        Else If param = str Then
        End If

        If """" = param Then
            If param <> str Then
            End If
        End If
    End Sub
End Module",
            // Test0.vb(6,17): warning CA1508: 'param = str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(6, 17, "param = str", "False"),
            // Test0.vb(10,16): warning CA1508: 'param <> str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(10, 16, "param <> str", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_ValueCompare_DifferentLiteral_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int param)
    {
        long str = 0;
        if (param != 1)
        {
        }
        else if (param == str)
        {
        }

        if (1 == param)
        {
            if (param != str)
            {
            }
        }
    }
}
",
            // Test0.cs(10,18): warning CA1508: 'param == str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 18, "param == str", "false"),
            // Test0.cs(16,17): warning CA1508: 'param != str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 17, "param != str", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As Integer)
        Dim str As Long = 0
        If param <> 1 Then
        Else If param = str Then
        End If

        If 1 = param Then
            If param <> str Then
            End If
        End If
    End Sub
End Module",
            // Test0.vb(6,17): warning CA1508: 'param = str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(6, 17, "param = str", "False"),
            // Test0.vb(10,16): warning CA1508: 'param <> str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(10, 16, "param <> str", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_StringCompare_DifferentLiterals_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, bool flag)
    {
        string str = flag ? ""a"" : """";
        if (param != """")
        {
        }
        else if (param == str)
        {
        }

        if ("""" == param)
        {
            if (param != str)
            {
            }
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, flag As Boolean)
        Dim str = If(flag, ""a"", """")
        If param <> """" Then
        Else If param = str Then
        End If

        If """" = param Then
            If param <> str Then
            End If
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_ValueCompare_DifferentLiterals_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(ulong param, bool flag)
    {
        var str = (byte)(flag ? 0 : 1);
        if (param != 1)
        {
        }
        else if (param == str)
        {
        }

        if (1 == param)
        {
            if (param != str)
            {
            }
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As ULong, flag As Boolean)
        Dim str As Short = If(flag, 0, 1)
        If param <> 1 Then
        Else If param = str Then
        End If

        If 1 = param Then
            If param <> str Then
            End If
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_WhileLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param)
    {
        string str = """";
        while (param == str)
        {
            // param = str here
            if (param == str)
            {
            }
            if (param != str)
            {
            }
        }

        // param is unknown here
        if (str == param)
        {
        }
        if (str != param)
        {
        }
    }
}
",
            // Test0.cs(10,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 17, "param == str", "true"),
            // Test0.cs(13,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(13, 17, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' While loop
    Private Sub M1(ByVal param As String)
        Dim str As String = """"
        While param = str
            ' param == str here
            If param = str Then
            End If
            If param <> str Then
            End If
        End While

        ' param is unknown here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(8,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 16, "param = str", "True"),
            // Test0.vb(10,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(10, 16, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ValueCompare_WhileLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(double param)
    {
        var str = (float)3.0;
        while (param == str)
        {
            // param = str here
            if (param == str)
            {
            }
            if (param != str)
            {
            }
        }

        // param is unknown here
        if (str == param)
        {
        }
        if (str != param)
        {
        }
    }
}
",
            // Test0.cs(10,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 17, "param == str", "true"),
            // Test0.cs(13,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(13, 17, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' While loop
    Private Sub M1(ByVal param As Double)
        Dim str As Single = 3.0
        While param = str
            ' param == str here
            If param = str Then
            End If
            If param <> str Then
            End If
        End While

        ' param is unknown here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(8,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 16, "param = str", "True"),
            // Test0.vb(10,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(10, 16, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_DoWhileLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param)
    {
        string str = """";
        do
        {
            // param is unknown here
            if (str == param)
            {
            }
            if (str != param)
            {
            }
        }
        while (param != str);

        // param = str here
        if (param == str)
        {
        }
        if (param != str)
        {
        }
    }
}
",
            // Test0.cs(20,13): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 13, "param == str", "true"),
            // Test0.cs(23,13): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(23, 13, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' Do-While top loop
    Private Sub M(ByVal param As String)
        Dim str As String = """"
        Do While param <> str
            ' param is unknown here
            If str = param Then
                End If
            If str <> param Then
            End If
        Loop

        ' param == str here
        If param = str Then
        End If
        If param <> str Then
        End If
    End Sub

    ' Do-While bottom loop
    Private Sub M2(ByVal param2 As String)
        Dim str As String = """"
        Do
            ' param2 is unknown here
            If str = param2 Then
                End If
            If str <> param2 Then
            End If
        Loop While param2 <> str

        ' param2 == str here
        If param2 = str Then
        End If
        If param2 <> str Then
        End If
    End Sub
End Module",
            // Test0.vb(15,12): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(15, 12, "param = str", "True"),
            // Test0.vb(17,12): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(17, 12, "param <> str", "False"),
            // Test0.vb(33,12): warning CA1508: 'param2 = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(33, 12, "param2 = str", "True"),
            // Test0.vb(35,12): warning CA1508: 'param2 <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(35, 12, "param2 <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_DoUntilLoopAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' Do-Until top loop
    Private Sub M(ByVal param As String)
        Dim str As String = """"
        Do Until param <> str
            ' param == str here
            If param = str Then
            End If
            If param <> str Then
            End If
        Loop

        ' param is unknown here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub

    ' Do-Until bottom loop
    Private Sub M2(ByVal param2 As String)
        Dim str As String = """"
        Do
            ' param2 is unknown here
            If str = param2 Then
                End If
            If str <> param2 Then
            End If
        Loop Until param2 = str

        ' param2 == str here
        If param2 = str Then
        End If
        If param2 <> str Then
        End If
    End Sub
End Module",
            // Test0.vb(8,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 16, "param = str", "True"),
            // Test0.vb(10,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(10, 16, "param <> str", "False"),
            // Test0.vb(33,12): warning CA1508: 'param2 = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(33, 12, "param2 = str", "True"),
            // Test0.vb(35,12): warning CA1508: 'param2 <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(35, 12, "param2 <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_ForLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2)
    {
        string str = """";
        for (param = str; param2 != str;)
        {
            // param = str here
            if (param == str)
            {
            }
            if (param != str)
            {
            }

            // param2 != str here, but we don't track not-contained values so no diagnostic.
            if (param2 == str)
            {
            }
            if (param2 != str)
            {
            }
        }

        // param2 == str here
        if (str == param2)
        {
        }
        if (str != param2)
        {
        }

        // param == str here
        if (str == param)
        {
        }
        if (str != param)
        {
        }
    }
}
",
            // Test0.cs(10,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 17, "param == str", "true"),
            // Test0.cs(13,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(13, 17, "param != str", "false"),
            // Test0.cs(27,13): warning CA1508: 'str == param2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(27, 13, "str == param2", "true"),
            // Test0.cs(30,13): warning CA1508: 'str != param2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(30, 13, "str != param2", "false"),
            // Test0.cs(35,13): warning CA1508: 'str == param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(35, 13, "str == param", "true"),
            // Test0.cs(38,13): warning CA1508: 'str != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(38, 13, "str != param", "false"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task IntegralValueCompare_ForLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(int param, uint param2)
    {
        int str = 1;
        for (param = str; param2 != str;)
        {
            // param = str here
            if (param == str)
            {
            }
            if (param != str)
            {
            }

            // param2 != str here, but we don't track not-contained values so no diagnostic.
            if (param2 == str)
            {
            }
            if (param2 != str)
            {
            }
        }
        
        // param2 == str here
        if (str == param2)
        {
        }
        if (str != param2)
        {
        }
        
        // param == str here
        if (str == param)
        {
        }
        if (str != param)
        {
        }
    }
}
",
            // Test0.cs(10,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 17, "param == str", "true"),
            // Test0.cs(13,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(13, 17, "param != str", "false"),
            // Test0.cs(27,13): warning CA1508: 'str == param2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(27, 13, "str == param2", "true"),
            // Test0.cs(30,13): warning CA1508: 'str != param2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(30, 13, "str != param2", "false"),
            // Test0.cs(35,13): warning CA1508: 'str == param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(35, 13, "str == param", "true"),
            // Test0.cs(38,13): warning CA1508: 'str != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(38, 13, "str != param", "false"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task IntegralValueCompare_ForLoop_02Async()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(int param, string param2, string param3)
    {
        for (int i = 0; i < param; i++)
        {
            var x = i == 0 ? param2 : param3;
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task EnumValueCompare_ForEachLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
enum Kind
{
    Kind1 = 1,
    Kind2 = 2
}

class Test
{
    private Kind MyKind { get; }
    private Test[] _tests;

    void M(int[] array)
    {
        foreach (var x in array)
        {
            var test = GetTest(x);
            if (test.MyKind != Kind.Kind2)
            {
                continue;
            }

            M2(test);
        }
    }

    Test GetTest(int x) => _tests[x];
    void M2(Test test) { }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task EnumValueCompare_ForEachLoop_02Async()
        {
            await VerifyCSharpAnalyzerAsync(@"
enum Kind
{
    Kind1 = 1,
    Kind2 = 2
}

class Test
{
    private Kind MyKind { get; }
    private Test[] _tests;

    void M(int[] array)
    {
        var kind = Kind.Kind2;
        foreach (var x in array)
        {
            var test = GetTest(x);
            if (test.MyKind != kind)
            {
                continue;
            }

            M2(test);
            kind = GetKind(test);
        }
    }

    Test GetTest(int x) => _tests[x];
    Kind GetKind(Test t) => t.MyKind;
    void M2(Test test) { }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task EnumValueCompare_ForEachLoop_03Async()
        {
            await VerifyCSharpAnalyzerAsync(@"
enum Kind
{
    Kind1 = 1,
    Kind2 = 2
}

class Test
{
    private Kind MyKind { get; }
    private Test[] _tests;

    void M(int[] array)
    {
        var kind = Kind.Kind2;
        foreach (var x in array)
        {
            var test = GetTest(x);
            var testKind = test.MyKind;
            if (testKind == kind &&
                testKind != Kind.Kind2) // Redundant check as 'kind' is always 'Kind.Kind2'
            {
                continue;
            }

            M2(test);
        }
    }

    Test GetTest(int x) => _tests[x];
    void M2(Test test) { }
}
",
            // Test0.cs(21,17): warning CA1508: 'Kind.Kind2 != Kind.Kind2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(21, 17, "testKind != Kind.Kind2", "false"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task StringCompare_CopyAnalysisAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, string param2)
    {
        string str = ""a"";
        if (param == str && param2 == str && param == param2)
        {
        }

        param = param2;
        if (param != str || param2 != str)
        {
        }
    }
}
",
            // Test0.cs(7,46): warning CA1508: 'param == param2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 46, "param == param2", "true"),
            // Test0.cs(12,29): warning CA1508: 'param2 != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(12, 29, "param2 != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String)
        Dim str = ""a""
        If param = str AndAlso param2 = str AndAlso param = param2 Then
        End If

        param = param2
        If param <> str OrElse param2 <> str Then
        End If
    End Sub
End Module",
            // Test0.vb(5,53): warning CA1508: 'param = param2' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 53, "param = param2", "True"),
            // Test0.vb(9,32): warning CA1508: 'param2 <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 32, "param2 <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_CopyAnalysisAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int param, int param2)
    {
        int str = 0;
        if (param == str && param2 == str && param == param2)
        {
        }

        param = param2;
        if (param != str || param2 != str)
        {
        }
    }
}
",
            // Test0.cs(7,46): warning CA1508: 'param == param2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 46, "param == param2", "true"),
            // Test0.cs(12,29): warning CA1508: 'param2 != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(12, 29, "param2 != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As Integer, param2 As Integer)
        Dim str As Integer = 1
        If param = str AndAlso param2 = str AndAlso param = param2 Then
        End If

        param = param2
        If param <> str OrElse param2 <> str Then
        End If
    End Sub
End Module",
            // Test0.vb(5,53): warning CA1508: 'param = param2' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 53, "param = param2", "True"),
            // Test0.vb(9,32): warning CA1508: 'param2 <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 32, "param2 <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_WithNonLiteral_ConditionalOr_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, string param2, bool flag)
    {
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        if (param == str || param == str2)
        {
        }

        if (str2 != param || param == strMayBeConst)
        {
        }

        if (param == strMayBeConst || str2 != param)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        If param = str OrElse param = str2 Then
        End If

        If str2 <> param OrElse param = strMayBeConst Then
        End If

        If param = strMayBeConst OrElse str2 <> param Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ValueCompare_WithNonLiteral_ConditionalOr_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(uint param, uint param2, bool flag)
    {
        long str = 1;
        ulong str2 = flag ? 2UL : 3UL;
        ulong strMayBeConst = param2;

        if (param == str || param == str2)
        {
        }

        if (str2 != param || param == strMayBeConst)
        {
        }

        if (param == strMayBeConst || str2 != param)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As UInteger, param2 As UInteger, flag As Boolean)
        Dim str As Long = 1
        Dim str2 As ULong = DirectCast(If(flag, 2UL, 3UL), ULong)
        Dim strMayBeConst As Ulong = param2

        If param = str OrElse param = str2 Then
        End If

        If str2 <> param OrElse param = strMayBeConst Then
        End If

        If param = strMayBeConst OrElse str2 <> param Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_WithNonLiteral_ConditionalAnd_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, string param2, bool flag)
    {
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        if (param == str && param2 == str2)
        {
        }

        if (param == strMayBeConst && str2 == param)
        {
        }

        if (param != str && param != str2)
        {
        }

        if (param != str && param2 != str2)
        {
        }

        if (str2 != param && param == strMayBeConst)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        If param = str AndAlso param2 = str2 Then
        End If

        If param = strMayBeConst AndAlso str2 = param Then
        End If

        If str2 <> param AndAlso param <> str Then
        End If

        If str2 <> param AndAlso param2 <> str Then
        End If

        If str2 <> param AndAlso param = strMayBeConst Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ValueCompare_WithNonLiteral_ConditionalAnd_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int param, int param2, bool flag)
    {
        int str = 1;
        int str2 = flag ? 2 : 3;
        int strMayBeConst = param2;

        if (param == str && param2 == str2)
        {
        }

        if (param == strMayBeConst && str2 == param)
        {
        }

        if (param != str && param != str2)
        {
        }

        if (param != str && param2 != str2)
        {
        }

        if (str2 != param && param == strMayBeConst)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As Integer, param2 As Integer, flag As Boolean)
        Dim str As Integer = 1
        Dim str2 As Integer = If(flag, 2, 3)
        Dim strMayBeConst As Integer = param2

        If param = str AndAlso param2 = str2 Then
        End If

        If param = strMayBeConst AndAlso str2 = param Then
        End If

        If str2 <> param AndAlso param <> str Then
        End If
        
        If str2 <> param AndAlso param2 <> str Then
        End If
        
        If str2 <> param AndAlso param = strMayBeConst Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_ConditionalAndOrNegation_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, bool flag, string param2)
    {
        string strConst = """";
        string strConst2 = flag ? ""a"" : """";
        string strMayBeNonConst = flag ? ""c"" : param2;

        if (param == strConst || !(strConst2 != param) && param != strMayBeNonConst)
        {
        }

        if (!(strConst2 == param && !(param != strConst)) || param == strMayBeNonConst)
        {
        }

        if (param != strConst && !(strConst2 != param || param != strMayBeNonConst))
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim strConst As String = """"
        Dim strConst2 As String = If(flag, ""a"", """")
        Dim strMayBeNonConst As String = If(flag, ""c"", param2)

        If param = strConst OrElse Not(strConst2 <> param) AndAlso param <> strMayBeNonConst Then
        End If

        If Not(strConst2 = param AndAlso Not (param <> strConst)) OrElse param <> strMayBeNonConst Then
        End If

        If param <> strConst AndAlso Not(strConst2 <> param OrElse param <> strMayBeNonConst) Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_ConditionalAndOrNegation_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, bool flag, string param2, string param3)
    {
        string strConst = """";
        string strConst2 = flag ? ""a"" : """";
        string strMayBeNonConst = flag ? ""c"" : param2;

        // First and last conditions are opposites, so infeasible.
        if (param == strConst && !(strConst2 != param || param != strConst))
        {
        }

        // First and last conditions are identical.
        if (param == strConst && !(strConst2 != param || param == strConst)){
        }

        // Comparing with maybe const, no diagnostic
        if (param3 == strConst && !(strConst2 == param3 || param3 == strMayBeNonConst))
        {
        }

        // We don't track not-equals values, no diagnostic
        if (param3 != strConst && !(strConst2 != param3 || param3 != strConst))
        {
        }
    }
}
",
            // Test0.cs(11,58): warning CA1508: 'param != strConst' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 58, "param != strConst", "false"),
            // Test0.cs(16,58): warning CA1508: 'param == strConst' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 58, "param == strConst", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String, flag As Boolean, param3 As String)
        Dim strConst As String = """"
        Dim strConst2 As String = If(flag, ""a"", """")
        Dim strMayBeNonConst As String = If(flag, ""c"", param2)

        ' First and last conditions are opposites, so infeasible.
        If param = strConst AndAlso Not(strConst2 <> param OrElse param <> strConst) Then
        End If

        ' First and last conditions are identical.
        If param = strConst AndAlso Not(strConst2 <> param OrElse param = strConst) Then
        End If

        ' Comparing with maybe const, no diagnostic
        If param3 = strConst AndAlso Not(strConst2 = param3 OrElse param3 = strMayBeNonConst) Then
        End If

        ' We don't track not-equals values, no diagnostic
        If param3 <> strConst AndAlso Not(strConst2 <> param3 OrElse param3 <> strConst) Then
        End If
    End Sub
End Module",
            // Test0.vb(9,67): warning CA1508: 'param <> strConst' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 67, "param <> strConst", "False"),
            // Test0.vb(13,67): warning CA1508: 'param = strConst' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(13, 67, "param = strConst", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_ContractCheck_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        System.Diagnostics.Contracts.Contract.Requires(param != """");
    }

    void M2(string param, string param2)
    {
        param2 = """";
        System.Diagnostics.Contracts.Contract.Requires(param == """" || param2 != param);
    }

    void M3(string param, string param2, string param3)
    {
        System.Diagnostics.Contracts.Contract.Requires(param == param2 && !(param2 != """") || param2 == param3);
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Private Sub M1(ByVal param As String)
        System.Diagnostics.Contracts.Contract.Requires(param <> """")
    End Sub

    Private Sub M2(ByVal param As String, ByVal param2 As String)
        param2 = """"
        System.Diagnostics.Contracts.Contract.Requires(param = """" OrElse param2 <> param)
    End Sub

    Private Sub M3(ByVal param As String, ByVal param2 As String, param3 As String)
        System.Diagnostics.Contracts.Contract.Requires(param = param2 AndAlso Not(param2 <> """") OrElse param2 = param3)
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task StringCompare_ContractCheck_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param)
    {
        var str = """";
        param = """";
        System.Diagnostics.Contracts.Contract.Requires(param == str);
    }
}
",
            // Test0.cs(8,56): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(8, 56, "param == str", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Private Sub M(ByVal param As String)
        Dim str = """"
        param = """"
        System.Diagnostics.Contracts.Contract.Requires(param <> str)
    End Sub
End Module",
            // Test0.vb(6,56): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(6, 56, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(1650, "https://github.com/dotnet/roslyn-analyzers/issues/1650")]
        public async Task StringCompare_InsideConstructorInitializer_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public bool Flag;
}

class Base
{
    protected Base(bool b) { }
}

class Test : Base
{
    public Test(string s1, string s2, string s3)
        : base(s1 == s2 && s2 == s3 ? (s1 == s3) : false)
    {
        var x = s1 == s3;
    }
}
",
            // Test0.cs(15,40): warning CA1508: 's1 == s3' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(15, 40, "s1 == s3", "true"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(1650, "https://github.com/dotnet/roslyn-analyzers/issues/1650")]
        public async Task StringCompare_InsideFieldInitializer_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public bool Flag;
}

class Test
{
    private static string s1, s2, s3;
    private bool b = s1 == s2 && s2 == s3 ? (s1 == s3) : false;
}
",
            // Test0.cs(10,46): warning CA1508: 's1 == s3' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 46, "s1 == s3", "true"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(1650, "https://github.com/dotnet/roslyn-analyzers/issues/1650")]
        public async Task StringCompare_InsidePropertyInitializer_ExpressionBody_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public bool Flag;
}

class Test
{
    private static string s1, s2, s3, s4, s5, s6;
    private bool B1 => s1 == s2 && s2 == s3 ? (s1 == s3) : false;
    private bool B2 { get; } = s4 == s5 && s5 == s6 ? (s4 == s6) : false;
}
",
            // Test0.cs(10,48): warning CA1508: 's1 == s3' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 48, "s1 == s3", "true"),
            // Test0.cs(11,56): warning CA1508: 's4 == s6' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 56, "s4 == s6", "true"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_IsConstantPattern_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D: C
{
}

class Test
{
    void M1_IsConstantPattern_AlwaysTrue(int c1)
    {
        c1 = 5;
        if (c1 is 5)
        {
            return;
        }
    }

    void M1_IsConstantPattern_AlwaysFalse(int c2)
    {
        c2 = 10;
        if (c2 is 5)
        {
            return;
        }
    }

    void M1_IsConstantPattern_Conversion_AlwaysTrue(short c3)
    {
        c3 = (short)5;
        if (c3 is 5)
        {
            return;
        }
    }
}
",
            // Test0.cs(15,13): warning CA1508: 'c1 is 5' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(15, 13, "c1 is 5", "true"),
            // Test0.cs(24,13): warning CA1508: 'c2 is 5' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "c2 is 5", "false"),
            // Test0.cs(33,13): warning CA1508: 'c3 is 5' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(33, 13, "c3 is 5", "true"));

            // VB does not support patterns.
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_IsConstantPattern_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D: C
{
}

class Test
{
    void M1_IsConstantPattern(int c1)
    {
        if (c1 is 5)
        {
            return;
        }
    }

    void M1_IsConstantPattern_02(int c2, bool flag)
    {
        if (flag)
        {
            c2 = 5;
        }

        if (c2 is 5)
        {
            return;
        }
    }
}
");

            // VB does not support patterns.
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_GotoLoopAsync()
        {
            // Ensure we bound the number of value content literals
            // and avoid infinite analysis iterations.
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    internal static uint ComputeStringHash(string text)
    {
        uint hashCode = 0;
        if (text != null)
        {
            hashCode = unchecked((uint)2166136261);
 
            int i = 0;
            goto start;

again:
            hashCode = unchecked((text[i] ^ hashCode) * 16777619);
            i = i + 1;

start:
            if (i < text.Length)
                goto again;
        }
        return hashCode;
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_MayBeLiteralAssignedInLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Diagnostics;

class C
{
    public int Int { get; }
    public C[] ArrayOfC { get; }
    void M(int x)
    {
        int lastOffset = -1;
        foreach (var c in ArrayOfC)
        {
            int offset = c.Int;
            if (offset >= 0)
            {
                if (lastOffset != offset)
                {
                    Debug.Assert(lastOffset < offset);
                    Debug.Assert((lastOffset >= 0) || (offset == 0));
                    lastOffset = offset;
                }
                else
                {
                    System.Console.Write(offset);
                }
            }
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_YieldBreakInTryFinallyAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Collections.Generic;

class C
{
    private static IEnumerable<IList<T>> M<T>(List<IEnumerator<T>> enumerators)
    {
        try
        {
            while (true)
            {
                for (int i = 0; i < enumerators.Count; i++)
                {
                    var e = enumerators[i];
                    if (!e.MoveNext())
                    {
                        yield break;
                    }
                }
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                enumerator.Dispose();
            }
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_NullableBoolAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    private object Field;
    public void M(C c)
    {
        bool? status = c.Field?.Equals(c);
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_DefaultExpressionAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
struct S
{
}

class C
{
    public void M(S s)
    {
        if (object.Equals(s, default(S)))
        {
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_Boxing_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public void M(int i)
    {
        object o = i;       // Implicit boxing.
        if ((int)o == i)    // Always true.
        {
        }
        
        object o2 = (object)i;    // Explicit boxing with direct cast.
        if ((int)o2 == i)         // Always true.
        {
        }

        object o3 = i as object;    // Explicit boxing with try cast.
        if ((int)o3 == i)           // Always true.
        {
        }

        if (o == (object)i)    // Always false, but our current implementation is conservative and does not flag it.
        {
        }
    }
}",
    // Test0.cs(7,13): warning CA1508: '(int)o == i' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
    GetCSharpResultAt(7, 13, "(int)o == i", "true"),
    // Test0.cs(12,13): warning CA1508: '(int)o2 == i' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
    GetCSharpResultAt(12, 13, "(int)o2 == i", "true"),
    // Test0.cs(17,13): warning CA1508: '(int)o3 == i' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
    GetCSharpResultAt(17, 13, "(int)o3 == i", "true"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_Boxing_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public void M(int i, int i2, int i3, int i4)
    {
        object o = i;       // Implicit boxing.
        i = 1;
        if ((int)o == i)    // May or may not be true.
        {
        }
        if (o is 1)         // May or may not be true.
        {
        }
        
        object o2 = (object)i2;    // Explicit boxing with direct cast.
        i2 = 2;
        if ((int)o2 == i2)         // May or may not be true.
        {
        }
        if (o2 is 2)               // May or may not be true.
        {
        }

        object o3 = i3;       // Implicit boxing.
        o3 = 3;
        if ((int)o3 == i3)    // May or may not be true.
        {
        }
        if (i3 == 3)           // May or may not be true.
        {
        }

        object o4 = (object)i4;    // Explicit boxing with direct cast.
        o4 = 4;
        if ((int)o4 == i4)         // May or may not be true.
        {
        }
        if (i4 == 4)               // May or may not be true.
        {
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_Unboxing_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public void M(object o, object o2, object o3)
    {
        var i = (int)o;         // Explicit unboxing with direct cast.
        if ((int)o == i)        // Always true.
        {
        }

        if (o == (object)i)     // Always false, but our current implementation is conservative and does not flag it.
        {
        }

        var i2 = o2 as int?;    // Explicit unboxing with try cast involving nullable types.
        if ((int)o2 == i2)      // Always true, but our current implementation is conservative and does not flag it.
        {
        }

        if (o2 == (object)i2)   // Always false, but our current implementation is conservative and does not flag it.
        {
        }
    }
}",
            // Test0.cs(7,13): warning CA1508: '(int)o == i' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 13, "(int)o == i", "true"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task ValueCompare_Unboxing_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public void M(object o, object o2, object o3, object o4)
    {
        var i = (int)o;         // Explicit unboxing with direct cast.
        i = 1;
        if ((int)o == i)        // May or may not be true.
        {
        }

        var i2 = (int)o2;         // Explicit unboxing with direct cast.
        o2 = 2;
        if ((int)o2 == i2)        // May or may not be true.
        {
        }

        var i3 = o3 as int?;    // Explicit unboxing with try cast involving nullable types.
        i3 = 3;
        if ((int)o3 == i3)      // May or may not be true.
        {
        }

        var i4 = o4 as int?;    // Explicit unboxing with try cast involving nullable types.
        o4 = 4;
        if ((int)o4 == i4)      // May or may not be true.
        {
        }
    }
}");
        }

        [Fact, WorkItem(1571, "https://github.com/dotnet/roslyn-analyzers/issues/1571")]
        public async Task ValueCompare_AssignedToTuple_NotDisposed_SpecialCases_DiagnosticAsync()
        {
            // NOTE: We do not support predicate analysis for tuple binary operator comparison yet.
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

class A
{
    public A(int i) { }
}

public class Test
{
    // Tuple binary compare with nested tuple.    
    ((A, int), (A, int)) M1()
    {
        A a = new A(1);
        A a2 = new A(2);
        ((A, int), (A, int)) b = ((a2, 0), (a2, 0));
        var b2 = ((a2, 0), (a2, 0));
        if (b == b2)    // This should get flagged as always 'true' once we implement predicate analysis for ITupleBinaryOperation
        {
        }

        return b;
    }

    // Declaration expression target
    A M2()
    {
        A a = new A(3);
        var ((a2, x), y) = ((a, 0), 1);
        if (a2 == a)
        {
        }

        return null;
    }

    // Declaration expression target with discards
    A M3()
    {
        A a = new A(4);
        var ((a2, _), _) = ((a, 0), 1);
        if (a2 == null)
        {
        }

        return null;
    }

    // Declaration expressions in target
    A M4()
    {
        A a = new A(5);
        ((var a2, var x), var y) = ((a, 0), 1);
        if (a == a2 || x == 0 || y == 1)
        {
        }

        return null;
    }

    // Discards in target
    A M5()
    {
        A a = new A(6);
        ((var a2, _), _) = ((a, 0), 1);
        if (null == a2)
        {
        }

        return null;
    }

    // Tuple binary compare
    (A, A) M6()
    {
        A a = new A(7);
        A a2 = new A(8);
        var c = (a2, a2);
        var c2 = (a2, a2);
        if (c == c2)        // This should get flagged as always 'true' once we implement predicate analysis for ITupleBinaryOperation
        {
        }

        var c3 = (a2, a);
        if (c == c3)        // This should get flagged as always 'false' once we implement predicate analysis for ITupleBinaryOperation
        {
        }

        var c4 = (a, a);
        if (c == c4)        // This should get flagged as always 'false' once we implement predicate analysis for ITupleBinaryOperation
        {
        }

        return c;
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") },
                    ExpectedDiagnostics =
                    {
                        // Test0.cs(30,13): warning CA1508: 'a2 == a' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(30, 13, "a2 == a", "true"),
                        // Test0.cs(42,13): warning CA1508: 'a2 == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(42, 13, "a2 == null", "false"),
                        // Test0.cs(54,13): warning CA1508: 'a == a2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(54, 13, "a == a2", "true"),
                        // Test0.cs(54,24): warning CA1508: 'x == 0' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(54, 24, "x == 0", "true"),
                        // Test0.cs(54,34): warning CA1508: 'y == 1' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(54, 34, "y == 1", "true"),
                        // Test0.cs(66,13): warning CA1508: 'null == a2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(66, 13, "null == a2", "false"),
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp7_3
            }.RunAsync();
        }

        [Fact, WorkItem(1571, "https://github.com/dotnet/roslyn-analyzers/issues/1571")]
        public async Task ValueCompare_AddedToTupleLiteral_SpecialCases_DiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

class A
{
    public A(int i) { }
}

public class Test
{
    // Tuple literal assignment cases.
    void M1()
    {
        A a = new A(1);
        var x = ((a, 0), 1);
        if (x.Item1.Item1 == a || x.Item1.a == a || x.Item2 == 1)
        {
        }
    }

    void M2()
    {
        A a = new A(2);
        A a2 = new A(3);
        var x = (a, a2);
        if (x.a == a || x.Item2 == a2)
        {
        }
    }

    void M3(out (A a, A a2) arg)
    {
        A a = new A(4);
        A a2 = new A(5);
        arg = (a, a2);
        arg = default((A, A));  // We don't yet analyze default tuple expression, so below redundant conditionals are not flagged.
        if (arg.Item1 == a || arg.a2 == a2)
        {
        }
    }

    void M4(out (A a, A a2) arg)
    {
        A a = new A(6);
        A a2 = new A(7);
        var a3 = (a, a2);
        arg = a3;
        arg = (null, null);
        if (arg.a == a || arg.Item2 == a2)
        {
        }
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") },
                    ExpectedDiagnostics =
                    {
                        // Test0.cs(16,13): warning CA1508: 'x.Item1.Item1 == a' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(16, 13, "x.Item1.Item1 == a", "true"),
                        // Test0.cs(16,35): warning CA1508: 'x.Item1.a == a' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(16, 35, "x.Item1.a == a", "true"),
                        // Test0.cs(16,53): warning CA1508: 'x.Item2 == 1' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(16, 53, "x.Item2 == 1", "true"),
                        // Test0.cs(26,13): warning CA1508: 'x.a == a' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(26, 13, "x.a == a", "true"),
                        // Test0.cs(26,25): warning CA1508: 'x.Item2 == a2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(26, 25, "x.Item2 == a2", "true"),
                        // Test0.cs(49,13): warning CA1508: 'arg.a == a' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(49, 13, "arg.a == a", "false"),
                        // Test0.cs(49,27): warning CA1508: 'arg.Item2 == a2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
                        GetCSharpResultAt(49, 27, "arg.Item2 == a2", "false"),
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp7_3
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task MethodWithNonConstantReturn_DefaultSwitchCaseInsideLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Operation
{
    public OperationKind Kind { get; }
    public Operation Parent { get; }
}

enum OperationKind
{
    Kind1,
    Kind2,
    Kind3
}

class Test
{
    bool M(Operation operation)
    {
        return IsRootOfCondition() == true;

        bool IsRootOfCondition()
        {
            var current = operation.Parent;
            while (current != null)
            {
                if (current.Kind == OperationKind.Kind1)
                {
                    return false;
                }

                current = current.Parent;
            }

            return current == null;
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task LogicalOrWrappedInsideParenthesisAndUnaryAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Class Test
    Public Sub M(x1 As Boolean, x2 As Boolean, t As Test)
        Dim y = Not (x1 Or x2)
        t?.M2()
    End Sub

    Private Sub M2()
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task DoWhileLoopWithSwitchAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.Runtime.CompilerServices

Friend Module TypeSymbolExtensions
    <Extension()>
    Public Function VisitType(type As TypeSymbol, predicate As Func(Of TypeSymbol, Boolean)) As TypeSymbol
        Dim current As TypeSymbol = type

        Do
            Select Case current.TypeKind
                Case TypeKind.Class
            End Select

            If predicate(current) Then
                Return current
            End If

            Select Case current.TypeKind
                Case TypeKind.Array
                    current = DirectCast(current, ArrayTypeSymbol).ElementType
                    Continue Do
            End Select
        Loop
    End Function
End Module

Class TypeSymbol
    Public ReadOnly Property TypeKind As TypeKind
End Class

Class ArrayTypeSymbol
    Inherits TypeSymbol
    Public ReadOnly Property ElementType As TypeSymbol
End Class

Enum TypeKind
    [Class]
    Array
End Enum
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ConditionalAccessInConditionalAndOperandAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Class Test
    Public ReadOnly Property Flag As Boolean
    Public Sub M(t As Test, flag As Boolean)
        If t?.Flag AndAlso flag Then
        End If
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task LoopWithMethodInvocationInConditionalAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System

Class Test
    Public Function GetNextDirective(predicate As Func(Of SyntaxNode, Boolean), token As SyntaxToken, d As SyntaxNode) As SyntaxNode
        Do While (token.Kind <> SyntaxKind.None)
            If predicate(d) Then
                Return d
            End If
        Loop
        Return Nothing
    End Function
End Class

Class SyntaxNode
End Class

Structure SyntaxToken
    Public Property Kind As SyntaxKind
End Structure

Enum SyntaxKind
    None
    Kind1
End Enum
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task LoopWithGotoTargetBeforeLoopAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
class A
{
    public static A M(int? x, A[] listOfA, A a)
    {
    RETRY:
        if (a == null)
        {
            return null;
        }

        foreach (var element in listOfA)
        {
            if (x != 1)
            {
                goto RETRY;
            }
        }

        return a;
    }

    private Kind Kind { get; }
}

enum Kind
{
    Kind1,
    Kind2
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ConditionalAccess_OperationNoneAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.Xml.Linq

Class Test
    Public Sub M(arg As XElement)
        Dim x = If(arg.@name, """")
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task Assignment_OperationNoneAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.Xml.Linq

Class Test
    Public Sub M(arg As XElement, arg2 As XElement)
        Dim x = If(arg, arg2)
        Dim a = arg.@name
        arg.@name = """"
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ConditionalExpression_OperationNoneAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.Linq

Class Test
    Public Function M(ifNode As SingleLineIfStatementSyntax) As Boolean
        Return TypeOf ifNode.Parent IsNot SingleLineLambdaExpressionSyntax AndAlso
                Not ifNode.Statements.Any(Function(n) n.IsKind(SyntaxKind.Kind1)) AndAlso
                Not If(ifNode.ElseClause?.Statements.Any(Function(n) n.IsKind(SyntaxKind.Kind1)), False)
    End Function
End Class

Class SingleLineIfStatementSyntax
    Inherits SyntaxNode
    Public ReadOnly Property Statements As SyntaxNode()
    Public ReadOnly Property ElseClause As SingleLineIfStatementSyntax
End Class

Class SingleLineLambdaExpressionSyntax
    Inherits SyntaxNode
End Class

Class SyntaxNode
    Public ReadOnly Property Parent As SyntaxNode
    Public Function IsKind(kind As SyntaxKind) As Boolean
        Return True
    End Function
End Class

Enum SyntaxKind
    None
    Kind1
    Kind2
End Enum
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task PointsToAnalysisForLoopOnStructFieldsAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;

namespace ClassLibrary14
{
    public class Class2
    {
        struct S
        {
            public string f1;
            public string f2;
            public bool f3;
            public bool f4;
            public bool f5;
            public bool f6;
        };

        public void M(Class2 c)
        {
            c?.M(null);
            S[] filesToReplace = new[]
            {
                new S { f1=""file1"", f2=@""string1"",f3=true, f4=true, f5=false, f6=true},
                new S { f1=""file2"",f2=@""string2"", f3=true, f4=true, f5=false, f6=true},
                new S { f1=""file3"", f2=@""string2"",f3=false, f4=false, f5=false, f6=true},
                new S { f1=""file4"", f2=@""string2"", f3=false, f4=false, f5=true, f6=true},
                new S { f1=""file5"", f2=@""string2"", f3=false, f4=false, f5=true, f6=false}
            };

            foreach (S fileDetails in filesToReplace)
            {
                bool fileIsUpToDate = false;
                var x1 = fileDetails.f1;
                var destinationDir = fileDetails.f2;
                var x3 = Path.Combine(destinationDir, fileDetails.f1);
                var x4 = fileDetails.f1;

                foreach (var residual in Directory.GetFiles(destinationDir, fileDetails.f1 + "".delete.*""))
                {
                }
            }
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ValueContentAnalysisWithLocalFunctionInvocationsInStaticMethodsAsync()
        {
            var editorconfig = "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

public static class C
{
    public static float NextSingle(this Random random, float minValue, float maxValue)
    {
        float AdjustValue(float value) => Single.IsNegativeInfinity(value) ? Single.MinValue : (Single.IsPositiveInfinity(value) ? Single.MaxValue : value);

        return (float)random.NextDouble(AdjustValue(minValue), AdjustValue(maxValue));
    }

    public static double NextDouble(this Random random, double minValue, double maxValue)
    {
        return minValue;
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorconfig}
") }
                }
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task PredicateAnalysisWithCastAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

public static class C
{
    private static int _f;
    public static bool M1(int x, int y)
    {
        y = x > 0 ?  x : 0;
        return !(bool)GetObject(y);
    }

    private static object GetObject(int o)
    {
        return (object)(o > _f);
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(2246, "https://github.com/dotnet/roslyn-analyzers/issues/2246")]
        public async Task NestedPredicateAnalysisWithDifferentStringsAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

public static class C
{
    private static bool Test(string A, string B, string C, string D)
    {
        bool result = false;

        if (string.Compare(A, B, StringComparison.OrdinalIgnoreCase) == 0)
        {
            if (string.Compare(C, D, StringComparison.OrdinalIgnoreCase) == 0)
            {
                result = true;
            }
        }

        return result;
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        [WorkItem(2681, "https://github.com/dotnet/roslyn-analyzers/issues/2681")]
        public async Task InterlockedOperations_NoDiagnosticAsync()
        {
            // Ensure that Interlocked increment/decrement/add operations
            // are not treated as absolute writes as it likely involves multiple threads
            // invoking the method and that can lead to false positives.
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    private int a;
    void M1()
    {
        a = 0;
        System.Threading.Interlocked.Increment(ref a);
        if (a == 1)
        {
        }

        a = 1;
        System.Threading.Interlocked.Decrement(ref a);
        if (a == 0)
        {
        }

        a = 2;
        System.Threading.Interlocked.Add(ref a, 1);
        if (a == 3)
        {
        }
    }
}");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1()
        Dim a As Integer = 0
        System.Threading.Interlocked.Increment(a)
        If a = 1 Then
        End If

        a = 1
        System.Threading.Interlocked.Decrement(a)
        If a = 0 Then
        End If

        a = 2
        System.Threading.Interlocked.Add(a, 1)
        If a = 3 Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact]
        public async Task ValueContentAnalysis_MergeForUnreachableCodeAsync()
        {
            var editorconfig = "dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;

public class C
{
    public void Load(C c1, C c2)
    {
        var x = c1 ?? c2;
        this.Load(null);
    }

    public void Load(Uri productFileUrl, Uri originalLocation = null)
    {
        if (productFileUrl == null)
        {
            throw new ArgumentNullException();
        }

        Uri feedLocationUri = originalLocation ?? productFileUrl;

        _ = feedLocationUri.LocalPath;
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorconfig}
") }
                }
            }.RunAsync();
        }

        [Theory]
        [InlineData("struct", "struct")]
        [InlineData("struct", "class")]
        [InlineData("class", "struct")]
        [InlineData("class", "class")]
        public async Task DataflowAcrossBranchesAsync(string typeTest, string typeA)
        {
            var test = new VerifyCS.Test
            {
                TestCode = $@"
using System;

namespace TestNamespace
{{
    public {typeA} A
    {{
        public int IntProperty {{ get; set; }}
    }}

    public {typeTest} Test
    {{
        public A A;

        public void Something(int param)
        {{
            Test t = new Test();
            t.A = new A();
            t.A.IntProperty = param;
            A a = new A();
            a.IntProperty = param;
            A a1 = new A();
            a1.IntProperty = param;
            A a2 = new A();
            a2.IntProperty = param;
            if (param >= 0)
            {{
                a1.IntProperty = 1;
                t.A = a1;                    // t.A now contains/points to a1
                a = a2;
            }}
            else
            {{
                a2.IntProperty = 1;
                t.A = a2;                    // t.A now contains/points to a2   
                a = a1;
            }}
        
            if (t.A.IntProperty == 1)        // t.A now contains/points either a1 or a2, both of which have .IntProperty = """"
                                             // However, we conservatively don't report it when 'A' is a class
            {{
            }}

            if (a.IntProperty == 1)          // a points to a1 or a2, and a.IntProperty = param for both cases.
            {{
            }}
        }}
    }}
}}"
            };

            if (typeA != "class")
            {
                test.ExpectedDiagnostics.Add(
                    // Test0.cs(33,17): warning CA1508: 't.A.IntProperty == 1' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                    GetCSharpResultAt(39, 17, "t.A.IntProperty == 1", "true"));
            }

            await test.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task TestNegationPatternAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    bool M(int input, object t)
    {
        var x = t?.ToString();
        if (input is not 10)
        {
            return input == 10;
        }

        if (t is not null)
        {
            return t != null;
        }

        return true;
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9,
                ExpectedDiagnostics =
                {
                    // Test0.cs(14,20): warning CA1508: 't != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                    GetCSharpResultAt(14, 20, "t != null", "true")
                }
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task TestNegationPattern_SwitchCaseAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    bool M(int input, object t)
    {
        var x = t?.ToString();

        bool result;
        switch (input)
        {
            case not 10:
                result = false;
                break;
            default:
                result = true;
                break;
        }

        switch (t)
        {
            case not null:
                result = t != null;
                break;
            default:
                result = t == null;
                break;
        }

        return result;
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9,
                ExpectedDiagnostics =
                {
                    // Test0.cs(22,26): warning CA1508: 't != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                    GetCSharpResultAt(22, 26, "t != null", "true"),
                    // Test0.cs(25,26): warning CA1508: 't == null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                    GetCSharpResultAt(25, 26, "t == null", "true")
                }
            }.RunAsync();
        }

        [Fact, WorkItem(4062, "https://github.com/dotnet/roslyn-analyzers/issues/4062")]
        public async Task TestNegationPattern_ExplicitConversionInFlowCapture_SwitchCaseAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    void M(int input, object t)
    {
        var x = t?.ToString();

        bool result;
        switch ((bool)t)
        {
            case not true:
                result = (bool)t == false;  // Consider: this should report a diagnostic
                break;
        }
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact, WorkItem(4062, "https://github.com/dotnet/roslyn-analyzers/issues/4062")]
        public async Task Test_ExplicitConversionInFlowCapture_ConditionalExpressionAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
public class Test
{
    private object M(string str)
    {
        int? intVal = int.TryParse(str, out var outInt) ? outInt : (int?)null;
        return (intVal.HasValue ? (object)intVal.Value : (object)str);
    }
}
");
        }

        [Fact, WorkItem(4062, "https://github.com/dotnet/roslyn-analyzers/issues/4062")]
        public async Task Test_ExplicitConversionInFlowCapture_ConditionalExpression_02Async()
        {
            await VerifyCSharpAnalyzerAsync(@"
public class Test
{
    private object M(Test t, int? intVal)
    {
        return (intVal.HasValue ? (object)intVal.Value : (object)t);
    }
}
");
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task TestRelationalPatternAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    bool M(int input, object t)
    {
        var x = t?.ToString();

        if (t is 10)
        {
            return (int)t == 10;
        }

        if (t is > 10)
        {
            return (int)t > 10;
        }

        return true;
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9,
                ExpectedDiagnostics =
                {
                    // Test0.cs(10,20): warning CA1508: '(int)t == 10' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                    GetCSharpResultAt(10, 20, "(int)t == 10", "true")
                }
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task TestRelationalPattern_SwitchCaseAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    bool M(int input, object t)
    {
        var x = t?.ToString();

        bool result;
        switch (input)
        {
            case > 10:
                result = false;
                break;
            default:
                result = true;
                break;
        }

        return result;
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task TestNegationAndRelationalPatternAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    bool M(int input, object t)
    {
        var x = t?.ToString();

        if (input is not > 10)
        {
            return input > 10;  // No range analysis to flag this as dead code.
        }

        return true;
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task TestNegationAndRelationalPattern_SwitchCaseAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    bool M(int input, object t)
    {
        var x = t?.ToString();

        bool result;
        switch (input)
        {
            case not > 10:
                result = false;
                break;
            default:
                result = true;
                break;
        }

        return result;
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task TestBinaryPatternAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    bool M(int input, object t)
    {
        var x = t?.ToString();

        if (t is Test or not null)
        {
            return t != null;   // In future, we might flag this as always 'true'.
        }

        return true;
    }
}
"
                    }
                },
                LanguageVersion = CSharpLanguageVersion.CSharp9
            }.RunAsync();
        }

#if NETCOREAPP
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(4387, "https://github.com/dotnet/roslyn-analyzers/issues/4387")]
        public async Task RangeAndIndexOperation_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
internal class Class1
{
    private static bool TryParseUnit(string unit)
    {
        char last = unit[^1];
        if (last != 'b')
            return false;

        string subUnit = unit[1..];
        if (subUnit != ""b"")
            return false;

        return true;
    }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }
#endif

        [Fact, WorkItem(5789, "https://github.com/dotnet/roslyn-analyzers/issues/5789")]
        public async Task TestVarPattern()
        {
            var source = @"
public class C
{
    public void M(object o)
    {
        if (o is var o2 && o2 != null)
        {
        }
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(6453, "https://github.com/dotnet/roslyn-analyzers/issues/6453")]
        public async Task IndexedValueCompare_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Collections.Generic;

sealed class Data
{
    public int Value { get; }
    public Data(int value)
    {
        Value = value;
    }
}

static class Test
{
    static void Filter(List<Data> list, int j)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            if (list[i + 1].Value != 0) continue;
            if (list[i].Value == 0) continue; // <-------- CA1508 False positive
            list.RemoveAt(i);
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(6532, "https://github.com/dotnet/roslyn-analyzers/issues/6532")]
        public Task TestTernaryOperator_NoDiagnosticAsync()
        {
            return VerifyCSharpAnalyzerAsync(@"
using System.Collections.Generic;

class Test
{
    void M()
    {
        var i = 0;
        i += M2() ? 1 : 0;
        _ = i != 0;
    }

    bool M2() => true;
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(6532, "https://github.com/dotnet/roslyn-analyzers/issues/6532")]
        public Task TestTernaryOperator2_NoDiagnosticAsync()
        {
            return VerifyCSharpAnalyzerAsync(@"
using System.Collections.Generic;

class Test
{
    void M()
    {
        var i = 1;
        i += M2() ? 1 : 0;
        _ = i != 2;
    }

    bool M2() => true;
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(6164, "https://github.com/dotnet/roslyn-analyzers/issues/6164")]
        public async Task DefaultNullableStringValue_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
#nullable enable

using System;

class C
{
    public static void Bar(ConsoleColor c)
    {
        string? color = default;
        if (c == ConsoleColor.Green)
            color = ""green"";

        if (color != null)
        {
        }
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(6483, "https://github.com/dotnet/roslyn-analyzers/issues/6483")]
        public async Task IsPatternExpression_Unboxing_NoDiagnosticsAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
class C
{
    public void Run(object value)
    {
        if (value is null)
        {
            return;
        }

        var x = value is double num1;
        if (x)
        {
            return;
        }

        if (!(value is int num2))
        {
            return;
        }
    }
}",
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(5160, "https://github.com/dotnet/roslyn-analyzers/issues/5160")]
        public async Task CatchBlock_WithinForLoop_NoDiagnosticsAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;

class C
{
    void M1()
    {
        const int attempts = 5;

        for (int i = 0; i < attempts; i++)
        {
            try
            {
                Console.WriteLine($""Hello world: {i}"");

                if (i == attempts - 1) // Last iteration.
                {
                    throw new InvalidOperationException(""Oops, something went wrong!"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($""Exception caught: {ex.Message}"");

                if (i == attempts - 1)
                {
                    Console.WriteLine(""This should never happen, according to CA1508."");
                }
            }
        }
    }

    void M2()
    {
        for (int i = 0; i <= 5; i++)
        {
            try
            {
                MayThrowException(i);
                if (i == 0)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                if (i == 1)
                {
                    Console.WriteLine();
                }
            }
        }
    }

    private void MayThrowException(int i)
    {
        if (i % 2 == 1)
        {
            throw new Exception();
        }
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(6983, "https://github.com/dotnet/roslyn-analyzers/issues/6983")]
        public Task DebugAssert_NoDiagnostic()
        {
            const string code = """
                                using System.Diagnostics;
                                
                                public static class MyClass
                                {
                                    internal const int MyConstant = 16;
                                
                                    public static void MyMethod()
                                    {
                                        Debug.Assert(MyConstant == 16);
                                    }
                                }
                                """;
            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(6983, "https://github.com/dotnet/roslyn-analyzers/issues/6983")]
        public Task DebugAssertWithMessage_NoDiagnostic()
        {
            const string code = """
                                using System.Diagnostics;
                                
                                public static class MyClass
                                {
                                    internal const int MyConstant = 16;
                                
                                    public static void MyMethod()
                                    {
                                        Debug.Assert(MyConstant == 16, "MyConstant is not 16");
                                    }
                                }
                                """;
            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
        [Fact, WorkItem(6983, "https://github.com/dotnet/roslyn-analyzers/issues/6983")]
        public Task AsMethodArgument_Diagnostic()
        {
            const string code = """
                                using System.Diagnostics;

                                public static class MyClass
                                {
                                    internal const int MyConstant = 16;
                                
                                    public static void MyMethod()
                                    {
                                        Test({|#0:MyConstant == 16|});
                                    }
                                    
                                    private static void Test(bool b) => throw null;
                                }
                                """;
            return new VerifyCS.Test
            {
                TestCode = code,
                ExpectedDiagnostics = { new DiagnosticResult(AvoidDeadConditionalCode.AlwaysTrueFalseOrNullRule).WithLocation(0).WithArguments("MyConstant == 16", "true") }
            }.RunAsync();
        }
    }
}
