// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidDeadConditionalCode,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.AvoidDeadConditionalCode,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
    public partial class AvoidDeadConditionalCodeTests
    {
        private static DiagnosticResult GetCSharpResultAt(int line, int column, string condition, string reason)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(AvoidDeadConditionalCode.AlwaysTrueFalseOrNullRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(condition, reason);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string condition, string reason)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(AvoidDeadConditionalCode.AlwaysTrueFalseOrNullRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(condition, reason);

        private static DiagnosticResult GetCSharpNeverNullResultAt(int line, int column, string condition, string reason)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(AvoidDeadConditionalCode.NeverNullRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(condition, reason);

        private static DiagnosticResult GetBasicNeverNullResultAt(int line, int column, string condition, string reason)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(AvoidDeadConditionalCode.NeverNullRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(condition, reason);

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static async Task VerifyBasicAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { source },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") },
                }
            };

            vbTest.ExpectedDiagnostics.AddRange(expected);

            await vbTest.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task SimpleNullCompare_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        if (param == null)
        {
        }

        if (null == param)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        If param Is Nothing Then
        End If

        If Nothing Is param Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task SimpleNullCompare_AfterAssignment_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        param = null;
        if (param == null)
        {
        }

        if (null != param)
        {
        }
    }
}
",
            // Test0.cs(7,13): warning CA1508: 'param == null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 13, @"param == null", "true"),
            // Test0.cs(11,13): warning CA1508: 'null != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 13, @"null != param", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        param = Nothing
        If param Is Nothing Then
        End If

        If Nothing IsNot param Then
        End If
    End Sub
End Module",
            // Test0.vb(5,12): warning CA1508: 'param Is Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 12, "param Is Nothing", "True"),
            // Test0.vb(8,12): warning CA1508: 'Nothing IsNot param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 12, "Nothing IsNot param", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_NullCompare_IsNullValue_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param, C param2)
    {
        C cNull = null;
        C cNotNull = new C();
        C cMayBeNull = param2;
        
        if (param != cNull)
        {
        }
        else if (param == cNotNull)
        {
        }
        else if (param == cMayBeNull)
        {
            if (param != cNotNull)
            {
            }
        }

        if (cNull == param)
        {
            if (param != cNull)
            {
            }

            if (param != cNotNull)
            {
            }

            if (param != cMayBeNull)
            {
            }

            if (param == cNull)
            {
            }

            if (param == cNotNull)
            {
            }

            if (param == cMayBeNull)
            {
            }
        }
    }
}
",
            // Test0.cs(17,18): warning CA1508: 'param == cNotNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(17, 18, "param == cNotNull", "false"),
            // Test0.cs(22,17): warning CA1508: 'param != cNotNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(22, 17, "param != cNotNull", "true"),
            // Test0.cs(29,17): warning CA1508: 'param != cNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(29, 17, "param != cNull", "false"),
            // Test0.cs(33,17): warning CA1508: 'param != cNotNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(33, 17, "param != cNotNull", "true"),
            // Test0.cs(41,17): warning CA1508: 'param == cNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(41, 17, "param == cNull", "true"),
            // Test0.cs(45,17): warning CA1508: 'param == cNotNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(45, 17, "param == cNotNull", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Sub M(param As C, param2 As C)
        Dim cNull As C = Nothing
        Dim cNotNull As C = New C()
        Dim cMayBeNull As C = param2

        If param IsNot cNull Then
        ElseIf param Is cNotNull Then
        ElseIf param Is cMayBeNull Then
            If param IsNot cNotNull Then
            End If
        End If

        If cNull Is param Then
            If param IsNot cNull Then
            End If

            If param IsNot cNotNull Then
            End If

            If param IsNot cMayBeNull Then
            End If

            If param Is cNull Then
            End If

            If param Is cNotNull Then
            End If

            If param Is cMayBeNull Then
            End If
        End If
    End Sub
End Module",
            // Test0.vb(12,16): warning CA1508: 'param Is cNotNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(12, 16, "param Is cNotNull", "False"),
            // Test0.vb(14,16): warning CA1508: 'param IsNot cNotNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(14, 16, "param IsNot cNotNull", "True"),
            // Test0.vb(19,16): warning CA1508: 'param IsNot cNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(19, 16, "param IsNot cNull", "False"),
            // Test0.vb(22,16): warning CA1508: 'param IsNot cNotNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(22, 16, "param IsNot cNotNull", "True"),
            // Test0.vb(28,16): warning CA1508: 'param Is cNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(28, 16, "param Is cNull", "True"),
            // Test0.vb(31,16): warning CA1508: 'param Is cNotNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(31, 16, "param Is cNotNull", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_NullCompare_IsNotNullValue_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param, C param2)
    {
        C cNull = null;
        C cNotNull = new C();
        C cMayBeNull = param2;
        
        if (param != cNotNull)
        {
        }
        else if (param == cNull)
        {
        }
        else if (param == cMayBeNull)
        {
            if (param != cNull)
            {
            }
        }

        if (cNotNull == param)
        {
            if (param != cNull)
            {
            }

            if (param != cNotNull)
            {
            }

            if (param != cMayBeNull)
            {
            }

            if (param == cNull)
            {
            }

            if (param == cNotNull)
            {
            }

            if (param == cMayBeNull)
            {
            }
        }
    }
}
",
            // Test0.cs(17,18): warning CA1508: 'param == cNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(17, 18, "param == cNull", "false"),
            // Test0.cs(22,17): warning CA1508: 'param != cNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(22, 17, "param != cNull", "true"),
            // Test0.cs(29,17): warning CA1508: 'param != cNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(29, 17, "param != cNull", "true"),
            // Test0.cs(33,17): warning CA1508: 'param != cNotNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(33, 17, "param != cNotNull", "false"),
            // Test0.cs(41,17): warning CA1508: 'param == cNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(41, 17, "param == cNull", "false"),
            // Test0.cs(45,17): warning CA1508: 'param == cNotNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(45, 17, "param == cNotNull", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Sub M(param As C, param2 As C)
        Dim cNull As C = Nothing
        Dim cNotNull As C = New C()
        Dim cMayBeNull As C = param2

        If param IsNot cNotNull Then
        ElseIf param Is cNotNull Then
        ElseIf param Is cMayBeNull Then
            If param IsNot cNotNull Then
            End If
        End If

        If cNotNull Is param Then
            If param IsNot cNull Then
            End If

            If param IsNot cNotNull Then
            End If

            If param IsNot cMayBeNull Then
            End If

            If param Is cNull Then
            End If

            If param Is cNotNull Then
            End If

            If param Is cMayBeNull Then
            End If
        End If
    End Sub
