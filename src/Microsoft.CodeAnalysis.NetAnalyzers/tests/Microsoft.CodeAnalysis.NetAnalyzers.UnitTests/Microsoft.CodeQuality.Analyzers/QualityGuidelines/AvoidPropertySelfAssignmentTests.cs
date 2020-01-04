// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public partial class AvoidPropertySelfAssignmentTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new AvoidPropertySelfAssignment();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AvoidPropertySelfAssignment();
        }

        [Fact]
        public void CSharpAssignmentInConstructorWithNoArguments()
        {
            VerifyCSharp(@"
class C
{
    private string P { get; set; }
    public C()
    {
        P = P;
    }
}
",
            GetCSharpResultAt(7, 13, "P"));
        }

        [Fact]
        public void CSharpAssignmentInConstructorUsingThisWithNoArguments()
        {
            VerifyCSharp(@"
class C
{
    private string P { get; set; }
    public C()
    {
        this.P = P;
    }
}
",
            GetCSharpResultAt(7, 18, "P"));
        }


        [Fact]
        public void CSharpAssignmentInConstructorWithSimilarArgument()
        {
            VerifyCSharp(@"
class C
{
    private string P { get; set; }
    public C(string p)
    {
        P = P;
    }
}
",
            GetCSharpResultAt(7, 13, "P"));
        }

        [Fact]
        public void CSharpAssignmentInMethodWithoutArguments()
        {
            VerifyCSharp(@"
class C
{
    private string P { get; set; }
    public void CSharpMethod()
    {
        P = P;
    }
}
",
            GetCSharpResultAt(7, 13, "P"));
        }

        [Fact]
        public void CSharpAssignmentInMethodWithSimilarArgumentName()
        {
            VerifyCSharp(@"
class C
{
    private string P { get; set; }
    public void CSharpMethod(string p)
    {
        P = P;
    }
}
",
            GetCSharpResultAt(7, 13, "P"));
        }

        [Fact]
        public void CSharpAdditionAssignmentOperatorDoesNotCauseDiagnosticToAppear()
        {
            VerifyCSharp(@"
class C
{
    private int Property { get; set; }
    public void CSharpMethod(string p)
    {
        Property += 1;
    }
}
");
        }

        [Fact]
        public void CSharpNormalPropertyAssignmentDoesNotCauseDiagnosticToAppear()
        {
            VerifyCSharp(@"
class C
{
    private string P { get; set; }
    public void CSharpMethod(string p)
    {
        P = p;
    }
}
");
        }

        [Fact]
        public void CSharpNormalAssignmentOfTwoDifferentPropertiesDoesNotCauseDiagnosticToAppear()
        {
            VerifyCSharp(@"
class C
{
    private string FirstP { get; set; }
    private string SecondP { get; set; }
    public C()
    {
        FirstP = SecondP;
    }
}
");
        }

        [Fact]
        public void CSharpNormalVariableAssignmentDoesNotCauseDiagnosticToAppear()
        {
            VerifyCSharp(@"
class C
{
    private string P { get; set; }
    public void CSharpMethod(string p)
    {
        var methodVariable = p;
    }
}
");
        }

        [Fact]
        public void CSharpNormalAssignmentWithTwoDifferentInstancesDoesNotCauseDiagnosticToAppear()
        {
            VerifyCSharp(@"
internal class A
{
    public string P { get; set; } = ""value"";
}

class C
{
    public C()
    {
        var a = new A();
        var b = new A();
        a.P = b.P;
    }
}
");
        }

        [Fact]
        public void CSharpIndexerAssignmentDoesNotCauseDiagnosticToAppear()
        {
            VerifyCSharp(@"
internal class A
{
    private int[] _a;
    public int this[int i] { get => _a[i]; set => _a[i] = value; }

    public void ExchangeValue(int i, int j)
    {
        var temp = this[i];
        this[i] = this[j];
        this[j] = temp;
    }
}
");
        }

        [Fact]
        public void CSharpIndexerAssignmentWithSameConstantIndexCausesDiagnosticToAppear()
        {
            VerifyCSharp(@"
internal class A
{
    private int[] _a;
    public int this[int i] { get => _a[i]; set => _a[i] = value; }

    public void M()
    {
        this[0] = this[0];
    }
}
",
            GetCSharpResultAt(9, 19, "this[]"));
        }

        [Fact]
        public void CSharpIndexerAssignmentWithSameLocalReferenceIndexCausesDiagnosticToAppear()
        {
            VerifyCSharp(@"
internal class A
{
    private int[] _a;
    public int this[int i] { get => _a[i]; set => _a[i] = value; }

    public void M()
    {
        int local = 0;
        this[local] = this[local];
    }
}
",
            GetCSharpResultAt(10, 23, "this[]"));
        }

        [Fact]
        public void CSharpIndexerAssignmentWithSameParameterReferenceIndexCausesDiagnosticToAppear()
        {
            VerifyCSharp(@"
internal class A
{
    private int[] _a;
    public int this[int i] { get => _a[i]; set => _a[i] = value; }

    public void M(int param)
    {
        this[param] = this[param];
    }
}
",
            GetCSharpResultAt(9, 23, "this[]"));
        }

        [Fact]
        public void VbAssignmentInConstructorWithNoArguments()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As String

    Public Sub New()
        [P] = [P]
    End Sub
End Class
",
            GetBasicResultAt(6, 15, "P"));
        }

        [Fact]
        public void VbAssignmentInConstructorUsingThisWithNoArguments()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As String

    Public Sub New()
        Me.[P] = [P]
    End Sub
End Class
",
            GetBasicResultAt(6, 18, "P"));
        }


        [Fact]
        public void VbAssignmentInConstructorWithSimilarArgument()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As String

    Public Sub New(ByVal [ctorArg] As String)
        [P] = [P]
    End Sub
End Class
",
            GetBasicResultAt(6, 15, "P"));
        }

        [Fact]
        public void VbAssignmentInMethodWithoutArguments()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As String

    Public Sub VbMethod()
        [P] = [P]
    End Sub
