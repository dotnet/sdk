// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidPropertySelfAssignment,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidPropertySelfAssignment,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public class AvoidPropertySelfAssignmentTests
    {
        [Fact]
        public async Task CSharpAssignmentInConstructorWithNoArgumentsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpAssignmentInConstructorUsingThisWithNoArgumentsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpAssignmentInConstructorWithSimilarArgumentAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpAssignmentInMethodWithoutArgumentsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpAssignmentInMethodWithSimilarArgumentNameAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpAdditionAssignmentOperatorDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpNormalPropertyAssignmentDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpNormalAssignmentOfTwoDifferentPropertiesDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpNormalVariableAssignmentDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpNormalAssignmentWithTwoDifferentInstancesDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpIndexerAssignmentDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpIndexerAssignmentWithSameConstantIndexCausesDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpIndexerAssignmentWithSameLocalReferenceIndexCausesDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task CSharpIndexerAssignmentWithSameParameterReferenceIndexCausesDiagnosticToAppearAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task VbAssignmentInConstructorWithNoArgumentsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task VbAssignmentInConstructorUsingThisWithNoArgumentsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task VbAssignmentInConstructorWithSimilarArgumentAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task VbAssignmentInMethodWithoutArgumentsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task VbAssignmentInMethodWithSimilarArgumentNameAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task VbAdditionAssignmentOperatorDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Private Property [P] As Integer

    Public Sub VbMethod(ByVal [methodArg] As String)
        [P] += 1
    End Sub
End Class
");
        }

        [Fact]
        public async Task VbNormalPropertyAssignmentDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Private Property [P] As String

    Public Sub VbMethod(ByVal [methodArg] As String)
        [P] = [methodArg]
    End Sub
End Class
");
        }

        [Fact]
        public async Task VbNormalAssignmentOfTwoDifferentPropertiesDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task VbNormalVariableAssignmentDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Class C
    Private Property [P] As String

    Public Sub VbMethod(ByVal [methodArg] As String)
        Dim methodVariable = [methodArg]
    End Sub
End Class
");
        }

        [Fact]
        public async Task VbNormalAssignmentWithTwoDifferentInstancesDoesNotCauseDiagnosticToAppearAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);
    }
}