End Module",
            // Test0.vb(12,16): warning CA1508: 'param Is cNotNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(12, 16, "param Is cNotNull", "True"),
            // Test0.vb(14,16): warning CA1508: 'param IsNot cNotNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(14, 16, "param IsNot cNotNull", "False"),
            // Test0.vb(19,16): warning CA1508: 'param IsNot cNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(19, 16, "param IsNot cNull", "True"),
            // Test0.vb(22,16): warning CA1508: 'param IsNot cNotNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(22, 16, "param IsNot cNotNull", "False"),
            // Test0.vb(28,16): warning CA1508: 'param Is cNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(28, 16, "param Is cNull", "False"),
            // Test0.vb(31,16): warning CA1508: 'param Is cNotNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(31, 16, "param Is cNotNull", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_NullCompare_IsNotNotNullValue_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param, C param2)
    {
        C cNull = null;
        C cNotNull = new C();
        C cMayBeNull = param2;
        
        if (param == cNotNull) // We do not track not-equal values so comparisons with cNotNull below are not flagged. 
        {
        }
        else if (param == cNull)
        {
        }
        else if (param == cMayBeNull)
        {
            if (param != cNotNull)
            {
            }
        }

        if (cNotNull != param)  // We do not track not-equal values so comparisons with cNotNull below are not flagged. 
        {
            if (param != cNull)
            {
            }

            if (param != cNotNull)
            {
            }

            if (param != cMayBeNull)
            {
            }

            if (param == cNull)
            {
            }

            if (param == cNotNull)
            {
            }

            if (param == cMayBeNull)
            {
            }
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Sub M(param As C, param2 As C)
        Dim cNull As C = Nothing
        Dim cNotNull As C = New C()
        Dim cMayBeNull As C = param2

        If param Is cNotNull Then   ' We do not track not-equal values so comparisons with cNotNull below are not flagged.
        ElseIf param Is cNotNull Then
        ElseIf param Is cMayBeNull Then
            If param IsNot cNotNull Then
            End If
        End If

        If cNotNull IsNot param Then    ' We do not track not-equal values so comparisons with cNotNull below are not flagged.
            If param IsNot cNull Then
            End If

            If param IsNot cNotNull Then
            End If

            If param IsNot cMayBeNull Then
            End If

            If param Is cNull Then
            End If

            If param Is cNotNull Then
            End If

            If param Is cMayBeNull Then
            End If
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ElseIf_NestedIf_NullCompare_IsMayBeNullValue_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param, C param2)
    {
        C cNull = null;
        C cNotNull = new C();
        C cMayBeNull = param2;
        
        if (param == cMayBeNull) // We do not track maybe values so comparisons with cMayBeNull below are not flagged. 
        {
        }
        else if (param == cNull)
        {
        }
        else if (param == cMayBeNull)
        {
            if (param != cNotNull)
            {
            }
        }

        if (cMayBeNull != param)  // We do not track maybe values so comparisons with cMayBeNull below are not flagged. 
        {
            if (param != cNull)
            {
            }

            if (param != cNotNull)
            {
            }

            if (param != cMayBeNull)
            {
            }

            if (param == cNull)
            {
            }

            if (param == cNotNull)
            {
            }

            if (param == cMayBeNull)
            {
            }
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Sub M(param As C, param2 As C)
        Dim cNull As C = Nothing
        Dim cNotNull As C = New C()
        Dim cMayBeNull As C = param2

        If param Is cMayBeNull Then   ' We do not track maybe values so comparisons with cNotNull below are not flagged.
        ElseIf param Is cNotNull Then
        ElseIf param Is cMayBeNull Then
            If param IsNot cNotNull Then
            End If
        End If

        If cMayBeNull IsNot param Then    ' We do not track maybe values so comparisons with cNotNull below are not flagged.
            If param IsNot cNull Then
            End If

            If param IsNot cNotNull Then
            End If

            If param IsNot cMayBeNull Then
            End If

            If param Is cNull Then
            End If

            If param Is cNotNull Then
            End If

            If param Is cMayBeNull Then
            End If
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_WhileLoop()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param)
    {
        string str = null;
        while (param == str)
        {
            // param = null here
            if (param == str)
            {
            }
            if (param != str)
            {
            }
        }

        // param is not-null here
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
            // Test0.cs(19, 13): warning CA1508: 'str == param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(19, 13, "str == param", "false"),
            // Test0.cs(22,13): warning CA1508: 'str != param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(22, 13, "str != param", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' While loop
    Private Sub M1(ByVal param As String)
        Dim str As String = Nothing
        While param = str
            ' param == null here
            If param = str Then
            End If
            If param <> str Then
            End If
        End While

        ' param is non-null here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(8,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 16, "param = str", "True"),
            // Test0.vb(10,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(10, 16, "param <> str", "False"),
            // Test0.vb(15,12): warning CA1508: 'str = param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(15, 12, "str = param", "False"),
            // Test0.vb(17,12): warning CA1508: 'str <> param' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(17, 12, "str <> param", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_WhileLoop_02()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2)
    {
        if (param != null)
        {
            while (param != null)
            {
                param = param2;
            }
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' While loop
    Private Sub M1(param As String, param2 As String)
        If param IsNot Nothing Then
            While param IsNot Nothing
                param = param2
            End While
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_WhileLoop_03()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public C ContainingC => new C();
}

class Test
{
    void M(C param)
    {
        if (param == null || param.ContainingC == null)
        {
            return;
        }

        while (param != null)
        {
            param = param.ContainingC;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
    Public ReadOnly Property ContainingC As C
End Class

Module Test
    Private Sub M1(param As C)
        If param Is Nothing OrElse param.ContainingC Is Nothing Then
            Return
        End If

        While param IsNot Nothing
            param = param.ContainingC
        End While
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_WhileLoop_04()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}
class Test
{
    private Test Next { get; }
    private C[] CInstances { get; }
    void M(bool flag)
    {
        var current = this;
        while (current != null)
        {
            foreach (var x in current.CInstances)
            {
                if (flag) return;
            }
            current = current.Next;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class Test
    Private Property [Next] As Test
    Private Property CInstances As C()

    Private Sub M(ByVal flag As Boolean)
        Dim current = Me
        While current IsNot Nothing
            For Each x In current.CInstances
                If flag Then Return
            Next

            current = current.[Next]
        End While
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_WhileLoop_05()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C[] args)
    {
        C local = null;
        int i = 0;
        while (i < args.Length)
        {
            // local may or may not be null here.
            if (local == null)
            {
                local = new C();
            }

            i++;
        }
        
        // local may or may not be null here.
        if (local != null)
        {
            return;
        }
    }
}

class C
{
}
");
            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(args As C())
        Dim local As C = Nothing
        Dim i As Integer = 0
        While i < args.Length
            ' local may or may not be null here.
            If local Is Nothing Then
                local = New C()
            End If
            i = i + 1
        End While

        ' local may or may not be null here.
        If local IsNot Nothing Then
            Return
        End If
    End Sub
End Module

Class C
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_WhileLoop_WithBreak()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2, bool flag)
    {
        string str = null;
        while (param == str)
        {
            // param = null here
            if (param == str)
            {
            }
            
            if (flag)
            {
                param = param2;
                break;
            }

            // param = null here
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
            // Test0.cs(21,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(21, 17, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' While loop
    Private Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str As String = Nothing
        While param = str
            ' param == null here
            If param = str Then
            End If
            If flag Then
                param = param2
                Exit While
            End If
            ' param == null here
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
            // Test0.vb(15,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(15, 16, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_WhileLoop_WithContinue()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2, string param3, bool flag)
    {
        string str = null;
        param3 = str;
        while (param == str)
        {
            // param = null here.
            if (param == str)
            {
            }
            
            // param3 is unknown here due to continue statement in the loop.
            if (param3 == str)
            {
            }
            
            if (flag)
            {
                param3 = param2;
                continue;
            }

            // param = null here.
            if (param != str)
            {
            }
            
            // param3 is unknown here due to continue statement in the loop.
            if (param3 != str)
            {
            }
        }

        // param = null here
        if (str == param)
        {
        }
        if (str != param)
        {
        }
    }
}
",
            // Test0.cs(11,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 17, "param == str", "true"),
            // Test0.cs(27,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(27, 17, "param != str", "false"),
            // Test0.cs(38,13): warning CA1508: 'str == param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(38, 13, "str == param", "false"),
            // Test0.cs(41,13): warning CA1508: 'str != param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(41, 13, "str != param", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' While loop
    Private Sub M1(param As String, param2 As String, param3 As String, flag As Boolean)
        Dim str As String = Nothing
        param3 = str
        While param = str
            ' param == null here
            If param = str Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If param3 = str Then
            End If
            If flag Then
                param3 = param2
                Continue While
            End If
            ' param == null here
            If param <> str Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If param3 <> str Then
            End If
        End While

        ' param = null here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(9,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 16, "param = str", "True"),
            // Test0.vb(19,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(19, 16, "param <> str", "False"),
            // Test0.vb(27,12): warning CA1508: 'str = param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(27, 12, "str = param", "False"),
            // Test0.vb(29,12): warning CA1508: 'str <> param' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(29, 12, "str <> param", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_DoWhileLoop()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M(C param)
    {
        C cNotNull = new C();
        do
        {
            // param is unknown here
            if (cNotNull == param)
            {
            }
            if (cNotNull != param)
            {
            }
        }
        while (param != cNotNull);

        // param = cNotNull here
        if (param == cNotNull)
        {
        }
        if (param != cNotNull)
        {
        }
    }
}
",
            // Test0.cs(24,13): warning CA1508: 'param == cNotNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "param == cNotNull", "true"),
            // Test0.cs(27,13): warning CA1508: 'param != cNotNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(27, 13, "param != cNotNull", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    ' Do-While top loop
    Private Sub M(ByVal param As String)
        Dim cNotNull As New C()
        Do While param IsNot cNotNull
            ' param is unknown here
            If cNotNull Is param Then
                End If
            If cNotNull IsNot param Then
            End If
        Loop

        ' param == cNotNull here
        If param Is cNotNull Then
        End If
        If param IsNot cNotNull Then
        End If
    End Sub

    ' Do-While bottom loop
    Private Sub M2(ByVal param2 As String)
        Dim cNotNull As New C()
        Do
            ' param2 is unknown here
            If cNotNull Is param2 Then
                End If
            If cNotNull IsNot param2 Then
            End If
        Loop While param2 IsNot cNotNull

        ' param2 == cNotNull here
        If param2 Is cNotNull Then
        End If
        If param2 IsNot cNotNull Then
        End If
    End Sub
End Module",
            // Test0.vb(18,12): warning CA1508: 'param Is cNotNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(18, 12, "param Is cNotNull", "True"),
            // Test0.vb(20,12): warning CA1508: 'param IsNot str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(20, 12, "param IsNot cNotNull", "False"),
            // Test0.vb(36,12): warning CA1508: 'param2 Is str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(36, 12, "param2 Is cNotNull", "True"),
            // Test0.vb(38,12): warning CA1508: 'param2 IsNot str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(38, 12, "param2 IsNot cNotNull", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_DoWhileLoop_02()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public C ContainingC => new C();
}

class Test
{
    void M(C param)
    {
        if (param.ContainingC == null)
        {
            return;
        }

        do
        {
            param = param.ContainingC;
        }
        while (param != null);
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
    Public ReadOnly Property ContainingC As C
End Class

Module Test
    Private Sub M1(param As C)
        If param.ContainingC Is Nothing Then
            Return
        End If

        Do 
            param = param.ContainingC
        Loop While param IsNot Nothing
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_DoWhileLoop_WithBreak()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M(C param, C param2, bool flag)
    {
        C cNotNull = new C();
        do
        {
            // param is unknown here
            if (cNotNull == param)
            {
            }
            if (flag)
            {
                param = param2;
                break;
            }
            if (cNotNull != param)
            {
            }
        }
        while (param != cNotNull);

        // param is unknown here
        if (param == cNotNull)
        {
        }
        if (param != cNotNull)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    ' Do-While top loop
    Private Sub M(param As String, param2 As String, flag As Boolean)
        Dim cNotNull As New C()
        Do While param IsNot cNotNull
            ' param is unknown here
            If cNotNull Is param Then
            End If
            If flag Then
                param = param2
                Exit Do
            End If
            If cNotNull IsNot param Then
            End If
        Loop

        ' param is unknown here due to Exit While
        If param Is cNotNull Then
        End If
        If param IsNot cNotNull Then
        End If
    End Sub

    ' Do-While bottom loop
    Private Sub M2(param As String, param2 As String, flag As Boolean)
        Dim cNotNull As New C()
        Do
            ' param2 is unknown here
            If cNotNull Is param2 Then
            End If
            If flag Then
                param2 = param
                Exit Do
            End If
            If cNotNull IsNot param2 Then
            End If
        Loop While param2 IsNot cNotNull

        ' param2 is unknown here due to Exit While
        If param2 Is cNotNull Then
        End If
        If param2 IsNot cNotNull Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_DoWhileLoop_WithContinue()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M(C param, C param2, C param3, bool flag)
    {
        C cNull = null;
        param3 = null;
        do
        {
            // param is unknown here from first loop iteration.
            if (cNull == param)
            {
            }
            // param3 is unknown here due to continue statement.
            if (cNull == param3)
            {
            }
            if (flag)
            {
                param3 = param2;
                continue;
            }
            // param is unknown here from first loop iteration.
            if (cNull != param)
            {
            }
            // param is unknown here due to continue statement.
            if (cNull != param)
            {
            }
        }
        while (param != cNull);

        // param = cNull here
        if (param == cNull)
        {
        }
        if (param != cNull)
        {
        }

        // param3 is unknwon here
        if (param3 == cNull)
        {
        }
        if (param3 != cNull)
        {
        }
    }
}
",
            // Test0.cs(39,13): warning CA1508: 'param == cNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(39, 13, "param == cNull", "true"),
            // Test0.cs(42,13): warning CA1508: 'param != cNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(42, 13, "param != cNull", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    ' Do-While top loop
    Private Sub M(param As C, param2 As C, param3 As C, flag As Boolean)
        Dim cNull As C = Nothing
        param3 = Nothing
        Do While param IsNot cNull
            ' param is non-null here
            If cNull Is param Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If cNull Is param3 Then
            End If
            If flag Then
                param3 = param2
                Continue Do
            End If
            ' param is non-null here
            If cNull IsNot param Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If cNull IsNot param3 Then
            End If
        Loop

        ' param = cNull here
        If param Is cNull Then
        End If
        If param IsNot cNull Then
        End If

        ' param3 is unknown here due.
        If param3 Is cNull Then
        End If
        If param3 IsNot cNull Then
        End If            
    End Sub

    ' Do-While bottom loop
    Private Sub M2(param As C, param2 As C, param4 As C, flag As Boolean)
        Dim cNull As C = Nothing
        param4 = Nothing
        Do
            ' param2 is unknown here from first loop iteration
            If cNull Is param2 Then
            End If
            ' param4 is unknown here due to continue statement in the loop
            If cNull Is param4 Then
            End If
            If flag Then
                param4 = param
                Continue Do
            End If
            ' param2 is unknown here from first loop iteration
            If cNull IsNot param2 Then
            End If
            ' param4 is unknown here due to continue statement in the loop
            If cNull IsNot param4 Then
            End If
        Loop While param2 IsNot cNull

        ' param2 = cNull here
        If param2 Is cNull Then
        End If
        If param2 IsNot cNull Then
        End If

        ' param4 is unknown here
        If param4 Is cNull Then
        End If
        If param4 IsNot cNull Then
        End If
    End Sub
End Module",
            // Test0.vb(12,16): warning CA1508: 'cNull Is param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(12, 16, "cNull Is param", "False"),
            // Test0.vb(22,16): warning CA1508: 'cNull IsNot param' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(22, 16, "cNull IsNot param", "True"),
            // Test0.vb(30,12): warning CA1508: 'param Is cNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(30, 12, "param Is cNull", "True"),
            // Test0.vb(32,12): warning CA1508: 'param IsNot cNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(32, 12, "param IsNot cNull", "False"),
            // Test0.vb(66,12): warning CA1508: 'param2 Is cNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(66, 12, "param2 Is cNull", "True"),
            // Test0.vb(68,12): warning CA1508: 'param2 IsNot cNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(68, 12, "param2 IsNot cNull", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_DoUntilLoop()
        {
            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' Do-Until top loop
    Private Sub M(ByVal param As String)
        Dim str As String = Nothing
        Do Until param <> str
            ' param == str here
            If param = str Then
            End If
            If param <> str Then
            End If
        Loop

        ' param is non-null here
        If str = param Then
        End If
        If str <> param Then
        End If
    End Sub

    ' Do-Until bottom loop
    Private Sub M2(ByVal param2 As String)
        Dim str As String = Nothing
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
            // Test0.vb(15,12): warning CA1508: 'str = param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(15, 12, "str = param", "False"),
            // Test0.vb(17,12): warning CA1508: 'str <> param' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(17, 12, "str <> param", "True"),
            // Test0.vb(33,12): warning CA1508: 'param2 = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(33, 12, "param2 = str", "True"),
            // Test0.vb(35,12): warning CA1508: 'param2 <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(35, 12, "param2 <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ForLoop()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C param, C param2)
    {
        C cNotNull = new C();
        for (param = cNotNull; param2 != cNotNull;)
        {
            // param = cNotNull here
            if (param == cNotNull)
            {
            }
            if (param != cNotNull)
            {
            }

            // param2 != cNotNull here, but we don't track not-contained values so no diagnostic.
            if (param2 == cNotNull)
            {
            }
            if (param2 != cNotNull)
            {
            }
        }
        
        // param2 == cNotNull here
        if (cNotNull == param2)
        {
        }
        if (cNotNull != param2)
        {
        }
        
        // param == cNotNull here
        if (cNotNull == param)
        {
        }
        if (cNotNull != param)
        {
        }
    }
}

class C
{
}
",
            // Test0.cs(10,17): warning CA1508: 'param == cNotNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 17, "param == cNotNull", "true"),
            // Test0.cs(13,17): warning CA1508: 'param != cNotNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(13, 17, "param != cNotNull", "false"),
            // Test0.cs(27,13): warning CA1508: 'cNotNull == param2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(27, 13, "cNotNull == param2", "true"),
            // Test0.cs(30,13): warning CA1508: 'cNotNull != param2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(30, 13, "cNotNull != param2", "false"),
            // Test0.cs(35,13): warning CA1508: 'cNotNull == param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(35, 13, "cNotNull == param", "true"),
            // Test0.cs(38,13): warning CA1508: 'cNotNull != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(38, 13, "cNotNull != param", "false"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ForLoop_02()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C[] args)
    {
        foreach (var arg in args)
        {
            if (arg == null)
            {
                return;
            }
        }
    }
}

class C
{
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ForLoop_03()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C[] args)
    {
        C local = null;
        for (int i = 0; i < args.Length; i++)
        {
            // local may or may not be null here.
            if (local == null)
            {
                local = new C();
            }
        }
        
        // local may or may not be null here.
        if (local != null)
        {
            return;
        }
    }
}

class C
{
}
");
            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(args As C())
        Dim local As C = Nothing
        For i As Integer = 0 To args.Length
            ' local may or may not be null here.
            If local Is Nothing Then
                local = New C()
            End If
        Next

        ' local may or may not be null here.
        If local IsNot Nothing Then
            Return
        End If
    End Sub
End Module

Class C
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ForLoop_WithBreak()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2, bool flag)
    {
        string str;
        for (str = null; param == str;)
        {
            // param = null here
            if (param == str)
            {
            }
            
            if (flag)
            {
                param = param2;
                break;
            }

            // param = null here
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
            // Test0.cs(21,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(21, 17, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' For loop
    Private Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str As String = Nothing
        For i As Integer = 0 To 10
            param = str
            ' param == null here
            If param = str Then
            End If
            If flag Then
                param = param2
                Exit For
            End If
            ' param == null here
            If param <> str Then
            End If
        Next

        ' param is unknown here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(9,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 16, "param = str", "True"),
            // Test0.vb(16,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 16, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ForLoop_WithContinue()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2, string param3, bool flag)
    {
        string str;
        param3 = null;
        for (str = null; param == str;)
        {
            // param = null here.
            if (param == str)
            {
            }
            
            // param3 is unknown here due to continue statement in the loop.
            if (param3 == str)
            {
            }
            
            if (flag)
            {
                param3 = param2;
                continue;
            }

            // param = null here.
            if (param != str)
            {
            }
            
            // param3 is unknown here due to continue statement in the loop.
            if (param3 != str)
            {
            }
        }

        // param = null here
        if (str == param)
        {
        }
        if (str != param)
        {
        }
    }
}
",
            // Test0.cs(11,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 17, "param == str", "true"),
            // Test0.cs(27,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(27, 17, "param != str", "false"),
            // Test0.cs(38,13): warning CA1508: 'str == param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(38, 13, "str == param", "false"),
            // Test0.cs(41,13): warning CA1508: 'str != param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(41, 13, "str != param", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Private Sub M1(param As String, param2 As String, param3 As String, flag As Boolean)
        Dim str As String = Nothing
        param3 = Nothing
        For i As Integer = 0 To 10
            param = str
            ' param == null here
            If param = str Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If param3 = str Then
            End If
            If flag Then
                param3 = param2
                Continue For
            End If
            ' param == null here
            If param <> str Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If param3 <> str Then
            End If
        Next

        ' param is unknown here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(9,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 16, "param = str", "True"),
            // Test0.vb(19,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(19, 16, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ValueCompare_ForLoop()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string[] args, int j)
    {
        int i;
        for (i = j; i < args.Length; i++)
        {
        }
        
        if (i == j)
        {
            return;
        }
    }
}
");
            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(args As C(), j As Integer)
        Dim i As Integer
        For i = j To args.Length
        Next

        If i = j Then
            Return
        End If
    End Sub
End Module

Class C
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ForEachLoop()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C[] args)
    {
        C local = null;
        foreach (var c in args)
        {
            // local may or may not be null here.
            if (local == null)
            {
                local = new C();
            }
        }
        
        // local may or may not be null here.
        if (local != null)
        {
            return;
        }
    }
}

class C
{
}
");
            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(args As C())
        Dim local As C = Nothing
        For Each c in args
            ' local may or may not be null here.
            If local Is Nothing Then
                local = New C()
            End If
        Next

        ' local may or may not be null here.
        If local IsNot Nothing Then
            Return
        End If
    End Sub
End Module

Class C
End Class
");
        }

        [Fact]
        public async Task NullCompare_ForEachLoop_WithBreak()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2, bool flag)
    {
        string str = null;
        param = str;
        foreach (var ch in param2)
        {
            // param = null here
            if (param == str)
            {
            }
            
            if (flag)
            {
                param = param2;
                break;
            }

            // param = null here
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
            // Test0.cs(11,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 17, "param == str", "true"),
            // Test0.cs(22,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(22, 17, "param != str", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    ' While loop
    Private Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str As String = Nothing
        param = str
        For Each ch As Char in param2
            ' param == null here
            If param = str Then
            End If
            If flag Then
                param = param2
                Exit For
            End If
            ' param == null here
            If param <> str Then
            End If
        Next

        ' param is unknown here
        If str = param Then
            End If
        If str <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(9,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 16, "param = str", "True"),
            // Test0.vb(16,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 16, "param <> str", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ForEachLoop_WithContinue()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(string param, string param2, string param3, bool flag)
    {
        string str = null;
        param3 = null;
        param = str;
        foreach (var ch in param2)
        {
            // param = null here.
            if (param == str)
            {
            }
            
            // param3 is unknown here due to continue statement in the loop.
            if (param3 == str)
            {
            }
            
            if (flag)
            {
                param3 = param2;
                continue;
            }

            // param = null here.
            if (param != str)
            {
            }
            
            // param3 is unknown here due to continue statement in the loop.
            if (param3 != str)
            {
            }
        }

        // param = null here
        if (str == param)
        {
        }
        if (str != param)
        {
        }

        // param3 is unkown here
        if (str == param3)
        {
        }
        if (str != param3)
        {
        }
    }
}
",
            // Test0.cs(12,17): warning CA1508: 'param == str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(12, 17, "param == str", "true"),
            // Test0.cs(28,17): warning CA1508: 'param != str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(28, 17, "param != str", "false"),
            // Test0.cs(39,13): warning CA1508: 'str == param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(39, 13, "str == param", "true"),
            // Test0.cs(42,13): warning CA1508: 'str != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(42, 13, "str != param", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Private Sub M1(param As String, param2 As String, param3 As String, flag As Boolean)
        Dim str As String = Nothing
        param3 = Nothing
        param = str
        For Each ch As Char in param2
            ' param == null here
            If param = str Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If param3 = str Then
            End If
            If flag Then
                param3 = param2
                Continue For
            End If
            ' param == null here
            If param <> str Then
            End If
            ' param3 is unknown here due to continue statement in the loop.
            If param3 <> str Then
            End If
        Next

        ' param = null here
        If str = param Then
            End If
        If str <> param Then
        End If

        ' param3 is unknown here
        If str = param3 Then
            End If
        If str <> param3 Then
        End If
    End Sub
End Module",
            // Test0.vb(9,16): warning CA1508: 'param = str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 16, "param = str", "True"),
            // Test0.vb(19,16): warning CA1508: 'param <> str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(19, 16, "param <> str", "False"),
            // Test0.vb(27,12): warning CA1508: 'str = param' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(27, 12, "str = param", "True"),
            // Test0.vb(29,12): warning CA1508: 'str <> param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(29, 12, "str <> param", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_SwitchStatement_01_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(C param, int i)
    {
        switch (i)
        {
            case 0:
                param = new C();
                break;

            case 1:
                param = null;
                break;

            default:
                return;
        }
        
        if (param != null)
        {
        }
    }
}