End Class
",
            GetBasicResultAt(6, 15, "P"));
        }

        [Fact]
        public void VbAssignmentInMethodWithSimilarArgumentName()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As String

    Public Sub VbMethod(ByVal [methodArg] As String)
        [P] = [P]
    End Sub
End Class
",
            GetBasicResultAt(6, 15, "P"));
        }

        [Fact]
        public void VbAdditionAssignmentOperatorDoesNotCauseDiagnosticToAppear()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As Integer

    Public Sub VbMethod(ByVal [methodArg] As String)
        [P] += 1
    End Sub
End Class
");
        }

        [Fact]
        public void VbNormalPropertyAssignmentDoesNotCauseDiagnosticToAppear()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As String

    Public Sub VbMethod(ByVal [methodArg] As String)
        [P] = [methodArg]
    End Sub
End Class
");
        }

        [Fact]
        public void VbNormalAssignmentOfTwoDifferentPropertiesDoesNotCauseDiagnosticToAppear()
        {
            VerifyBasic(@"
Class C
    Private Property FirstP As String
    Private Property SecondP As String

    Public Sub New()
        FirstP = SecondP
    End Sub
End Class
");
        }

        [Fact]
        public void VbNormalVariableAssignmentDoesNotCauseDiagnosticToAppear()
        {
            VerifyBasic(@"
Class C
    Private Property [P] As String

    Public Sub VbMethod(ByVal [methodArg] As String)
        Dim methodVariable = [methodArg]
    End Sub
End Class
");
        }

        [Fact]
        public void VbNormalAssignmentWithTwoDifferentInstancesDoesNotCauseDiagnosticToAppear()
        {
            VerifyBasic(@"
Friend Class A
    Public Property [P] As String = ""value""
End Class

Class C
    Public Sub New()
        Dim a = New A()
        Dim b = New A()
        a.[P] = b.[P]
    End Sub
End Class
");
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, string symbolName)
        {
            return GetCSharpResultAt(line, column, AvoidPropertySelfAssignment.Rule, symbolName);
        }

        private DiagnosticResult GetBasicResultAt(int line, int column, string symbolName)
        {
            return GetBasicResultAt(line, column, AvoidPropertySelfAssignment.Rule, symbolName);
        }
    }
}
