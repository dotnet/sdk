// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Data.ReviewSqlQueriesForSecurityVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Data.ReviewSqlQueriesForSecurityVulnerabilities,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Data.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ValueContentAnalysis)]
    public class ReviewSQLQueriesForSecurityVulnerabilitiesTests_FlowAnalysis : ReviewSQLQueriesForSecurityVulnerabilitiesTests
    {
        private static DiagnosticResult GetCSharpResultAt(int line, int column, string invokedSymbol, string containingMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(invokedSymbol, containingMethod);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string invokedSymbol, string containingMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(invokedSymbol, containingMethod);

        [Fact]
        public async Task FlowAnalysis_LocalWithConstantInitializer_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        string str = """";
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim str As String = """"
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_LocalWithConstantAssignment_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        string str;
        str = """";
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim str As String
        str = """"
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_ParameterWithConstantAssignment_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string str)
    {{
        str = """";
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(str As String)
        str = """"
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_LocalWithAllConstantAssignments_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string nonconst, bool flag)
    {{
        string str = """", str2 = """", str3 = ""nonempty"";
        if (flag) {{ str = str2; }}
        else  {{ str = str3; }}
        Command c = new Command1(str, str);
        str = nonconst; // assignment with non-constant value after call should not affect
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(nonconst as String, flag as Boolean)
        Dim str = """", str2 = """", str3 = ""nonempty""
        If flag Then
            str = str2
        Else
            str = str3
        End If
        Dim c As New Command1(str, str)
        str = nonconst ' assignment with non-constant value after call should not affect
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_ParameterWithAllConstantAssignments_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string nonconst, bool flag, string str)
    {{
        string str2 = """", str3 = ""nonempty"";
        str = """";
        if (flag) {{ str = str2; }}
        else  {{ str = str3; }}
        Command c = new Command1(str, str);
        str = nonconst; // assignment with non-constant value after call should not affect
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(nonconst as String, flag as Boolean, str as String)
        Dim str2 = """", str3 = ""nonempty""
        str = """"
        If flag Then
            str = str2
        Else
            str = str3
        End If
        Dim c As New Command1(str, str)
        str = nonconst ' assignment with non-constant value after call should not affect
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_ConstantFieldInitializer_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    const string _field = """";
    void M1()
    {{
        string str = _field;
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Const _field As String = """"
    Sub M1()
        Dim str As String = _field
        Dim c As New Command1(str, str)
    End Sub
End Class");
        }

        [Fact]
        public async Task FlowAnalysis_ConversionsInInitializer_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        object obj = """";          // Implicit conversion from string to object
        string str = (string)obj;   // Explicit conversion from object to string
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Sub M1()
        Dim obj As Object = """"                        ' Implicit conversion from string to object
        Dim str As String = DirectCast(obj, String)     ' Explicit conversion from object to string
        Dim c As New Command1(str, str)
    End Sub
End Class");
        }

        [Fact]
        public async Task FlowAnalysis_ImplicitUserDefinedConversionsInInitializer_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    Test(string s)
    {{
    }}

    void M1()
    {{
        Test t = """";     // Implicit user defined conversion
        string str = t;    // Implicit user defined conversion
        Command c = new Command1(str, str);
    }}

    public static implicit operator Test(string value)
    {{
        return null;
    }}

    public static implicit operator string(Test value)
    {{
        return null;
    }}
}}
",
            GetCSharpResultAt(103, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test

    Private Sub New(ByVal s As String)
        MyBase.New
    End Sub

    Private Sub M1()
        Dim t As Test = """"    ' Implicit user defined conversion
        Dim str As String = t   ' Implicit user defined conversion
        Dim c As New Command1(str, str)
    End Sub

    Public Shared Widening Operator CType(ByVal value As String) As Test
        Return Nothing
    End Operator

    Public Shared Widening Operator CType(ByVal value As Test) As String
        Return Nothing
    End Operator
End Class",
            GetBasicResultAt(140, 18, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_ExplicitUserDefinedConversionsInInitializer_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    Test(string s)
    {{
    }}

    void M1()
    {{
        Test t = (Test)"""";       // Explicit user defined conversion
        string str = (string)t;    // Explicit user defined conversion
        Command c = new Command1(str, str);
    }}

    public static explicit operator Test(string value)
    {{
        return null;
    }}

    public static explicit operator string(Test value)
    {{
        return null;
    }}
}}
",
            GetCSharpResultAt(103, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
Option Strict On

{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test

    Private Sub New(ByVal s As String)
        MyBase.New
    End Sub

    Private Sub M1()
        Dim t As Test = CType("""", Test)       ' Explicit user defined conversion
        Dim str As String = CType(t, String)    ' Explicit user defined conversion
        Dim c As New Command1(str, str)
    End Sub

    Public Shared Narrowing Operator CType(ByVal value As String) As Test
        Return Nothing
    End Operator

    Public Shared Narrowing Operator CType(ByVal value As Test) As String
        Return Nothing
    End Operator
End Class",
            GetBasicResultAt(142, 18, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_LocalInitializerWithInvocation_Diagnostic()
        {
            // Currently, we do not do any interprocedural or context sensitive flow analysis.
            // So method calls are assumed to always return a MayBe result.
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string command)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        string str = SomeString();
        Adapter c = new Adapter1(str, str);
    }}

    string SomeString() => """";
}}
",
            GetCSharpResultAt(98, 21, "Adapter1.Adapter1(string cmd, string command)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, command As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim str As String = SomeString()
        Dim c As New Adapter1(str, str)
    End Sub

    Function SomeString()
        Return """"
    End Function
End Module",
            GetBasicResultAt(134, 18, "Sub Adapter1.New(cmd As String, command As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_LocalWithByRefEscape_Diagnostic()
        {
            // Local/parameter passed by ref/out are assumed to be non-constant after the usage.
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = """";
        M2(ref str);
        Adapter c = new Adapter1(str, str);

        param = """";
        M2(ref param);
        c = new Adapter1(param, param);
    }}

    void M2(ref string str)
    {{
        str = """";
    }}
}}
",
            GetCSharpResultAt(99, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            GetCSharpResultAt(103, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = """"
        M2(str)
        Dim c As New Adapter1(str, str)

        param = """"
        M2(param)
        c = New Adapter1(param, param)
    End Sub

    Sub M2(ByRef str as String)
        str = """"
    End Sub
End Module",
            GetBasicResultAt(135, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            GetBasicResultAt(139, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_StringEmptyInitializer_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        string str = string.Empty;
        Adapter c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim str As String = String.Empty
        Dim c As New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_NameOfExpression_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = nameof(param);
        Adapter c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String = NameOf(param)
        Dim c As New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_NullOrDefaultValue_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        string str = default(string);
        Adapter c = new Adapter1(str, str);

        str = null;
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim str As String = Nothing
        Dim c As New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_InterpolatedString_Constant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var local = """";
        string str = $""text_{{""literal""}}_{{local}}"";
        Adapter c = new Adapter1(str, str);

        str = $"""";
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim local = """"
        Dim str As String = $""text_{{""literal""}}_{{local}}""
        Dim c As New Adapter1(str, str)

        str = $""""
        c = New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_InterpolatedString_NonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var local = """";
        string str = $""text_{{""literal""}}_{{local}}_{{param}}"";     // param might be non-constant.
        Adapter c = new Adapter1(str, str);
    }}
}}
",
            GetCSharpResultAt(99, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim local = """"
        Dim str As String = $""text_{{""literal""}}_{{local}}_{{param}}""     ' param might be non-constant.
        Dim c As New Adapter1(str, str)
    End Sub
End Module",
            GetBasicResultAt(135, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_BinaryAdd_Constant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str1 = """", str2 = """";
        string str = str1 + str2 + (str1 + str2);
        Adapter c = new Adapter1(str, str);

        str += str1;
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str1 = """", str2 = """"
        Dim str As String = str1 + str2 + (str1 + str2)
        Dim c As New Adapter1(str, str)

        str += str1
        c = New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_BinaryAdd_NonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str1 = """";
        string str = str1 + param;
        Adapter c = new Adapter1(str, str);

        str = """";
        str += param;
        c = new Adapter1(str, str);
    }}
}}
",
            GetCSharpResultAt(99, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            GetCSharpResultAt(103, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str1 = """"
        Dim str As String = str1 + param
        Dim c As New Adapter1(str, str)

        str1 = """"
        str += param
        c = New Adapter1(str, str)
    End Sub
End Module",
            GetBasicResultAt(135, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            GetBasicResultAt(139, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_NullCoalesce_Constant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str1 = """";
        string str = str1 ?? param;
        Adapter c = new Adapter1(str, str);

        str1 = null;
        str = str1 ?? """";
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str1 = """"
        Dim str As String = If(str1, param)
        Dim c As New Adapter1(str, str)

        str1 = Nothing
        str = If(str1, """")
        c = New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_NullCoalesce_NonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string str1, string param)
    {{
        string str = str1 ?? """";
        Adapter c = new Adapter1(str, str);

        str1 = null;
        str = str1 ?? param;
        c = new Adapter1(str, str);

        str1 = param;
        str = str1 ?? """";
        c = new Adapter1(str, str);
    }}
}}
",
            GetCSharpResultAt(98, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            GetCSharpResultAt(102, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            GetCSharpResultAt(106, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(str1 as String, param As String)
        Dim str As String = If(str1, """")
        Dim c As New Adapter1(str, str)

        str1 = Nothing
        str = If(str1, param)
        c = New Adapter1(str, str)

        str1 = param
        str = If(str1, """")
        c = New Adapter1(str, str)
    End Sub
End Module",
            GetBasicResultAt(134, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            GetBasicResultAt(138, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            GetBasicResultAt(142, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/1569")]
        public async Task FlowAnalysis_ConditionalAccess_Constant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public readonly string X = """";
}}

class Test
{{
    void M1(A a)
    {{
        string str = a?.X;
        Adapter c = new Adapter1(str, str);

        a = new A();
        str = a?.X;
        c = new Adapter1(str, str);

        a = null;
        str = a?.X;
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public ReadOnly X As String = """"
End Class

Module Test
    Sub M1(a As A)
        Dim str As String = a?.X
        Dim c As New Adapter1(str, str)

        a = new A()
        str = a?.X
        c = New Adapter1(str, str)

        a = Nothing
        str = a?.X
        c = New Adapter1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_ConditionalAccess_NonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string X;
}}

class Test
{{
    void M1(A a, string param)
    {{
        string str = a?.X;
        Adapter c = new Adapter1(str, str);

        a = new A();
        str = a?.X;
        c = new Adapter1(str, str);

        a.X = """";
        str = a?.X;
        c = new Adapter1(str, str);

        a.X = param;
        str = a?.X;
        c = new Adapter1(str, str);

        a = null;
        str = a?.X;     // result is always null, so no diagnostic
        c = new Adapter1(str, str);
    }}
}}
",
        GetCSharpResultAt(103, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
        GetCSharpResultAt(107, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
        GetCSharpResultAt(115, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public X As String
End Class

Module Test
    Sub M1(a As A, param As String)
        Dim str As String = a?.X
        Dim c As New Adapter1(str, str)

        a = new A()
        str = a?.X
        c = New Adapter1(str, str)

        a.X = """"
        str = a?.X
        c = New Adapter1(str, str)

        a.X = param
        str = a?.X
        c = New Adapter1(str, str)

        a = Nothing
        str = a?.X                  ' result is always null, so no diagnostic
        c = New Adapter1(str, str)
    End Sub
End Module",
            GetBasicResultAt(138, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            GetBasicResultAt(142, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            GetBasicResultAt(150, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_WhileLoop_NonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = """";
        while (true)
        {{
            Adapter c = new Adapter1(str, str);
            str = param;
        }}
    }}
}}
",
            GetCSharpResultAt(100, 25, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str = """"
        While True
            Dim c As New Adapter1(str, str)
            str = param
        End While
    End Sub
End Module",
            GetBasicResultAt(135, 22, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_ForLoop_NonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = """";
        for (int i = 0; i < 10; i++)
        {{
            Adapter c = new Adapter1(str, str);
            str = param;
        }}
    }}
}}
",
            GetCSharpResultAt(100, 25, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str = """"
        For i As Integer = 0 To 10
            Dim c As New Adapter1(str, str)
            str = param
        Next
    End Sub
End Module",
            GetBasicResultAt(135, 22, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_ForEachLoop_NonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = """";
        foreach (var i in new[] {{ 1, 2, 3 }})
        {{
            Adapter c = new Adapter1(str, str);
            str = param;
        }}
    }}
}}
",
            GetCSharpResultAt(100, 25, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str = """"
        For Each i In New Integer() {{1, 2, 3}}
            Dim c As New Adapter1(str, str)
            str = param
        Next
    End Sub
End Module",
            GetBasicResultAt(135, 22, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_Conditional_Constant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        if (param == """")
        {{
            Adapter c = new Adapter1(param, param);
        }}

        Adapter c2 = param == """" ? new Adapter1(param, param) : null;
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        If param = """" Then
            Dim c As New Adapter1(param, param)
        End If

        Dim c2 As Adapter = If(param = """", New Adapter1(param, param), Nothing)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_LocalFunctionInvocation_EmptyBody_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        string str;
        str = """";

        void MyLocalFunction()
        {{
        }};

        MyLocalFunction();    // This should not change state of 'str'.
        Command c = new Command1(str, str);
    }}
}}
");

            // VB has no local functions.
        }

        [Fact]
        public async Task FlowAnalysis_LocalFunctionInvocation_ChangesCapturedValueToConstant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str;
        str = param;

        void MyLocalFunction()
        {{
            str = """";
        }};

        MyLocalFunction();    // This should change state of 'str' to be a constant.
        Command c = new Command1(str, str);
    }}
}}
");

            // VB has no local functions.
        }

        [Fact]
        public async Task FlowAnalysis_LocalFunctionInvocation_ChangesCapturedValueToNonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Command3 : Command
{{
    public Command3(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str, str2 = param;
        str = """";

        void MyLocalFunction()
        {{
            str2 = str;
            str = param;
        }};

        MyLocalFunction();    // This should change state of 'str' to be a non-constant and 'str2' to be a constant.
        Command c = new Command1(str, str);     // Diagnostic
        c = new Command2(str2, str2);           // No Diagnostic

        MyLocalFunction();    // This should change state of 'str2' to also be a non-constant.
        c = new Command3(str2, str2);           // Diagnostic
    }}
}}
",
            // Test0.cs(121,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(121, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(125,13): warning CA2100: Review if the query string passed to 'Command3.Command3(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(125, 13, "Command3.Command3(string cmd, string parameter2)", "M1"));

            // VB has no local functions.
        }

        [Fact]
        public async Task FlowAnalysis_LocalFunctionInvocation_ChangesCapturedValueContextSensitive_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str;
        str = """";

        void MyLocalFunction(string param2)
        {{
            str = param2;
        }};

        MyLocalFunction(str);    // This should change state of 'str' to be a constant.
        Command c = new Command1(str, str);
    }}
}}
");

            // VB has no local functions.
        }

        [Fact]
        public async Task FlowAnalysis_LocalFunctionInvocation_ReturnValueContextSensitive_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str;
        str = """";

        string MyLocalFunction(string param2)
        {{
            return param2;
        }};

        str = MyLocalFunction(str);    // This should change state of 'str' to be a constant.
        Command c = new Command1(str, str);
    }}
}}
");

            // VB has no local functions.
        }

        [Fact]
        public async Task FlowAnalysis_LambdaInvocation_EmptyBody_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1()
    {{
        string str;
        str = """";

        System.Action myLambda = () =>
        {{
        }};

        myLambda();    // This should not change state of 'str'.
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1()
        Dim str As String
        str = """"

        Dim myLambda As System.Action = Sub()
                                        End Sub

        myLambda()      ' This should not change state of 'str'.
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_LambdaInvocation_ChangesCapturedValueToConstant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str;
        str = param;

        System.Action myLambda = () =>
        {{
            str = """";
        }};

        myLambda();    // This should change the state of 'str' to be a constant.
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String
        str = param

        Dim myLambda As System.Action = Sub()
                                            str = """"
                                        End Sub

        myLambda()      ' This should change the state of 'str' to be a constant.
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_LambdaInvocation_ChangesCapturedValueToNonConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Command3 : Command
{{
    public Command3(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str, str2 = param;
        str = """";

        System.Action myLambda = () =>
        {{
            str2 = str;
            str = param;
        }};

        myLambda();    // This should change state of 'str' to be a non-constant and 'str2' to be a constant.
        Command c = new Command1(str, str);     // Diagnostic
        c = new Command2(str2, str2);           // No Diagnostic

        myLambda();    // This should change state of 'str2' to also be a non-constant.
        c = new Command3(str2, str2);           // Diagnostic
    }}
}}
",
            // Test0.cs(121,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(121, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(125,13): warning CA2100: Review if the query string passed to 'Command3.Command3(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(125, 13, "Command3.Command3(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command3
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String, str2 As String = param
        str = """"

        Dim myLambda As System.Action = Sub()
                                           str2 = str
                                           str = param
                                        End Sub

        myLambda()      ' This should change state of 'str' to be a non-constant and 'str2' to be a constant.
        Dim c As Command = New Command1(str, str)     ' Diagnostic
        c = New Command2(str2, str2)                  ' No Diagnostic

        myLambda()      ' This should change state of 'str' to also be a non-constant.
        c = New Command3(str2, str2)                  ' Diagnostic
    End Sub
End Module",
            // Test0.vb(156,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(156, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(160,13): warning CA2100: Review if the query string passed to 'Sub Command3.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(160, 13, "Sub Command3.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_LambdaInvocation_ChangesCapturedValueContextSensitive_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str;
        str = """";

        System.Action<string> myLambda = (string param2) =>
        {{
            str = param2;
        }};

        myLambda(str);    // This should change state of 'str' to be a constant.
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String
        str = """"

        Dim myLambda As System.Action(Of String) =  Sub(param2 As String)
                                                        str = param2
                                                    End Sub

        myLambda(str)      '  This should change state of 'str' to be a constant.
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Fact]
        public async Task FlowAnalysis_LambdaInvocation_ReturnValueContextSensitive_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str;
        str = """";

        System.Func<string, string> myLambda = (string param2) =>
        {{
            return param2;
        }};

        str = myLambda(str);    // This should change state of 'str' to be a constant.
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str As String
        str = """"

        Dim myLambda As System.Func(Of String, String) =    Function (param2 As String)
                                                                Return param2
                                                            End Function

        str = myLambda(str)      '  This should change state of 'str' to be a constant.
        Dim c As New Command1(str, str)
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsToAnalysis_CopySemanticsForString_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    public string Field;
    void M1(Test t, string param)
    {{
        t.Field = """";
        string str = t.Field;
        t.Field = param; // This should not affect location/value of 'str'.
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Public Field As String
    Sub M1(t As Test, param As String)
        t.Field = """"
        Dim str As String = t.Field
        t.Field = param ' This should not affect location/value of 'str'.
        Dim c As New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeCopy_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    public string Field;
    void M1(Test t)
    {{
        t.Field = """";
        Test t2 = t;
        string str = t2.Field;
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Public Field As String
    Sub M1(t As Test)
        t.Field = """"
        Dim t2 As Test = t
        Dim str As String = t2.Field
        Dim c As New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueTypeCopy_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct Test
{{
    public string Field;
    void M1(Test t)
    {{
        t.Field = """";
        Test t2 = t;
        string str = t2.Field;
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure Test
    Public Field As String
    Sub M1(t As Test)
        t.Field = """"
        Dim t2 As Test = t
        Dim str As String = t2.Field
        Dim c As New Command1(str, str)
    End Sub
End Structure");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeNestingCopy_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        t.a.Field = """";
        Test t2 = t;
        string str = t2.a.Field;
        Command c = new Command1(str, str);

        str = param;
        A a = t.a;
        str = a.Field;
        c = new Command1(str, str);

        t.a.Field = param;
        a = t.a;
        A b = a;
        t2.a.Field = """";
        str = b.Field;
        c = new Command1(str, str);

    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
End Class

Class Test
    Public a As A
    Sub M1(t As Test, param As String)
        t.a.Field = """"
        Dim t2 As Test = t
        Dim str As String = t2.a.Field
        Dim c As New Command1(str, str)

        str = param
        Dim a As A = t.a
        str = a.Field
        c = New Command1(str, str)

        t.a.Field = param
        a = t.a
        Dim b As A = a
        t2.a.Field = """"
        str = b.Field
        c = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueTypeNestingCopy_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        t.a.Field = """";
        Test t2 = t;
        string str = t2.a.Field;
        Command c = new Command1(str, str);

        str = param;
        A a = t.a;
        str = a.Field;
        c = new Command1(str, str);

        t.a.Field = param;
        a = t.a;
        A b = a;
        t2.a.Field = """";  // 't2.a' and 'b' point to different value type objects.
        str = b.Field;
        c = new Command1(str, str);
    }}
}}
",
        // Test0.cs(118,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
        GetCSharpResultAt(118, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
End Structure

Class Test
    Public a As A
    Sub M1(t As Test, param As String)
        t.a.Field = """"
        Dim t2 As Test = t
        Dim str As String = t2.a.Field
        Dim c As New Command1(str, str)

        str = param
        Dim a As A = t.a
        str = a.Field
        c = New Command1(str, str)

        t.a.Field = param
        a = t.a
        Dim b As A = a
        t2.a.Field = """"       ' 't2.a' and 'b' point to different value type objects.
        str = b.Field
        c = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(153,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(153, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]

        public async Task FlowAnalysis_PointsTo_ValueTypeNestingCopy_02_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        t.a.Field = """";
        Test t2 = new Test();
        t = t2;             // This should clear out all the data about 't'
        string str = t.a.Field;
        Command c = new Command1(str, str);

        t.a.Field = """";
        A a = new A() {{ Field = param }};
        t2 = new Test(){{ a = a }};
        t = t2;             // This should clear out all the data about 't'
        str = t.a.Field;
        c = new Command1(str, str);
    }}
}}
",
        // Test0.cs(107,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
        GetCSharpResultAt(107, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
        // Test0.cs(114,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
        GetCSharpResultAt(114, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
End Structure

Class Test
    Public a As A
    Sub M1(t As Test, param As String) 
        t.a.Field = """"
        Dim t2 As New Test()
        t = t2             ' This should clear out all the data about 't'
        Dim str As String = t.a.Field
        Dim c As New Command1(str, str)

        t.a.Field = """"
        Dim a As New A() With {{ .Field = param }}
        t2 = New Test() With {{ .a = a }}
        t = t2             ' This should clear out all the data about 't'
        str = t.a.Field
        c = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(142,18): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(142, 18, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(149,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(149, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeAllocationAndInitializer_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        t = new Test();
        string str = t.a.Field;         // Unknown value.
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(105,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(105, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
    Public Field2 As String
End Class

Class Test

    Public a As A

    Private Sub M1(ByVal t As Test, ByVal param As String)
        t = New Test()
        Dim str As String = t.a.Field       ' Unknown value.
        Dim c As Command = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(143,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(143, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeAllocationAndInitializer_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        A b = new A();
        b.Field = """";
        t = new Test() {{ a = b }};
        string str = t.a.Field;                 //  'a' and 'b' point to same object.
        Command c = new Command1(str, str);

        str = param;
        t = new Test() {{ a = {{ Field = """" }} }};
        str = t.a.Field;
        c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
    Public Field2 As String
End Class

Class Test

    Public a As A

    Private Sub M1(ByVal t As Test, ByVal param As String)
        Dim b As A = New A()
        b.Field = """"
        t = New Test() With {{.a = b}}
        Dim str As String = t.a.Field
        Dim c As Command = New Command1(str, str)

        str = param
        t = New Test() With {{.a = New A() With {{.Field = """", .Field2 = .Field}} }}
        str = t.a.Field2
        c = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueTypeAllocationAndInitializer_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        t = new Test();
        string str = t.a.Field;         // Unknown value.
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(105,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(105, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
    Public Field2 As String
End Structure

Class Test

    Public a As A

    Private Sub M1(ByVal t As Test, ByVal param As String)
        t = New Test()
        Dim str As String = t.a.Field       ' Unknown value.
        Dim c As Command = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(143,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(143, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueTypeAllocationAndInitializer_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        A b = new A();
        b.Field = """";
        t = new Test() {{ a = b }};
        string str = t.a.Field;                 //  'a' and 'b' have the same value.
        Command c = new Command1(str, str);

        t.a.Field = param;
        str = param;
        t = new Test() {{ a = {{ Field = """" }} }};
        str = t.a.Field;
        c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
    Public Field2 As String
End Structure

Class Test

    Public a As A

    Private Sub M1(ByVal t As Test, ByVal param As String)
        Dim b As A = New A()
        b.Field = """"
        t = New Test() With {{.a = b}}
        Dim str As String = t.a.Field
        Dim c As Command = New Command1(str, str)

        str = param
        t = New Test() With {{.a = New A() With {{.Field = """", .Field2 = .Field}} }}
        str = t.a.Field2
        c = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueTypeAllocationAndInitializer_02_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

struct Test
{{
    public A a;
    void M1(Test t, string param)
    {{
        A b = new A();
        b.Field = """";
        t = new Test() {{ a = b }};
        Test t2 = t;
        string str = t2.a.Field;                 //  'a' and 'b' have the same value.
        Command1 c = new Command1(str, str);

        t.a.Field = param;
        str = param;
        t = new Test() {{ a = {{ Field = """" }} }};
        t2 = t;
        str = t2.a.Field;
        c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
    Public Field2 As String
End Structure

Structure Test

    Public a As A

    Private Sub M1(ByVal t As Test, ByVal param As String)
        Dim b As A = New A()
        b.Field = """"
        t = New Test() With {{.a = b}}
        Dim t2 As Test = t
        Dim str As String = t2.a.Field
        Dim c As Command = New Command1(str, str)

        str = param
        t = New Test() With {{.a = New A() With {{.Field = """", .Field2 = .Field}} }}
        t2 = t
        str = t2.a.Field2
        c = New Command1(str, str)
    End Sub
End Structure");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeCollectionInitializer_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(string param)
    {{
        var list = new System.Collections.Generic.List<string>() {{ """", param }};
        string str = list[1];
        Command c = new Command1(str, str);

        var list2 = new System.Collections.Generic.List<Test>() {{
            new Test() {{ a = {{ Field = """" }} }},
            new Test() {{ a = {{ Field = param }} }}
        }};
        str = list2[1].a.Field;
        c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(105,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(105, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(112,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(112, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
    Public Field2 As String
End Class

Class Test

    Public a As A

    Private Sub M1(ByVal param As String)
        Dim list = New System.Collections.Generic.List(Of String) From {{"""", param}}
        Dim str As String = list(1)
        Dim c As Command = New Command1(str, str)

        Dim list2 = New System.Collections.Generic.List(Of Test) From {{
            New Test() With {{ .a = New A() With {{ .Field = """", .Field2 = .Field }} }},
            New Test() With {{ .a = New A() With {{ .Field = param, .Field2 = .Field }} }}
        }}

        str = list2(1).a.Field2
        c = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(143,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(143, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(151,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(151, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/1570")]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeCollectionInitializer_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(string param)
    {{
        var list = new System.Collections.Generic.List<string>() {{ """", param }};
        string str = list[0];
        Command c = new Command1(str, str);

        var list2 = new System.Collections.Generic.List<Test>() {{
            new Test() {{ a = {{ Field = """" }} }},
            new Test() {{ a = {{ Field = param }} }}
        }};
        str = list2[0].a.Field;
        c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
    Public Field2 As String
End Class

Class Test

    Public a As A

    Private Sub M1(ByVal param As String)
        Dim list = New System.Collections.Generic.List(Of String) From {{"""", param}}
        Dim str As String = list(1)
        Dim c As Command = New Command1(str, str)

        Dim list2 = New System.Collections.Generic.List(Of Test) From {{
            New Test() With {{ .a = New A() With {{ .Field = """", .Field2 = .Field }} }},
            New Test() With {{ .a = New A() With {{ .Field = param, .Field2 = .Field }} }}
        }}

        str = list2(1).a.Field2
        c = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_DynamicObjectCreation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    public Test(int i)
    {{
    }}
    public Test(string s)
    {{
    }}

    void M1(Test t, string param, dynamic d)
    {{
        t = new Test(d);
        string str = t.a.Field;         // Unknown value.
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(112,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(112, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_DynamicObjectCreation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    public Test(int i)
    {{
    }}
    public Test(string s)
    {{
    }}

    void M1(Test t, string param, dynamic d)
    {{
        A b = new A();
        b.Field = """";
        t = new Test(d) {{ a = b }};
        string str = t.a.Field;                 //  'a' and 'b' point to same object.
        Command c = new Command1(str, str);

        str = param;
        t = new Test(d) {{ a = {{ Field = """" }} }};
        str = t.a.Field;
        c = new Command1(str, str);
    }}
}}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_AnonymousObjectCreation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var t = new {{ Field = """", Field2 = param }};
        string str = t.Field2;                  // Unknown value.
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(99,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(99, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Private Sub M1(ByVal param As String)
        Dim t As New With {{Key .Field1 = """", .Field2 = param }}
        Dim str As String = t.Field2       ' Unknown value.
        Dim c As Command = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(135,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(135, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task CSharp_FlowAnalysis_PointsTo_AnonymousObjectCreation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var t = new {{ Field = """", Field2 = param }};
        var t2 = new {{ Field = param, Field2 = """" }};

        string str = t.Field;
        Command c = new Command1(str, str);

        str = param;
        t = t2;
        str = t.Field2 + t2.Field2;
        c = new Command1(str, str);
    }}
}}
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/1568")]
        public async Task VisualBasic_FlowAnalysis_PointsTo_AnonymousObjectCreation_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Private Sub M1(ByVal param As String)
        Dim t As New With {{Key .Field1 = """", .Field2 = .Field1 }}
        Dim t2 As New With {{Key .Field1 = param, .Field2 = """" }}

        Dim str As String = t.Field2
        Dim c As Command = New Command1(str, str)

        str = param
        t = t2
        str = t.Field2 + t2.Field2
        c = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_BaseDerived__Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Base
{{
    public string Field;
}}
class Derived : Base
{{
}}

class Test
{{
    public Base B;
    void M1(string param)
    {{
        Test t = new Test();
        Derived d = new Derived();
        d.Field = param;
        t.B = new Base();
        t.B.Field = """";
        t.B = d;                    // t.B now points to d
        string str = t.B.Field;     // d.Field has unknown value.
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(113,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(113, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Base
    Public Field As String
End Class

Class Derived
    Inherits Base
End Class

Class Test
    Public b As Base

    Private Sub M1(ByVal param As String)
        Dim t As New Test()
        Dim d As New Derived()
        d.Field = param
        t.B = New Base()
        t.B.Field = """"
        t.B = d                             ' t.B now points to d
        Dim str As String = t.B.Field       ' d.Field has unknown value.
        Dim c As Command = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(150,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(150, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_BaseDerived_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Base
{{
    public string Field;
}}
class Derived : Base
{{
}}

class Test
{{
    public Base B;
    void M1(string param)
    {{
        Test t = new Test();
        Derived d = new Derived();
        d.Field = """";
        t.B = new Base();
        t.B.Field = param;
        t.B = d;                    // t.B now points to d
        string str = t.B.Field;     // d.Field is empty string.
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Base
    Public Field As String
End Class

Class Derived
    Inherits Base
End Class

Class Test
    Public b As Base

    Private Sub M1(ByVal param As String)
        Dim t As New Test()
        Dim d As New Derived()
        d.Field = """"
        t.B = New Base()
        t.B.Field = param
        t.B = d                             ' t.B now points to d
        Dim str As String = t.B.Field       ' d.Field is empty string.
        Dim c As Command = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_BaseDerived_IfStatement_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Base
{{
    public string Field;
}}
class Derived : Base
{{
}}

class Test
{{
    public Base B;
    void M1(string param)
    {{
        Test t = new Test();
        t.B = new Base();
        t.B.Field = param;
        if (param != null)
        {{
            Derived d = new Derived();
            d.Field = param;
            t.B = d;                    // t.B now points to d
        }}
        else 
        {{
            Base b = new Base();
            b.Field = """";
            t.B = b;                    // t.B now points to b
        }}

        string str = t.B.Field;         // t.B now points to either b or d, but d.Field could be an unknown value (param) in one code path.
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(123,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(123, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Base
    Public Field As String
End Class

Class Derived
    Inherits Base
End Class

Class Test
    Public b As Base

    Private Sub M1(ByVal param As String)
        Dim t As New Test()
        t.B = New Base()
        t.B.Field = param
        If param IsNot Nothing Then
            Dim d As New Derived()
            d.Field = param
            t.B = d                             ' t.B now points to d
        Else
            Dim b As New Base()
            b.Field = """"
            t.B = b                             ' t.B now points to b
        End If
        Dim str As String = t.B.Field           ' t.B now points to either b or d, but d.Field could be an unknown value (param) in one code path.
        Dim c As Command = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(156,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(156, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_BaseDerived_IfStatement_02_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Base
{{
    public string Field;
}}
class Derived : Base
{{
}}

class Test
{{
    public Base B;
    void M1(string param, string param2)
    {{
        Test t = new Test();
        t.B = new Base();
        t.B.Field = param;
        if (param != null)
        {{
            Derived d = new Derived();
            d.Field = """";
            t.B = d;                    // t.B now points to d
            if (param2 != null)
            {{
                d.Field = param;        // t.B.Field is unknown in this code path.
            }}
        }}
        else
        {{
            Base b = new Base();
            b.Field = """";
            t.B = b;                    // t.B now points to b
        }}

        string str = t.B.Field;         // t.B now points to either b or d, but d.Field could be an unknown value (param) in one code path.
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(127,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(127, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Base
    Public Field As String
End Class

Class Derived
    Inherits Base
End Class

Class Test
    Public b As Base

    Private Sub M1(ByVal param As String, ByVal param2 As String)
        Dim t As New Test()
        t.B = New Base()
        t.B.Field = param
        If param IsNot Nothing Then
            Dim d As New Derived()
            d.Field = """"
            t.B = d                             ' t.B now points to d
            If param2 IsNot Nothing Then
                d.Field = param                 ' t.B.Field is unknown in this code path.
            End If
        Else
            Dim b As New Base()
            b.Field = """"
            t.B = b                             ' t.B now points to b
        End If
        Dim str As String = t.B.Field           ' t.B now points to either b or d, but d.Field could be an unknown value (param) in one code path.
        Dim c As Command = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(159,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(159, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_BaseDerived_IfStatement_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Base
{{
    public string Field;
}}
class Derived : Base
{{
}}

class Test
{{
    public Base B;
    void M1(string param)
    {{
        Test t = new Test();
        t.B = new Base();
        t.B.Field = param;
        if (param != null)
        {{
            Derived d = new Derived();
            d.Field = """";
            t.B = d;                    // t.B now points to d
        }}
        else
        {{
            Base b = new Base();
            b.Field = """";
            t.B = b;                    // t.B now points to b
        }}

        string str = t.B.Field;         // t.B now points to either b or d, both of which have .Field = """"
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Base
    Public Field As String
End Class

Class Derived
    Inherits Base
End Class

Class Test
    Public b As Base

    Private Sub M1(ByVal param As String)
        Dim t As New Test()
        t.B = New Base()
        t.B.Field = param
        If param IsNot Nothing Then
            Dim d As New Derived()
            d.Field = """"
            t.B = d                             ' t.B now points to d
        Else
            Dim b As New Base()
            b.Field = """"
            t.B = b                             ' t.B now points to b
        End If
        Dim str As String = t.B.Field           ' t.B now points to either b or d, both of which have .Field = """"
        Dim c As Command = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_ThisInstanceReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        string str = this.a.Field;         // Unknown value.
        Command c = new Command1(str, str);

        str = this.Field;           // Unknown value.
        c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(106,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(106, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(109,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(109, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
    Public Field2 As String
End Class

Class Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Dim str As String = Me.a.Field       ' Unknown value.
        Dim c As Command = New Command1(str, str)

        str = Me.Field                       ' Unknown value.
        c = New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(143,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(143, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(146,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(146, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_ThisInstanceReference_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        A b = new A();
        b.Field = """";
        this.a = b;
        string str = this.a.Field;
        Command1 c = new Command1(str, str);

        str = param;
        this.a = new A() {{ Field = """" }};
        str = this.a.Field;
        c = new Command1(str, str);

        str = param;
        Field = """";
        str = this.Field;
        c = new Command1(str, str);
     }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
    Public Field2 As String
End Class

Class Test

    Public a As A
    Public Field As String
    Public Field2 As String
 
    Private Sub M1(ByVal param As String)
        Dim b As A = New A()
        b.Field = """"
        Me.a  = b
        Dim str As String = a.Field
        Dim c As New Command1(str, str)

        str = param
        Me.a = New A() With {{.Field = """", .Field2 = .Field}}
        str = a.Field2
        c = New Command1(str, str)

        str = param
        Me.Field = """"
        Field2 = Field
        str = Me.Field2
        c = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueType_ThisInstanceReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

struct Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        string str = this.a.Field;         // Unknown value.
        Command c = new Command1(str, str);

        str = this.Field;           // Unknown value.
        c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(106,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(106, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(109,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(109, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
    Public Field2 As String
End Structure

Structure Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Dim str As String = Me.a.Field       ' Unknown value.
        Dim c As Command = New Command1(str, str)

        str = Me.Field                       ' Unknown value.
        c = New Command1(str, str)
    End Sub
End Structure",
            // Test0.vb(143,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(143, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(146,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(146, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueType_ThisInstanceReference_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

struct Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        A b = new A();
        b.Field = """";
        this.a = b;
        string str = this.a.Field;
        Command1 c = new Command1(str, str);

        str = param;
        this.a = new A() {{ Field = """" }};
        str = this.a.Field;
        c = new Command1(str, str);

        str = param;
        Field = """";
        str = this.Field;
        c = new Command1(str, str);
     }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
    Public Field2 As String
End Structure

Structure Test

    Public a As A
    Public Field As String
    Public Field2 As String
 
    Private Sub M1(ByVal param As String)
        Dim b As A = New A()
        b.Field = """"
        Me.a  = b
        Dim str As String = a.Field
        Dim c As New Command1(str, str)

        str = param
        Me.a = New A() With {{.Field = """", .Field2 = .Field}}
        str = a.Field2
        c = New Command1(str, str)

        str = param
        Me.Field = """"
        Field2 = Field
        str = Me.Field2
        c = New Command1(str, str)
    End Sub
End Structure");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_InvocationInstanceReceiver_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        this.a.Field = """";
        this.M2();
        string str = this.a.Field;         // Unknown value.
        Command c = new Command1(str, str);

        Test t = new Test();
        t.Field = """";
        t.M2();
        str = t.Field;                     // Unknown value.
        c = new Command1(str, str);
    }}

    public void M2()
    {{
    }}
}}
",
            // Test0.cs(108,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(108, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(114,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(114, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
End Class

Class Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Me.a.Field = """"
        Me.M2()
        Dim str As String = Me.a.Field       ' Unknown value.
        Dim c As Command = New Command1(str, str)

        Dim t As New Test()
        t.Field = """"
        t.M2()
        str = Me.Field                       ' Unknown value.
        c = New Command1(str, str)
    End Sub

    Public Sub M2()
    End Sub
End Class",
            // Test0.vb(144,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(144, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(150,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(150, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueType_InvocationInstanceReceiver_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

struct Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        this.a.Field = """";
        this.M2();
        string str = this.a.Field;         // Unknown value.
        Command c = new Command1(str, str);

        Test t = new Test();
        t.Field = """";
        t.M2();
        str = t.Field;                     // Unknown value.
        c = new Command1(str, str);
    }}

    public void M2()
    {{
    }}
}}
",
            // Test0.cs(108,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(108, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(114,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(114, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
End Structure

Structure Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Me.a.Field = """"
        Me.M2()
        Dim str As String = Me.a.Field       ' Unknown value.
        Dim c As Command = New Command1(str, str)

        Dim t As New Test()
        t.Field = """"
        t.M2()
        str = Me.Field                       ' Unknown value.
        c = New Command1(str, str)
    End Sub

    Public Sub M2()
    End Sub
End Structure",
            // Test0.vb(144,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(144, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(150,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(150, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_Argument_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        Test t = new Test();
        t.Field = """";
        M2(t);
        string str = t.Field;               // Unknown value.
        Command c = new Command1(str, str);

        t.a.Field = """";
        this.M2(t);
        str = t.a.Field;                    // Unknown value.
        c = new Command1(str, str);

        t.a.Field = """";
        this.M3(ref t);
        str = t.a.Field;                    // Unknown value.
        c = new Command1(str, str);
    }}

    public void M2(Test t)
    {{
    }}

    public void M3(ref Test t)
    {{
    }}
}}
",
            // Test0.cs(109,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(109, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(114,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(114, 13, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(119,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(119, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
End Class

Class Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Dim t As New Test()
        t.Field = """"
        M2(t)
        Dim str As String = t.Field                       ' Unknown value.
        Dim c As Command = New Command1(str, str)

        t.a.Field = """"
        Me.M2(t)
        str = t.a.Field                                   ' Unknown value.
        c = New Command1(str, str)

        t.a.Field = """"
        Me.M3(t)
        str = t.a.Field                                   ' Unknown value.
        c = New Command1(str, str)
    End Sub

    Public Sub M2(t As Test)
    End Sub

    Public Sub M3(ByRef t as Test)
    End Sub
End Class",
            // Test0.vb(145,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(145, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(150,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(150, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(155,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(155, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceType_ThisArgument_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        Test t = new Test();
        this.a.Field = """";
        t.M2(this);
        string str = a.Field;               // Unknown value.
        Command c = new Command1(str, str);

        this.Field = """";
        t.M2(this);
        str = Field;                        // Unknown value.
        c = new Command1(str, str);
    }}

    public void M2(Test t)
    {{
    }}
}}
",
            // Test0.cs(109,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(109, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(114,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(114, 13, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
End Class

Class Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Dim t as New Test()
        Me.a.Field = """"
        t.M2(Me)
        Dim str As String = a.Field                        ' Unknown value.
        Dim c As Command = New Command1(str, str)

        Me.Field = """"
        t.M2(Me)
        str = Field                                        ' Unknown value.
        c = New Command1(str, str)
    End Sub

    Public Sub M2(t As Test)
    End Sub
End Class",
            // Test0.vb(145,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(145, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(150,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(150, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueType_Argument_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

struct Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        Test t = new Test();
        t.Field = """";
        M2(t);                              // Passing by value cannot change contents of a value type.
        string str = t.Field;
        Command c = new Command1(str, str);

        t.a.Field = """";
        this.M2(t);                         // Passing by value cannot change contents of a value type.
        str = t.a.Field;
        c = new Command1(str, str);
    }}

    public void M2(Test t)
    {{
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
End Structure

Structure Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Dim t As New Test()
        t.Field = """"
        M2(t)                                             ' Passing by value cannot change contents of a value type.
        Dim str As String = t.Field
        Dim c As Command = New Command1(str, str)

        t.a.Field = """"
        Me.M2(t)                                          ' Passing by value cannot change contents of a value type.
        str = t.a.Field
        c = New Command1(str, str)
    End Sub

    Public Sub M2(t As Test)
    End Sub

    Public Sub M3(ByRef t as Test)
    End Sub
End Structure");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueType_Argument_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

struct Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        Test t = new Test();
        t.a.Field = """";
        this.M2(ref t);
        string str = t.a.Field;                    // Passing by ref can change contents of a value type.
        Command c = new Command1(str, str);
    }}

    public void M2(ref Test t)
    {{
    }}
}}
",
            // Test0.cs(109,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(109, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
End Structure

Structure Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Dim t As New Test()
        t.a.Field = """"
        Me.M2(t)                                          ' Passing by ref can change contents of a value type.
        Dim str As String = t.a.Field
        Dim c As Command = New Command1(str, str)
    End Sub

    Public Sub M2(ByRef t as Test)
    End Sub
End Structure",
            // Test0.vb(145,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(145, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ValueType_ThisArgument_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string Field;
}}

struct Test
{{
    public A a;
    public string Field;

    void M1(string param)
    {{
        Test t = new Test();
        this.a.Field = """";
        t.M2(this);                              // Passing by value cannot change contents of a value type.
        string str = a.Field;
        Command c = new Command1(str, str);

        this.Field = """";
        t.M2(this);                              // Passing by value cannot change contents of a value type.
        str = Field;
        c = new Command1(str, str);
    }}

    public void M2(Test t)
    {{
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public Field As String
End Structure

Structure Test

    Public a As A
    Public Field As String

    Private Sub M1(ByVal param As String)
        Dim t as New Test()
        Me.a.Field = """"
        t.M2(Me)
        Dim str As String = a.Field                        ' Unknown value.
        Dim c As Command = New Command1(str, str)

        Me.Field = """"
        t.M2(Me)
        str = Field                                        ' Unknown value.
        c = New Command1(str, str)
    End Sub

    Public Sub M2(t As Test)
    End Sub
End Structure");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ConstantArrayElement_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string[] strArray)
    {{
        strArray[0] = """";
        string str = strArray[0];
        Adapter c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(strArray As String())
        strArray(0) = """"
        Dim str As String = strArray(0)
        Dim c As New Adapter1(str, str)
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_NonConstantArrayElement_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string[] strArray, string param)
    {{
        strArray[0] = param;
        string str = strArray[0];
        Adapter c = new Adapter1(str, str);
    }}
}}
",
            // Test0.cs(99,21): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(99, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(strArray As String(), param As String)
        strArray(0) = param
        Dim str As String = strArray(0)
        Dim c As New Adapter1(str, str)
    End Sub
End Module",
            // Test0.vb(135,18): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(135, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/1577")]
        public async Task FlowAnalysis_PointsTo_ConstantArrayElement_NonConstantIndex_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string[] strArray, int i)
    {{
        strArray[i] = """";
        string str = strArray[i];
        Adapter c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(strArray As String(), i As Integer)
        strArray(i) = """"
        Dim str As String = strArray(i)
        Dim c As New Adapter1(str, str)
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_NonConstantArrayElement_NonConstantIndex_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string[] strArray, int i, int j)
    {{
        strArray[i] = """";
        i = j;                          // We don't know value of 'i' now
        string str = strArray[i];       // Unknown value
        Adapter c = new Adapter1(str, str);
    }}
}}
",
            // Test0.cs(100,21): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(100, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(strArray As String(), i As Integer, j As Integer)
        strArray(i) = """"
        i = j
        Dim str As String = strArray(i)
        Dim c As New Adapter1(str, str)
    End Sub
End Module",
            // Test0.vb(136,18): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(136, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ArrayInitializer_ConstantArrayElement_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string[] strArray = new string[] {{ """", param }} ;
        string str = strArray[0];
        Adapter c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim strArray As String() = New String() {{ """", param }}
        Dim str As String = strArray(0)
        Dim c As New Adapter1(str, str)
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ArrayInitializer_NonConstantArrayElement_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string[] strArray = new string[] {{ """", param }} ;
        string str = strArray[1];
        Adapter c = new Adapter1(str, str);
    }}
}}
",
            GetCSharpResultAt(99, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim strArray As String() = New String() {{ """", param }}
        Dim str As String = strArray(1)
        Dim c As New Adapter1(str, str)
    End Sub
End Module",
            GetBasicResultAt(135, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ConstantArrayElement_ArrayFieldInReferenceType_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string[] StringArrayA;
}}

class Test
{{
    public A a;
    public string[] StringArray;

    void M1(Test t, string[] strArray1, string[] strArray2, string[] strArray3)
    {{
        t.StringArray = strArray1;
        strArray1[0] = """";
        string str = t.StringArray[0];
        Adapter c = new Adapter1(str, str);

        strArray2[1] = """";
        t.StringArray = strArray2;
        str = t.StringArray[1];
        c = new Adapter1(str, str);

        strArray3[1000] = """";
        t.a.StringArrayA = strArray3;
        Test t2 = t;
        str = t2.a.StringArrayA[1000];
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public StringArrayA As String()
End Class

Class Test
    Public a As A
    Public StringArray As String()

    Sub M1(t As Test, strArray1 As String(), strArray2 As String(), strArray3 As String())
        t.StringArray = strArray1
        strArray1(0) = """"
        Dim str As String = t.StringArray(0)
        Dim c As New Adapter1(str, str)

        strArray2(1) = """"
        t.StringArray = strArray2
        str = t.StringArray(1)
        c = New Adapter1(str, str)

        strArray3(1000) = """"
        t.a.StringArrayA = strArray3
        Dim t2 As Test = t
        str = t2.a.StringArrayA(1000)
        c = New Adapter1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_NonConstantArrayElement_ArrayFieldInReferenceType_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string[] StringArrayA;
}}

class Test
{{
    public A a;
    public string[] StringArray;

    void M1(Test t, string[] strArray1, string[] strArray2, string[] strArray3, string param)
    {{
        t.StringArray = strArray1;
        string str = t.StringArray[1];
        Adapter c = new Adapter1(str, str);

        strArray2[1] = """";
        t.StringArray = strArray2;
        strArray2[1] = param;
        str = t.StringArray[1];
        c = new Adapter1(str, str);

        strArray3[1000] = """";
        t.a.StringArrayA = strArray3;
        Test t2 = t;
        string[] strArray4 = strArray3;
        strArray4[1000] = param;
        str = t2.a.StringArrayA[1000];
        c = new Adapter1(str, str);
    }}
}}
",
            // Test0.cs(107,21): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(107, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            // Test0.cs(113,13): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(113, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            // Test0.cs(121,13): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(121, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public StringArrayA As String()
End Class

Class Test
    Public a As A
    Public StringArray As String()

    Sub M1(t As Test, strArray1 As String(), strArray2 As String(), strArray3 As String(), param As String)
        t.StringArray = strArray1
        Dim str As String = t.StringArray(0)
        Dim c As New Adapter1(str, str)

        strArray2(1) = """"
        t.StringArray = strArray2
        strArray2(1) = param
        str = t.StringArray(1)
        c = New Adapter1(str, str)

        strArray3(1000) = """"
        t.a.StringArrayA = strArray3
        Dim t2 As Test = t
        Dim strArray4 As String() = strArray3
        strArray4(1000) = param
        str = t2.a.StringArrayA(1000)
        c = New Adapter1(str, str)
    End Sub
End Class",
            // Test0.vb(142,18): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(142, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(148,13): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(148, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(156,13): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(156, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ConstantArrayElement_ArrayFieldInValueType_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string[] StringArrayA;
}}

struct Test
{{
    public A a;
    public string[] StringArray;

    void M1(Test t, string[] strArray1, string[] strArray2, string[] strArray3)
    {{
        t.StringArray = strArray1;
        strArray1[0] = """";
        string str = t.StringArray[0];
        Adapter c = new Adapter1(str, str);

        strArray2[1] = """";
        t.StringArray = strArray2;
        str = t.StringArray[1];
        c = new Adapter1(str, str);

        strArray3[1000] = """";
        t.a.StringArrayA = strArray3;
        Test t2 = t;
        str = t2.a.StringArrayA[1000];
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public StringArrayA As String()
End Structure

Structure Test
    Public a As A
    Public StringArray As String()

    Sub M1(t As Test, strArray1 As String(), strArray2 As String(), strArray3 As String())
        t.StringArray = strArray1
        strArray1(0) = """"
        Dim str As String = t.StringArray(0)
        Dim c As New Adapter1(str, str)

        strArray2(1) = """"
        t.StringArray = strArray2
        str = t.StringArray(1)
        c = New Adapter1(str, str)

        strArray3(1000) = """"
        t.a.StringArrayA = strArray3
        Dim t2 As Test = t
        str = t2.a.StringArrayA(1000)
        c = New Adapter1(str, str)
    End Sub
End Structure");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_NonConstantArrayElement_ArrayFieldInValueType_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

struct A
{{
    public string[] StringArrayA;
}}

struct Test
{{
    public A a;
    public string[] StringArray;

    void M1(Test t, string[] strArray1, string[] strArray2, string[] strArray3, string param)
    {{
        t.StringArray = strArray1;
        string str = t.StringArray[1];
        Adapter c = new Adapter1(str, str);

        strArray2[1] = """";
        t.StringArray = strArray2;
        strArray2[1] = param;
        str = t.StringArray[1];
        c = new Adapter1(str, str);

        strArray3[1000] = """";
        t.a.StringArrayA = strArray3;
        Test t2 = t;
        string[] strArray4 = strArray3;
        strArray4[1000] = param;
        str = t2.a.StringArrayA[1000];
        c = new Adapter1(str, str);
    }}
}}
",
            // Test0.cs(107,21): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(107, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            // Test0.cs(113,13): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(113, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            // Test0.cs(121,13): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(121, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Structure A
    Public StringArrayA As String()
End Structure

Structure Test
    Public a As A
    Public StringArray As String()

    Sub M1(t As Test, strArray1 As String(), strArray2 As String(), strArray3 As String(), param As String)
        t.StringArray = strArray1
        Dim str As String = t.StringArray(0)
        Dim c As New Adapter1(str, str)

        strArray2(1) = """"
        t.StringArray = strArray2
        strArray2(1) = param
        str = t.StringArray(1)
        c = New Adapter1(str, str)

        strArray3(1000) = """"
        t.a.StringArrayA = strArray3
        Dim t2 As Test = t
        Dim strArray4 As String() = strArray3
        strArray4(1000) = param
        str = t2.a.StringArrayA(1000)
        c = New Adapter1(str, str)
    End Sub
End Structure",
            // Test0.vb(142,18): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(142, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(148,13): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(148, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(156,13): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(156, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ConstantArrayElement_IndexerFieldInReferenceType_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    private string[] _stringArray;
    public string this[int i]
    {{
        get => _stringArray[i];
        set => _stringArray[i] = value;
    }}

}}

class Test
{{
    public A a;

    private string[] _stringArray;
    public string this[int i]
    {{
        get => _stringArray[i];
        set => _stringArray[i] = value;
    }}

    void M1(Test t, string[] strArray1)
    {{
        strArray1[0] = """";
        t[0] = strArray1[0];
        string str = t[0];
        Adapter c = new Adapter1(str, str);

        A a = new A();
        t.a = a;
        Test t2 = t;
        a[1000] = """";
        str = t2.a[1000];
        c = new Adapter1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Private _stringArray As String()
    Public Property StringArray(i As Integer) As String
        Get
            Return _stringArray(i)
        End Get
        Set(value As String)
            _stringArray(i) = value
        End Set
    End Property
End Class

Class Test
    Public a As A
    
    Private _stringArray As String()
    Public Property StringArray(i As Integer) As String
        Get
            Return _stringArray(i)
        End Get
        Set(value As String)
            _stringArray(i) = value
        End Set
    End Property

    Sub M1(t As Test, strArray1 As String())
        strArray1(0) = """"
        t.StringArray(0) = strArray1(0)
        Dim str As String = t.StringArray(0)
        Dim c As New Adapter1(str, str)

        Dim a As new A()
        t.a = a
        Dim t2 As Test = t
        a.StringArray(1000) = """"
        str = t2.a.StringArray(1000)
        c = New Adapter1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_NonConstantArrayElement_IndexerFieldInReferenceType_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Adapter1 : Adapter
{{
    public Adapter1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    private string[] _stringArray;
    public string this[int i]
    {{
        get => _stringArray[i];
        set => _stringArray[i] = value;
    }}

}}

class Test
{{
    public A a;

    private string[] _stringArray;
    public string this[int i]
    {{
        get => _stringArray[i];
        set => _stringArray[i] = value;
    }}

    void M1(Test t, string[] strArray1, string param)
    {{
        t[0] = """";
        string str = t[1];
        Adapter c = new Adapter1(str, str);

        A a = new A();
        t.a = a;
        Test t2 = t;
        a[1000] = param;
        str = t2.a[1000];
        c = new Adapter1(str, str);
    }}
}}
",
            // Test0.cs(119,21): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(119, 21, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"),
            // Test0.cs(126,13): warning CA2100: Review if the query string passed to 'Adapter1.Adapter1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(126, 13, "Adapter1.Adapter1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Adapter1
    Inherits Adapter

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Private _stringArray As String()
    Public Property StringArray(i As Integer) As String
        Get
            Return _stringArray(i)
        End Get
        Set(value As String)
            _stringArray(i) = value
        End Set
    End Property
End Class

Class Test
    Public a As A
    
    Private _stringArray As String()
    Public Property StringArray(i As Integer) As String
        Get
            Return _stringArray(i)
        End Get
        Set(value As String)
            _stringArray(i) = value
        End Set
    End Property

    Sub M1(t As Test, strArray1 As String(), param As String)
        t.StringArray(0) = """"
        Dim str As String = t.StringArray(1)
        Dim c As New Adapter1(str, str)

        Dim a As new A()
        t.a = a
        Dim t2 As Test = t
        a.StringArray(1000) = param
        str = t2.a.StringArray(1000)
        c = New Adapter1(str, str)
    End Sub
End Class",
            // Test0.vb(159,18): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(159, 18, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(166,13): warning CA2100: Review if the query string passed to 'Sub Adapter1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(166, 13, "Sub Adapter1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeArray_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(string param)
    {{
        A b = new A() {{ Field = """" }};
        A c = new A() {{ Field = param }};
        Test t = new Test() {{ a = c }};
        Test[] testArray = new Test[] {{ new Test() {{ a = b }}, t }};
        string str = testArray[1].a.Field;         // testArray[1].a points to c.
        Command cmd = new Command1(str, str);

        b.Field = param;
        str = testArray[0].a.Field;         // testArray[0].a points to b.
        cmd = new Command1(str, str);
    }}
}}
",
            // Test0.cs(108,23): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(108, 23, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(112,15): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(112, 15, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
End Class

Class Test
    Public a As A

    Private Sub M1(ByVal param As String)
        Dim b As A = New A() With {{.Field = """"}}
        Dim c As A = New A() With {{.Field = param}}
        Dim t As Test = New Test() With {{.a = c}}
        Dim testArray As Test() = New Test() {{New Test() With {{.a = b}}, t}}
        Dim str As String = testArray(1).a.Field         ' testArray[1].a points to c.
        Dim cmd As Command = New Command1(str, str)

        b.Field = param
        str = testArray(0).a.Field         ' testArray[0].a points to b.
        cmd = New Command1(str, str)
    End Sub
End Class
",
            // Test0.vb(144,30): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(144, 30, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(148,15): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(148, 15, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_ReferenceTypeArray_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class A
{{
    public string Field;
}}

class Test
{{
    public A a;
    void M1(string param)
    {{
        A b = new A() {{ Field = """" }};
        A c = new A() {{ Field = param }};
        Test t = new Test() {{ a = c }};
        Test[] testArray = new Test[] {{ new Test() {{ a = b }}, t }};
        string str = testArray[0].a.Field;         // testArray[0].a points to b.
        Command cmd = new Command1(str, str);

        c.Field = b.Field;
        str = testArray[1].a.Field;         // testArray[1].a points to c.
        cmd = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class A
    Public Field As String
End Class

Class Test
    Public a As A

    Private Sub M1(ByVal param As String)
        Dim b As A = New A() With {{.Field = """"}}
        Dim c As A = New A() With {{.Field = param}}
        Dim t As Test = New Test() With {{.a = c}}
        Dim testArray As Test() = New Test() {{New Test() With {{.a = b}}, t}}
        Dim str As String = testArray(0).a.Field         ' testArray[0].a points to b.
        Dim cmd As Command = New Command1(str, str)

        c.Field = b.Field
        str = testArray(1).a.Field         ' testArray[1].a points to c.
        cmd = New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_AutoGeneratedProperty_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    public string AutoGeneratedProperty {{ get; set; }}

    void M1()
    {{
        AutoGeneratedProperty = """";
        string str = AutoGeneratedProperty;
        Command c = new Command1(str, str);
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Public Property AutoGeneratedProperty As String
    Sub M1()
        AutoGeneratedProperty = """"
        Dim str As String = AutoGeneratedProperty
        Dim c As New Command1(str, str)
    End Sub
End Class");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PointsTo_CustomProperty_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    string _value;
    string _value2;
    public string MyProperty {{ get => _value; set => _value = value + _value2; }}

    void M1(string param)
    {{
        _value2 = param;
        MyProperty = """";
        string str = MyProperty;
        Command c = new Command1(str, str);
    }}
}}
",
            // Test0.cs(104,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(104, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Test
    Private _value, _value2 As String
    Public Property MyProperty As String
        Get
            Return _value
        End Get
        Set(value As String)
            _value = value + _value2
        End Set
    End Property

    Sub M1(param As String)
        _value2 = param
        MyProperty = """"
        Dim str As String = MyProperty
        Dim c As New Command1(str, str)
    End Sub
End Class",
            // Test0.vb(146,18): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(146, 18, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ParameterComparedWithConstant_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        if (param == """")
        {{
            Command c = new Command1(param, param);
        }}

        if ("""" == param)
        {{
            Command c2 = new Command1(param, param);
        }}

        if (param != """")
        {{
        }}
        else
        {{
            Command c3 = new Command1(param, param);
        }}

        if ("""" != param)
        {{
        }}
        else
        {{
            Command c4 = new Command1(param, param);
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        If param = """" Then
            Dim c As New Command1(param, param)
        End If

        If """" = param Then
            Dim c2 As New Command1(param, param)
        End If

        If param <> """" Then
        Else
            Dim c3 As New Command1(param, param)
        End If

        If """" <> param Then
        Else
            Dim c4 As New Command1(param, param)
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ParameterComparedWithConstant_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        if (param != """")
        {{
            Command c = new Command1(param, param);
        }}

        if ("""" != param)
        {{
            Command c2 = new Command1(param, param);
        }}
    }}
}}
",
            // Test0.cs(99,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(99, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(104,26): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(104, 26, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        If param <> """" Then
            Dim c As New Command1(param, param)
        End If

        If """" <> param Then
            Dim c2 As New Command1(param, param)
        End If
    End Sub
End Module",
            // Test0.vb(134,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(134, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(138,23): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(138, 23, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ParameterComparedWithConstant_WithNegation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        if (!(param != """"))
        {{
            Command c = new Command1(param, param);
        }}

        if (!("""" != param))
        {{
            Command c2 = new Command1(param, param);
        }}

        if (!!(param != """"))
        {{
        }}
        else
        {{
            Command c3 = new Command1(param, param);
        }}

        if (!("""" == param))
        {{
        }}
        else
        {{
            Command c4 = new Command1(param, param);
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        If Not (param <> """") Then
            Dim c As New Command1(param, param)
        End If

        If Not ("""" <> param) Then
            Dim c2 As New Command1(param, param)
        End If

        If Not Not (param <> """") Then
        Else
            Dim c3 As New Command1(param, param)
        End If

        If Not ("""" = param) Then
        Else
            Dim c4 As New Command1(param, param)
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ParameterComparedWithConstant_WithNegation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        if (!(param == """"))
        {{
            Command c = new Command1(param, param);
        }}

        if (!!("""" != param))
        {{
            Command c2 = new Command1(param, param);
        }}

        if (!("""" != param))
        {{
        }}
        else
        {{
            Command c3 = new Command1(param, param);
        }}
    }}
}}
",
            // Test0.cs(99,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(99, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(104,26): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(104, 26, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(112,26): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(112, 26, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        If Not (param = """") Then
            Dim c As New Command1(param, param)
        End If

        If Not Not ("""" <> param) Then
            Dim c2 As New Command1(param, param)
        End If

        If Not ("""" <> param) Then
        Else
            Dim c3 As New Command1(param, param)
        End If
    End Sub
End Module",
            // Test0.vb(134,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(134, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(138,23): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(138, 23, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(143,23): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(143, 23, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ParameterComparedWithLocal_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = """";
        if (param == str)
        {{
            Command c = new Command1(param, param);
        }}

        if (str == param)
        {{
            Command c2 = new Command1(param, param);
        }}

        if (param != str)
        {{
        }}
        else
        {{
            Command c3 = new Command1(param, param);
        }}

        if (str != param)
        {{
        }}
        else
        {{
            Command c4 = new Command1(param, param);
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim str = """"
        If param = str Then
            Dim c As New Command1(param, param)
        End If

        If str = param Then
            Dim c2 As New Command1(param, param)
        End If

        If param <> str Then
        Else
            Dim c3 As New Command1(param, param)
        End If

        If str <> param Then
        Else
            Dim c4 As New Command1(param, param)
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ParameterComparedWithLocal_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, bool flag)
    {{
        string str = flag ? param2 : """";
        string str2 = param2;
        if (param == str)
        {{
            Command c = new Command1(param, param);
        }}

        if (str2 == param)
        {{
            Command c2 = new Command1(param, param);
        }}

        if (param != str2)
        {{
        }}
        else
        {{
            Command c3 = new Command1(param, param);
        }}

        if (str != param)
        {{
        }}
        else
        {{
            Command c4 = new Command1(param, param);
        }}
    }}
}}
",
            // Test0.cs(101,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(101, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(106,26): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(106, 26, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(114,26): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(114, 26, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(122,26): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(122, 26, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = If(flag, param2, """")
        Dim str2 = param2
        If param = str Then
            Dim c As New Command1(param, param)
        End If

        If str2 = param Then
            Dim c2 As New Command1(param, param)
        End If

        If param <> str2 Then
        Else
            Dim c3 As New Command1(param, param)
        End If

        If str <> param Then
        Else
            Dim c4 As New Command1(param, param)
        End If
    End Sub
End Module",
            // Test0.vb(136,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(136, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(140,23): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(140, 23, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(145,23): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(145, 23, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(150,23): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(150, 23, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_NestedIfElse_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2)
    {{
        string str = """";
        if (param == str)
        {{
            if (param2 != str)
            {{
            }}
            else
            {{
                Command c = new Command1(param, param);
                c = new Command1(param2, param2);
            }}
        }}

        if (str == param)
        {{
            Command c = new Command1(param, param);
        }}
        else if (param2 != str)
        {{
            if ((str + ""a"") != param)
            {{
            }}
            else
            {{
                Command c = new Command1(param, param);
            }}
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String)
        Dim str = """"
        If param = str Then
            If param2 <> str Then
            Else
                Dim c As Command = New Command1(param, param)
                c = New Command1(param2, param2)
            End If
        End If

        If str = param Then
            Dim c As Command = New Command1(param, param)
        ElseIf param2 <> str Then
            If(str & ""a"") <> param Then
            Else
                Dim c As Command = New Command1(param, param)
            End If
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_NestedIfElse_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Command3 : Command
{{
    public Command3(string cmd, string parameter2)
    {{
    }}
}}

class Command4 : Command
{{
    public Command4(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2)
    {{
        string str = """";
        string str2 = param2;
        if (param == str || param != str2)
        {{
            Command c = new Command1(param, param);
            if (param == param2)
            {{
                Command c2 = new Command2(param2, param2);
            }}
        }}

        if (param == str)
        {{
        }}
        else
        {{
            if (str != param2 || param2 == str)
            {{
                Command c = new Command3(param2, param2);
            }}
            else
            {{
            }}

            Command c2 = new Command4(param, param);
        }}
    }}
}}
",
            // Test0.cs(122,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(122, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(125,30): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(125, 30, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(136,29): warning CA2100: Review if the query string passed to 'Command3.Command3(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(136, 29, "Command3.Command3(string cmd, string parameter2)", "M1"),
            // Test0.cs(142,26): warning CA2100: Review if the query string passed to 'Command4.Command4(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(142, 26, "Command4.Command4(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command
    Public Sub New(ByVal cmd As String, ByVal parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command
    Public Sub New(ByVal cmd As String, ByVal parameter2 As String)
    End Sub
End Class

Class Command3
    Inherits Command
    Public Sub New(ByVal cmd As String, ByVal parameter2 As String)
    End Sub
End Class

Class Command4
    Inherits Command
    Public Sub New(ByVal cmd As String, ByVal parameter2 As String)
    End Sub
End Class

Class Test
    Private Sub M1(ByVal param As String, ByVal param2 As String)
        Dim str As String = """"
        Dim str2 As String = param2

        If param = str OrElse param <> str2 Then
            Dim c As Command = New Command1(param, param)
            If param = param2 Then
                Dim c2 As Command = New Command2(param2, param2)
            End If
        End If

        If param = str Then
        Else
            If str <> param2 OrElse param2 = str Then
                Dim c As Command = New Command3(param2, param2)
            Else
            End If
            Dim c2 As Command = New Command4(param, param)
        End If
    End Sub
End Class",
            // Test0.vb(154,32): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(154, 32, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(156,37): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(156, 37, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(163,36): warning CA2100: Review if the query string passed to 'Sub Command3.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(163, 36, "Sub Command3.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(166,33): warning CA2100: Review if the query string passed to 'Sub Command4.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(166, 33, "Sub Command4.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_Loops()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        Command c = null;
        string str = """";
        while (param == str)
        {{
            c = new Command1(param, param); // param == str here
        }}

        c = new Command2(param, param); // param is unknown here
    }}

    void M2(string param)
    {{
        Command c = null;
        string str = """";
        do
        {{
            c = new Command2(param, param); // param is unknown here.
        }}
        while (param != str);

        c = new Command1(param, param); // param == str here
    }}

    void M3(string param, string param2)
    {{
        Command c = null;
        string str = """";
        for (param = str; param2 != str;)
        {{
            c = new Command1(param, param); // param == str here
            c = new Command2(param2, param2); // param2 != str here
        }}

        c = new Command1(param2, param2); // param2 == str here
        c = new Command1(param, param); // param == str here
    }}
}}
",
            // Test0.cs(111,13): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(111, 13, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(120,17): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M2', accepts any user input.
            GetCSharpResultAt(120, 17, "Command2.Command2(string cmd, string parameter2)", "M2"),
            // Test0.cs(134,17): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M3', accepts any user input.
            GetCSharpResultAt(134, 17, "Command2.Command2(string cmd, string parameter2)", "M3"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    ' While loop
    Private Sub M1(ByVal param As String)
        Dim c As Command = Nothing
        Dim str As String = """"
        While param = str
            c = New Command1(param, param) ' param == str here
        End While

        c = New Command2(param, param) ' param is unknown here
    End Sub

    ' Do-While top loop
    Private Sub M2(ByVal param As String)
        Dim c As Command = Nothing
        Dim str As String = """"
        Do While param <> str
            c = New Command2(param, param) ' param is unknown here
        Loop

        c = New Command1(param, param) ' param = str here
    End Sub

    ' Do-Until top loop
    Private Sub M3(ByVal param As String)
        Dim c As Command = Nothing
        Dim str As String = """"
        Do Until param <> str
            c = New Command1(param, param) ' param = str here
        Loop

        c = New Command2(param, param) ' param is unknown here
    End Sub

    ' Do-While bottom loop
    Private Sub M4(ByVal param As String)
        Dim c As Command = Nothing
        Dim str As String = """"
        Do
            c = New Command2(param, param) ' param is unknown here
        Loop While param <> str

        c = New Command1(param, param) ' param = str here
    End Sub

    ' Do-Until bottom loop
    Private Sub M5(ByVal param As String)
        Dim c As Command = Nothing
        Dim str As String = """"
        Do
            c = New Command2(param, param) ' param is unknown here
        Loop Until param = str

        c = New Command1(param, param) ' param = str here
    End Sub
End Module",
            // Test0.vb(147,13): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(147, 13, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(155,17): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M2', accepts any user input.
            GetBasicResultAt(155, 17, "Sub Command2.New(cmd As String, parameter2 As String)", "M2"),
            // Test0.vb(169,13): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M3', accepts any user input.
            GetBasicResultAt(169, 13, "Sub Command2.New(cmd As String, parameter2 As String)", "M3"),
            // Test0.vb(177,17): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M4', accepts any user input.
            GetBasicResultAt(177, 17, "Sub Command2.New(cmd As String, parameter2 As String)", "M4"),
            // Test0.vb(188,17): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M5', accepts any user input.
            GetBasicResultAt(188, 17, "Sub Command2.New(cmd As String, parameter2 As String)", "M5"));
        }

        [Fact]
        public async Task FlowAnalysis_SwitchStatement()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    // Const in all switch cases.
    void M1(string param, string param2, int i)
    {{
        string str = """";
        switch (i)
        {{
            case 0:
                param = str;
                break;
            case 1 :
                param = str;
                break;
            default:
                param2 = str;
                param = param2;
                break;
        }}

        Command c = new Command1(param, param); // param = str here
    }}

    // Const in all switch cases except one which has a return.
    void M2(string param, string param2, int i)
    {{
        string str = """";
        switch (i)
        {{
            case 0:
                param = str;
                break;
            case 1 :
                param = str + ""a"";
                break;
            default:
                param = param2;
                return;
        }}

        Command c = new Command1(param, param); // param is const here
    }}

    // Non-const in one of the intermediate switch case.
    void M3(string param, string param2, int i)
    {{
        string str = """";
        switch (i)
        {{
            case 0:
                param = str;
                break;
            case 1 :
                break;
            default:
                param = str;
                break;
        }}

        Command c = new Command2(param, param); // param is unknown here for i = 1.
    }}

    // No default clause.
    void M4(string param, string param2, int i)
    {{
        string str = """";
        switch (i)
        {{
            case 0:
                param = str;
                break;
            case 1 :
                param = str;
                break;
        }}

        Command c = new Command2(param, param); // param is unknown here for i != 0 or 1.
    }}

    // Switch case with multiple clauses.
    void M5(string param, string param2, int i)
    {{
        string str = """";
        switch (i)
        {{
            case 0:
            case 1:
            default:
                param = str;
                break;
        }}

        Command c = new Command1(param, param); // param is const here.
    }}
}}
",
            // Test0.cs(159,21): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M3', accepts any user input.
            GetCSharpResultAt(159, 21, "Command2.Command2(string cmd, string parameter2)", "M3"),
            // Test0.cs(176,21): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M4', accepts any user input.
            GetCSharpResultAt(176, 21, "Command2.Command2(string cmd, string parameter2)", "M4"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    ' Const in all switch cases.
    Private Sub M1(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Select Case i
            Case 0
                param = str
            Case 1
                param = str
            Case Else
                param2 = str
                param = param2
        End Select

        Dim c As Command = New Command1(param, param) ' param = str here
    End Sub

    ' Const in all switch cases except one which has a return.
    Private Sub M2(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Select Case i
            Case 0
                param = str
            Case 1
                param = str & ""a""
            Case Else
                param = param2
                Return
        End Select

        Dim c As Command = New Command1(param, param) ' param is const here
    End Sub

    ' Non-const in one of the intermediate switch case.
    Private Sub M3(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Select Case i
            Case 0
                param = str
            Case 1
                Exit Select
            Case Else
                param = str
        End Select

        Dim c As Command = New Command2(param, param) ' param is unknown here for i = 1.
    End Sub

    ' No Case Else
    Private Sub M4(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Select Case i
            Case 0
                param = str
            Case 1
                param = str
        End Select

        Dim c As Command = New Command2(param, param) ' param is unknown here for i != 0 or 1.
    End Sub

    ' Switch case with multiple clauses.
    Private Sub M5(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Select Case i
            Case 0, 1
                param = str
            Case Else
                param = str
        End Select

        Dim c As Command = New Command1(param, param) ' param = str here.
    End Sub
End Module",
            // Test0.vb(183,28): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M3', accepts any user input.
            GetBasicResultAt(183, 28, "Sub Command2.New(cmd As String, parameter2 As String)", "M3"),
            // Test0.vb(196,28): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M4', accepts any user input.
            GetBasicResultAt(196, 28, "Sub Command2.New(cmd As String, parameter2 As String)", "M4"));
        }

        [Fact]
        public async Task FlowAnalysis_TryCatch()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, int i)
    {{
        string str = """";
        if (param != str)
        {{
            throw new System.ArgumentException();
        }}

        Command c = new Command1(param, param); // param = str here
    }}

    void M2(string param, string param2, int i)
    {{
        string str = """";
        try
        {{
            param = param2;
        }}
        finally
        {{
            param = str;
        }}

        Command c = new Command1(param, param); // param is str here
    }}

    void M3(string param, string param2, int i)
    {{
        string str = """";
        try
        {{
            param = str;
        }}
        catch (System.Exception ex)
        {{
            param = param2;
        }}

        Command c = new Command2(param, param); // param is unknown here for catch.
    }}

    void M4(string param, string param2, int i)
    {{
        string str = """";
        try
        {{
            param = str;
        }}
        catch (System.IO.IOException ex)
        {{
            param = param2;
        }}
        catch (System.Exception ex)
        {{
            param = str;
        }}

        Command c = new Command2(param, param); // param is unknown here for IOException.
    }}

    void M5(string param, string param2, int i)
    {{
        string str = """";
        try
        {{
        }}
        catch (System.Exception ex)
        {{
            param = param2;
        }}
        finally
        {{
            param = str;
        }}

        Command c = new Command1(param, param); // param is str here from finally.
    }}

    void M6(string param, string param2, int i)
    {{
        string str = """";
        try
        {{
            if (i == 0)
            {{
                throw new System.ArgumentException();
            }}
            param = str;
        }}
        catch (System.ArgumentException ex)
        {{
            param = param2;
        }}
        catch (System.Exception ex)
        {{
            param = str;
        }}

        Command c = new Command2(param, param); // param is unknown here for ArgumentException.
    }}
}}
",
            // Test0.cs(140,21): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M3', accepts any user input.
            GetCSharpResultAt(140, 21, "Command2.Command2(string cmd, string parameter2)", "M3"),
            // Test0.cs(159,21): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M4', accepts any user input.
            GetCSharpResultAt(159, 21, "Command2.Command2(string cmd, string parameter2)", "M4"),
            // Test0.cs(200,21): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M6', accepts any user input.
            GetCSharpResultAt(200, 21, "Command2.Command2(string cmd, string parameter2)", "M6"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Private Sub M1(ByVal param As String, ByVal i As Integer)
        Dim str As String = """"
        If param <> str Then
            Throw New System.ArgumentException()
        End If

        Dim c As Command = New Command1(param, param) ' param = str here
    End Sub

    Private Sub M2(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Try
            param = param2
        Finally
            param = str
        End Try

        Dim c As Command = New Command1(param, param) ' param = str here
    End Sub

    Private Sub M3(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Try
            param = str
        Catch ex As System.Exception
            param = param2
        End Try

        Dim c As Command = New Command2(param, param) ' param is unknown here for catch.
    End Sub

    Private Sub M4(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Try
            param = str
        Catch ex As System.IO.IOException
            param = param2
        Catch ex As System.Exception
            param = str
        End Try

        Dim c As Command = New Command2(param, param) ' param is unknown here for IOException.
    End Sub

    Private Sub M5(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Try
        Catch ex As System.Exception
            param = param2
        Finally
            param = str
        End Try

        Dim c As Command = New Command1(param, param) ' param is str here from finally.
    End Sub

    Private Sub M6(ByVal param As String, ByVal param2 As String, ByVal i As Integer)
        Dim str As String = """"
        Try
            If i = 0 Then
                Throw New System.ArgumentException()
            End If

            param = str
        Catch ex As System.ArgumentException
            param = param2
        Catch ex As System.Exception
            param = str
        End Try

        Dim c As Command = New Command2(param, param) ' param is unknown here for ArgumentException.
    End Sub
End Module",
            // Test0.vb(167,28): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M3', accepts any user input.
            GetBasicResultAt(167, 28, "Sub Command2.New(cmd As String, parameter2 As String)", "M3"),
            // Test0.vb(180,28): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M4', accepts any user input.
            GetBasicResultAt(180, 28, "Sub Command2.New(cmd As String, parameter2 As String)", "M4"),
            // Test0.vb(209,28): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M6', accepts any user input.
            GetBasicResultAt(209, 28, "Sub Command2.New(cmd As String, parameter2 As String)", "M6"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_CatchFilter()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        string str = """";
        try
        {{
        }}
        catch (System.Exception ex) when (param == str)
        {{
            var c = new Command1(param, param); // param == str here from filter.
        }}
    }}

    void M2(string param)
    {{
        string str = """";
        try
        {{
            param = str;
        }}
        catch (System.Exception ex) when (param == str)
        {{
        }}

        var c = new Command1(param, param); // param == str here from try and filter.
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Private Sub M1(ByVal param As String)
        Dim str As String = """"
        Try
        Catch ex As System.Exception When param = str
            Dim c = New Command1(param, param)  ' param = str here from filter.
        End Try
    End Sub

    Private Sub M2(ByVal param As String)
        Dim str As String = """"
        Try
            param = str
        Catch ex As System.Exception When param = str
        End Try

        Dim c = New Command1(param, param) ' param = str here from try and filter.
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_CopyAnalysis_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, string param3)
    {{
        string str = ""a"";
        if (param == str && param == param2)
        {{
            Command c = new Command1(param2, param2);
        }}

        param = param2;
        if (param == str)
        {{
            Command c = new Command1(param2, param2);
        }}

        if (param == str && param2 == str)
        {{
            Command c = new Command1(param, param);
            c = new Command1(param2, param2);
        }}

        string str2 = ""b"";
        if (param2 != str2)
        {{
        }}
        else
        {{
            param2 = param3;
            Command c = new Command1(param, param);
        }}
    }}
}}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, param3 As String)
        Dim str = ""a""
        If param = str AndAlso param = param2 Then
            Dim c As Command = New Command1(param2, param2)
        End If

        param = param2
        If param = str Then
            Dim c As Command = New Command1(param2, param2)
        End If

        If param = str AndAlso param2 = str Then
            Dim c As Command = New Command1(param, param)
            c = New Command1(param2, param2)
        End If

        Dim str2 = ""b""
        If str2 <> param2 Then
        Else
            param2 = param3
            Dim c As Command = New Command1(param, param)
        End If
    End Sub
End Module"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") }
                }
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_CopyAnalysis_Diagnostic()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Command3 : Command
{{
    public Command3(string cmd, string parameter2)
    {{
    }}
}}

class Command4 : Command
{{
    public Command4(string cmd, string parameter2)
    {{
    }}
}}

class Command5 : Command
{{
    public Command5(string cmd, string parameter2)
    {{
    }}
}}

class Command6 : Command
{{
    public Command6(string cmd, string parameter2)
    {{
    }}
}}

class Command7 : Command
{{
    public Command7(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, string param3)
    {{
        string str = param3;
        if (param == str && param == param2)
        {{
            Command c = new Command1(param2, param2);
        }}

        str = ""a"";
        param = param2;
        // Always false, we set the copy values of the involved variables to invalid to prevent unnecessary diagnostics.
        if (param2 == str && param != str)
        {{
            Command c = new Command2(param2, param2);
        }}

        // Always true, but does not tell anything about value of param2 in either if or else.
        // Else branch can never be entered, hence we set the copy values of the involved variables to invalid.
        if (param2 == str || param != str)
        {{
            Command c = new Command3(param2, param2);
        }}
        else
        {{
            Command c = new Command4(param2, param2);
        }}

        if (param == str && param2 == str)
        {{
        }}
        else
        {{
            Command c = new Command5(param, param);
            c = new Command6(param2, param2);
        }}

        string str2 = ""b"";
        if (param2 != str2)
        {{
            param2 = ""a"";
            Command c = new Command7(param, param);
        }}
    }}
}}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") },
                    ExpectedDiagnostics =
                    {
                        // Test0.cs(142,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
                        GetCSharpResultAt(142, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
                        // Test0.cs(157,25): warning CA2100: Review if the query string passed to 'Command3.Command3(string cmd, string parameter2)' in 'M1', accepts any user input.
                        GetCSharpResultAt(157, 25, "Command3.Command3(string cmd, string parameter2)", "M1"),
                        // Test0.cs(169,25): warning CA2100: Review if the query string passed to 'Command5.Command5(string cmd, string parameter2)' in 'M1', accepts any user input.
                        GetCSharpResultAt(169, 25, "Command5.Command5(string cmd, string parameter2)", "M1"),
                        // Test0.cs(170,17): warning CA2100: Review if the query string passed to 'Command6.Command6(string cmd, string parameter2)' in 'M1', accepts any user input.
                        GetCSharpResultAt(170, 17, "Command6.Command6(string cmd, string parameter2)", "M1"),
                        // Test0.cs(177,25): warning CA2100: Review if the query string passed to 'Command7.Command7(string cmd, string parameter2)' in 'M1', accepts any user input.
                        GetCSharpResultAt(177, 25, "Command7.Command7(string cmd, string parameter2)", "M1"),
                    }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command3
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command4
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command5
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command6
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command7
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Private Sub M1(ByVal param As String, ByVal param2 As String, ByVal param3 As String)
        Dim str As String = param3
        If param = str AndAlso param = param2 Then
            Dim c As Command = New Command1(param2, param2)
        End If

        str = ""a""
        param = param2
        ' Always false, we set the copy values of the involved variables to invalid to prevent unnecessary diagnostics.
        If param2 = str AndAlso param <> str Then
            Dim c As Command = New Command2(param2, param2)
        End If

        ' Always true, but does not tell anything about value of param2 in either if or else.
        ' Else branch can never be entered, hence we set the copy values of the involved variables to invalid.
        If param2 = str OrElse param <> str Then
            Dim c As Command = New Command3(param2, param2)
        Else
            Dim c As Command = New Command4(param2, param2)
        End If

        If param = str AndAlso param2 = str Then
        Else
            Dim c As Command = New Command5(param, param)
            c = New Command6(param2, param2)
        End If

        Dim str2 As String = ""b""
        If param2 <> str2 Then
            param2 = ""a""
            Dim c As Command = New Command7(param, param)
        End If
    End Sub
End Module"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") },
                    ExpectedDiagnostics =
                    {
                        // Test0.vb(177,32): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
                        GetBasicResultAt(177, 32, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
                        // Test0.vb(190,32): warning CA2100: Review if the query string passed to 'Sub Command3.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
                        GetBasicResultAt(190, 32, "Sub Command3.New(cmd As String, parameter2 As String)", "M1"),
                        // Test0.vb(197,32): warning CA2100: Review if the query string passed to 'Sub Command5.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
                        GetBasicResultAt(197, 32, "Sub Command5.New(cmd As String, parameter2 As String)", "M1"),
                        // Test0.vb(198,17): warning CA2100: Review if the query string passed to 'Sub Command6.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
                        GetBasicResultAt(198, 17, "Sub Command6.New(cmd As String, parameter2 As String)", "M1"),
                        // Test0.vb(204,32): warning CA2100: Review if the query string passed to 'Sub Command7.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
                        GetBasicResultAt(204, 32, "Sub Command7.New(cmd As String, parameter2 As String)", "M1"),
                    }
                }
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalOr_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, bool flag)
    {{
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        // 1. const in left, const in right.
        if (param == str || param == str2)
        {{
            Command c = new Command1(param, param);
        }}

        // 2. Creation in else: negation of non-const in left, maybe-const in right.
        if (str2 != param || param == strMayBeConst)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        // 3. Creation in else: maybe-const in left, negation of non-const in right.
        if (param == strMayBeConst || str2 != param)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        ' 1. const in left, const in right.
        If param = str OrElse param = str2 Then
            Dim c As New Command1(param, param)
        End If

        ' 2. Creation in else: negation of non-const in left, maybe-const in right.
        If str2 <> param OrElse param = strMayBeConst Then
        Else
            Dim c As New Command1(param, param)
        End If

        ' 3. Creation in else: maybe-const in left, negation of non-const in right.
        If param = strMayBeConst OrElse str2 <> param Then
        Else
            Dim c As New Command1(param, param)
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalOr_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, bool flag)
    {{
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        // 1. const in left, const in right.
        if (param == str || param == str2)
        {{
            Command c = new Command1(param, param); // No diagnostic
        }}

        // 2. Creation in if and else: negation of non-const in left, maybe-const in right.
        if (str2 != param || param == strMayBeConst)
        {{
            Command c = new Command2(param, param); // Diagnostic
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        // 3. Creation in if and else: maybe-const in left, negation of non-const in right.
        if (param == strMayBeConst || str2 != param)
        {{
            Command c = new Command2(param, param); // Diagnostic
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}
    }}
}}
",
            // Test0.cs(117,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(117, 25, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(127,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(127, 25, "Command2.Command2(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        ' 1. const in left, const in right.
        If param = str OrElse param = str2 Then
            Dim c As New Command1(param, param) ' No diagnostic
        End If

        ' 2. Creation in if and else: negation of non-const in left, maybe-const in right.
        If str2 <> param OrElse param = strMayBeConst Then
            Dim c As New Command2(param, param)
        Else
            Dim c As New Command1(param, param)
        End If

        ' 3. Creation in if and else: maybe-const in left, negation of non-const in right.
        If param = strMayBeConst OrElse str2 <> param Then
            Dim c As New Command2(param, param)
        Else
            Dim c As New Command1(param, param)
        End If
    End Sub
End Module",
            // Test0.vb(151,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(151, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(158,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(158, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalOr_02_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, bool flag)
    {{
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        // 1. const in left, const in right.
        if (param == str || param == str2)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        // 2. const in left, maybe-const in right.
        if (param == str || str2 != param)
        {{
            Command c = new Command1(param, param);
        }}

        // 3. const in left, maybe-const in right, creation in else.
        if (param == str || strMayBeConst != param)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        // 4. maybe-const in left, const in right.
        if (param == strMayBeConst || str2 != param)
        {{
            Command c = new Command1(param, param);
        }}

        // 5. maybe-const in left and right.
        if (param == strMayBeConst || param == strMayBeConst + ""c"")
        {{
            Command c = new Command1(param, param);
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}
    }}
}}
",
            // Test0.cs(107,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(107, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(113,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(113, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(122,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(122, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(128,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(128, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(134,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(134, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(138,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(138, 25, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        ' 1. const in left, const in right.
        If param = str OrElse param = str2 Then
        Else
            Dim c As New Command1(param, param)
        End If

        ' 2. const in left, maybe-const in right.
        If str = param OrElse strMayBeConst <> param Then
            Dim c As New Command1(param, param)
        End If

        ' 3. maybe-const in left, const in right.
        If param = strMayBeConst OrElse param <> str2 Then
            Dim c As New Command1(param, param)
        End If

        ' 4. maybe-const in left, const in right.
        If param = strMayBeConst OrElse str2 <> param Then
            Dim c As New Command1(param, param)
        End If

        ' 5. maybe-const in left and right.
        If param = strMayBeConst OrElse param = strMayBeConst + ""c"" Then
            Dim c As New Command1(param, param)
        Else
            Dim c As New Command1(param, param)
        End If
    End Sub
End Module",
            // Test0.vb(140,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(140, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(145,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(145, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(150,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(150, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(155,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(155, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(160,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(160, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(162,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(162, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalAnd_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, bool flag)
    {{
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        // 1. const in left, const in right.
        if (param == str && param2 == str2)
        {{
            Command c = new Command1(param, param);
        }}

        // 2. maybe-const in left, const in right.
        if (param == strMayBeConst && str2 == param)
        {{
            Command c = new Command1(param, param);
        }}

        // 3. Creation in else: non-const in left, non-const in right.
        if (param != str && param != str2)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        ' 1. const in left, const in right.
        If param = str AndAlso param2 = str2 Then
            Dim c As New Command1(param, param)
        End If

        ' 2. maybe-const in left, const in right.
        If param = strMayBeConst AndAlso str2 = param Then
            Dim c As New Command1(param, param)
        End If

        ' 3. Creation in else: non-const in left, non-const in right.
        If str2 <> param AndAlso param <> str Then
        Else
            Dim c As New Command1(param, param)
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalAnd_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, bool flag)
    {{
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        // 1. const in left, const in right.
        if (param == str && param2 == str2)
        {{
            Command c = new Command1(param, param); // No diagnostic
        }}

        // 2. Creation in if and else: negation of non-const in left, maybe-const in right.
        if (str2 != param && param == strMayBeConst)
        {{
            Command c = new Command2(param, param); // Diagnostic
        }}
        else
        {{
            Command c = new Command2(param, param); // Diagnostic where left of '&&' is true and right of '&&' is false.
        }}

        // 3. Creation in if and else: maybe-const in left, negation of non-const in right.
        if (param == strMayBeConst && str2 != param)
        {{
            Command c = new Command2(param, param); // Diagnostic (if both are true, param maybe non-const)
        }}
        else
        {{
            Command c = new Command2(param, param); // Diagnostic (if left is false, param maybe non-const)
        }}

        // 4. Creation in else: non-const in left, non-const different variable in right.
        if (param != str && param2 != str2)
        {{
        }}
        else
        {{
            Command c = new Command2(param, param); // Diagnostic (if left is true and right is false, param maybe non-const)
        }}

        // 5. Creation in else: negation of non-const in left, maybe-const in right.
        if (str2 != param && param == strMayBeConst)
        {{
        }}
        else
        {{
            Command c = new Command2(param, param); // Diagnostic (if left is true and right is false, param maybe non-const)
        }}
    }}
}}
",
            // Test0.cs(117,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(117, 25, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(121,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(121, 25, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(127,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(127, 25, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(131,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(131, 25, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(140, 25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(140, 25, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(149,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(149, 25, "Command2.Command2(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        ' 1. const in left, const in right.
        If param = str AndAlso param2 = str2 Then
            Dim c As New Command1(param, param) ' No diagnostic
        End If

        ' 2. Creation in if and else: negation of non-const in left, maybe-const in right.
        If str2 <> param AndAlso param = strMayBeConst Then
            Dim c As New Command2(param, param) ' Diagnostic
        Else
            Dim c As New Command2(param, param) ' Diagnostic where left of 'AndAlso' is true and right of 'AndAlso' is false.
        End If

        ' 3. Creation in if and else: maybe-const in left, negation of non-const in right.
        If param = strMayBeConst AndAlso str2 <> param Then
            Dim c As New Command2(param, param) ' Diagnostic (if both are true, param maybe non-const)
        Else
            Dim c As New Command2(param, param) ' Diagnostic (if left is false, param maybe non-const)
        End If

        ' 4. Creation in else: non-const in left, non-const differen variable in right.
        If str2 <> param AndAlso param2 <> str Then
        Else
            Dim c As New Command2(param, param) ' Diagnostic (if left is true and right is false, param maybe non-const)
        End If

        ' 5. Creation in else: negation of non-const in left, maybe-const in right.
        If str2 <> param AndAlso param = strMayBeConst Then
        Else
            Dim c As New Command2(param, param) ' Diagnostic (if left is true and right is false, param maybe non-const)
        End If
    End Sub
End Module",
            // Test0.vb(151,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(151, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(153,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(153, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(158,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(158, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(160,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(160, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(166,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(166, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(172,22): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(172, 22, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalAnd_02_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, string param2, bool flag)
    {{
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        string strMayBeConst = param2;

        // 1. const in left, const in right.
        if (param == str && param == str2)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        // 2. const in left, maybe-const in right, creation in else.
        if (param == str && str2 != param)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        // 3. const in left, maybe-const in right, creation in else.
        if (param == str && strMayBeConst != param)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        // 4. maybe-const in left, non-const in right.
        if (param == strMayBeConst && str2 != param)
        {{
            Command c = new Command1(param, param);
        }}

        // 5. maybe-const in left and right.
        if (param == strMayBeConst && param == strMayBeConst + ""c"")
        {{
            Command c = new Command1(param, param);
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}
    }}
}}
",
            // Test0.cs(107,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(107, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(116,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(116, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(125,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(125, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(131,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(131, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(137,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(137, 25, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(141,25): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(141, 25, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        Dim strMayBeConst = param2

        ' 1. const in left, const in right.
        If param = str AndAlso param = str2 Then
        Else
            Dim c As New Command1(param, param)
        End If

        ' 2. const in left, maybe-const in right.
        If str = param AndAlso str2 <> param Then
        Else
            Dim c As New Command1(param, param)
        End If

        ' 3. maybe-const in left, const in right, creation in else.
        If param = str AndAlso strMayBeConst <> param Then
        Else
            Dim c As New Command1(param, param)
        End If

        ' 4. maybe-const in left, non-const in right.
        If param = strMayBeConst AndAlso str2 <> param Then
            Dim c As New Command1(param, param)
        End If

        ' 5. maybe-const in left and right.
        If param = strMayBeConst AndAlso param = strMayBeConst + ""c"" Then
            Dim c As New Command1(param, param)
        Else
            Dim c As New Command1(param, param)
        End If
    End Sub
End Module",
            // Test0.vb(140,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(140, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(146,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(146, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(152,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(152, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(157,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(157, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(162,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(162, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(164,22): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(164, 22, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_Conditional_WithNegation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, bool flag)
    {{
        string str = """";
        string str2 = flag ? ""a"" : ""b"";
        if (param == str || !(param != str2))
        {{
            Command c = new Command1(param, param);
        }}

        if (!(param != str) || str2 == param)
        {{
            Command c = new Command1(param, param);
        }}

        if (!(param == str || param == str2))
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        if (!(!(str != param) && !!(param != str2)))
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, flag As Boolean)
        Dim str = """"
        Dim str2 = If(flag, ""a"", ""b"")
        If param = str OrElse Not (param <> str2) Then
            Dim c As New Command1(param, param)
        End If

        If Not (str <> param) OrElse str2 = param Then
            Dim c2 As New Command1(param, param)
        End If

        If Not (param = str OrElse param = str2) Then
        Else
            Dim c3 As New Command1(param, param)
        End If

        If Not (Not (str <> param) AndAlso Not Not (param <> str2)) Then
        Else
            Dim c4 As New Command1(param, param)
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalAndOrNegation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, bool flag, string param2)
    {{
        string strConst = """";
        string strConst2 = flag ? ""a"" : """";
        string strMayBeNonConst = flag ? ""c"" : param2;

        if (param == strConst || !(strConst2 != param) && param != strMayBeNonConst)
        {{
            Command c = new Command1(param, param);
        }}

        if (!(strConst2 == param && !(param != strConst)) || param == strMayBeNonConst)
        {{
        }}
        else
        {{
            Command c = new Command1(param, param);
        }}

        if (param != strConst && !(strConst2 != param || param != strMayBeNonConst))
        {{
            Command c = new Command1(param, param);
        }}
    }}
}}
");

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim strConst As String = """"
        Dim strConst2 As String = If(flag, ""a"", """")
        Dim strMayBeNonConst As String = If(flag, ""c"", param2)

        If param = strConst OrElse Not(strConst2 <> param) AndAlso param <> strMayBeNonConst Then
            Dim c As Command = New Command1(param, param)
        End If

        If Not(strConst2 = param AndAlso Not (param <> strConst)) OrElse param <> strMayBeNonConst Then
        Else
            Dim c As Command = New Command1(param, param)
        End If

        If param <> strConst AndAlso Not(strConst2 <> param OrElse param <> strMayBeNonConst) Then
            Dim c As Command = New Command1(param, param)
        End If
    End Sub
End Module");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ConditionalAndOrNegation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Command3 : Command
{{
    public Command3(string cmd, string parameter2)
    {{
    }}
}}

class Command4 : Command
{{
    public Command4(string cmd, string parameter2)
    {{
    }}
}}

class Command5 : Command
{{
    public Command5(string cmd, string parameter2)
    {{
    }}
}}

class Command6 : Command
{{
    public Command6(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param, bool flag, string param2)
    {{
        string strConst = """";
        string strConst2 = flag ? ""a"" : """";
        string strMayBeNonConst = flag ? ""c"" : param2;

        if (param != strConst && !(strConst2 != param || param != strConst))  // First and last conditions are opposites, so infeasible.
        {{
            Command c = new Command1(param, param);
        }}
        else
        {{
            Command c = new Command2(param, param);
        }}

        if (param == strConst && !(strConst2 == param || param == strMayBeNonConst))
        {{
            Command c = new Command3(param, param);   // No diagnostic here as first condition ensures param == strConst.
        }}
        else
        {{
            Command c = new Command4(param, param);
        }}

        if (param != strConst && !(strConst2 != param || param != strMayBeNonConst))
        {{
            Command c = new Command5(param, param);   // No diagnostic expected here
        }}
        else
        {{
            Command c = new Command6(param, param);
        }}
    }}
}}
",
            // Test0.cs(142,25): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(142, 25, "Command2.Command2(string cmd, string parameter2)", "M1"),
            // Test0.cs(151,25): warning CA2100: Review if the query string passed to 'Command4.Command4(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(151, 25, "Command4.Command4(string cmd, string parameter2)", "M1"),
            // Test0.cs(160,25): warning CA2100: Review if the query string passed to 'Command6.Command6(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(160, 25, "Command6.Command6(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command3
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command4
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command5
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command6
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String, param2 As String, flag As Boolean)
        Dim strConst As String = """"
        Dim strConst2 As String = If(flag, ""a"", """")
        Dim strMayBeNonConst As String = If(flag, ""c"", param2)

        If param <> strConst AndAlso Not(strConst2 <> param OrElse param <> strConst) Then ' First and last conditions are opposites, so infeasible.
            Dim c As Command = New Command1(param, param)
        Else
            Dim c As Command = New Command2(param, param)
        End If

        If param = strConst AndAlso Not(strConst2 = param OrElse param = strMayBeNonConst) Then
            Dim c As Command = New Command3(param, param)   ' No diagnostic here as first condition ensures param = strConst.
        Else
            Dim c As Command = New Command4(param, param)
        End If

        If param <> strConst AndAlso Not(strConst2 <> param OrElse param <> strMayBeNonConst) Then
            Dim c As Command = New Command5(param, param)   ' No diagnostic expected here
        Else
            Dim c As Command = New Command6(param, param)
        End If
    End Sub
End Module",
            // Test0.vb(175,32): warning CA2100: Review if the query string passed to 'Sub Command2.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(175, 32, "Sub Command2.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(181,32): warning CA2100: Review if the query string passed to 'Sub Command4.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(181, 32, "Sub Command4.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(187,32): warning CA2100: Review if the query string passed to 'Sub Command6.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(187, 32, "Sub Command6.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ComparisonInNonCondition_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        var x = param == """";  // This predicate should not affect subsequent code.
        Command c = new Command1(param, param);
    }}
}}
",
            // Test0.cs(98,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(98, 21, "Command1.Command1(string cmd, string parameter2)", "M1"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Sub M1(param As String)
        Dim x = param = """"    ' This predicate should not affect subsequent code.
        Dim c As New Command1(param, param)
    End Sub
End Module",
            // Test0.vb(134,18): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(134, 18, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ComparisonInNonCondition_02_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        Command c = null;
        var x = param == """" && ((c = new Command1(param, param)) != null);    // Left operand of && ensures no diagnostic for Command1 instantiation.
        c = new Command2(param, param);     // This code should fire as 'param == """"' does not apply here.
    }}
}}
",
            // Test0.cs(106,13): warning CA2100: Review if the query string passed to 'Command2.Command2(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(106, 13, "Command2.Command2(string cmd, string parameter2)", "M1"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ContractCheck_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        System.Diagnostics.Contracts.Contract.Requires(param == """");
        Command c = new Command1(param, param);
    }}

    void M2(string param, string param2)
    {{
        System.Diagnostics.Contracts.Contract.Requires(param == """" && param2 == param);
        Command c = new Command1(param, param);
        c = new Command1(param2, param2);
    }}

    void M3(string param, string param2)
    {{
        System.Diagnostics.Contracts.Contract.Requires(param == param2 && !(param2 != """"));
        Command c = new Command1(param, param);
        c = new Command1(param2, param2);
    }}

    void M4_Assume(string param)
    {{
        System.Diagnostics.Contracts.Contract.Assume(param == """");
        Command c = new Command1(param, param);
    }}

    void M5_Assert(string param)
    {{
        System.Diagnostics.Contracts.Contract.Assert(param == """");
        Command c = new Command1(param, param);
    }}
}}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
{SetupCodeBasic}

Class Command1
    Inherits Command

    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Private Sub M1(ByVal param As String)
        System.Diagnostics.Contracts.Contract.Requires(param = """")
        Dim c As Command = New Command1(param, param)
    End Sub

    Private Sub M2(ByVal param As String, ByVal param2 As String)
        System.Diagnostics.Contracts.Contract.Requires(param = """" AndAlso param2 = param)
        Dim c As Command = New Command1(param, param)
        c = New Command1(param2, param2)
    End Sub

    Private Sub M3(ByVal param As String, ByVal param2 As String)
        System.Diagnostics.Contracts.Contract.Requires(param = param2 AndAlso Not(param2 <> """"))
        Dim c As Command = New Command1(param, param)
        c = New Command1(param2, param2)
    End Sub

    Private Sub M4_Assume(ByVal param As String)
        System.Diagnostics.Contracts.Contract.Assume(param = """")
        Dim c As Command = New Command1(param, param)
    End Sub

    Private Sub M5_Assert(ByVal param As String)
        System.Diagnostics.Contracts.Contract.Assert(param = """")
        Dim c As Command = New Command1(param, param)
    End Sub
End Module"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") }
                }
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task FlowAnalysis_PredicateAnalysis_ContractCheck_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
{SetupCodeCSharp}

class Command1 : Command
{{
    public Command1(string cmd, string parameter2)
    {{
    }}
}}

class Command2 : Command
{{
    public Command2(string cmd, string parameter2)
    {{
    }}
}}

class Test
{{
    void M1(string param)
    {{
        System.Diagnostics.Contracts.Contract.Requires(param != """");
        Command c = new Command1(param, param);
    }}

    void M2(string param, string param2)
    {{
        param2 = """";
        System.Diagnostics.Contracts.Contract.Requires(param == """" || param2 != param);
        Command c = new Command1(param, param);
        c = new Command2(param2, param2);   // No diagnostic.
    }}

    void M3(string param, string param2, string param3)
    {{
        System.Diagnostics.Contracts.Contract.Requires(param == param2 && !(param2 != """") || param2 == param3);
        Command c = new Command1(param, param);
        c = new Command1(param2, param2);
    }}
}}
",
            // Test0.cs(105,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M1', accepts any user input.
            GetCSharpResultAt(105, 21, "Command1.Command1(string cmd, string parameter2)", "M1"),
            // Test0.cs(112,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M2', accepts any user input.
            GetCSharpResultAt(112, 21, "Command1.Command1(string cmd, string parameter2)", "M2"),
            // Test0.cs(119,21): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M3', accepts any user input.
            GetCSharpResultAt(119, 21, "Command1.Command1(string cmd, string parameter2)", "M3"),
            // Test0.cs(120,13): warning CA2100: Review if the query string passed to 'Command1.Command1(string cmd, string parameter2)' in 'M3', accepts any user input.
            GetCSharpResultAt(120, 13, "Command1.Command1(string cmd, string parameter2)", "M3"));

            await VerifyVB.VerifyAnalyzerAsync($@"
{SetupCodeBasic}

Class Command1
    Inherits Command
    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Class Command2
    Inherits Command
    Sub New(cmd As String, parameter2 As String)
    End Sub
End Class

Module Test
    Private Sub M1(ByVal param As String)
        System.Diagnostics.Contracts.Contract.Requires(param <> """")
        Dim c As Command = New Command1(param, param)
    End Sub

    Private Sub M2(ByVal param As String, ByVal param2 As String)
        param2 = """"
        System.Diagnostics.Contracts.Contract.Requires(param = """" OrElse param2 <> param)
        Dim c As Command = New Command1(param, param)
        c = New Command2(param2, param2)    ' No diagnostic
    End Sub

    Private Sub M3(ByVal param As String, ByVal param2 As String, param3 As String)
        System.Diagnostics.Contracts.Contract.Requires(param = param2 AndAlso Not(param2 <> """") OrElse param2 = param3)
        Dim c As Command = New Command1(param, param)
        c = New Command1(param2, param2)
    End Sub
End Module",
            // Test0.vb(139,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M1', accepts any user input.
            GetBasicResultAt(139, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M1"),
            // Test0.vb(145,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M2', accepts any user input.
            GetBasicResultAt(145, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M2"),
            // Test0.vb(151,28): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M3', accepts any user input.
            GetBasicResultAt(151, 28, "Sub Command1.New(cmd As String, parameter2 As String)", "M3"),
            // Test0.vb(152,13): warning CA2100: Review if the query string passed to 'Sub Command1.New(cmd As String, parameter2 As String)' in 'M3', accepts any user input.
            GetBasicResultAt(152, 13, "Sub Command1.New(cmd As String, parameter2 As String)", "M3"));
        }
    }
}