class C
{
}
");
            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M(param As C, i As Integer)
        Select Case i
            Case 0
                param = New C()
            Case 1
                param = Nothing
            Case Else
                Return
        End Select

        If param IsNot Nothing Then
        End If
    End Sub
End Class

Class C
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_SwitchStatement_02_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C param, int i)
    {
        switch (i)
        {
            case 0:
                param = new C();
                break;

            case 1:                
                return;

            default:
                param = null;
                break;
        }
        
        if (param != null)
        {
        }
    }
}

class C
{
}
");
            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M(param As C, i As Integer)
        Select Case i
            Case 0
                param = New C()
            Case 1
                Return
            Case Else
                param = Nothing
        End Select

        If param IsNot Nothing Then
        End If
    End Sub
End Class

Class C
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_SwitchStatement_03_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C param, int i)
    {
        switch (i)
        {
            case 0:
                param = new C();
                return;

            case 1:                
                return;

            default:
                param = new C();
                return;
        }
        
        if (param != null)
        {
        }
    }
}

class C
{
}
");
            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M(param As C, i As Integer)
        Select Case i
            Case 0
                param = New C()
                Return
            Case 1
                Return
            Case Else
                param = Nothing
                Return
        End Select

        If param IsNot Nothing Then
        End If
    End Sub
End Class

Class C
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_SwitchStatement_01_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C param, int i)
    {
        switch (i)
        {
            case 0:
                param = new C();
                break;

            case 1:
                param = new C();
                break;

            default:
                return;
        }
        
        if (param != null)
        {
        }
    }
}

class C
{
}
",
            // Test0.cs(20,13): warning CA1508: 'param != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 13, "param != null", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M(param As C, i As Integer)
        Select Case i
            Case 0
                param = New C()
            Case 1
                param = New C()
            Case Else
                Return
        End Select

        If param IsNot Nothing Then
        End If
    End Sub
End Class

Class C
End Class
",
            // Test0.vb(13,12): warning CA1508: 'param IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(13, 12, "param IsNot Nothing", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_SwitchStatement_02_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(C param, int i)
    {
        switch (i)
        {
            default:
                param = null;
                break;

            case 1:
                return;

            case 0:
                param = null;
                break;
        }
        
        if (param != null)
        {
        }
    }
}

class C
{
}
",
            // Test0.cs(20,13): warning CA1508: 'param != null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 13, "param != null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M(param As C, i As Integer)
        Select Case i
            Case 1
                Return
            Case 0
                param = Nothing
            Case Else
                param = Nothing
        End Select

        If param IsNot Nothing Then
        End If
    End Sub
End Class

Class C
End Class
",
            // Test0.vb(13,12): warning CA1508: 'param IsNot Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(13, 12, "param IsNot Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCompare_CopyAnalysis()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(C param, C param2)
    {
        C cNotNull = new C();
        if (param == cNotNull && param2 == cNotNull && param == param2)
        {
        }

        param = param2;
        if (param != cNotNull || param2 != cNotNull)
        {
        }
    }
}

class C
{
}
",
            // Test0.cs(7,56): warning CA1508: 'param == param2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 56, "param == param2", "true"),
            // Test0.cs(12,34): warning CA1508: 'param2 != cNotNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(12, 34, "param2 != cNotNull", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As C, param2 As C)
        Dim cNotNull As New C()
        If param Is cNotNull AndAlso param2 Is cNotNull AndAlso param Is param2 Then
        End If

        param = param2
        If param IsNot cNotNull OrElse param2 IsNot cNotNull Then
        End If
    End Sub
End Module

Class C
End Class
",
            // Test0.vb(5,65): warning CA1508: 'param Is param2' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 65, "param Is param2", "True"),
            // Test0.vb(9,40): warning CA1508: 'param2 IsNot cNotNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 40, "param2 IsNot cNotNull", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ConditionalOr_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, string param2)
    {
        string strNotNull = """";
        string strNull = null;
        string strMayBeNull = param2;

        if (param == strNotNull || param == strNull)
        {
        }

        if (strNull != param || param == strMayBeNull)
        {
        }

        if (param == strMayBeNull || strNull != param)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String)
        Dim strNotNull = """"
        Dim strNull = Nothing
        Dim strMayBeNull = param2

        If param = strNotNull OrElse param = strNull Then
        End If

        If strNull <> param OrElse param = strMayBeNull Then
        End If

        If param = strMayBeNull OrElse strNull <> param Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ConditionalOr_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, string param2)
    {
        string strNotNull = """";
        string strNull = null;
        string strMayBeNull = param2;

        if (strNull != param || param == strNotNull)
        {
        }

        if (param != strNotNull || strNull != param)
        {
        }
    }
}
",
            // Test0.cs(10,33): warning CA1508: 'param == strNotNull' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 33, "param == strNotNull", "false"),
            // Test0.cs(14,36): warning CA1508: 'strNull != param' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(14, 36, "strNull != param", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String)
        Dim strNotNull = """"
        Dim strNull = Nothing
        Dim strMayBeNull = param2

        If strNull <> param OrElse param = strNotNull Then
        End If

        If param <> strNotNull OrElse strNull <> param Then
        End If
    End Sub
End Module",
            // Test0.vb(8,36): warning CA1508: 'param = strNotNull' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 36, "param = strNotNull", "False"),
            // Test0.vb(11,39): warning CA1508: 'strNull <> param' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(11, 39, "strNull <> param", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ConditionalAnd_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, string param2)
    {
        string strNotNull = """";
        string strNull = null;
        string strMayBeNull = param2;

        if (param == strNotNull && param2 == strNull)
        {
        }

        if (param == strMayBeNull && strNull == param)
        {
        }

        if (param != strNotNull && param != strNull)
        {
        }

        if (param != strNotNull && param2 != strNull)
        {
        }

        if (strNull != param && param == strMayBeNull)
        {
        }

        if (param2 == strNull && param != strNotNull && param == param2)
        {
        }

        if (strNull != param && param == strNotNull)
        {
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim strNotNull = """"
        Dim strNull = Nothing
        Dim strMayBeNull = param2

        If param = strNotNull AndAlso param2 = strNull Then
        End If

        If param = strMayBeNull AndAlso strNull = param Then
        End If

        If strNull <> param AndAlso param <> strNotNull Then
        End If

        If strNull <> param AndAlso param2 <> strNotNull Then
        End If

        If strNull <> param AndAlso param = strMayBeNull Then
        End If

        If param2 = strNull AndAlso param <> strNotNull AndAlso param = param2 Then
        End If

        If strNull <> param AndAlso param = strNotNull Then
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ConditionalAnd_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param, string param2)
    {
        string strNotNull = """";
        string strNull = null;
        string strMayBeNull = param2;

        if (param == strNotNull && param != strNull)
        {
        }

        if (strNull != param || (param == strMayBeNull && strNull != param))
        {
        }
    }
}
",
            // Test0.cs(10,36): warning CA1508: 'param != strNull' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(10, 36, "param != strNull", "true"),
            // Test0.cs(14,59): warning CA1508: 'strNull != param' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(14, 59, "strNull != param", "false"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String, param2 As String)
        Dim strNotNull = """"
        Dim strNull = Nothing
        Dim strMayBeNull = param2

        If param = strNotNull AndAlso param <> strNull Then
        End If

        If strNull <> param OrElse (param = strMayBeNull AndAlso strNull <> param) Then
            System.Console.Write("""")
        End If
    End Sub
End Module",
            // Test0.vb(8,39): warning CA1508: 'param <> strNull' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 39, "param <> strNull", "True"),
            // Test0.vb(11,66): warning CA1508: 'strNull <> param' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(11, 66, "strNull <> param", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ConditionaAndOrNullCompare_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(string param)
    {
        string str = """";
        if (param != null || param == str)
        {
        }

        if (null == param && param != str)
        {
        }
    }
}
",
            // Test0.cs(7,30): warning CA1508: 'param == str' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 30, "param == str", "false"),
            // Test0.cs(11,30): warning CA1508: 'param != str' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 30, "param != str", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Sub M1(param As String)
        Dim str = """"
        If param <> Nothing OrElse param = str Then
        End If

        If Nothing = param AndAlso param <> str Then
        End If
    End Sub
End Module",
            // Test0.vb(5,36): warning CA1508: 'param = str' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 36, "param = str", "False"),
            // Test0.vb(8,36): warning CA1508: 'param <> str' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 36, "param <> str", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ContractCheck_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param)
    {
        System.Diagnostics.Contracts.Contract.Requires(param != null);
    }

    void M2(C param, C param2, C param3)
    {
        System.Diagnostics.Contracts.Contract.Requires(param == param2 && !(param2 != null) || param2 == param3);
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Private Sub M1(param As C)
        System.Diagnostics.Contracts.Contract.Requires(param IsNot Nothing)
    End Sub

    Private Sub M2(param As C, param2 As C, param3 As C)
        System.Diagnostics.Contracts.Contract.Requires(param Is param2 AndAlso Not(param2 IsNot Nothing) OrElse param2 Is param3)
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_ContractCheck_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param)
    {
        C c = null;
        param = null;
        System.Diagnostics.Contracts.Contract.Requires(param == c);
    }

    void M2(C param)
    {
        C c = null;
        param = null;
        System.Diagnostics.Contracts.Contract.Requires(param != c);
    }

    void M3(C param)
    {
        var c = new C();
        param = param ?? c;
        System.Diagnostics.Contracts.Contract.Requires(param == null);
    }

    void M4(C param)
    {
        var c = new C();
        param = param ?? c;
        System.Diagnostics.Contracts.Contract.Requires(param != null);
    }
}
",
            // Test0.cs(12,56): warning CA1508: 'param == c' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(12, 56, "param == c", "true"),
            // Test0.cs(19,56): warning CA1508: 'param != c' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(19, 56, "param != c", "false"),
            // Test0.cs(26,56): warning CA1508: 'param == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(26, 56, "param == null", "false"),
            // Test0.cs(33,56): warning CA1508: 'param != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(33, 56, "param != null", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Private Sub M1(param As C)
        Dim c As C = Nothing
        param = Nothing
        System.Diagnostics.Contracts.Contract.Requires(param Is c)
    End Sub

    Private Sub M2(param As C)
        Dim c As C = Nothing
        param = Nothing
        System.Diagnostics.Contracts.Contract.Requires(param IsNot c)
    End Sub

    Private Sub M3(param As C)
        Dim c As C = New C()
        param = If (param, c)
        System.Diagnostics.Contracts.Contract.Requires(param Is Nothing)
    End Sub

    Private Sub M4(param As C)
        Dim c As C = New C()
        param = If (param, c)
        System.Diagnostics.Contracts.Contract.Requires(param IsNot Nothing)
    End Sub
End Module",
            // Test0.vb(9,56): warning CA1508: 'param Is c' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 56, "param Is c", "True"),
            // Test0.vb(15,56): warning CA1508: 'param IsNot c' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(15, 56, "param IsNot c", "False"),
            // Test0.vb(21,56): warning CA1508: 'param Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(21, 56, "param Is Nothing", "False"),
            // Test0.vb(27,56): warning CA1508: 'param IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(27, 56, "param IsNot Nothing", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_InAssignment_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param)
    {
        C c = null;
        param = null;
        bool flag = param == c;
    }
}
",
            // Test0.cs(12,21): warning CA1508: 'param == c' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(12, 21, "param == c", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Private Sub M1(param As C)
        Dim c As C = Nothing
        param = Nothing
        Dim flag As Boolean = param Is c
    End Sub
End Module",
            // Test0.vb(9,31): warning CA1508: 'param Is c' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 31, "param Is c", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_Nested_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param)
    {
        if (param == null)
        {
            param = M2();
            if (param == null)
            {
            }
        }
    }

    C M2() => new C();
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Private Sub M1(param As C)
        If param Is Nothing Then
            param = M2()
            If param Is Nothing Then
            End If
        End If
    End Sub

    Private Function M2() As C
        Return New C()
    End Function
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCoalesce_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param, bool flag)
    {
        param = param ?? new C();

        C c = flag ? null : new C();
        c = c ?? new C();
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Private Sub M1(param As C, flag As Boolean)
        param = If (param, New C())

        Dim c As C = If (flag, Nothing, New C())
        c = If (c, New C())
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCoalesce_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C param)
    {
        C c = null;
        param = c ?? new C();
    }

    void M2(C param)
    {
        C c = new C();
        param = c ?? new C();
    }

    void M3(C param)
    {
        var local = param ?? throw new System.ArgumentNullException(nameof(param));
        local = local ?? new C();
    }
}
",
            // Test0.cs(11,17): warning CA1508: 'c' is always 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(11, 17, "c", "null"),
            // Test0.cs(17,17): warning CA1508: 'c' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(17, 17, "c", "null"),
            // Test0.cs(23,17): warning CA1508: 'local' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(23, 17, "local", "null"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Module Test
    Private Sub M1(param As C)
        Dim c As C = Nothing
        param = If (c, New C())
    End Sub

    Private Sub M2(param As C)
        Dim c As C = New C()
        param = If (c, New C())
    End Sub
End Module",
            // Test0.vb(8,21): warning CA1508: 'c' is always 'Nothing'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(8, 21, "c", "Nothing"),
            // Test0.vb(13,21): warning CA1508: 'c' is never 'Nothing'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicNeverNullResultAt(13, 21, "c", "Nothing"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCoalesce_NullableValueType_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public int X;
}

class Test
{
    void M1(C param)
    {
        var x = param?.X ?? 0;
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
    Public X As Integer
End Class

Module Test
    Private Sub M1(param As C)
        Dim x = If(param?.X, 0)
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCoalesce_NullableValueType_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int? x)
    {
        x = null;
        if (x == null)
        {
            return;
        }
    }
}
",
            // Test0.cs(7,13): warning CA1508: 'x == null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 13, "x == null", "true"));

            await VerifyBasicAnalyzerAsync(@"
Module Test
    Private Sub M1(x As Integer?)
        x = Nothing
        If x Is Nothing Then
        End If
    End Sub
End Module",
            // Test0.vb(5,12): warning CA1508: 'x Is Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 12, "x Is Nothing", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ConditionalAccess_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public int X;
}

class Test
{
    void M1(C param, bool flag)
    {
        var x = param?.X;

        C c = flag ? null : new C();
        x = c?.X;
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
    Public X As Integer
End Class

Module Test
    Private Sub M1(param As C, flag As Boolean)
        Dim x = param?.X

        Dim c As C = If (flag, Nothing, New C())
        x = c?.X
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ConditionalAccess_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public int X;
}

class Test
{
    void M1(C param)
    {
        param = null;
        var x = param?.X;
    }

    void M2(C param)
    {
        param = new C();
        var x = param?.X;
    }

    void M3(C param)
    {
        var local = param ?? throw new System.ArgumentNullException(nameof(param));
        var x = local?.X;
    }
}
",
            // Test0.cs(12,17): warning CA1508: 'param' is always 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(12, 17, "param", "null"),
            // Test0.cs(18,17): warning CA1508: 'param' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(18, 17, "param", "null"),
            // Test0.cs(24,17): warning CA1508: 'local' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(24, 17, "local", "null"));

            await VerifyBasicAnalyzerAsync(@"
Class C
    Public X As Integer
End Class

Module Test
    Private Sub M1(param As C)
        param = Nothing
        Dim x = param?.X
    End Sub

    Private Sub M2(param As C)
        param = New C()
        Dim x = param?.X
    End Sub
End Module",
            // Test0.vb(9,17): warning CA1508: 'param' is always 'Nothing'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(9, 17, "param", "Nothing"),
            // Test0.vb(14,17): warning CA1508: 'param' is never 'Nothing'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicNeverNullResultAt(14, 17, "param", "Nothing"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ConditionalAccessNullCoalesce_Field_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public int X;
}

class Test
{
    public C _c;
    void M1()
    {
        var x = _c?.X;
        var y = _c ?? new C();
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
    Public X As Integer
End Class

Class Test
    Public _c As C
    Private Sub M1()
        Dim x = _c?.X
        Dim x2 = If(_c, new C())
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_NoDiagnostic()
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
    void M1(C c)
    {
        if (c == null)
        {
            return;
        }

        var d = c as D;
        if (d == null)
        {
            return;
        }

        if (c is D)
        {
            return;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C)
        If c Is Nothing Then
            Return
        End If

        Dim d = TryCast(c, D)
        If d Is Nothing Then
            Return
        End If

        If TypeOf(c) Is D Then
            Return
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_02_NoDiagnostic()
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
    void M1(C c, bool flag)
    {
        if (flag)
        {
            c = new D();
        }
        else
        {
            c = new C();
        }

        var d = c as D;
        if (d == null)
        {
            return;
        }

        if (d == c)
        {
            return;
        }
    }

    void M2(C c, bool flag)
    {
        if (flag)
        {
            c = new D();
        }
        else
        {
            c = null;
        }

        var d = c as D;
        if (d == null)
        {
            return;
        }

        if (d == c)
        {
            return;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C, flag As Boolean)
        If flag Then
            c = New D()
        Else
            c = New C()
        End If

        Dim d = TryCast(c, D)
        If d Is Nothing Then
            Return
        End If

        If d Is c Then
            Return
        End If
    End Sub

    Private Sub M2(c As C, flag As Boolean)
        If flag Then
            c = New D()
        Else
            c = Nothing
        End If

        Dim d = TryCast(c, D)
        If d Is Nothing Then
            Return
        End If

        If d Is c Then
            Return
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(4411, "https://github.com/dotnet/roslyn-analyzers/issues/4411")]
        public async Task NullCheck_AfterTryCast_03_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class A
{
    void M(object o)
    {
        if (o is A a)
        {
            _ = (a as B)?.ToString();
        }
    }
}

class B : A
{
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(4383, "https://github.com/dotnet/roslyn-analyzers/issues/4383")]
        public async Task NullCheck_AfterTryCast_04_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal static class Class1
{
    public static ReadOnlyDictionary<TKey, ReadOnlyCollection<TValue>> AsReadOnlyItems<TKey, TValue>(this IDictionary<TKey, IList<TValue>> self, IEqualityComparer<TKey> keyComparer = null)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));

        keyComparer ??= (self as Dictionary<TKey, TValue>)?.Comparer;
        var copy = new Dictionary<TKey, ReadOnlyCollection<TValue>>(self.Count, keyComparer);
        foreach (var kvp in self)
        {
            if (kvp.Value is null)
            {
                copy.Add(kvp.Key, null);
                continue;
            }

            var readOnlyValue = kvp.Value as ReadOnlyCollection<TValue> ?? new ReadOnlyCollection<TValue>(kvp.Value);
            copy.Add(kvp.Key, readOnlyValue);
        }

        return new ReadOnlyDictionary<TKey, ReadOnlyCollection<TValue>>(copy);
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_Diagnostic()
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
    void M1(C c)
    {
        c = new D();
        var d = c as D;
        if (d == null)
        {
            return;
        }
    }

    void M2(C c)
    {
        c = new D();
        var d = c as D;
        if (d == c)
        {
            return;
        }
    }
}
",
            // Test0.cs(16,13): warning CA1508: 'd == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 13, "d == null", "false"),
            // Test0.cs(26,13): warning CA1508: 'd == c' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(26, 13, "d == c", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C)
        c = New D()
        Dim d = TryCast(c, D)
        If d Is Nothing Then
            Return
        End If
    End Sub

    Private Sub M2(c As C)
        c = New D()
        Dim d = TryCast(c, D)
        If d Is c Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(13,12): warning CA1508: 'd Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(13, 12, "d Is Nothing", "False"),
            // Test0.vb(21,12): warning CA1508: 'd Is c' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(21, 12, "d Is c", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_02_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D: C
{
}

class E_DerivesFromD: D
{
}

class F_DerivesFromC: C
{
}

class G_DerivesFromF: F_DerivesFromC
{
}

class Test
{
    void M1(C c, bool flag)
    {
        if (flag)
        {
            c = new D();
        }
        else
        {
            c = new E_DerivesFromD();
        }

        var d = c as D;
        if (d == null)
        {
            return;
        }
    }

    void M2(C c, bool flag)
    {
        if (flag)
        {
            c = new D();
        }
        else
        {
            c = new E_DerivesFromD();
        }

        var d = c as D;
        if (d != c)
        {
            return;
        }
    }

    void M3(C c, bool flag)
    {
        if (flag)
        {
            c = new F_DerivesFromC();
        }
        else
        {
            c = new G_DerivesFromF();
        }

        var d = c as D;
        if (d == null)
        {
            return;
        }
    }

    void M4(C c, bool flag)
    {
        if (flag)
        {
            c = new F_DerivesFromC();
        }
        else
        {
            c = new G_DerivesFromF();
        }

        var d = c as D;
        if (d != c)
        {
            return;
        }
    }
}
",
            // Test0.cs(36,13): warning CA1508: 'd == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(36, 13, "d == null", "false"),
            // Test0.cs(54,13): warning CA1508: 'd != c' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(54, 13, "d != c", "false"),
            // Test0.cs(72,13): warning CA1508: 'd == null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(72, 13, "d == null", "true"),
            // Test0.cs(90,13): warning CA1508: 'd != c' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(90, 13, "d != c", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
    Inherits C

End Class

Class E_DerivesFromD
    Inherits D

End Class

Class F_DerivesFromC
    Inherits C

End Class

Class G_DerivesFromF
    Inherits F_DerivesFromC

End Class

Class Test

    Private Sub M1(c As C, flag As Boolean)
        If flag Then
            c = New D()
        Else
            c = New E_DerivesFromD()
        End If

        Dim d = TryCast(c, D)
        If d Is Nothing Then
            Return
        End If
    End Sub

    Private Sub M2(c As C, flag As Boolean)
        If flag Then
            c = New D()
        Else
            c = New E_DerivesFromD()
        End If

        Dim d = TryCast(c, D)
        If d IsNot c Then
            Return
        End If
    End Sub

    Private Sub M3(c As C, flag As Boolean)
        If flag Then
            c = New F_DerivesFromC()
        Else
            c = New G_DerivesFromF()
        End If

        Dim d = TryCast(c, D)
        If d Is Nothing Then
            Return
        End If
    End Sub

    Private Sub M4(c As C, flag As Boolean)
        If flag Then
            c = New F_DerivesFromC()
        Else
            c = New G_DerivesFromF()
        End If

        Dim d = TryCast(c, D)
        If d IsNot c Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(35,12): warning CA1508: 'd Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(35, 12, "d Is Nothing", "False"),
            // Test0.vb(48,12): warning CA1508: 'd IsNot c' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(48, 12, "d IsNot c", "False"),
            // Test0.vb(61,12): warning CA1508: 'd Is Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(61, 12, "d Is Nothing", "True"),
            // Test0.vb(74,12): warning CA1508: 'd IsNot c' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(74, 12, "d IsNot c", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_IsPattern_Diagnostic()
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
    void M1_IsDeclarationPattern_AlwaysTrue(C c1)
    {
        c1 = new D();
        if (c1 is D d1)
        {
            return;
        }
    }

    void M1_IsDeclarationPattern_AlwaysFalse(C c2)
    {
        c2 = null;
        if (c2 is D d2)
        {
            return;
        }
    }

    void M1_IsConstantPattern_AlwaysTrue(C c3)
    {
        c3 = new C();
        var d3 = c3 as D; // Our analysis is currently conservative here.
        if (d3 is null)
        {
            return;
        }
    }

    void M1_IsConstantPattern_AlwaysFalse(C c4)
    {
        c4 = new D();
        var d4 = c4 as D;
        if (d4 is null)
        {
            return;
        }
    }

    void M2_IsDeclarationPattern_AlwaysTrue(C c5)
    {
        c5 = new D();
        var d5 = c5 as D;
        if (d5 is C c5_2)
        {
            return;
        }
    }
}
",
            // Test0.cs(15,13): warning CA1508: 'c1 is D d1' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(15, 13, "c1 is D d1", "true"),
            // Test0.cs(24,13): warning CA1508: 'c2 is D d2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "c2 is D d2", "false"),
            // Test0.cs(44,13): warning CA1508: 'd4 is null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(44, 13, "d4 is null", "false"),
            // Test0.cs(54,13): warning CA1508: 'd5 is C c5_2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(54, 13, "d5 is C c5_2", "true"));

            // VB does not support patterns.
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task DeclarationPattern_DiscardSymbol_Diagnostic()
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
    bool M1(C c1)
    {
        switch (c1)
        {
            case D _:
                return true;

            case C c2:
                return c1 == c2;

            default:
                return true;
        }
    }
}
",
            // Test0.cs(20,24): warning CA1508: 'c1 == c2' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 24, "c1 == c2", "true"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_IsPattern_NoDiagnostic()
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
    void M1_IsDeclarationPattern(C c1, bool flag)
    {
        c1 = new D();
        if (flag)
        {
            c1 = new C();
        }

        if (c1 is D d1)
        {
            return;
        }
    }

    void M1_IsConstantPattern(C c2, bool flag)
    {
        c2 = new D();
        if (flag)
        {
            c2 = new C();
        }

        var d2 = c2 as D;
        if (d2 is null)
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
        public async Task NullCheck_AfterTryCast_Interfaces_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
interface I
{
}

interface I2 : I
{
}

class C : I
{
}

class Test
{
    void M1(object o)
    {
        if (o == null)
        {
            return;
        }

        var i = o as I;
        if (i == null)
        {
            return;
        }

        var i2 = i as I2;
        if (i2 == null)
        {
            return;
        }

        var c = i as C;
        if (c == null)
        {
            return;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Interface I
End Interface

Interface I2
    Inherits I
End Interface

Class C
    Implements I
End Class

Class Test
    Private Sub M1(o As Object)
        If o Is Nothing Then
            Return
        End If

        Dim i = TryCast(o, I)
        If i Is Nothing Then
            Return
        End If

        Dim i2 = TryCast(i, I2)
        If i2 Is Nothing Then
            Return
        End If

        Dim c = TryCast(i, C)
        If c Is Nothing Then
            Return
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_Interfaces_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
interface I
{
}

interface I2 : I
{
}

class C : I
{
}

class Test
{
    void M1(C c, I2 i2)
    {
        if (c == null || i2 == null)
        {
            return;
        }

        var i = c as I;
        if (i == null)
        {
            return;
        }

        i = i2 as I;
        if (i == null)
        {
            return;
        }
    }
}
",
            // Test0.cs(24,13): warning CA1508: 'i == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "i == null", "false"),
            // Test0.cs(30,13): warning CA1508: 'i == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(30, 13, "i == null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Interface I
End Interface

Interface I2
    Inherits I
End Interface

Class C
    Implements I
End Class

Class Test
    Private Sub M1(c As C, i2 As I2)
        If c Is Nothing OrElse i2 Is Nothing Then
            Return
        End If

        Dim i = TryCast(c, I)
        If i Is Nothing Then
            Return
        End If

        i = TryCast(i2, I)
        If i Is Nothing Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(20,12): warning CA1508: 'i Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(20, 12, "i Is Nothing", "False"),
            // Test0.vb(25,12): warning CA1508: 'i Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(25, 12, "i Is Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_TypeParameter_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D : C
{
}

class Test<T>
    where T : C
{
    void M1(C c)
    {
        if (c == null)
        {
            return;
        }

        var t = c as T;
        if (t == null)
        {
            return;
        }
    }

    void M2(T t)
    {
        if (t == null)
        {
            return;
        }

        var d = t as D;
        if (d == null)
        {
            return;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
    Inherits C
End Class

Class Test(Of T As C)
    Private Sub M1(c As C)
        If c Is Nothing Then
            Return
        End If

        Dim t = TryCast(c, T)
        If t Is Nothing Then
            Return
        End If
    End Sub

    Private Sub M2(t As T)
        If t Is Nothing Then
            Return
        End If

        Dim d = TryCast(t, D)
        If d Is Nothing Then
            Return
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterTryCast_TypeParameter_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D : C
{
}

class Test<T>
    where T : C
{
    void M1(T t)
    {
        if (t == null)
        {
            return;
        }

        var c = t as C;
        if (c == null)
        {
            return;
        }
    }
}
",
            // Test0.cs(21,13): warning CA1508: 'c == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(21, 13, "c == null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
    Inherits C
End Class

Class Test(Of T As C)
    Private Sub M1(t As T)
        If t Is Nothing Then
            Return
        End If

        Dim c = TryCast(t, C)
        If c Is Nothing Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(16,12): warning CA1508: 'c Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 12, "c Is Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_NoDiagnostic()
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
    void M1(C c, bool flag)
    {
        if (flag)
        {
            c = new D();
        }
        else
        {
            c = null;
        }

        var d = (D)c;
        if (d == null)
        {
            return;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C, flag As Boolean)
        If flag Then
            c = New D()
        Else
            c = Nothing
        End If

        Dim d = TryCast(c, D)
        If d Is Nothing Then
            Return
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_NarrowingConversion_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int value)
    {
        if ((sbyte)value == value)
        {
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(4415, "https://github.com/dotnet/roslyn-analyzers/issues/4415")]
        public async Task NullCheck_AfterDirectCast_NullableValueType_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

internal class Class1
{
    public static void M(object obj)
    {
        var d = (DateTime?)obj;
        if (d != null)
        {
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_Diagnostic()
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
    void M1(C c)
    {
        if (c == null)
        {
            return;
        }

        var d = (D)c;
        if (d == null)
        {
            return;
        }
    }
}
",
            // Test0.cs(20,13): warning CA1508: 'd == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 13, "d == null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C)
        If c Is Nothing Then
            Return
        End If

        Dim d = DirectCast(c, D)
        If d Is Nothing Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(16,12): warning CA1508: 'd Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 12, "d Is Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_02_Diagnostic()
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
    void M1(C c)
    {
        if (c == null)
        {
            return;
        }

        var d = (D)c;
        if (d == c)
        {
            return;
        }
    }
}
",
            // Test0.cs(20,13): warning CA1508: 'd == c' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 13, "d == c", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C)
        If c Is Nothing Then
            Return
        End If

        Dim d = DirectCast(c, D)
        If d Is c Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(16,12): warning CA1508: 'd Is c' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 12, "d Is c", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_03_Diagnostic()
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
    void M1(C c, bool flag)
    {
        if (flag)
        {
            c = new D();
        }
        else
        {
            c = new C();
        }

        var d = (D)c;
        if (d == null)
        {
            return;
        }

        if (d == c)
        {
            return;
        }
    }

    void M2(C c, bool flag)
    {
        if (flag)
        {
            c = new D();
        }
        else
        {
            c = null;
        }

        var d = (D)c;
        if (d == null)
        {
            return;
        }

        if (d == c)
        {
            return;
        }
    }
}
",
            // Test0.cs(24,13): warning CA1508: 'd == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "d == null", "false"),
            // Test0.cs(29,13): warning CA1508: 'd == c' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(29, 13, "d == c", "true"),
            // Test0.cs(52,13): warning CA1508: 'd == c' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(52, 13, "d == c", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C, flag As Boolean)
        If flag Then
            c = New D()
        Else
            c = New C()
        End If

        Dim d = DirectCast(c, D)
        If d Is Nothing Then
            Return
        End If

        If d Is c Then
            Return
        End If
    End Sub

    Private Sub M2(c As C, flag As Boolean)
        If flag Then
            c = New D()
        Else
            c = Nothing
        End If

        Dim d = DirectCast(c, D)
        If d Is Nothing Then
            Return
        End If

        If d Is c Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(18,12): warning CA1508: 'd Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(18, 12, "d Is Nothing", "False"),
            // Test0.vb(22,12): warning CA1508: 'd Is c' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(22, 12, "d Is c", "True"),
            // Test0.vb(39,12): warning CA1508: 'd Is c' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(39, 12, "d Is c", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_04_Diagnostic()
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
    void M1(C c)
    {
        c = new D();
        var local = c;
        var d = (D)c;
        if (d == local)
        {
            return;
        }
    }
}
",
            // Test0.cs(17,13): warning CA1508: 'd == local' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(17, 13, "d == local", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class Test
    Private Sub M1(c As C)
        c = New D()
        Dim local = c
        Dim d = DirectCast(c, D)
        If d Is local Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(14,12): warning CA1508: 'd Is local' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(14, 12, "d Is local", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_05_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D: C
{
}

class E_DerivesFromC : C
{
}

class Test
{
    void M1(C c)
    {
        c = new E_DerivesFromC();
        var local = c;
        var d = (D)c;
        if (d == local)
        {
            return;
        }
    }
}
",
            // Test0.cs(21,13): warning CA1508: 'd == local' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(21, 13, "d == local", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
  Inherits C
End Class

Class E_DerivesFromC
  Inherits C
End Class

Class Test
    Private Sub M1(c As C)
        c = New E_DerivesFromC()
        Dim local = c
        Dim d = DirectCast(c, D)
        If d Is local Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(18,12): warning CA1508: 'd Is local' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(18, 12, "d Is local", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_Interfaces_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
interface I
{
}

interface I2 : I
{
}

class C : I
{
}

class Test
{
    void M1(object o)
    {
        var i = (I)o;
        if (i == null)
        {
            return;
        }
    }

    void M2(I i)
    {
        var i2 = (I2)i;
        if (i2 == null)
        {
            return;
        }
    }

    void M3(I i)
    {
        var c = (C)i;
        if (c == null)
        {
            return;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Interface I
End Interface

Interface I2
    Inherits I
End Interface

Class C
    Implements I
End Class

Class Test
    Private Sub M1(o As Object)
        Dim i = DirectCast(o, I)
        If i Is Nothing Then
            Return
        End If
    End Sub

    Private Sub M2(i As I)
        Dim i2 = DirectCast(i, I2)
        If i2 Is Nothing Then
            Return
        End If
    End Sub

    Private Sub M3(i As I)
        Dim c = DirectCast(i, C)
        If c Is Nothing Then
            Return
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_Interfaces_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
interface I
{
}

interface I2 : I
{
}

class C : I
{
}

class Test
{
    void M1(C c, I2 i2)
    {
        if (c == null || i2 == null)
        {
            return;
        }

        var i = (I)c;
        if (i == null)
        {
            return;
        }

        i = (I)i2;
        if (i == null)
        {
            return;
        }
    }
}
",
            // Test0.cs(24,13): warning CA1508: 'i == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "i == null", "false"),
            // Test0.cs(30,13): warning CA1508: 'i == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(30, 13, "i == null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Interface I
End Interface

Interface I2
    Inherits I
End Interface

Class C
    Implements I
End Class

Class Test
    Private Sub M1(c As C, i2 As I2)
        If c Is Nothing OrElse i2 Is Nothing Then
            Return
        End If

        Dim i = DirectCast(c, I)
        If i Is Nothing Then
            Return
        End If

        i = DirectCast(i2, I)
        If i Is Nothing Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(20,12): warning CA1508: 'i Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(20, 12, "i Is Nothing", "False"),
            // Test0.vb(25,12): warning CA1508: 'i Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(25, 12, "i Is Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_Interfaces_02_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
interface I
{
}

interface I2 : I
{
}

class C : I
{
}

class Test
{
    void M1(object o)
    {
        if (o == null)
        {
            return;
        }

        var i = (I)o;
        if (i == null)
        {
            return;
        }

        var i2 = (I2)i;
        if (i2 == null)
        {
            return;
        }

        var c = (C)i;
        if (c == null)
        {
            return;
        }
    }
}
",
            // Test0.cs(24,13): warning CA1508: 'i == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "i == null", "false"),
            // Test0.cs(30,13): warning CA1508: 'i2 == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(30, 13, "i2 == null", "false"),
            // Test0.cs(36,13): warning CA1508: 'c == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(36, 13, "c == null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Interface I
End Interface

Interface I2
    Inherits I
End Interface

Class C
    Implements I
End Class

Class Test
    Private Sub M1(o As Object)
        If o Is Nothing Then
            Return
        End If

        Dim i = DirectCast(o, I)
        If i Is Nothing Then
            Return
        End If

        Dim i2 = DirectCast(i, I2)
        If i2 Is Nothing Then
            Return
        End If

        Dim c = DirectCast(i, C)
        If c Is Nothing Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(20,12): warning CA1508: 'i Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(20, 12, "i Is Nothing", "False"),
            // Test0.vb(25,12): warning CA1508: 'i2 Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(25, 12, "i2 Is Nothing", "False"),
            // Test0.vb(30,12): warning CA1508: 'c Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(30, 12, "c Is Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_TypeParameter_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D : C
{
}

class Test<T>
    where T : C
{
    void M1(T t)
    {
        if (t == null)
        {
            return;
        }

        var d = {|CS0030:(D)t|};   // Compiler error CS0030: Cannot convert type 'T' to 'D'
        if (d == null)
        {
            return;
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
    Inherits C
End Class

Class Test(Of T As C)
    Private Sub M1(t As T)
        If t Is Nothing Then
            Return
        End If

        Dim d = DirectCast({|BC30311:t|}, D)    '  Compiler error BC30311: Value of type 'T' cannot be converted to 'D'
        If d Is Nothing Then
            Return
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AfterDirectCast_TypeParameter_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class D : C
{
}

class Test<T>
    where T : C
{
    void M1(C c)
    {
        if (c == null)
        {
            return;
        }

        var t = (T)c;
        if (t == null)
        {
            return;
        }
    }

    void M2(T t)
    {
        if (t == null)
        {
            return;
        }

        var c = (C)t;
        if (c == null)
        {
            return;
        }
    }
}
",
            // Test0.cs(21,13): warning CA1508: 't == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(21, 13, "t == null", "false"),
            // Test0.cs(35,13): warning CA1508: 'c == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(35, 13, "c == null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class D
    Inherits C
End Class

Class Test(Of T As C)
    Private Sub M1(c As C)
        If c Is Nothing Then
            Return
        End If

        Dim t = DirectCast(c, T)
        If t Is Nothing Then
            Return
        End If
    End Sub

    Private Sub M2(t As T)
        If t Is Nothing Then
            Return
        End If

        Dim c = DirectCast(t, C)
        If c Is Nothing Then
            Return
        End If
    End Sub
End Class",
            // Test0.vb(16,12): warning CA1508: 't Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 12, "t Is Nothing", "False"),
            // Test0.vb(27,12): warning CA1508: 'c Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(27, 12, "c Is Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_ThrowExpressionWithoutArgument_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(C c)
    {
        try
        {
        }
        catch (System.Exception ex)
        {
            if (c == null)
            {
                throw;
            }
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AssignedInCatch_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1()
    {
        object o = null;
        try
        {
        }
        catch (System.Exception ex)
        {
            o = ex;
        }
        catch
        {
            o = null;
        }

        if (o != null)
        {
            return;
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_OutArgument_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public void Cleanup() { }
}

class Test
{
    bool M1()
    {
        C c = null;
        try
        {
            if (M2(out c))
            {
                return true;
            }
        }
        finally
        {
            c?.Cleanup();
        }

        return false;
    }

    bool M2(out C c)
    {
        c = new C();
        return true;
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_RefArgument_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int count)
    {
        int initialCount = count;
        M2(ref count);
        if (initialCount == count)
        {
        }
    }

    int M2(ref int count)
    {
        count++;
        return 0;
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_LogicalOr_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1(int x, int y)
    {
        var z = x;
        z |= y;
        if (z == x)
        {
        }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_DeconstructionAssignment_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(int x, int y)
    {
        C c = null;
        (c, x) = M2();
        if (c != null)
        {
        }
    }

    (C, int) M2() => (new C(), 0);
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_DeconstructionAssignment_InLambda_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(int x, int y)
    {
        C c = null;
        System.Action a = () => (c, x) = M2();
        a();
        if (c != null)
        {
        }
    }

    (C, int) M2() => (new C(), 0);
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/1647")]
        public async Task NullCheck_DeconstructionAssignment_InLambdaPassedAsArgument_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    void M1(int x, int y)
    {
        C c = null;
        System.Action a = () => (c, x) = M2();
        Invoke(a);
        if (c != null)
        {
        }
    }

    (C, int) M2() => (new C(), 0);
    void Invoke(System.Action a) => a();
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_AwaitExpression_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Threading.Tasks;

class Test
{
    Task<Test> M() => null;

    async Task M2()
    {
        Test t = await M();
        if (t == null)
        {
            return;
        }
    }
}
");
            await VerifyBasicAnalyzerAsync(@"
Imports System.Threading.Tasks

Class Test
    Private Function M() As Task(Of Test)
        Return Nothing
    End Function

    Private Async Function M2() As Task
        Dim t As Test = Await M()
        If t Is Nothing Then
            Return
        End If
    End Function
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_CollectionAddAndCount_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.Collections.Generic;

class Test
{
    void M(List<int> map)
    {
        System.Action a = () => {};
        a();

        var initialCount = map.Count;
        map.Add(0);
        if (map.Count == initialCount)
        {
            return;
        }
    }
}

class C
{
}
");
            await VerifyBasicAnalyzerAsync(@"
Imports System.Collections.Immutable

Class Test
    Private Sub M(map As ImmutableDictionary(Of C, Integer).Builder, arr As C())
        Dim initialCount = map.Count
        For Each element in arr
            map.Add(element, 0)
        Next

        If map.Count = initialCount Then
            Return
        End If
    End Sub
End Class

Class C
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCheck_BothSidesOfEquals_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M(Test a, Test b)
    {
        // If a is not-null, ensure b is not null.
        // If a is null, b may or may not be null.
        if ((a != null && b != null) == (a != null))
        {
            return;
        }
        if ((a != null) == (a != null && b != null))
        {
            return;
        }
    }
}
");
            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M(a As Test, b As Test)
        ' If a is not-null, ensure b is not null.
        ' If a is null, b may or may not be null.
        If (a IsNot Nothing AndAlso b IsNot Nothing) = (a IsNot Nothing) Then
            Return
        End If
        If (a IsNot Nothing) = (a IsNot Nothing AndAlso b IsNot Nothing) Then
            Return
        End If
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ConditionalAccessCheck_InsideLocalFunction_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    public bool Flag;
    bool? M(Test t)
    {
        bool? MyFunc() { return t?.Flag; }

        return MyFunc();
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ConditionalAccessCheck_InsideLocalFunction_02_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    public bool Flag;
    bool? M(Test t)
    {
        return MyFunc();

        bool? MyFunc() { return t?.Flag; }
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ConditionalAccessCheck_InsideInitializer_NoDiagnostic()
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
    public Test(C c)
        : base(c?.Flag == true)
    {
        var x = c?.Flag == true;
    }
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(1650, "https://github.com/dotnet/roslyn-analyzers/issues/1650")]
        public async Task ConditionalAccessCheck_InsideConstructorInitializer_Diagnostic()
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
    public Test(C c)
        : base(c != null ? (c?.Flag == true) : false)
    {
        var x = c != null;
    }
}
",
            // Test0.cs(15,29): warning CA1508: 'c' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(15, 29, "c", "null"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(1650, "https://github.com/dotnet/roslyn-analyzers/issues/1650")]
        public async Task ConditionalAccessCheck_InsideFieldInitializer_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public bool Flag;
}

class Test
{
    private static C c;
    private bool b = c != null ? (c?.Flag == true) : false;
}
",
            // Test0.cs(10,35): warning CA1508: 'c' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(10, 35, "c", "null"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(1650, "https://github.com/dotnet/roslyn-analyzers/issues/1650")]
        public async Task ConditionalAccessCheck_InsidePropertyInitializer_ExpressionBody_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    public bool Flag;
}

class Test
{
    private static C c1, c2;
    private bool B1 => c1 != null ? (c1?.Flag == true) : false;
    private bool B2 { get; } = c2 != null ? (c2?.Flag == true) : false;
}
",
            // Test0.cs(10,38): warning CA1508: 'c1' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(10, 38, "c1", "null"),
            // Test0.cs(11,46): warning CA1508: 'c2' is never 'null'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpNeverNullResultAt(11, 46, "c2", "null"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task InsideLockStatement_FieldCheck_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
}

class Test
{
    private C _c;
    private readonly object _gate = new object();
    void EnsureC()
    {
        if (_c == null)
        {
            lock (_gate)
            {
                if (_c == null)
                {
                    _c = new C();
                }
            }
        }
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class C
End Class

Class Test
    Private _c As C
    Private ReadOnly _gate As New Object

    Private Sub EnsureC()
        If _c Is Nothing Then
            SyncLock _gate
                If _c Is Nothing Then
                    _c = New C()
                End If
            End SyncLock
        End If
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task TestCompilerGeneratedNullCheckNotFlagged()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

public class C : IDisposable
{
    public void Dispose() { }

    public void M(C c)
    {
        if (c == null)
        {
            return;
        }

        // Compiler implicitly generates a null-check for assigned variable here.
        // We should not flag compiler generate operations as redundant.
        using (c = new C())
        {
        }
    }
}");
        }

#if NETCOREAPP
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(4327, "https://github.com/dotnet/roslyn-analyzers/issues/4327")]
        public async Task TestCompilerGeneratedNullCheckNotFlagged_02()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System.Collections.Generic;
using System.Threading.Tasks;

class Class1
{
    static async IAsyncEnumerable<int> M(IAsyncEnumerable<int> iae)
    {
        await foreach (int i in iae.ConfigureAwait(false))
        {
            if (i > 0)
                yield return i;
        }
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }
#endif

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact, WorkItem(4382, "https://github.com/dotnet/roslyn-analyzers/issues/4382")]
        public async Task TestCompilerGeneratedNullCheckNotFlagged_03()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

class C
{
    void F(IDisposable x)
    {
        if (x != null)
        {
            using (x) { }
        }
    }
}");
        }

        [Fact]
        public async Task StaticObjectReferenceEquals_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        if (ReferenceEquals(c, null))
        {
            return;
        }

        if (c != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!ReferenceEquals(c, c2))
        {
            return;
        }

        if (c != c2)
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!ReferenceEquals(c, c2))
        {
        }
    }
}",
            // Test0.cs(16,13): warning CA1508: 'c != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 13, "c != null", "true"),
            // Test0.cs(28,13): warning CA1508: 'c != c2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(28, 13, "c != c2", "false"),
            // Test0.cs(40,14): warning CA1508: 'ReferenceEquals(c, c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(40, 14, "ReferenceEquals(c, c2)", "true"));

            await VerifyBasicAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        If ReferenceEquals(c, Nothing) Then
            Return
        End If

        If c IsNot Nothing Then
        End If
    End Sub

    Public Sub M2(c As C, c2 As C)
        If Not ReferenceEquals(c, c2) Then
            Return
        End If

        If c IsNot c2 Then
        End If
    End Sub

    Public Sub M3(c As C, c2 As C)
        If c IsNot c2 Then
            Return
        End If

        If Not ReferenceEquals(c, c2) Then
        End If
    End Sub
End Class",
            // Test0.vb(12,12): warning CA1508: 'c IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(12, 12, "c IsNot Nothing", "True"),
            // Test0.vb(21,12): warning CA1508: 'c IsNot c2' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(21, 12, "c IsNot c2", "False"),
            // Test0.vb(30,16): warning CA1508: 'ReferenceEquals(c, c2)' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(30, 16, "ReferenceEquals(c, c2)", "True"));
        }

        [Fact]
        public async Task StaticObjectEquals_NoObjectEqualsOverride_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        if (object.Equals(c, null))
        {
            return;
        }

        if (c != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!object.Equals(c, c2))
        {
            return;
        }

        if (c != c2)
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!object.Equals(c, c2))
        {
        }
    }
}",
            // Test0.cs(16,13): warning CA1508: 'c != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 13, "c != null", "true"),
            // Test0.cs(28,13): warning CA1508: 'c != c2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(28, 13, "c != c2", "false"),
            // Test0.cs(40,14): warning CA1508: 'object.Equals(c, c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(40, 14, "object.Equals(c, c2)", "true"));

            await VerifyBasicAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        If object.Equals(c, Nothing) Then
            Return
        End If

        If c IsNot Nothing Then
        End If
    End Sub

    Public Sub M2(c As C, c2 As C)
        If Not object.Equals(c, c2) Then
            Return
        End If

        If c IsNot c2 Then
        End If
    End Sub

    Public Sub M3(c As C, c2 As C)
        If c IsNot c2 Then
            Return
        End If

        If Not object.Equals(c, c2) Then
        End If
    End Sub
End Class",
            // Test0.vb(12,12): warning CA1508: 'c IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(12, 12, "c IsNot Nothing", "True"),
            // Test0.vb(21,12): warning CA1508: 'c IsNot c2' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(21, 12, "c IsNot c2", "False"),
            // Test0.vb(30,16): warning CA1508: 'object.Equals(c, c2)' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(30, 16, "object.Equals(c, c2)", "True"));
        }

        [Fact]
        public async Task StaticObjectEquals_ObjectEqualsOverride_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
public class C
{
    public int X;

    public override bool Equals(object other) => true;
}

public class Test
{
    public void M1(C c)
    {
        if (object.Equals(c, null))
        {
            return;
        }

        if (c != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!object.Equals(c, c2))
        {
            return;
        }

        if (c != c2)    // No diagnostic reported here as 'object.Equals(c, c2)' does not guarantee reference equality due to Equals override in C.
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!object.Equals(c, c2))
        {
        }
    }
}",
            // Test0.cs(18,13): warning CA1508: 'c != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(18, 13, "c != null", "true"),
            // Test0.cs(42,14): warning CA1508: 'object.Equals(c, c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(42, 14, "object.Equals(c, c2)", "true"));

            await VerifyBasicAnalyzerAsync(@"
Public Class C
    Public X As Integer

    Public Overrides Function Equals(other As Object) As Boolean
        Return True
    End Function
End Class

Public Class Test
    Public Sub M1(c As C)
        If object.Equals(c, Nothing) Then
            Return
        End If

        If c IsNot Nothing Then
        End If
    End Sub

    Public Sub M2(c As C, c2 As C)
        If Not object.Equals(c, c2) Then
            Return
        End If

        If c IsNot c2 Then      ' No diagnostic reported here as 'object.Equals(c, c2)' does not guarantee reference equality due to Equals override in C.
        End If
    End Sub

    Public Sub M3(c As C, c2 As C)
        If c IsNot c2 Then
            Return
        End If

        If Not object.Equals(c, c2) Then
        End If
    End Sub
End Class",
            // Test0.vb(16,12): warning CA1508: 'c IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 12, "c IsNot Nothing", "True"),
            // Test0.vb(34,16): warning CA1508: 'object.Equals(c, c2)' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(34, 16, "object.Equals(c, c2)", "True"));
        }

        [Fact]
        public async Task ObjectEquals_NoObjectEqualsOverride_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c, C c2)
    {
        if (c == null || !c.Equals(c2))
        {
            return;
        }

        if (c2 != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!c.Equals(c2))
        {
            return;
        }

        if (c != c2)
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!c.Equals(c2))
        {
        }
    }
}",
            // Test0.cs(16,13): warning CA1508: 'c2 != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(16, 13, "c2 != null", "true"),
            // Test0.cs(28,13): warning CA1508: 'c != c2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(28, 13, "c != c2", "false"),
            // Test0.cs(40,14): warning CA1508: 'c.Equals(c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(40, 14, "c.Equals(c2)", "true"));

            await VerifyBasicAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C, c2 As C)
        If c Is Nothing OrElse Not c.Equals(c2) Then
            Return
        End If

        If c2 IsNot Nothing Then
        End If
    End Sub

    Public Sub M2(c As C, c2 As C)
        If Not c.Equals(c2) Then
            Return
        End If

        If c IsNot c2 Then
        End If
    End Sub

    Public Sub M3(c As C, c2 As C)
        If c IsNot c2 Then
            Return
        End If

        If Not c.Equals(c2) Then
        End If
    End Sub
End Class",
            // Test0.vb(12,12): warning CA1508: 'c2 IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(12, 12, "c2 IsNot Nothing", "True"),
            // Test0.vb(21,12): warning CA1508: 'c IsNot c2' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(21, 12, "c IsNot c2", "False"),
            // Test0.vb(30,16): warning CA1508: 'c.Equals(c2)' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(30, 16, "c.Equals(c2)", "True"));
        }

        [Fact]
        public async Task ObjectEquals_ObjectEqualsOverride_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
public class C
{
    public int X;

    public override bool Equals(object other) => true;
}

public class Test
{
    public void M1(C c, C c2)
    {
        if (c == null || !c.Equals(c2))
        {
            return;
        }

        if (c2 != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!c.Equals(c2))
        {
            return;
        }

        if (c != c2)    // No diagnostic reported here as 'c.Equals(c2)' only guarantees value equality.
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!c.Equals(c2))
        {
        }
    }
}",
            // Test0.cs(18,13): warning CA1508: 'c2 != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(18, 13, "c2 != null", "true"),
            // Test0.cs(42,14): warning CA1508: 'c.Equals(c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(42, 14, "c.Equals(c2)", "true"));

            await VerifyBasicAnalyzerAsync(@"
Public Class C
    Public X As Integer

    Public Overrides Function Equals(other As Object) As Boolean
        Return True
    End Function
End Class

Public Class Test
    Public Sub M1(c As C, c2 As C)
        If c Is Nothing OrElse Not c.Equals(c2) Then
            Return
        End If

        If c2 IsNot Nothing Then
        End If
    End Sub

    Public Sub M2(c As C, c2 As C)
        If Not c.Equals(c2) Then
            Return
        End If

        If c IsNot c2 Then      ' No diagnostic reported here as 'c.Equals(c2)' only guarantees value equality.
        End If
    End Sub

    Public Sub M3(c As C, c2 As C)
        If c IsNot c2 Then
            Return
        End If

        If Not c.Equals(c2) Then
        End If
    End Sub
End Class",
            // Test0.vb(16,12): warning CA1508: 'c2 IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(16, 12, "c2 IsNot Nothing", "True"),
            // Test0.vb(34,16): warning CA1508: 'c.Equals(c2)' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(34, 16, "c.Equals(c2)", "True"));
        }

        [Fact]
        public async Task IEquatableEquals_ExplicitImplementation_Diagnostic()
        {
            // Explicit implementation of Equals means c1.Equals(c2) performs
            // reference equality using object.Equals(object) overload.

            await VerifyCSharpAnalyzerAsync(@"
using System;

public class C : IEquatable<C>
{
    public int X;

    bool IEquatable<C>.Equals(C other) => true;
}

public class Test
{
    public void M1(C c, C c2)
    {
        if (c == null || !c.Equals(c2))
        {
            return;
        }

        if (c2 != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!c.Equals(c2))
        {
            return;
        }

        if (c != c2)
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!c.Equals(c2))
        {
        }
    }
}
",
            // Test0.cs(20,13): warning CA1508: 'c2 != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 13, "c2 != null", "true"),
            // Test0.cs(32,13): warning CA1508: 'c != c2' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(32, 13, "c != c2", "false"),
            // Test0.cs(44,14): warning CA1508: 'c.Equals(c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(44, 14, "c.Equals(c2)", "true"));

            await VerifyBasicAnalyzerAsync(@"
Imports System

Public Class C
    Implements IEquatable(Of C)

    Public X As Integer
    Private Function Equals(other As C) As Boolean Implements IEquatable(Of C).Equals
        Return True
    End Function
End Class

Public Class Test
    Public Sub M1(c As C, c2 As C)
        If c Is Nothing OrElse Not c.Equals(c2) Then
            Return
        End If

        If c2 IsNot Nothing Then
        End If
    End Sub

    Public Sub M2(c As C, c2 As C)
        If Not c.Equals(c2) Then
            Return
        End If

        If c IsNot c2 Then
        End If
    End Sub

    Public Sub M3(c As C, c2 As C)
        If c IsNot c2 Then
            Return
        End If

        If Not c.Equals(c2) Then
        End If
    End Sub
End Class",
            // Test0.vb(19,12): warning CA1508: 'c2 IsNot Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(19, 12, "c2 IsNot Nothing", "True"),
            // Test0.vb(28,12): warning CA1508: 'c IsNot c2' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(28, 12, "c IsNot c2", "False"),
            // Test0.vb(37,16): warning CA1508: 'c.Equals(c2)' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(37, 16, "c.Equals(c2)", "True"));
        }

        [Fact]
        public async Task IEquatableEquals_ImplicitImplementation_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

public class C : IEquatable<C>
{
    public int X;

    public bool Equals(C other) => true;
}

public class Test
{
    public void M1(C c, C c2)
    {
        if (c == null || !c.Equals(c2))
        {
            return;
        }

        if (c2 != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!c.Equals(c2))
        {
            return;
        }

        if (c != c2)    // No diagnostic reported here as 'c.Equals(c2)' only guarantees value equality.
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!c.Equals(c2))
        {
        }
    }
}
",
            // Test0.cs(20,13): warning CA1508: 'c2 != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(20, 13, "c2 != null", "true"),
            // Test0.cs(44,14): warning CA1508: 'c.Equals(c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(44, 14, "c.Equals(c2)", "true"));
        }

        [Fact]
        public async Task IEquatableEquals_Override_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

public abstract class MyEquatable<T> : IEquatable<T>
{
    public abstract bool Equals(T other);
}

public class C : MyEquatable<C>
{
    public int X;
    public override bool Equals(C other) => true;
}

public class Test
{
    public void M1(C c, C c2)
    {
        if (c == null || !c.Equals(c2))
        {
            return;
        }

        if (c2 != null)
        {
        }
    }

    public void M2(C c, C c2)
    {
        if (!c.Equals(c2))
        {
            return;
        }

        if (c != c2)    // No diagnostic reported here as 'c.Equals(c2)' only guarantees value equality.
        {
        }
    }

    public void M3(C c, C c2)
    {
        if (c != c2)
        {
            return;
        }

        if (!c.Equals(c2))
        {
        }
    }
}
",
            // Test0.cs(24,13): warning CA1508: 'c2 != null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(24, 13, "c2 != null", "true"),
            // Test0.cs(48,14): warning CA1508: 'c.Equals(c2)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(48, 14, "c.Equals(c2)", "true"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task MultidimensionalArray_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M()
    {
        var x = new int[,] { { 1, 2 }, { 2, 3 } };
        if (x == null)
        {
        }
    }
}
",
            // Test0.cs(7,13): warning CA1508: 'x == null' is always 'false'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(7, 13, "x == null", "false"));

            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M()
        Dim x = New Integer(,) { { 1, 2 }, { 2, 3 } }
        If x Is Nothing Then
        End If
    End Sub
End Class
",
            // Test0.vb(5,12): warning CA1508: 'x Is Nothing' is always 'False'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(5, 12, "x Is Nothing", "False"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_LambdaResult_Diagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1()
    {
        string x = null;
        System.Func<string, bool> isNonNull = s => s != null;

        if (isNonNull(x))
        {
            return;
        }
    }
}
",
            // Test0.cs(9,13): warning CA1508: 'isNonNull(x)' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
            GetCSharpResultAt(9, 13, "isNonNull(x)", "true"));

            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M1()
        Dim x As String = Nothing
        Dim isNonNull As System.Func(Of String, Boolean) = Function(s) s IsNot Nothing

        If isNonNull(x) Then
            Return
        End If
    End Sub
End Class
",
            // Test0.vb(7,12): warning CA1508: 'isNonNull(x)' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
            GetBasicResultAt(7, 12, "isNonNull(x)", "True"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_LambdaResult_EditorConfig_NoInterproceduralLambdaAnalysis_NoDiagnostic()
        {
            const string editorConfigText = "dotnet_code_quality.max_interprocedural_lambda_or_local_function_call_chain = 0";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class Test
{
    void M1()
    {
        string x = null;
        System.Func<string, bool> isNonNull = s => s != null;

        if (isNonNull(x))
        {
            return;
        }
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Class Test
    Private Sub M1()
        Dim x As String = Nothing
        Dim isNonNull As System.Func(Of String, Boolean) = Function(s) s IsNot Nothing

        If isNonNull(x) Then
            Return
        End If
    End Sub
End Class
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_LambdaResult_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1()
    {
        System.Action<string> myLambda = (string x) =>
        {
            System.Func<string, bool> isNonNull = s => s != null;

            if (isNonNull(x))
            {
                return;
            }
        };

        myLambda(null);
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M1()
        Dim myLambda As System.Action(Of String) = Sub(x As String)
                                                       Dim isNonNull As System.Func(Of String, Boolean) = Function(s) s IsNot Nothing

                                                       If isNonNull(x) Then
                                                           Return
                                                       End If
                                                   End Sub

        myLambda(Nothing)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_LambdaResult_NoDiagnostic_02()
        {
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1()
    {
        System.Action<string> myLambda = (string x) =>
        {
            System.Func<string, bool> isNonNull = s => s != null;

            if (isNonNull(x))
            {
                return;
            }
        };

        myLambda(null);
        myLambda("""");
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M1()
        Dim myLambda As System.Action(Of String) = Sub(x As String)
                                                       Dim isNonNull As System.Func(Of String, Boolean) = Function(s) s IsNot Nothing

                                                       If isNonNull(x) Then
                                                           Return
                                                       End If
                                                   End Sub

        myLambda(Nothing)
        myLambda("""")
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCheck_LambdaResult_NoDiagnostic_03()
        {
            // We do not analyze lambdas/local functions independent of the calling context
            // Hence no diagnostic reported for dead code in lambda here.
            await VerifyCSharpAnalyzerAsync(@"
class Test
{
    void M1()
    {
        System.Action myLambda = () =>
        {
            System.Func<string, bool> isNonNull = s => s != null;

            if (isNonNull(null))
            {
                return;
            }
        };

        myLambda();
    }
}
");

            await VerifyBasicAnalyzerAsync(@"
Class Test
    Private Sub M1()
        Dim myLambda As System.Action = Sub()
                                            Dim isNonNull As System.Func(Of String, Boolean) = Function(s) s IsNot Nothing

                                            If isNonNull(Nothing) Then
                                                Return
                                            End If
                                        End Sub
        myLambda()
    End Sub
End Class");
        }

        [Fact, WorkItem(1855, "https://github.com/dotnet/roslyn-analyzers/issues/1855")]
        public async Task PointsToAbstractValue_MakeMayBeNull_AssertsKindNotEqualKnownKnownLValueCaptures()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;
 namespace Blah
{
    public class SomeSetting
    {
        private string[] _modules;
        private readonly IDictionary<string, string> _values;
         public SomeSetting()
        {
            _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Modules = new string[0];
        }
         public SomeSetting(SomeSetting settings)
        {
            _values = new Dictionary<string, string>(settings._values, StringComparer.OrdinalIgnoreCase);
            Modules = settings.Modules;
        }
         public string this[string key]
        {
            get
            {
                string retVal;
                return _values.TryGetValue(key, out retVal) ? retVal : null;
            }
            set { _values[key] = value; }
        }
         /// <summary>
        /// List of available modules for this setting
        /// </summary>
        public string[] Modules
        {
            get
            {
                return _modules ?? (Modules = (_values[""Modules""] ?? """").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                                                                     .Select(t => t.Trim())
                                                                     .ToArray();
            }
            set
            {
                _modules = value;
                this[""Modules""] = string.Join("";"", value);
            }
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task NullCompare_Unboxing_InstanceEntityAssert()
        {
            await VerifyCSharpAnalyzerAsync(@"
struct S
{
    public C C { get; }
}

class C
{
    private object _field;
    public void M(C c)
    {
        var x = (_field as C) ?? ((S)_field).C;
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NullCompare_PointsToFlowCaptureAssert()
        {
            await VerifyCSharpAnalyzerAsync(@"
class C
{
    private C _field;
    public void M(int[] array, int x)
    {
        C c = null;
        C c2 = null;
        C c3 = null;

        LocalFunction2();
        LocalFunction3();

        void LocalFunction()
        {
            c = c ?? GetC();
            c.M2();
        }

        void LocalFunction2()
        {
            LocalFunction();
            c2 = c2 ?? GetC();
            c2.M2();
        }

        void LocalFunction3()
        {
            LocalFunction();
            c3 = c3 ?? GetC();
            c3.M2();
        }
    }

    C GetC() => _field;
    void M2() { }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NestedLambdaAndLocalFunctionsWithCaptures()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;

class C
{
    public void M(C p, C p1, C p2)
    {
        C l1 = p1;
        LocalFunction();
        return;

        void LocalFunction()
        {
            C l2 = p2;
            M2(s => s != null && p != null && l1 != null && l2 != null);
        }
    }

    void M2(Func<C, bool> f) { }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task CopyAnalysisAssert_IndexerArrayAccessWithCast()
        {
            await VerifyCSharpAnalyzerAsync(@"
public struct S
{
    public object Value { get; }
}

public class C
{
    public void M(S[] arguments)
    {
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Value != null)
            {
                var value = (string)arguments[i].Value;
                var value2 = (S)arguments[i + 1].Value;
            }
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task ArrayInitializerNotParentedByArrayCreation()
        {
            await VerifyBasicAnalyzerAsync(@"
Class C
    Public Sub F(p As Object)
        Dim a = {|BC30451:M|}
        Dim b = If(p, new C())
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.CA1508.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.CA1508.excluded_symbol_names = M*")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class Test
{
    void M1(string param)
    {
        param = null;
        if (param == null)
        {
        }
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(
                    // Test0.cs(7,13): warning CA1508: 'param == null' is always 'true'. Remove or refactor the condition(s) to avoid dead code.
                    GetCSharpResultAt(7, 13, @"param == null", "true")
                );
            }

            await csharpTest.RunAsync();

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Module Test
    Sub M1(param As String)
        param = Nothing
        If param Is Nothing Then
        End If
    End Sub
End Module"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            };

            if (editorConfigText.Length == 0)
            {
                vbTest.ExpectedDiagnostics.Add(
                    // Test0.vb(5,12): warning CA1508: 'param Is Nothing' is always 'True'. Remove or refactor the condition(s) to avoid dead code.
                    GetBasicResultAt(5, 12, "param Is Nothing", "True")
                );
            }

            await vbTest.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        [WorkItem(3063, "https://github.com/dotnet/roslyn-analyzers/issues/3063")]
        [WorkItem(2985, "https://github.com/dotnet/roslyn-analyzers/issues/2985")]
        public async Task UsingBlock_NoDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;

public class Class1
{
    public static void M1(string values)
    {
        if (values == null) { }

        using (var sw = new StringWriter()) { }
    }

    public static void M2(byte[] data)
	{
		using (var ms = new MemoryStream(data))
		{
			Console.WriteLine(ms);
		}
	}
}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task UsingStatement_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;
using System.IO;

public class Class1
{
    public void M1(string path)
    {
        if (!File.Exists(path))
            return;

        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        DoSomething(fileStream);
    }

    private static void DoSomething(Stream stream) { }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory, WorkItem(3685, "https://github.com/dotnet/roslyn-analyzers/issues/3685")]
        [InlineData("IsNullOrWhiteSpace")]
        [InlineData("IsNullOrEmpty")]
        public async Task StringNullCheckApis(string apiName)
        {
            await new VerifyCS.Test
            {
                TestCode = $@"
using System;
using System.IO;

public class Class1
{{
    public void M1(object value)
    {{
        if (value is string stringValue)
        {{
            if (string.{apiName}(stringValue))
            {{
            }}
        }}
    }}
}}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        [WorkItem(3845, "https://github.com/dotnet/roslyn-analyzers/issues/3845")]
        public async Task ParamArrayNullCheckIsNotFlagged()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(params int[] p)
    {
        if (p == null)
        {
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub M(ParamArray p As Integer())
        If p is Nothing Then
        End If
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        [WorkItem(4509, "https://github.com/dotnet/roslyn-analyzers/issues/4509")]
        public async Task GenericFieldNullCheckIsNotFlagged()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class MyClass<T>
{
    private T m_myValue;

    public void M()
    {
        var x = this.m_myValue?.ToString();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class [MyClass](Of T)
    Private m_myValue As T

    Public Sub M()
        Dim x = Me.m_myValue?.ToString()
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        [WorkItem(4548, "https://github.com/dotnet/roslyn-analyzers/issues/4548")]
        public async Task ActivatorCreateInstanceNullCheckIsNotFlagged()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void M()
    {
        var value = Activator.CreateInstance(typeof(int?));
        if (value is null)
        {
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public Sub M()
        Dim value = Activator.CreateInstance(GetType(Integer?))

        If value Is Nothing Then
        End If
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        [WorkItem(4548, "https://github.com/dotnet/roslyn-analyzers/issues/4548")]
        public async Task FactoryMethodWithNullableReturnIsNotFlagged()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;

#nullable enable

public class C
{
    public void M(bool flag)
    {
        var value = CreateC(flag);
        if (value is null)
        {
        }
    }

    public static C? CreateC(bool flag)
    {
        return flag ? new C() : null;
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
            }.RunAsync();
        }
    }
}
