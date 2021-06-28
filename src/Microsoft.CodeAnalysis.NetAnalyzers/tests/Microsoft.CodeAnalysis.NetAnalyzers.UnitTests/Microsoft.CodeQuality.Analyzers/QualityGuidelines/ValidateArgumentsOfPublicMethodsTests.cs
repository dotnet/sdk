// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.ValidateArgumentsOfPublicMethods,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.ValidateArgumentsOfPublicMethods,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.ParameterValidationAnalysis)]
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PointsToAnalysis)]
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
    public class ValidateArgumentsOfPublicMethodsTests
    {
        private static DiagnosticResult GetCSharpResultAt(int line, int column, string methodSignature, string parameterName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(methodSignature, parameterName);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string methodSignature, string parameterName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(methodSignature, parameterName);

        [Fact]
        public async Task ValueTypeParameter_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct C
{
    public int X;
}

public class Test
{
    public int M1(C c)
    {
        return c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure C
    Public X As Integer
End Structure

Public Class Test
    Public Function M1(c As C) As Integer
        Return c.X
    End Function
End Class");
        }

        [Fact]
        public async Task ReferenceTypeParameter_NoUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Test
{
    public void M1(string str)
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Test
    Public Sub M1(str As String)
    End Sub
End Class");
        }

        [Fact]
        public async Task ReferenceTypeParameter_NoHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Test
{
    public void M1(string str)
    {
        var x = str;
        M2(str);
    }

    private void M2(string str)
    {
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Test
    Public Sub M1(str As String)
        Dim x = str
        M2(str)
    End Sub

    Private Sub M2(str As String)
    End Sub
End Class");
        }

        [Fact]
        public async Task NonExternallyVisibleMethod_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    private int M1(C c)
    {
        return c.X;
    }

    internal int M2(C c)
    {
        return c.X;
    }
}

internal class Test2
{
    public int M1(C c)
    {
        return c.X;
    }

    protected int M2(C c)
    {
        return c.X;
    }

    internal int M3(C c)
    {
        return c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Private Function M1(c As C) As Integer
        Return c.X
    End Function

    Friend Function M2(c As C) As Integer
        Return c.X
    End Function
End Class

Friend Class Test2
    Public Function M1(c As C) As Integer
        Return c.X
    End Function

    Protected Function M2(c As C) As Integer
        Return c.X
    End Function

    Friend Function M3(c As C) As Integer
        Return c.X
    End Function
End Class");
        }

        [Fact]
        public async Task HazardousUsage_MethodReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Test
{
    public void M1(string str)
    {
        var x = str.ToString();
    }
}
",
            // Test0.cs(6,17): warning CA1062: In externally visible method 'void Test.M1(string str)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(6, 17, "void Test.M1(string str)", "str"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Test
    Public Sub M1(str As String)
        Dim x = str.ToString()
    End Sub
End Class
",
            // Test0.vb(4,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(4, 17, "Sub Test.M1(str As String)", "str"));
        }

        [Fact]
        public async Task HazardousUsage_FieldReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        var x = c.X;
    }
}
",
            // Test0.cs(11,17): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(11, 17, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        Dim x = c.X
    End Sub
End Class
",
            // Test0.vb(8,17): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(8, 17, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task HazardousUsage_PropertyReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X { get; }
}

public class Test
{
    public void M1(C c)
    {
        var x = c.X;
    }
}
",
            // Test0.cs(11,17): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(11, 17, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public ReadOnly Property X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        Dim x = c.X
    End Sub
End Class
",
            // Test0.vb(8,17): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(8, 17, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task HazardousUsage_EventReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public delegate void MyDelegate();
    public event MyDelegate X;
}

public class Test
{
    public void M1(C c)
    {
        c.X += MyHandler;
    }

    private void MyHandler()
    {
    }
}
",
            // Test0.cs(12,9): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(12, 9, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Event X()
End Class

Public Class Test
    Public Sub M1(c As C)
        AddHandler c.X, AddressOf MyHandler
    End Sub

    Private Sub MyHandler()
    End Sub
End Class
",
            // Test0.vb(8,20): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(8, 20, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task HazardousUsage_ArrayElementReference_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Test
{
    public void M1(Test[] tArray)
    {
        var x = tArray[0];
    }
}
",
            // Test0.cs(6,17): warning CA1062: In externally visible method 'void Test.M1(Test[] tArray)', validate parameter 'tArray' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(6, 17, "void Test.M1(Test[] tArray)", "tArray"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Test
    Public Sub M1(tArray As Test())
        Dim x = tArray(0)
    End Sub
End Class
",
            // Test0.vb(4,17): warning CA1062: In externally visible method 'Sub Test.M1(tArray As Test())', validate parameter 'tArray' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(4, 17, "Sub Test.M1(tArray As Test())", "tArray"));
        }

        [Fact]
        public async Task HazardousUsage_ReferenceInConditiona_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public bool X;
}

public class Test
{
    public void M1(C c)
    {
        if (c.X)
        {
        }
    }
}
",
            // Test0.cs(11,13): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(11, 13, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Boolean
End Class

Public Class Test
    Public Sub M1(c As C)
        If c.X Then
        End If
    End Sub
End Class
",
            // Test0.vb(8,12): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(8, 12, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task MultipleHazardousUsages_OneReportPerParameter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
    public int Y;    
}

public class Test
{
    public void M1(C c1, C c2)
    {
        var x = c1.X;   // Diagnostic
        var y = c1.Y;
        var x2 = c1.X;

        var x3 = c2.X;   // Diagnostic
        var y2 = c2.Y;
        var x4 = c2.X;
    }
}
",
        // Test0.cs(12,17): warning CA1062: In externally visible method 'void Test.M1(C c1, C c2)', validate parameter 'c1' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
        GetCSharpResultAt(12, 17, "void Test.M1(C c1, C c2)", "c1"),
        // Test0.cs(16,18): warning CA1062: In externally visible method 'void Test.M1(C c1, C c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
        GetCSharpResultAt(16, 18, "void Test.M1(C c1, C c2)", "c2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
    Public Y As Integer
End Class

Public Class Test

    Public Sub M1(c1 As C, c2 As C)
        Dim x = c1.X    ' Diagnostic
        Dim y = c1.Y
        Dim x2 = c1.X

        Dim x3 = c2.X    ' Diagnostic
        Dim y2 = c2.Y
        Dim x4 = c2.X
    End Sub
End Class
",
            // Test0.vb(10,17): warning CA1062: In externally visible method 'Sub Test.M1(c1 As C, c2 As C)', validate parameter 'c1' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(10, 17, "Sub Test.M1(c1 As C, c2 As C)", "c1"),
            // Test0.vb(14,18): warning CA1062: In externally visible method 'Sub Test.M1(c1 As C, c2 As C)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(14, 18, "Sub Test.M1(c1 As C, c2 As C)", "c2"));
        }

        [Fact]
        public async Task HazardousUsage_OptionalParameter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Test
{
    private const string _constStr = """";
    public void M1(string str = _constStr)
    {
        var x = str.ToString();
    }
}
",
            // Test0.cs(7,17): warning CA1062: In externally visible method 'void Test.M1(string str = "")', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(7, 17, @"void Test.M1(string str = """")", "str"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Test
    Private Const _constStr As String = """"
    Public Sub M1(Optional str As String = _constStr)
        Dim x = str.ToString()
    End Sub
End Class
",
            // Test0.vb(5,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String = "")', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(5, 17, @"Sub Test.M1(str As String = """")", "str"));
        }

        [Fact]
        public async Task ConditionalAccessUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        var y = x?.ToString();
        
        var z = c?.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str
        Dim y = x?.ToString()

        Dim z = c?.X
    End Sub
End Class");
        }

        [Fact]
        public async Task ValidatedNonNullAttribute_PossibleNullRefUsage_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class ValidatedNotNullAttribute : System.Attribute
{
}

public class Test
{
    public void M1([ValidatedNotNullAttribute]string str)
    {
        var x = str.ToString();
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class ValidatedNotNullAttribute
    Inherits System.Attribute
End Class

Public Class Test
    Public Sub M1(<ValidatedNotNullAttribute>str As String)
        Dim x = str.ToString()
    End Sub
End Class
");
        }

        [Fact]
        public async Task ValidatedNonNullAttribute_PossibleNullRefUsageOnDifferentParam_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class ValidatedNotNullAttribute : System.Attribute
{
}

public class Test
{
    public void M1([ValidatedNotNullAttribute]string str, string str2)
    {
        var x = str.ToString() + str2.ToString();
    }
}
",
            // Test0.cs(10,34): warning CA1062: In externally visible method 'void Test.M1(string str, string str2)', validate parameter 'str2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(10, 34, "void Test.M1(string str, string str2)", "str2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class ValidatedNotNullAttribute
    Inherits System.Attribute
End Class

Public Class Test
    Public Sub M1(<ValidatedNotNullAttribute>str As String, str2 As String)
        Dim x = str.ToString() + str2.ToString()
    End Sub
End Class
",
            // Test0.vb(8,34): warning CA1062: In externally visible method 'Sub Test.M1(str As String, str2 As String)', validate parameter 'str2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(8, 34, "Sub Test.M1(str As String, str2 As String)", "str2"));
        }

        [Fact, WorkItem(4248, "https://github.com/dotnet/roslyn-analyzers/issues/4248")]
        public async Task NotNullAttribute_PossibleNullRefUsage_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp31,
                TestCode = @"
using System.Diagnostics.CodeAnalysis;

public class Test
{
    public void M1([NotNull]string str)
    {
        var x = str.ToString();
    }
}
",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp31,
                TestCode = @"
Imports System.Diagnostics.CodeAnalysis

Public Class Test
    Public Sub M1(<NotNull>str As String)
        Dim x = str.ToString()
    End Sub
End Class
",
            }.RunAsync();
        }

        [Fact]
        public async Task DefiniteSimpleAssignment_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        x = ""newString"";
        var y = x.ToString();

        c = new C();
        var z = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str
        x = ""newString""
        Dim y = x.ToString()

        c = New C()
        Dim z = c.X
    End Sub
End Class");
        }

        [Fact]
        public async Task AssignedToFieldAndValidated_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    private C _c;
    private Test _t;
    public void M1(C c)
    {
        _c = c;
        _t._c = c;
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }

        var z = _c.X + _t._c.X + c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Private _c As C
    Private _t As Test
    Public Sub M1(c As C)
        _c = c
        _t._c = c
        If c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If

        Dim z = _c.X + _t._c.X + c.X
    End Sub
End Class");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(PointsToAnalysisKind.None)]
        [InlineData(PointsToAnalysisKind.PartialWithoutTrackingFieldsAndProperties)]
        [InlineData(PointsToAnalysisKind.Complete)]
        public async Task AssignedToFieldAndNotValidated_BeforeHazardousUsages_Diagnostic(PointsToAnalysisKind? pointsToAnalysisKind)
        {
            var editorConfig = pointsToAnalysisKind.HasValue ?
                $"dotnet_code_quality.CA1062.points_to_analysis_kind = {pointsToAnalysisKind}" :
                string.Empty;

            var csCode = @"
using System;

public class C
{
    public int X;
}

public class Test
{
    private C _c;
    private Test _t;
    public void M1(C c)
    {
        _t._c = c;
        var z = _t._c.X;
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }
    }

    public void M2(C c)
    {
        _c = c;
        _t._c = c;
        var z = _c.X + _t._c.X;
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }
    }
}";
            var csTest = new VerifyCS.Test()
            {
                TestState =
                {
                    Sources = { csCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfig}") },
                }
            };

            if (pointsToAnalysisKind == PointsToAnalysisKind.Complete)
            {
                csTest.ExpectedDiagnostics.AddRange(new[]
                {
                    // Test0.cs(16,17): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(16, 17, "void Test.M1(C c)", "c"),
                    // Test0.cs(27,17): warning CA1062: In externally visible method 'void Test.M2(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(27, 17, "void Test.M2(C c)", "c")
                });
            }

            await csTest.RunAsync();

            var vbCode = @"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Private _c As C
    Private _t As Test
    Public Sub M1(c As C)
        _t._c = c
        Dim z = _t._c.X
        If c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If
    End Sub

    Public Sub M2(c As C)
        _c = c
        _t._c = c
        Dim z = _c.X + _t._c.X
        If c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If
    End Sub
End Class";

            var vbTest = new VerifyVB.Test()
            {
                TestState =
                {
                    Sources = { vbCode },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"[*]\r\n{editorConfig}") },
                }
            };

            if (pointsToAnalysisKind == PointsToAnalysisKind.Complete)
            {
                vbTest.ExpectedDiagnostics.AddRange(new[]
                {
                    // Test0.vb(13,17): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetBasicResultAt(13, 17, "Sub Test.M1(c As C)", "c"),
                    // Test0.vb(22,17): warning CA1062: In externally visible method 'Sub Test.M2(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetBasicResultAt(22, 17, "Sub Test.M2(c As C)", "c")
                });
            }

            await vbTest.RunAsync();
        }

        [Fact]
        public async Task MayBeAssigned_BeforeHazardousUsages_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c, bool flag)
    {
        var x = str;
        if (flag)
        {
            x = ""newString"";
            c = new C();
        }

        // Below may or may not cause null refs
        var y = x.ToString();
        var z = c.X;
    }
}
",
            // Test0.cs(19,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c, bool flag)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(19, 17, "void Test.M1(string str, C c, bool flag)", "str"),
            // Test0.cs(20,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c, bool flag)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(20, 17, "void Test.M1(string str, C c, bool flag)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C, flag As Boolean)
        Dim x = str

        If flag Then
            x = ""newString""
            c = New C()
        End If

        ' Below may or may not cause null refs
        Dim y = x.ToString()
        Dim z = c.X
    End Sub
End Class",
            // Test0.vb(16,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C, flag As Boolean)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(16, 17, "Sub Test.M1(str As String, c As C, flag As Boolean)", "str"),
            // Test0.vb(17,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C, flag As Boolean)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(17, 17, "Sub Test.M1(str As String, c As C, flag As Boolean)", "c"));
        }

        [Fact]
        public async Task ConditionalButDefiniteNonNullAssigned_BeforeHazardousUsages_NoDiagnostic_CopyAnalysis()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c, bool flag)
    {
        var x = str;
        if (str == null || c == null)
        {
            x = ""newString"";
            c = new C();
        }

        // x and c are both non-null here.
        var y = x.ToString();
        var z = c.X;
    }

    public void M2(string str, C c, bool flag)
    {
        var x = str;
        if (str == null)
        {
            x = ""newString"";
        }

        if (c == null)
        {
            c = new C();
        }

        // x and c are both non-null here.
        var y = x.ToString();
        var z = c.X;
    }

    public void M3(string str, C c, bool flag)
    {
        var x = str ?? ""newString"";
        c = c ?? new C();

        // x and c are both non-null here.
        var y = x.ToString();
        var z = c.X;
    }

    public void M4(string str, C c, bool flag)
    {
        var x = str != null ? str : ""newString"";
        c = c != null ? c : new C();

        // x and c are both non-null here.
        var y = x.ToString();
        var z = c.X;
    }
}
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
                        @"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(ByVal str As String, ByVal c As C, ByVal flag As Boolean)
        Dim x = str
        If str Is Nothing OrElse c Is Nothing Then
            x = ""newString""
            c = New C()
        End If

        ' x and c are both non-null here.
        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Public Sub M2(ByVal str As String, ByVal c As C, ByVal flag As Boolean)
        Dim x = str
        If str Is Nothing Then
            x = ""newString""
        End If

        If c Is Nothing Then
            c = New C()
        End If

        ' x and c are both non-null here.
        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Public Sub M3(ByVal str As String, ByVal c As C, ByVal flag As Boolean)
        Dim x = If(str, ""newString"")
        c = If(c, New C())

        ' x and c are both non-null here.
        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Public Sub M4(ByVal str As String, ByVal c As C, ByVal flag As Boolean)
        Dim x = If(str IsNot Nothing, str, ""newString"")
        c = If(c IsNot Nothing, c, New C())

        ' x and c are both non-null here.
        Dim y = x.ToString()
        Dim z = c.X
    End Sub

End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") }
                }
            }.RunAsync();
        }

        [Fact]
        public async Task ThrowOnNull_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }

        var y = x.ToString();
        var z = c.X;
    }

    public void M2(string str, C c)
    {
        var x = str;
        if (str == null || c == null)
        {
            throw new ArgumentException();
        }

        var y = x.ToString();
        var z = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(ByVal str As String, ByVal c As C)
        Dim x = str
        If str Is Nothing Then
            Throw New ArgumentNullException(NameOf(str))
        End If

        If c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If

        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Public Sub M2(ByVal str As String, ByVal c As C)
        Dim x = str
        If str Is Nothing OrElse c Is Nothing Then
            Throw New ArgumentException()
        End If

        Dim y = x.ToString()
        Dim z = c.X
    End Sub
End Class");
        }

        [Fact]
        public async Task ThrowOnNullForSomeParameter_HazardousUsageForDifferentParameter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        if (str == null)
        {
            throw new System.ArgumentNullException(nameof(str));
        }

        var x = str;
        var y = x.ToString();

        var z = c.X;
    }

    public void M2(string str, C c)
    {
        var x = str;
        if (str == null)
        {
            if (c == null)
            {
                throw new System.ArgumentNullException(nameof(c));
            }

            throw new System.ArgumentNullException(nameof(str));
        }

        var y = x.ToString();

        var z = c.X;
    }
}
",
            // Test0.cs(19,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(19, 17, "void Test.M1(string str, C c)", "c"),
            // Test0.cs(37,17): warning CA1062: In externally visible method 'void Test.M2(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(37, 17, "void Test.M2(string str, C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        If str Is Nothing Then
            Throw New System.ArgumentNullException(NameOf(str))
        End If

        Dim x = str
        Dim y = x.ToString()

        Dim z = c.X
    End Sub

    Public Sub M2(str As String, c As C)
        Dim x = str
        If str Is Nothing Then
            If c Is Nothing Then
                Throw New System.ArgumentNullException(NameOf(c))
            End If

            Throw New System.ArgumentNullException(NameOf(str))
        End If

        Dim y = x.ToString()

        Dim z = c.X
    End Sub
End Class
",
            // Test0.vb(15,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(15, 17, "Sub Test.M1(str As String, c As C)", "c"),
            // Test0.vb(30,17): warning CA1062: In externally visible method 'Sub Test.M2(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(30, 17, "Sub Test.M2(str As String, c As C)", "c"));
        }

        [Fact]
        public async Task ThrowOnNull_AfterHazardousUsages_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        var z = c.X;
        if (c == null)
        {
            throw new System.ArgumentNullException(nameof(c));
        }
    }
}
",
            // Test0.cs(11,17): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(11, 17, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        Dim z = c.X
        If c Is Nothing Then
            Throw New System.ArgumentNullException(NameOf(c))
        End If
    End Sub
End Class
",
            // Test0.vb(8,17): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(8, 17, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task NullCoalescingThrowExpressionOnNull_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str ?? throw new ArgumentNullException(nameof(str));
        var y = x.ToString();

        c = c ?? throw new ArgumentNullException(nameof(c));
        var z = c.X;
    }
}
");
            // Throw expression not supported for VB.
        }

        [Fact]
        public async Task ThrowOnNull_UncommonNullCheckSyntax_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        if (null == str)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (null == c)
        {
            throw new ArgumentNullException(nameof(c));
        }

        var y = x.ToString();
        var z = c.X;
    }

    public void M2(string str, C c)
    {
        var x = str;
        if ((object)str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (null == (object)c)
        {
            throw new ArgumentNullException(nameof(c));
        }

        var y = x.ToString();
        var z = c.X;
    }

    public void M3(string str, C c)
    {
        var x = str;
        object myNullObject = null;
        if (str == myNullObject || myNullObject == c)
        {
            throw new ArgumentException();
        }

        var y = x.ToString();
        var z = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(ByVal str As String, ByVal c As C)
        Dim x = str
        If Nothing Is str Then
            Throw New ArgumentNullException(NameOf(str))
        End If

        If Nothing Is c Then
            Throw New ArgumentNullException(NameOf(c))
        End If

        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Public Sub M2(ByVal str As String, ByVal c As C)
        Dim x = str
        If DirectCast(str, System.Object) Is Nothing Then
            Throw New ArgumentNullException(NameOf(str))
        End If

        If Nothing Is CType(c, System.Object) Then
            Throw New ArgumentNullException(NameOf(c))
        End If

        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Public Sub M3(ByVal str As String, ByVal c As C)
        Dim x = str
        Dim myNullObject As System.Object = Nothing
        If str Is myNullObject OrElse myNullObject Is c Then
            Throw New ArgumentException()
        End If

        Dim y = x.ToString()
        Dim z = c.X
    End Sub
End Class");
        }

        [Fact]
        public async Task ContractCheck_NoDiagnostic_CopyAnalysis()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        System.Diagnostics.Contracts.Contract.Requires(x != null);
        System.Diagnostics.Contracts.Contract.Requires(c != null);
        var y = str.ToString();
        var z = c.X;
    }

    public void M2(string str, C c)
    {
        var x = str;
        System.Diagnostics.Contracts.Contract.Requires(x != null && c != null);
        var y = str.ToString();
        var z = c.X;
    }

    public void M3(C c1, C c2)
    {
        System.Diagnostics.Contracts.Contract.Requires(c1 != null && c1 == c2);
        var z = c1.X + c2.X;
    }

    void M4_Assume(C c)
    {
        System.Diagnostics.Contracts.Contract.Assume(c != null);
        var z = c.X;
    }

    void M5_Assert(C c)
    {
        System.Diagnostics.Contracts.Contract.Assert(c != null);
        var z = c.X;
    }
}
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
                        @"
Public Class C

    Public X As Integer
End Class

Public Class Test

    Public Sub M1(str As String, c As C)
        Dim x = str
        System.Diagnostics.Contracts.Contract.Requires(x IsNot Nothing)
        System.Diagnostics.Contracts.Contract.Requires(c IsNot Nothing)
        Dim y = str.ToString()
        Dim z = c.X
    End Sub

    Public Sub M2(str As String, c As C)
        Dim x = str
        System.Diagnostics.Contracts.Contract.Requires(x IsNot Nothing AndAlso c IsNot Nothing)
        Dim y = str.ToString()
        Dim z = c.X
    End Sub

    Public Sub M3(c1 As C, c2 As C)
        System.Diagnostics.Contracts.Contract.Requires(c1 IsNot Nothing AndAlso c1 Is c2)
        Dim z = c1.X + c2.X
    End Sub

    Private Sub M4_Assume(c As C)
        System.Diagnostics.Contracts.Contract.Assume(c IsNot Nothing)
        Dim z = c.X
    End Sub

    Private Sub M5_Assert(c As C)
        System.Diagnostics.Contracts.Contract.Assert(c IsNot Nothing)
        Dim z = c.X
    End Sub
End Class
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.copy_analysis = true") }
                }
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PredicateAnalysis)]
        [Fact]
        public async Task ContractCheck_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    private C _c;
    public void M1(string str, C c)
    {
        var x = str;
        System.Diagnostics.Contracts.Contract.Requires(x == null);
        System.Diagnostics.Contracts.Contract.Requires(c == _c);
        var y = str.ToString();
        var z = c.X;
    }

    public void M2(string str, C c)
    {
        var x = str;
        System.Diagnostics.Contracts.Contract.Requires(x != null || c != null);
        var y = str.ToString();
        var z = c.X;
    }

    public void M3(C c1, C c2)
    {
        System.Diagnostics.Contracts.Contract.Requires(c1 == null && c1 == c2);
        var z = c2.X;
    }

    public void M4(C c1, C c2)
    {
        System.Diagnostics.Contracts.Contract.Requires(c1 == null && c1 == c2 && c2 != null);   // Infeasible condition
        var z = c2.X;
    }
}
",
            // Test0.cs(15,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(15, 17, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(16,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(16, 17, "void Test.M1(string str, C c)", "c"),
            // Test0.cs(23,17): warning CA1062: In externally visible method 'void Test.M2(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(23, 17, "void Test.M2(string str, C c)", "str"),
            // Test0.cs(24,17): warning CA1062: In externally visible method 'void Test.M2(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(24, 17, "void Test.M2(string str, C c)", "c"),
            // Test0.cs(30,17): warning CA1062: In externally visible method 'void Test.M3(C c1, C c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(30, 17, "void Test.M3(C c1, C c2)", "c2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C

    Public X As Integer
End Class

Public Class Test

    Private _c As C

    Public Sub M1(str As String, c As C)
        Dim x = str
        System.Diagnostics.Contracts.Contract.Requires(x Is Nothing)
        System.Diagnostics.Contracts.Contract.Requires(c Is _c)
        Dim y = str.ToString()
        Dim z = c.X
    End Sub

    Public Sub M2(str As String, c As C)
        Dim x = str
        System.Diagnostics.Contracts.Contract.Requires(x IsNot Nothing OrElse c IsNot Nothing)
        Dim y = str.ToString()
        Dim z = c.X
    End Sub

    Public Sub M3(c1 As C, c2 As C)
        System.Diagnostics.Contracts.Contract.Requires(c1 Is Nothing AndAlso c1 Is c2)
        Dim z = c2.X
    End Sub

    Public Sub M4(c1 As C, c2 As C)
        System.Diagnostics.Contracts.Contract.Requires(c1 Is Nothing AndAlso c1 Is c2 AndAlso c2 IsNot Nothing) ' Infeasible condition
        Dim z = c2.X
    End Sub
End Class",
            // Test0.vb(15,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(15, 17, "Sub Test.M1(str As String, c As C)", "str"),
            // Test0.vb(16,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(16, 17, "Sub Test.M1(str As String, c As C)", "c"),
            // Test0.vb(22,17): warning CA1062: In externally visible method 'Sub Test.M2(str As String, c As C)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(22, 17, "Sub Test.M2(str As String, c As C)", "str"),
            // Test0.vb(23,17): warning CA1062: In externally visible method 'Sub Test.M2(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(23, 17, "Sub Test.M2(str As String, c As C)", "c"),
            // Test0.vb(28,17): warning CA1062: In externally visible method 'Sub Test.M3(c1 As C, c2 As C)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(28, 17, "Sub Test.M3(c1 As C, c2 As C)", "c2"));
        }

        [Fact]
        public async Task ReturnOnNull_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        if (str == null)
        {
            return;
        }

        if (c == null)
        {
            return;
        }

        var x = str;
        var y = x.ToString();

        var z = c.X;
    }

    public void M2(string str, C c)
    {
        if (str == null || c == null)
        {
            return;
        }

        var x = str;
        var y = x.ToString();

        var z = c.X;
    }

    public void M3(string str, C c)
    {
        if (str == null || c == null)
        {
            return;
        }
        else
        {
            var x = str;
            var y = x.ToString();

            var z = c.X;
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        If str Is Nothing Then
            Return
        End If

        If c Is Nothing Then
            Return
        End If

        Dim x = str
        Dim y = x.ToString()

        Dim z = c.X
    End Sub

    Public Sub M2(str As String, c As C)
        If str Is Nothing OrElse c Is Nothing Then
            Return
        End If

        Dim x = str
        Dim y = x.ToString()

        Dim z = c.X
    End Sub

    Public Sub M3(str As String, c As C)
        If str Is Nothing OrElse c Is Nothing Then
            Return
        Else
            Dim x = str
            Dim y = x.ToString()

            Dim z = c.X
        End If
    End Sub
End Class
");
        }

        [Fact]
        public async Task ReturnOnNullForSomeParameter_HazardousUsageForDifferentParameter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        if (str == null)
        {
            return;
        }

        var x = str;
        var y = x.ToString();

        var z = c.X;
    }

    public void M2(string str, C c)
    {
        var x = str;
        if (str == null)
        {
            if (c == null)
            {
                return;
            }

            return;
        }

        var y = x.ToString();

        var z = c.X;
    }
}
",
            // Test0.cs(19,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(19, 17, "void Test.M1(string str, C c)", "c"),
            // Test0.cs(37,17): warning CA1062: In externally visible method 'void Test.M2(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(37, 17, "void Test.M2(string str, C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        If str Is Nothing Then
            Return
        End If

        Dim x = str
        Dim y = x.ToString()

        Dim z = c.X
    End Sub

    Public Sub M2(str As String, c As C)
        Dim x = str
        If str Is Nothing Then
            If c Is Nothing Then
                Return
            End If

            Return
        End If

        Dim y = x.ToString()

        Dim z = c.X
    End Sub
End Class
",
            // Test0.vb(15,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(15, 17, "Sub Test.M1(str As String, c As C)", "c"),
            // Test0.vb(30,17): warning CA1062: In externally visible method 'Sub Test.M2(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(30, 17, "Sub Test.M2(str As String, c As C)", "c"));
        }

        [Fact]
        public async Task StringIsNullCheck_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Test
{
    public void M1(string str)
    {
        if (!string.IsNullOrEmpty(str))
        {
            var y = str.ToString();
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Test
    Public Sub M1(ByVal str As String)
        If Not String.IsNullOrEmpty(str) Then
            Dim y = str.ToString()
        End If
    End Sub
End Class");
        }

        [Fact]
        public async Task StringIsNullCheck_WithCopyAnalysis_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Test
{
    public void M1(string str)
    {
        var x = str;
        if (!string.IsNullOrEmpty(str))
        {
            var y = x.ToString();
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Test
    Public Sub M1(ByVal str As String)
        Dim x = str
        If Not String.IsNullOrEmpty(str) Then
            Dim y = x.ToString()
        End If
    End Sub
End Class");
        }

        [Fact]
        public async Task SpecialCase_ExceptionGetObjectData_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

public class MyException : Exception
{
    public MyException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
    }
}

public class Test
{
    public void M1(MyException ex, SerializationInfo info, StreamingContext context)
    {
        if (ex != null)
        {
            ex.GetObjectData(info, context);
            var name = info.AssemblyName;
        }
    }

    public void M2(SerializationInfo info, StreamingContext context)
    {
        var ex = new MyException(info, context);
        var name = info.AssemblyName;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.Serialization

Public Class MyException
    Inherits Exception
    Public Sub New(info As SerializationInfo, context As StreamingContext)
        MyBase.New(info, context)
    End Sub

    Public Overrides Sub GetObjectData(info As SerializationInfo, context As StreamingContext)
    End Sub
End Class

Public Class Test
    Public Sub M1(ex As MyException, info As SerializationInfo, context As StreamingContext)
        If ex IsNot Nothing Then
            ex.GetObjectData(info, context)
            Dim name = info.AssemblyName
        End If
    End Sub

    Public Sub M2(info As SerializationInfo, context As StreamingContext)
        Dim ex = New MyException(info, context)
        Dim name = info.AssemblyName
    End Sub
End Class
");
        }

        [Fact]
        public async Task NullCheckWithNegationBasedCondition_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        if (!(str == null || !(null != c)))
        {
            var y = x.ToString();
            var z = c.X;
        }        
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(ByVal str As String, ByVal c As C)
        Dim x = str
        If Not (str Is Nothing OrElse Not (Nothing IsNot c)) Then
            Dim y = x.ToString()
            Dim z = c.X
        End If
    End Sub
End Class");
        }

        [Fact]
        public async Task HazardousUsageInInvokedMethod_PrivateMethod_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c1, C c2)
    {
        M2(c1); // No diagnostic
        M3(c2); // Diagnostic
    }

    private static void M2(C c)
    {
    }

    private static void M3(C c)
    {
        var x = c.X;
    }
}
",
            // Test0.cs(12,12): warning CA1062: In externally visible method 'void Test.M1(C c1, C c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(12, 12, "void Test.M1(C c1, C c2)", "c2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c1 As C, c2 As C)
        M2(c1) ' No diagnostic
        M3(c2) ' Diagnostic
    End Sub

    Private Shared Sub M2(c As C)
    End Sub

    Private Shared Sub M3(c As C)
        Dim x = c.X
    End Sub
End Class
",
            // Test0.vb(9,12): warning CA1062: In externally visible method 'Sub Test.M1(c1 As C, c2 As C)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(9, 12, "Sub Test.M1(c1 As C, c2 As C)", "c2"));
        }

        [Theory]
        [InlineData(@"dotnet_code_quality.interprocedural_analysis_kind = None")]
        [InlineData(@"dotnet_code_quality.max_interprocedural_method_call_chain = 0")]
        [InlineData(@"dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive
                      dotnet_code_quality.max_interprocedural_method_call_chain = 0")]
        public async Task HazardousUsageInInvokedMethod_PrivateMethod_EditorConfig_NoInterproceduralAnalysis_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c1, C c2)
    {
        M2(c1); // No diagnostic
        M3(c2); // Diagnostic
    }

    private static void M2(C c)
    {
    }

    private static void M3(C c)
    {
        var x = c.X;
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
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c1 As C, c2 As C)
        M2(c1) ' No diagnostic
        M3(c2) ' Diagnostic
    End Sub

    Private Shared Sub M2(c As C)
    End Sub

    Private Shared Sub M3(c As C)
        Dim x = c.X
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

        [Theory, WorkItem(2525, "https://github.com/dotnet/roslyn-analyzers/issues/2525")]
        [InlineData(@"dotnet_code_quality.interprocedural_analysis_kind = None")]
        [InlineData(@"dotnet_code_quality.max_interprocedural_method_call_chain = 0")]
        [InlineData(@"dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive
                      dotnet_code_quality.max_interprocedural_method_call_chain = 0")]
        public async Task ValidatedNotNullAttributeInInvokedMethod_EditorConfig_NoInterproceduralAnalysis_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class ValidatedNotNullAttribute : System.Attribute
{
}

public class C
{
    public void M1(C c1, C c2)
    {
        Validate(c1);
        var x = c1.ToString(); // No diagnostic

        NoValidate(c2);
        x = c2.ToString(); // Diagnostic
    }

    private static void Validate([ValidatedNotNullAttribute]C c)
    {
    }

    private static void NoValidate(C c)
    {
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
                ExpectedDiagnostics =
                {
                    // Test0.cs(14,13): warning CA1062: In externally visible method 'void C.M1(C c1, C c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(14, 13, "void C.M1(C c1, C c2)", "c2"),
                }
            }.RunAsync();
        }

        [Fact, WorkItem(2525, "https://github.com/dotnet/roslyn-analyzers/issues/2525")]
        public async Task ValidatedNotNullAttributeInInvokedMethod_EditorConfig_NoInterproceduralAnalysis_NoDiagnostic_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class ValidatedNotNullAttribute : Attribute { }

internal static class Param
{
    public static void RequireNotNull([ValidatedNotNull] object value)
    {
    }
}

public class DataThing
{
    public IList<object> Items { get; }
}

public static class Issue2578Test
{
    public static void DoSomething(DataThing input)
    {
        Param.RequireNotNull(input);

        // This line still generates a CA1062 error.
        SomeMethod(input);
    }

    private static void SomeMethod(DataThing input)
    {
        input.Items.Any();
    }
}
");
        }

        [Fact, WorkItem(2525, "https://github.com/dotnet/roslyn-analyzers/issues/2525")]
        public async Task ValidatedNotNullAttributeInInvokedMethod_EditorConfig_NoInterproceduralAnalysis_NoDiagnostic_03()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class ValidatedNotNullAttribute : Attribute { }

internal static class Param
{
    public static void RequireNotNull([ValidatedNotNull] object value)
    {
        Param.RequireNotNull2(value);
    }

    public static void RequireNotNull2([ValidatedNotNull] object value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
    }
}

public class DataThing
{
    public IList<object> Items { get; }
}

public static class Issue2578Test
{
    public static void DoSomething(DataThing input)
    {
        Param.RequireNotNull(input);

        // This line still generates a CA1062 error.
        SomeMethod(input);
    }

    private static void SomeMethod(DataThing input)
    {
        input.Items.Any();
    }
}
");
        }

        [Theory, WorkItem(2578, "https://github.com/dotnet/roslyn-analyzers/issues/2578")]
        // Match by method name
        [InlineData(@"dotnet_code_quality.interprocedural_analysis_kind = None
                      dotnet_code_quality.null_check_validation_methods = Validate")]
        // Match multiple methods by method documentation ID
        [InlineData(@"dotnet_code_quality.interprocedural_analysis_kind = None
                      dotnet_code_quality.null_check_validation_methods = C.Validate(C)|Helper`1.Validate(C)|Helper`1.Validate``1(C,``0)")]
        // Match multiple methods by method documentation ID with "M:" prefix
        [InlineData(@"dotnet_code_quality.interprocedural_analysis_kind = None
                      dotnet_code_quality.null_check_validation_methods = M:C.Validate(C)|M:Helper`1.Validate(C)|M:Helper`1.Validate``1(C,``0)")]
        public async Task NullCheckValidationMethod_ConfiguredInEditorConfig_NoInterproceduralAnalysis_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public void M1(C c1, C c2, C c3, C c4, C c5, C c6)
    {
        Validate(c1);
        var x = c1.ToString(); // No diagnostic

        Helper<int>.Validate(c2);
        x = c2.ToString(); // No diagnostic

        Helper<int>.Validate<object>(c3, null);
        x = c3.ToString(); // No diagnostic

        NoValidate(c4);
        x = c4.ToString(); // Diagnostic

        Helper<int>.NoValidate(c5);
        x = c5.ToString(); // Diagnostic

        Helper<int>.NoValidate<object>(c6, null);
        x = c6.ToString(); // Diagnostic
    }

    private static void Validate(C c)
    {
    }

    private static void NoValidate(C c)
    {
    }
}

internal static class Helper<T>
{
    internal static void Validate(C c)
    {
    }

    internal static void NoValidate(C c)
    {
    }

    internal static void Validate<U>(C c, U u)
    {
    }

    internal static void NoValidate<U>(C c, U u)
    {
    }
}
"
},
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
                ExpectedDiagnostics =
                {
                    // Test0.cs(16,13): warning CA1062: In externally visible method 'void C.M1(C c1, C c2, C c3, C c4, C c5, C c6)', validate parameter 'c4' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(16, 13, "void C.M1(C c1, C c2, C c3, C c4, C c5, C c6)", "c4"),
                    // Test0.cs(19,13): warning CA1062: In externally visible method 'void C.M1(C c1, C c2, C c3, C c4, C c5, C c6)', validate parameter 'c5' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(19, 13, "void C.M1(C c1, C c2, C c3, C c4, C c5, C c6)", "c5"),
                    // Test0.cs(22,13): warning CA1062: In externally visible method 'void C.M1(C c1, C c2, C c3, C c4, C c5, C c6)', validate parameter 'c6' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(22, 13, "void C.M1(C c1, C c2, C c3, C c4, C c5, C c6)", "c6"),
                }
            }.RunAsync();
        }

        [Fact, WorkItem(1707, "https://github.com/dotnet/roslyn-analyzers/issues/1707")]
        public async Task HazardousUsageInInvokedMethod_PrivateMethod_Generic_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c1, C c2)
    {
        M2(c1); // No diagnostic
        M3(c2); // Diagnostic
    }

    private static void M2<T>(T c) where T: C
    {
    }

    private static void M3<T>(T c) where T: C
    {
        var x = c.X;
    }
}
",
            // Test0.cs(12,12): warning CA1062: In externally visible method 'void Test.M1(C c1, C c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(12, 12, "void Test.M1(C c1, C c2)", "c2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c1 As C, c2 As C)
        M2(c1) ' No diagnostic
        M3(c2) ' Diagnostic
    End Sub

    Private Shared Sub M2(Of T As C)(c As T)
    End Sub

    Private Shared Sub M3(Of T As C)(c As T)
        Dim x = c.X
    End Sub
End Class
",
            // Test0.vb(9,12): warning CA1062: In externally visible method 'Sub Test.M1(c1 As C, c2 As C)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(9, 12, "Sub Test.M1(c1 As C, c2 As C)", "c2"));
        }

        [Fact]
        public async Task HazardousUsageInInvokedMethod_PublicMethod_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c1, C c2)
    {
        M2(c1); // No diagnostic
        M3(c2); // No diagnostic here, diagnostic in M3
    }

    public void M2(C c)
    {
    }

    public void M3(C c)
    {
        var x = c.X;    // Diagnostic
    }
}
",
            // Test0.cs(21,17): warning CA1062: In externally visible method 'void Test.M3(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(21, 17, "void Test.M3(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c1 As C, c2 As C)
        M2(c1) ' No diagnostic
        M3(c2) ' No diagnostic here, diagnostic in M3
    End Sub

    Public Sub M2(c As C)
    End Sub

    Public Sub M3(c As C)
        Dim x = c.X     ' Diagnostic
    End Sub
End Class
",
            // Test0.vb(16,17): warning CA1062: In externally visible method 'Sub Test.M3(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(16, 17, "Sub Test.M3(c As C)", "c"));
        }

        [Fact, WorkItem(1707, "https://github.com/dotnet/roslyn-analyzers/issues/1707")]
        public async Task HazardousUsageInInvokedMethod_PublicMethod_Generic_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c1, C c2)
    {
        M2(c1); // No diagnostic
        M3(c2); // No diagnostic here, diagnostic in M3
    }

    public void M2<T>(T c) where T: C
    {
    }

    public void M3<T>(T c) where T: C
    {
        var x = c.X;    // Diagnostic
    }
}
",
            // Test0.cs(21,17): warning CA1062: In externally visible method 'void Test.M3<T>(T c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(21, 17, "void Test.M3<T>(T c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c1 As C, c2 As C)
        M2(c1) ' No diagnostic
        M3(c2) ' No diagnostic here, diagnostic in M3
    End Sub

    Public Sub M2(Of T As C)(c As T)
    End Sub

    Public Sub M3(Of T As C)(c As T)
        Dim x = c.X
    End Sub
End Class
",
            // Test0.vb(16,17): warning CA1062: In externally visible method 'Sub Test.M3(Of T)(c As T)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(16, 17, "Sub Test.M3(Of T)(c As T)", "c"));
        }

        [Fact]
        public async Task HazardousUsageInInvokedMethod_PrivateMethod_MultipleLevelsDown_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        M2(c); // No diagnostic, currently we do not analyze invocations in invoked method.
    }

    private static void M2(C c)
    {
        M3(c);
    }

    private static void M3(C c)
    {
        var x = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        M2(c) ' No diagnostic, currently we do not analyze invocations in invoked method.
    End Sub

    Private Shared Sub M2(c As C)
        M3(c)
    End Sub

    Private Shared Sub M3(c As C)
        Dim x = c.X
    End Sub
End Class
");
        }

        [Fact]
        public async Task HazardousUsageInInvokedMethod_WithInvocationCycles_Diagnostic()
        {
            // Code with cyclic call graph to verify we don't analyze indefinitely.
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c1, C c2)
    {
        M2(c1); // No diagnostic
        M3(c2); // Diagnostic
    }

    private static void M2(C c)
    {
        M3(c);
    }

    private static void M3(C c)
    {
        M2(c);
        var x = c.X;
    }
}
",
            // Test0.cs(12,12): warning CA1062: In externally visible method 'void Test.M1(C c1, C c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(12, 12, "void Test.M1(C c1, C c2)", "c2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c1 As C, c2 As C)
        M2(c1) ' No diagnostic
        M3(c2) ' Diagnostic
    End Sub

    Private Shared Sub M2(c As C)
    End Sub

    Private Shared Sub M3(c As C)
        Dim x = c.X
    End Sub
End Class
",
            // Test0.vb(9,12): warning CA1062: In externally visible method 'Sub Test.M1(c1 As C, c2 As C)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(9, 12, "Sub Test.M1(c1 As C, c2 As C)", "c2"));
        }

        [Fact]
        public async Task HazardousUsageInInvokedMethod_InvokedAfterValidation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        if (c != null)
        {
            M2(c); // No diagnostic
        }
    }

    private static void M2(C c)
    {
        var x = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        If c IsNot Nothing Then
            M2(c) ' No diagnostic
        End If
    End Sub

    Private Shared Sub M2(c As C)
        Dim x = c.X
    End Sub
End Class
");
        }

        [Fact]
        public async Task ValidatedInInvokedMethod_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        M2(c); // Validation method
        var x = c.X;    // No diagnostic here.
    }

    private static void M2(C c)
    {
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        M2(c) ' Validation method
        Dim x = c.X
    End Sub

    Private Shared Sub M2(c As C)
        If c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If
    End Sub
End Class");
        }

        [Fact, WorkItem(1707, "https://github.com/dotnet/roslyn-analyzers/issues/1707")]
        public async Task ValidatedInInvokedMethod_Generic_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        M2(c); // Validation method
        var x = c.X;    // No diagnostic here.
    }

    private static void M2<T>(T c) where T: class
    {
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        M2(c) ' Validation method
        Dim x = c.X    ' No diagnostic here.
    End Sub

    Private Shared Sub M2(Of T As Class)(c As T)
        If c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If
    End Sub
End Class");
        }

        [Fact, WorkItem(2504, "https://github.com/dotnet/roslyn-analyzers/issues/2504")]
        public async Task ValidatedInInvokedMethod_Generic_02_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        M2(c); // Validation method
        var x = c.X;    // No diagnostic here.
    }

    private static T M2<T>(T c)
    {
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }

        return c;
    }
}
");
        }

        [Fact]
        public async Task MaybeValidatedInInvokedMethod_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    private bool _flag;

    public void M1(C c)
    {
        M2(c); // Validation method - validates 'c' on some paths.
        var x = c.X;    // Diagnostic.
    }

    private void M2(C c)
    {
        if (_flag && c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }
    }
}
",
            // Test0.cs(16,17): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(16, 17, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Private _flag As Boolean

    Public Sub M1(c As C)
        M2(c) ' Validation method - validates 'c' on some paths.
        Dim x = c.X     ' Diagnostic
    End Sub

    Private Sub M2(c As C)
        If _flag AndAlso c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If
    End Sub
End Class",
            // Test0.vb(13,17): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(13, 17, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task ValidatedButNoExceptionThrownInInvokedMethod_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        M2(c);
        var x = c.X;    // Diagnostic.
    }

    public void M2(C c)
    {
        if (c == null)
        {
            return;
        }

        var x = c.X;    // No Diagnostic.
    }
}
",
            // Test0.cs(14,17): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(14, 17, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        M2(c)
        Dim x = c.X     ' Diagnostic
    End Sub

    Public Sub M2(c As C)
        If c Is Nothing Then
            Return
        End If

        Dim x = c.X     ' No Diagnostic
    End Sub
End Class",
            // Test0.vb(11,17): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(11, 17, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task ValidatedInInvokedMethod_AfterHazardousUsage_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        var x = c.X;    // Diagnostic.
        M2(c); // Validation method
    }

    private static void M2(C c)
    {
        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }
    }
}
",
            // Test0.cs(13,17): warning CA1062: In externally visible method 'void Test.M1(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(13, 17, "void Test.M1(C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        Dim x = c.X     ' Diagnostic
        M2(c) ' Validation method
    End Sub

    Private Shared Sub M2(c As C)
        If c Is Nothing Then
            Throw New ArgumentNullException(NameOf(c))
        End If
    End Sub
End Class",
            // Test0.vb(10,17): warning CA1062: In externally visible method 'Sub Test.M1(c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(10, 17, "Sub Test.M1(c As C)", "c"));
        }

        [Fact]
        public async Task WhileLoop_NullCheckInCondition_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        while (str != null && c != null)
        {
            var y = x.ToString();
            var z = c.X;
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Sub M1(str As String, c As C)
        Dim x = str
        While str IsNot Nothing AndAlso c IsNot Nothing
            Dim y = x.ToString()
            Dim z = c.X
        End While
    End Sub
End Class");
        }

        [Fact]
        public async Task WhileLoop_NullCheckInCondition_HazardousUsageOnExit_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;
        while (str != null && c != null)
        {
            var y = x.ToString();
            var z = c.X;
        }

        x = str.ToString();
        var z2 = c.X;
    }
}
",
            // Test0.cs(18,13): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(18, 13, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(19,18): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(19, 18, "void Test.M1(string str, C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str
        While str IsNot Nothing AndAlso c IsNot Nothing
            Dim y = x.ToString()
            Dim z = c.X
        End While

        x = str.ToString()
        Dim z2 = c.X
    End Sub
End Class",
            // Test0.vb(14,13): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(14, 13, "Sub Test.M1(str As String, c As C)", "str"),
            // Test0.vb(15,18): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(15, 18, "Sub Test.M1(str As String, c As C)", "c"));
        }

        [Fact]
        public async Task ForLoop_NullCheckInCondition_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        for (var x = str; str != null && c != null;)
        {
            var y = x.ToString();
            var z = c.X;
        }
    }
}
");
        }

        [Fact]
        public async Task ForLoop_NullCheckInCondition_HazardousUsageOnExit_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        for (var x = str; str != null;)
        {
            var y = x.ToString();
            var z = c.X;    // Diagnostic
        }

        var x2 = str.ToString();    // Diagnostic
        var z2 = c.X;
    }
}
",
            // Test0.cs(14,21): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(14, 21, "void Test.M1(string str, C c)", "c"),
            // Test0.cs(17,18): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(17, 18, "void Test.M1(string str, C c)", "str"));
        }

        [Fact]
        public async Task LocalFunctionInvocation_EmptyBody_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;

        void MyLocalFunction()
        {
        };

        MyLocalFunction();    // This should not change state of parameters.
        var y = x.ToString();
        var z = c.X;
    }
}
",
            // Test0.cs(18,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(18, 17, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(19,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(19, 17, "void Test.M1(string str, C c)", "c"));

            // VB has no local functions.
        }

        [Fact]
        public async Task LocalFunction_HazardousUsagesInBody_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;

        void MyLocalFunction()
        {
            // Below should fire diagnostics.
            var y = x.ToString();
            var z = c.X;
        };

        MyLocalFunction();
        MyLocalFunction(); // Do not fire duplicate diagnostics
    }
}
",
            // Test0.cs(16,21): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(16, 21, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(17,21): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(17, 21, "void Test.M1(string str, C c)", "c"));

            // VB has no local functions.
        }

        [Fact]
        public async Task LambdaInvocation_EmptyBody_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;

        System.Action myLambda = () =>
        {
        };

        myLambda();    // This should not change state of parameters.
        var y = x.ToString();
        var z = c.X;
    }
}
",
            // Test0.cs(18,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(18, 17, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(19,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(19, 17, "void Test.M1(string str, C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str

        Dim myLambda As System.Action = Sub()
                                        End Sub

        myLambda()      ' This should not change state of parameters.
        Dim y = x.ToString()
        Dim z = c.X
    End Sub
End Class",
            // Test0.vb(14,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(14, 17, "Sub Test.M1(str As String, c As C)", "str"),
            // Test0.vb(15,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(15, 17, "Sub Test.M1(str As String, c As C)", "c"));
        }

        [Fact]
        public async Task Lambda_HazardousUsagesInBody_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;

        System.Action myLambda = () =>
        {
            // Below should fire diagnostics.
            var y = x.ToString();
            var z = c.X;
        };

        myLambda();
        myLambda(); // Do not fire duplicate diagnostics
    }
}
",
            // Test0.cs(16,21): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(16, 21, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(17,21): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(17, 21, "void Test.M1(string str, C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str

        Dim myLambda As System.Action = Sub()
                                            ' Below should fire diagnostics.
                                            Dim y = x.ToString()
                                            Dim z = c.X
                                        End Sub

        myLambda()
        myLambda() ' Do not fire duplicate diagnostics
    End Sub
End Class",
            // Test0.vb(12,53): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(12, 53, "Sub Test.M1(str As String, c As C)", "str"),
            // Test0.vb(13,53): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(13, 53, "Sub Test.M1(str As String, c As C)", "c"));
        }

        [Fact]
        public async Task DelegateInvocation_ValidatedArguments_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;

        System.Action<string, C> myDelegate = M2;
        myDelegate(x, c);

        var y = x.ToString();
        var z = c.X;
    }

    private void M2(string x, C c)
    {
        if (x == null)
        {
            throw new ArgumentNullException(nameof(x));
        }

        if (c == null)
        {
            throw new ArgumentNullException(nameof(c));
        }
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str

        Dim myDelegate As System.Action(Of String, C) = AddressOf M2
        myDelegate(x, c)

        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Private Sub M2(x As String, c As C)
        If x Is Nothing Then
            Throw New System.ArgumentNullException(NameOf(x))
        End If

        If c Is Nothing Then
            Throw New System.ArgumentNullException(NameOf(c))
        End If
    End Sub
End Class");
        }

        [Fact]
        public async Task DelegateInvocation_EmptyBody_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;

        System.Action<string, C> myDelegate = M2;
        myDelegate(x, c);

        var y = x.ToString();
        var z = c.X;
    }

    private void M2(string x, C c)
    {
    }
}
",
            // Test0.cs(16,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(16, 17, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(17,17): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(17, 17, "void Test.M1(string str, C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str

        Dim myDelegate As System.Action(Of String, C) = AddressOf M2
        myDelegate(x, c)

        Dim y = x.ToString()
        Dim z = c.X
    End Sub

    Private Sub M2(x As String, c As C)
    End Sub
End Class",
            // Test0.vb(13,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(13, 17, "Sub Test.M1(str As String, c As C)", "str"),
            // Test0.vb(14,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(14, 17, "Sub Test.M1(str As String, c As C)", "c"));
        }

        [Fact]
        public async Task DelegateInvocation_HazardousUsagesInBody_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(string str, C c)
    {
        var x = str;

        System.Action<string, C> myDelegate = M2;
        myDelegate(x, c);
    }

    private void M2(string x, C c)
    {
        var y = x.ToString();
        var z = c.X;
    }
}
",
            // Test0.cs(14,20): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(14, 20, "void Test.M1(string str, C c)", "str"),
            // Test0.cs(14,23): warning CA1062: In externally visible method 'void Test.M1(string str, C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(14, 23, "void Test.M1(string str, C c)", "c"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(str As String, c As C)
        Dim x = str

        Dim myDelegate As System.Action(Of String, C) = AddressOf M2
        myDelegate(x, c)
    End Sub

    Private Sub M2(x As String, c As C)
        Dim y = x.ToString()
        Dim z = c.X
    End Sub
End Class",
            // Test0.vb(11,20): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(11, 20, "Sub Test.M1(str As String, c As C)", "str"),
            // Test0.vb(11,23): warning CA1062: In externally visible method 'Sub Test.M1(str As String, c As C)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetBasicResultAt(11, 23, "Sub Test.M1(str As String, c As C)", "c"));
        }

        [Fact]
        public async Task TryCast_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
}

public class B : A
{
}

public class Test
{
    public void M1(A a)
    {
        if (a is B)
        {
        }

        if (a is B b)
        {
        }

        var c = a as B;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
End Class

Public Class B
    Inherits A
End Class
Public Class Test
    Public Sub M1(a As A)
        If TypeOf(a) Is B Then
        End If

        Dim b = TryCast(a, b)
    End Sub
End Class");
        }

        [Fact]
        public async Task DirectCastToObject_BeforeNullCheck_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public int X;
}

public class Test
{
    public void M1(C c)
    {
        if ((object)c == null)
        {
            return;
        }

        var x = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        If DirectCast(c, Object) Is Nothing Then
            Return
        End If

        Dim x = c.X
    End Sub
End Class");
        }

        [Fact]
        public async Task StaticObjectReferenceEquals_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        var x = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        If ReferenceEquals(c, Nothing) Then
            Return
        End If

        Dim x = c.X
    End Sub
End Class");
        }

        [Fact]
        public async Task StaticObjectEquals_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        var x = c.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C)
        If Object.Equals(c, Nothing) Then
            Return
        End If

        Dim x = c.X
    End Sub
End Class");
        }

        [Fact]
        public async Task ObjectEquals_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        var x = c2.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public X As Integer
End Class

Public Class Test
    Public Sub M1(c As C, c2 As C)
        If c Is Nothing OrElse Not c.Equals(c2) Then
            Return
        End If

        Dim x = c2.X
    End Sub
End Class");
        }

        [Fact]
        public async Task ObjectEqualsOverride_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        var x = c2.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
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

        Dim x = c2.X
    End Sub
End Class");
        }

        [Fact]
        public async Task IEquatableEquals_ExplicitImplementation_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        var x = c2.X;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Implements IEquatable(Of C)

    Public X As Integer
    Public Function Equals(other As C) As Boolean Implements IEquatable(Of C).Equals
        Return True
    End Function
End Class

Public Class Test
    Public Sub M1(c As C, c2 As C)
        If c Is Nothing OrElse Not c.Equals(c2) Then
            Return
        End If

        Dim x = c2.X
    End Sub
End Class");
        }

        [Fact]
        public async Task IEquatableEquals_ImplicitImplementation_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        var x = c2.X;
    }
}
");
        }

        [Fact]
        public async Task IEquatableEquals_Override_BeforeHazardousUsages_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

        var x = c2.X;
    }
}
");
        }

        [Fact, WorkItem(1852, "https://github.com/dotnet/roslyn-analyzers/issues/1852")]
        public async Task Issue1852()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        delegate object Des(Stream s);

        public object Deserialize(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return DoDeserialization(formatter.Deserialize, new MemoryStream(bytes));
        }

        private object DoDeserialization(Des des, Stream stream)
        {
            return des(stream);
        }
    }
}");
        }

        [Fact, WorkItem(1856, "https://github.com/dotnet/roslyn-analyzers/issues/1856")]
        public async Task PointsToDataFlowOperationVisitor_VisitInstanceReference_Assert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Linq;
 namespace Blah
{
    public class ContentContext
    {
        public XElement Data { get; set; }
         public XElement Element(string elementName)
        {
            var element = Data.Element(elementName);
            if (element == null)
            {
                element = new XElement(elementName);
                Data.Add(element);
            }
            return element;
        }
    }
     public interface IDef
    {
        string Name { get; }
    }
     public interface IContent
    {
        T As<T>();
        IDef Definition { get; }
    }
     public class Container
    {
        private XElement _element;
         private void SetElement(XElement value)
        {
            _element = value;
        }
         public XElement Element
        {
            get
            {
                return _element ?? (_element = new XElement(""Data""));
            }
        }
         public string Data
        {
            get
            {
                return _element == null ? null : Element.ToString(SaveOptions.DisableFormatting);
            }
            set
            {
                SetElement(string.IsNullOrEmpty(value) ? null : XElement.Parse(value, LoadOptions.PreserveWhitespace));
            }
        }
    }
     public class ContainerPart
    {
        public Container Container;
        public Container VersionContainer;
    }
     public abstract class Idk<TContent> where TContent : IContent, new()
    {
        public static void ExportInfo(TContent part, ContentContext context)
        {
            var containerPart = part.As<ContainerPart>();
             if (containerPart == null)
            {
                return;
            }
             Action<XElement, bool> exportInfo = (element, versioned) => {
                if (element == null)
                {
                    return;
                }
                 var elementName = GetContainerXmlElementName(part, versioned);
                foreach (var attribute in element.Attributes())
                {
                    context.Element(elementName).SetAttributeValue(attribute.Name, attribute.Value);
                }
            };
             exportInfo(containerPart.VersionContainer.Element.Element(part.Definition.Name), true);
            exportInfo(containerPart.Container.Element.Element(part.Definition.Name), false);
        }
         private static string GetContainerXmlElementName(TContent part, bool versioned)
        {
            return part.Definition.Name + ""-"" + (versioned ? ""VersionInfoset"" : ""Infoset"");
        }
    }
}",
                // Test0.cs(77,21): warning CA1062: In externally visible method 'void Idk<TContent>.ExportInfo(TContent part, ContentContext context)', validate parameter 'context' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                GetCSharpResultAt(77, 21, "void Idk<TContent>.ExportInfo(TContent part, ContentContext context)", "context"));
        }

        [Fact, WorkItem(1856, "https://github.com/dotnet/roslyn-analyzers/issues/1856")]
        public async Task InvocationThroughAnUninitializedLocalInstance()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    private int _field;
    public void M(C c)
    {
        C c2;
        {|CS0165:c2|}.M2(c);
    }

    private void M2(C c)
    {
        var x = c._field;
    }
}
",
            // Test0.cs(8,15): warning CA1062: In externally visible method 'void C.M(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(8, 15, "void C.M(C c)", "c"));
        }

        [Fact, WorkItem(1870, "https://github.com/dotnet/roslyn-analyzers/issues/1870")]
        public async Task Issue1870()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Reflection;
 namespace ANamespace {
    public interface IInterface {
    }
     public class PropConvert {
        public static IInterface ToSettings(object o) {
            if (IsATypeOfSomeSort(o.GetType())) {
                dynamic b = new PropBag();
                 foreach (var p in o.GetType().GetProperties()) {
                    b[p.Name] = p.GetValue(o, null);
                }
                 return b;
            }
             return null;
        }
         private static bool IsATypeOfSomeSort(Type type) {
            return type.IsGenericType
                && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false);
        }
    }
     public class PropBag : DynamicObject, IInterface {
        internal readonly Dictionary<string, IInterface> _properties = new Dictionary<string, IInterface>();
         public static dynamic New() {
            return new PropBag();
        }
         public void SetMember(string name, object value) {
            if (value == null && _properties.ContainsKey(name)) {
                _properties.Remove(name);
            }
            else {
                _properties[name] = PropConvert.ToSettings(value);
            }
        }
    }
}",
            // Test0.cs(12,35): warning CA1062: In externally visible method 'IInterface PropConvert.ToSettings(object o)', validate parameter 'o' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(12, 35, "IInterface PropConvert.ToSettings(object o)", "o"));
        }

        [Fact, WorkItem(1870, "https://github.com/dotnet/roslyn-analyzers/issues/1870")]
        public async Task Issue1870_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

namespace ANamespace
{
    public static class SomeExtensions
    {
        public static Dictionary<string, string> Merge(this Dictionary<string, string> dictionary, Dictionary<string, string> dictionaryToMerge) {
            if (dictionaryToMerge == null)
                return dictionary;

            var newDictionary = new Dictionary<string, string>(dictionary);

            foreach (var valueDictionary in dictionaryToMerge)
                newDictionary[valueDictionary.Key] = valueDictionary.Value;

            return newDictionary;
        }
    }
}");
        }

        [Fact, WorkItem(1886, "https://github.com/dotnet/roslyn-analyzers/issues/1886")]
        public async Task Issue1886()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum Status
{
    Status1,
    Status2,
}

public class C1
{
    public Status MyStatus { get; set; }
}

public class C2
{
    public static void M(C1 c, bool f)
    {
        if (c== null)
        {
            return;
        }

        c.MyStatus = f ? Status.Status1 : Status.Status2;
    }
}");
        }

        [Fact, WorkItem(1891, "https://github.com/dotnet/roslyn-analyzers/issues/1891")]
        public async Task Issue1891()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.IO;
using System.Threading;
using System.Web;

public interface IContext
{
    HttpContext HttpContext { get; }
}

public class CaptureStream : Stream
{
    public CaptureStream(Stream innerStream)
    {
        _innerStream = innerStream;
        _captureStream = new MemoryStream();
    }

    private readonly Stream _innerStream;
    private readonly MemoryStream _captureStream;

    public override bool CanRead
    {
        get { return _innerStream.CanRead; }
    }

    public override bool CanSeek
    {
        get { return _innerStream.CanSeek; }
    }

    public override bool CanWrite
    {
        get { return _innerStream.CanWrite; }
    }

    public override long Length
    {
        get { return _innerStream.Length; }
    }

    public override long Position
    {
        get { return _innerStream.Position; }
        set { _innerStream.Position = value; }
    }

    public override long Seek(long offset, SeekOrigin direction)
    {
        return _innerStream.Seek(offset, direction);
    }

    public override void SetLength(long length)
    {
        _innerStream.SetLength(length);
    }

    public override void Close()
    {
        _innerStream.Close();
    }

    public override void Flush()
    {
        if (_captureStream.Length > 0)
        {
            OnCaptured();
            _captureStream.SetLength(0);
        }

        _innerStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _innerStream.Read(buffer, offset, count);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _captureStream.Write(buffer, offset, count);
        _innerStream.Write(buffer, offset, count);
    }

    public event Action<byte[]> Captured;

    protected virtual void OnCaptured()
    {
        Captured(_captureStream.ToArray());
    }
}

public class Class1
{
    private string AField;
    private bool ASwitch;

    public void Method(IContext aContext)
    {
        var captureHandlerIsAttached = false;

        try
        {
            if (!ASwitch)
                return;

            Console.WriteLine(AField);

            if (!HasUrl(aContext))
            {
                return;
            }

            var response = aContext.HttpContext.Response;
            var captureStream = new CaptureStream(null);
            response.Filter = captureStream;

            captureStream.Captured += (output) => {
                try
                {
                    if (response.StatusCode != 200)
                    {
                        Console.WriteLine(AField);
                        return;
                    }

                    Console.WriteLine(aContext.HttpContext.Request.Url.AbsolutePath);
                }
                finally
                {
                    ReleaseTheLock();
                }
            };

            captureHandlerIsAttached = true;
        }
        finally
        {
            if (!captureHandlerIsAttached)
                ReleaseTheLock();
        }
    }

    private void ReleaseTheLock()
    {
        if (AField != null && Monitor.IsEntered(AField))
        {
            Monitor.Exit(AField);
            AField = null;
        }
    }

    protected virtual bool HasUrl(IContext filterContext)
    {
        if (filterContext.HttpContext.Request.Url == null)
        {
            return false;
        }
        return true;
    }
}"
                    },
                },
                ExpectedDiagnostics =
                {
                    // Test0.cs(115,28): warning CA1062: In externally visible method 'void Class1.Method(IContext aContext)', validate parameter 'aContext' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(115, 28, "void Class1.Method(IContext aContext)", "aContext"),
                    // Test0.cs(156,13): warning CA1062: In externally visible method 'bool Class1.HasUrl(IContext filterContext)', validate parameter 'filterContext' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(156, 13, "bool Class1.HasUrl(IContext filterContext)", "filterContext"),
                }
            }.RunAsync();
        }

        [Fact]
        public async Task MakeNullAndMakeMayBeNullAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections;
using System.Collections.Generic;

public class Class1
{
    public virtual void M1(Class1 node, bool flag1, bool flag2)
    {
        foreach (var child in node.M2())
        {
            if (flag1)
            {
                if (flag2)
                {
                    M1(child);
                }
            }
            else if (flag2)
            {
                if (!flag1)
                {
                    M3(child);
                }
            }
        }
    }

    public virtual void M1(Class1 node)
    {
    }

    private Enumerator M2() => default(Enumerator);

    private void M3(object o) { }

    private struct Enumerator : IReadOnlyList<Class1>
    {
        public Class1 this[int index] => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public IEnumerator<Class1> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}",
            // Test0.cs(10,31): warning CA1062: In externally visible method 'void Class1.M1(Class1 node, bool flag1, bool flag2)', validate parameter 'node' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(10, 31, "void Class1.M1(Class1 node, bool flag1, bool flag2)", "node"));
        }

        [Fact]
        public async Task OutParameterAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(string s, bool b)
    {
        C2.M(s, b);
    }
}

internal class C2
{
    public static void M(string s, bool b)
    {
        char? unused;
        M(s, b, out unused);
    }

    public static void M(string s, bool b, out char? ch)
    {
        ch = null;
        while (b)
        {
            if (!string.IsNullOrEmpty(s))
            {
                ch = s[0];
            }
        }
    }
}");
        }

        [Fact]
        public async Task OutParameterAssert_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;
using System.IO;
using System.Text;

public class C
{
    public void M(Encoding encoding, Stream stream)
    {
        EncodingExtensions.GetMaxCharCountOrThrowIfHuge(encoding, stream);
    }
}

internal static class EncodingExtensions
{
    internal static int GetMaxCharCountOrThrowIfHuge(this Encoding encoding, Stream stream)
    {
        Debug.Assert(stream.CanSeek);
        long length = stream.Length;

        int maxCharCount;
        if (encoding.TryGetMaxCharCount(length, out maxCharCount))
        {
            return maxCharCount;
        }

        return 0;
    }

    internal static bool TryGetMaxCharCount(this Encoding encoding, long length, out int maxCharCount)
    {
        maxCharCount = 0;
        return false;
    }
}",
            // Test0.cs(10,67): warning CA1062: In externally visible method 'void C.M(Encoding encoding, Stream stream)', validate parameter 'stream' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(10, 67, "void C.M(Encoding encoding, Stream stream)", "stream"));
        }

        [Fact]
        public async Task GetValueOrDefaultAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct S
{
    public bool Flag;
    public object Old;
    public object New;

    public bool Equals(S other)
    {
        return this.Flag == other.Flag
            && (this.Old == null ? other.Old == null : this.Old.Equals(other.Old))
            && (this.New == null ? other.New == null : this.New.Equals(other.New));
    }
 
    public override bool Equals(object obj)
    {
        return obj is S && Equals((S)obj);
    }
}");
        }

        [Fact]
        public async Task GetValueOrDefaultAssert_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct S
{
    public object Node;
    public int Index;
    public S2 Token;

    public bool Equals(S other)
    {
        return Node == other.Node && Index == other.Index && Token.Equals(other.Token);
    }

    public override bool Equals(object obj)
    {
        return obj is S && Equals((S)obj);
    }
}

public struct S2
{
    public object Parent { get; } 
    internal object Node { get; } 
    internal int Index { get; } 
    internal int Position { get; }

    public bool Equals(S2 other)
    {
        return Parent == other.Parent &&
            Node == other.Node &&
            Position == other.Position &&
            Index == other.Index;
    }
}");
        }

        [Fact]
        public async Task GetValueAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct S
{
    public int Major { get; }
    public int Minor { get; }
    public static S None = new S();
    
    public bool Equals(S other)
    {
        return this.Major == other.Major && this.Minor == other.Minor;
    }

    public override bool Equals(object obj)
    {
        return obj is S && Equals((S)obj);
    }

    public bool IsValid => true;
}

public struct S2
{
    public S s { get; private set; }

    public bool IsNone(object o)
    {
        if (!s.Equals(S.None) && !s.IsValid)
        {
        }

        return true;
    }
}");
        }

        [Fact]
        public async Task SameFlowCaptureIdAcrossInterproceduralMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public bool Flag;
    public void M(C c, bool flag)
    {
        c = c ?? Create(flag);
    }

    public C Create(bool flag)
    {
        if (Flag == flag)
        {
            return this;
        }

        return new C() { Flag = flag };
    }
}");
        }

        [Fact]
        public async Task HashCodeClashForUnequalPointsToAbstractValues()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

internal static class FileUtilities
{
    internal static string ResolveRelativePath(string path, string baseDirectory)
    {
        return ResolveRelativePath(path, null, baseDirectory);
    }

    internal static string ResolveRelativePath(string path, string basePath, string baseDirectory)
    {
        Debug.Assert(baseDirectory == null || PathUtilities.IsAbsolute(baseDirectory));
        return ResolveRelativePath(PathKind.Empty, path, basePath, baseDirectory);
    }
 
    private static string ResolveRelativePath(PathKind kind, string path, string basePath, string baseDirectory)
    {
        baseDirectory = GetBaseDirectory(basePath, baseDirectory);
        return baseDirectory;
    }
 
    private static string GetBaseDirectory(string basePath, string baseDirectory)
    {
        string resolvedBasePath = ResolveRelativePath(basePath, baseDirectory);
        if (resolvedBasePath == null)
        {
            return baseDirectory;
        }

        return resolvedBasePath;
    }
}

internal enum PathKind
{
    Empty,
    Relative,
}

internal static class PathUtilities
{
    public static bool IsAbsolute(string path) => true;
}

public class C
{
    private string _baseDirectory;
    public string ResolveReference(string path, string baseFilePath, bool flag)
    {
        string resolvedPath;
 
        if (baseFilePath != null)
        {
            resolvedPath = FileUtilities.ResolveRelativePath(path, baseFilePath, _baseDirectory);
            Debug.Assert(resolvedPath == null || PathUtilities.IsAbsolute(resolvedPath));
            if (flag)
            {
                return resolvedPath;
            }
        }

        return null;
    }
}");
        }

        [Fact]
        public async Task AssignmentInTry_CatchWithThrow()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

public class C
{
    private C2 _c;

    private readonly Func<C2> _createC2;

    public void M1(C2 c1, bool flag)
    {
        C2 c2;
        try
        {
            c2 = (_createC2 != null) ? _createC2() : null;
        }
        catch (IOException ex) when (ex != null)
        {
            var message = flag ? null : """";
            throw new Exception(message);
        }

        _c = c2;
    }
}

public class C2
{
}
");
        }

        [Fact]
        public async Task AnalysisEntityWithIndexAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct C1
{
    public void M1(int index, C2 c2)
    {
        for (int i = 0; i < index; i++)
        {
            c2.M2(this[i]);
        }
    }

    public S this[int i]
    {
        get { return new S(); }
    }
}

public class C2
{
    public void M2(S s) { }
}

public struct S { }
",
            // Test0.cs(8,13): warning CA1062: In externally visible method 'void C1.M1(int index, C2 c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(8, 13, "void C1.M1(int index, C2 c2)", "c2"));
        }

        [Fact]
        public async Task NonMonotonicMergeAssert_FieldAllocatedInCallee()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public class C1
{
    public void M1(int index, int index2, C2 c2, S[] items)
    {
        for (int i = 0; i < index; i++)
        {
            c2.Add(this[i]);
        }

        c2.AddRange(items);

        for (int i = index; i < index2; i++)
        {
            c2.Add(this[i]);
        }
    }

    public S this[int i]
    {
        get { return new S(); }
    }
}

public class C2
{
    private S[] _nodes;
    private int _count;

    internal void AddRange(IEnumerable<S> items)
    {
        if (items != null)
        {
            foreach (var item in items)
            {
                this.Add(item);
            }
        }
    }
 
    internal void Add(S item)
    {
        if (_nodes == null || _count >= _nodes.Length)
        {
            this.Grow(_count == 0 ? 8 : _nodes.Length * 2);
        }
 
        _nodes[_count++] = item;
    }

    private void Grow(int size)
    {
        var tmp = new S[size];
        Array.Copy(_nodes, tmp, _nodes.Length);
        _nodes = tmp;
    }
}

public struct S { }
",
            // Test0.cs(11,13): warning CA1062: In externally visible method 'void C1.M1(int index, int index2, C2 c2, S[] items)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(11, 13, "void C1.M1(int index, int index2, C2 c2, S[] items)", "c2"));
        }

        [Fact]
        public async Task NonMonotonicMergeAssert_LValueFlowCatpure_ResetAcrossInterproceduralCall()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;

public class C<T> where T : class
{
    internal delegate T Factory();
    private readonly Factory _factory;

    public void M(string s, object o)
    {
        o = o ?? new object();
        AllocateSlow(s);
        var x = [|s|].Length;
    }

    private T AllocateSlow(string s)
    {
        if (s != null)
        {
            return null;
        }

        return _factory();
    }
}");
        }

        [Fact]
        public async Task YieldReturn_WithinLoop()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class C
{
    public string Path;
    public C[] Array;

    public IEnumerable<object> M(object o)
    {
        foreach (C item in Array)
        {
            yield return M2(item, o) ?? (C)new E(item.Path);
        }
    }

    private D M2(C item, object o)
    {
        var resolved = item.Path;
        if (resolved != null)
        {
            return new D();
        }

        return null;
    }
}

public class D : C
{
}

public class E : C
{
    public E(string s)
    {
        if (s == null)
        {
            throw new System.ArgumentNullException(nameof(s));
        }
    }
}");
        }

        [Fact]
        public async Task NonMonotonicMergeAssert_UnknownValueMerge()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;
using System.IO;

public class B
{
    internal C Node;

    public void WriteTo(TextWriter writer)
    {
        Node?.WriteTo(writer);
    }
}

public class C
{
    internal void WriteTo(TextWriter writer)
    {
        var stack = new Stack<(C node, bool leading, bool trailing)>();
        ProcessStack(writer, stack);
    }

    private static void ProcessStack(TextWriter writer, Stack<(C node, bool leading, bool trailing)> stack)
    {
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var currentNode = current.node;
            var currentLeading = current.leading;
            var currentTrailing = current.trailing;
        }
    }
}
");
        }

        [Fact]
        public async Task NonMonotonicMergeAssert_DefaultEntityEntryMissing()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class C
{
    private IEnumerable<Reference> References { get; }
    private string BaseDirectory { get; }
    public IEnumerable<BaseResolvedReference> ResolveReferences(Loader loader)
    {
        foreach (var reference in References)
        {
            yield return ResolveReference(reference, loader)
                ?? (BaseResolvedReference)new UnresolvedReference(reference.FilePath);
        }
    }

    private ResolvedReference ResolveReference(Reference reference, Loader loader)
    {
        string resolvedPath = FileUtilities.ResolveRelativePath(reference.FilePath);
        if (resolvedPath != null)
        {
            resolvedPath = FileUtilities.TryNormalizeAbsolutePath(resolvedPath);
        }

        if (resolvedPath != null)
        {
            return new ResolvedReference(resolvedPath, loader);
        }

        return null;
    }
}

public abstract class BaseResolvedReference { }
public class ResolvedReference : BaseResolvedReference {  public ResolvedReference(string path, Loader loader) { } }
public class UnresolvedReference : BaseResolvedReference { public UnresolvedReference(string path) { } }
public class Reference { public string FilePath { get; } }
public class Loader { }
internal static class FileUtilities
{
    public static string ResolveRelativePath(string path) => path.Length > 0 ? path : ""newPath"";
    public static string TryNormalizeAbsolutePath(string path) => ""newPath"";
}
");
        }

        [Fact]
        public async Task NonMonotonicMergeAssert_DefaultEntityEntryMissing_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public class C
{
    private GreenNode[] _nodes;
    private int _count;

    public void Add(IEnumerable<SyntaxNodeOrToken> nodeOrTokens)
    {
        foreach (var n in nodeOrTokens)
        {
            this.Add(n);
        }
    }

    private void Add(SyntaxNodeOrToken item)
    {
        var x = item.Token;
    }
}

public struct SyntaxNodeOrToken
{
    internal GreenNode Token { get; }
}

public class GreenNode { }
",
            // Test0.cs(12,27): warning CA1062: In externally visible method 'void C.Add(IEnumerable<SyntaxNodeOrToken> nodeOrTokens)', validate parameter 'nodeOrTokens' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(12, 27, "void C.Add(IEnumerable<SyntaxNodeOrToken> nodeOrTokens)", "nodeOrTokens"));
        }

        [Fact]
        public async Task ComparisonOfValueTypeCastToObjectWithNull()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(object p)
    {
        var s = new S();
        var o = (object)s;
        var y = o != null ? o.GetType().ToString() : p.ToString();
    }
}

public struct S { }",
            // Test0.cs(8,54): warning CA1062: In externally visible method 'void C.M(object p)', validate parameter 'p' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(8, 54, "void C.M(object p)", "p"));
        }

        [Fact]
        public async Task InvalidParentInstanceAssertForAnalysisEntity()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;

public struct S
{
    private readonly CancellationToken _cancellationToken;
    public void M(object obj, C c)
    {
        c?.M2(obj, _cancellationToken);
    }
}

public class C
{
    public void M2(object obj, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var x = obj.ToString();
    }
}",
            // Test0.cs(18,17): warning CA1062: In externally visible method 'void C.M2(object obj, CancellationToken cancellationToken)', validate parameter 'obj' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(18, 17, "void C.M2(object obj, CancellationToken cancellationToken)", "obj"));
        }

        [Fact]
        public async Task InvalidParentInstanceAssertForAnalysisEntity_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;

public struct S
{
    private readonly CancellationToken _cancellationToken;
    public void M(object obj)
    {
        C.M2(obj, _cancellationToken);
    }
}

internal static class C
{
    public static void M2(object obj, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var x = obj.ToString();
    }
}",
            // Test0.cs(9,14): warning CA1062: In externally visible method 'void S.M(object obj)', validate parameter 'obj' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(9, 14, "void S.M(object obj)", "obj"));
        }

        [Fact]
        public async Task IndexedEntityInstanceLocationMergeAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;
using System.Threading;

public class C
{
    public void M(string id, List<object> containers, int index, bool flag)
    {
        M2(id, containers, index, flag);
    }

    private void M2(string id, List<object> containers, int index, bool flag)
    {
        M3(id, containers, index, flag);
    }

    private void M3(string id, List<object> containers, int index, bool flag)
    {
        for (int i = 0, n = containers.Count; i < n; i++)
        {
            if (flag)
            {
                index++;
                var returnType = ParseTypeSymbol(id);

                if (returnType != null)
                {
                }
            }
        }
    }

    private object ParseTypeSymbol(string id)
    {
        var results = Allocate();
        try
        {
            M3(results);
            if (results.Count == 0)
            {
                return null;
            }
            else
            {
                return results[0];
            }
        }
        finally
        {
            Free(results);
        }
    }

    private void M3(List<object> results)
    {
        results.AddRange(new object[] { 1, 2 });
    }

    private struct Element
    {
        internal List<object> Value;
    }

    internal delegate List<object> Factory();
    private readonly Factory _factory;

    private List<object> _firstItem;
    private readonly Element[] _items;

    private List<object> Allocate()
    {
        var inst = _firstItem;
        if (inst == null || inst != Interlocked.CompareExchange(ref _firstItem, null, inst))
        {
            inst = AllocateSlow();
        }

        return inst;
    }

    private List<object> AllocateSlow()
    {
        var items = _items;

        for (int i = 0; i < items.Length; i++)
        {
            List<object> inst = items[i].Value;
            if (inst != null)
            {
                if (inst == Interlocked.CompareExchange(ref items[i].Value, null, inst))
                {
                    return inst;
                }
            }
        }

        return CreateInstance();
    }

    private List<object> CreateInstance()
    {
        var inst = _factory();
        return inst;
    }

    private static void Free(List<object> results) { }
}");
        }

        [Fact]
        public async Task CopyValueMergeAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(object obj, ref int index)
    {
        var startIndex = index;
        var endIndex = index;

        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                index = startIndex;

                if (i > j)
                {
                    endIndex = index;
                }
            }

            index = endIndex;
        }
    }
}");
        }

        [Fact]
        public async Task CopyValueInvalidResetDataAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public Func<bool> Predicate { get; }

    public void M(object o)
    {
        M2(this, Predicate);
    }

    private static void M2(C c, Func<bool> predicate)
    {
        M3(c, predicate);
    }

    private static void M3(C c, Func<bool> predicate)
    {
        M4(c, predicate);
    }

    private static void M4(C c, Func<bool> predicate)
    {
        for (int i = 0; i < 10; i++)
        {
            if (predicate())
            {
                return;
            }
        }
    }
}");
        }

        [Fact]
        public async Task CopyValueAddressSharedEntityAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void M(object o)
    {
        int xLocal;
        M2(o, out xLocal);
    }

    private void M2(object o, out int xParam)
    {
        xParam = 0;
        var x = o.ToString();
    }
}",
            // Test0.cs(9,12): warning CA1062: In externally visible method 'void C.M(object o)', validate parameter 'o' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(9, 12, "void C.M(object o)", "o"));
        }

        [Fact]
        public async Task CopyValueAddressSharedEntityAssert_RecursiveInvocations()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public void M(object o, int param)
    {
        int xLocal;
        int param2 = param;
        M2(o, out xLocal, ref param2);
    }

    private void M2(object o, out int xParam, ref int param2)
    {
        xParam = 0;
        if (param2 < 10)
        {
            param2++;
            M2(o, out xParam, ref param2);
        }

        var x = o.ToString();
    }
}",
            // Test0.cs(10,12): warning CA1062: In externally visible method 'void C.M(object o, int param)', validate parameter 'o' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(10, 12, "void C.M(object o, int param)", "o"));
        }

        [Fact]
        public async Task CopyValueTrackingEntityWithUnknownInstanceLocationAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public struct S { }
public class D
{
    public S S;
}

public class C
{
    public D D;
    private List<C> list;

    public void M(object o)
    {
        foreach (C c in list)
        {
            LocalFunction(ref c.D);
        }

        return;

        void LocalFunction(ref D d)
        {
            LocalFunction2(ref d.S);
        }

        void LocalFunction2(ref S s)
        {
        }
    }
}");
        }

        [Fact]
        public async Task RecursiveLocalFunctionInvocation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public class C
{
    public int Field;
    public object M(C c)
    {
        c = LocalFunction(c);
        return c;

        C LocalFunction(C c2)
        {
            if (c2.Field > 0)
            {
                c2 = LocalFunction(new C());
            }

            return c2;
        }
    }
}",
            // Test0.cs(15,17): warning CA1062: In externally visible method 'object C.M(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(15, 17, "object C.M(C c)", "c"));
        }

        [Fact]
        public async Task MultiChainedLocalFunctionInvocations()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

public class C
{
    public int Field;
    public object M(C c)
    {
        c = LocalFunction1(c);
        return c;

        C LocalFunction1(C c2)
        {
            return LocalFunction2(c2);
        }

        C LocalFunction2(C c2)
        {
            return LocalFunction3(c2);
        }

        C LocalFunction3(C c2)
        {
            if (c2.Field > 0)
            {
                c2 = LocalFunction3(new C());
            }

            return c2;
        }
    }
}");
        }

        [Fact]
        public async Task MultiChainedLambdaInvocations()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public object M(C c)
    {
        Func<C, C> lambda1 = (C c2) =>
        {
            return c2;
        };

        Func<C, C> lambda2 = (C c2) =>
        {
            return lambda1(c2);
        };

        Func<C, C> lambda3 = (C c2) =>
        {
            return lambda2(c2);
        };

        c = lambda3(c);
        return c;
    }
}");
        }

        [Fact]
        public async Task IsPatterExpression_UndefinedValueAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class C
{
    public void M(C c)
    {
        if (c is D d)
        {
            M2(d);
        }
    }

    private void M2(D d)
    {
    }
}

public class D : C { }
");
        }

        [WorkItem(1939, "https://github.com/dotnet/roslyn-analyzers/issues/1939")]
        [Fact]
        public async Task Issue1939()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Test
{
    public void M(bool b, int maxR, int maxC, object o)
    {
        Func<int, int, int> l = (r, c) => r * maxC + c;
        if (!b)
            l = (r, c) => c * maxR + r;
        for (int r = 0; r < maxR; r++)
        {
            for (int c = 0; c < maxC; c++)
            {
                int i = l(r, c);
            }
        }
    }
}
");
        }

        [Fact]
        public async Task CopyAnalysisGetTrimmedDataAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int Length { get; set; }
    public void M(C c)
    {
        int length1 = c.Length;
        M2(c);
    }

    private void M2(C c)
    {
        int length2 = c.Length;
        Console.WriteLine(length2);
    }
}",
            // Test0.cs(9,23): warning CA1062: In externally visible method 'void C.M(C c)', validate parameter 'c' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(9, 23, "void C.M(C c)", "c"));
        }

        [Fact]
        public async Task CopyAnalysisGetTrimmedDataAssert_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Text;

public class C
{
    public static C M(Stream stream)
    {
        var x = M2(stream);
        if (x >= 1000)
        {
            return M1(stream);
        }

        return null;
    }

    internal static C M1(Stream stream)
    {
        long longLength = stream.Length;
        M2(stream);
        return null;
    }

    internal static int M2(Stream stream)
    {
        long length = stream.Length;
        return 0;
    }
}
",
            // Test0.cs(10,20): warning CA1062: In externally visible method 'C C.M(Stream stream)', validate parameter 'stream' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(10, 20, "C C.M(Stream stream)", "stream"));
        }

        [Fact]
        public async Task CopyAnalysisFlowCaptureReturnValueAssert()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int Length { get; set; }
    public void M(S s1, S s2, bool flag, object obj)
    {
        switch (M2(s1, flag))
        {
            case Kind.Kind1:
                break;
            default:
                throw new Exception();
        }

        switch (M2(s2, flag))
        {
            case Kind.Kind2:
                break;
            default:
                throw new Exception();
        }
    }

    private static Kind M2(S s, bool flag)
    {
        return flag ? s.Kind : default(Kind);
    }
}

public struct S
{
    public Kind Kind { get; }
}

public enum Kind
{
    Kind1,
    Kind2
}");
        }

        [Fact]
        public async Task CopyAnalysisFlowCaptureReturnValueAssert_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    public int Length { get; set; }
    public void M(S s1, S s2, bool flag, object obj)
    {
        switch (M2(s1, flag))
        {
            case Kind.Kind1:
                break;
            default:
                throw new Exception();
        }

        switch (M2(s2, flag))
        {
            case Kind.Kind2:
                break;
            default:
                throw new Exception();
        }
    }

    private static Kind M2(S s, bool flag)
    {
        var x = flag ? s.Kind : default(Kind);
        return x;
    }
}

public struct S
{
    public Kind Kind { get; }
}

public enum Kind
{
    Kind1,
    Kind2
}");
        }

        [WorkItem(1943, "https://github.com/dotnet/roslyn-analyzers/issues/1943")]
        [Fact]
        public async Task Issue1943()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSystemWeb,
                TestState =
                {
                    Sources =
                    {
                        @"
using System.IO;
using System.Web;

namespace MyComments
{
    public interface ISomething
    {
        string Action(string s1, string s2, object o1);
    }

    public class Class
    {
        public static ISomething Something;
        public delegate string MyDelegate(string d, params object[] p);
        public MyDelegate T;

        public void M(HttpContext httpContext, TextWriter Output, int id, int count, int pendingCount)
        {
            var text = """";

            if (id != 0)
            {
                var totalCount = count + pendingCount;
                var totalText = T(""1 count"", ""{0} counts"", totalCount);
                if (totalCount == 0)
                {
                    text += totalText.ToString();
                }
                else
                {
                    text +=
                        Something.Action(
                            totalText.ToString(),
                            ""Details"",
                            new
                            {
                                id = id,
                                returnUrl = [|httpContext|].Request.Url
                            });
                }

                if (pendingCount > 0)
                {
                    text += "" "" + Something.Action(
                        T(""({0} pending)"", pendingCount).ToString(),
                        ""Details"",
                        new
                        {
                            id = id,
                            returnUrl = httpContext.Request.Url
                        });
                }
            }

            [|Output|].Write(text);
        }
    }
}
"
                    },
                }
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task CopyAnalysisAssert_AddressSharedOutParam()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(C c)
    {
        M(out c);

        if (c == default(C))
        {
        }
    }

    private void M<T>(out T t)
    {
        t = default(T);
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.CopyAnalysis)]
        [Fact]
        public async Task CopyAnalysisAssert_ApplyInterproceduralResult()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SyntaxNode
{
    public SyntaxNode Parent { get; }
    private bool _flag;
    private bool M2() => _flag;
    public int RawKind { get; }

    public static bool M(SyntaxNode node)
    {
        var parent = node.Parent;
        if (parent == null || !node.M2())
        {
            return false;
        }

        switch (parent.Kind())
        {
            case SyntaxKind.Kind1:
                var d = (QualifiedNameSyntax)parent;
                return d.Right == node ? M(parent) : false;

            case SyntaxKind.Kind2:
                var e = (AliasQualifiedNameSyntax)parent;
                return e.Name == node ? M(parent) : false;
        }

        var f = node.Parent as AttributeSyntax;
        return f != null && f.Name == node;
    }
}

public static class Extensions
{
    public static SyntaxKind Kind(this SyntaxNode node)
    {
        var rawKind = node.RawKind;
        return IsCSharpKind(rawKind) ? (SyntaxKind)rawKind : SyntaxKind.Kind4;
    }

    private static bool IsCSharpKind(int rawKind)
        => rawKind > 0;
}

public enum SyntaxKind
{
    Kind1,
    Kind2,
    Kind3,
    Kind4
}

public class NameSyntax : SyntaxNode
{
}

public class SimpleNameSyntax : NameSyntax
{
}

public class QualifiedNameSyntax : NameSyntax
{
    public SimpleNameSyntax Right { get; }
}

public class AliasQualifiedNameSyntax : NameSyntax
{
    public SimpleNameSyntax Name { get; }
}

public class AttributeSyntax : CSharpSyntaxNode
{
    public SimpleNameSyntax Name { get; }
}

public class CSharpSyntaxNode : SyntaxNode
{
}
",
            // Test0.cs(11,22): warning CA1062: In externally visible method 'bool SyntaxNode.M(SyntaxNode node)', validate parameter 'node' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(11, 22, "bool SyntaxNode.M(SyntaxNode node)", "node"),
            // Test0.cs(37,23): warning CA1062: In externally visible method 'SyntaxKind Extensions.Kind(SyntaxNode node)', validate parameter 'node' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(37, 23, "SyntaxKind Extensions.Kind(SyntaxNode node)", "node"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2339, "https://github.com/dotnet/roslyn-analyzers/issues/2339")]
        public async Task ParameterReassignedAfterNullCheck()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public static class C
{
    public static string AsString(this Exception exception)
    {
        if (exception == null)
        {
            return null;
        }

        while (exception.InnerException != null)
        {
            exception = exception.InnerException;
        }

        return exception.Message;
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2327, "https://github.com/dotnet/roslyn-analyzers/issues/2327")]
        public async Task ForEachLoopsAfterNullCheck()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.ObjectModel;

public static class MainClass
{
    public static void Initialize(ModelsCollection models)
    {
        if (models == null) throw new ArgumentNullException(nameof(models));

        foreach (var item in models)
        {
            Console.WriteLine(item.Name);
        }

        foreach (var item in models)
        {
            Console.WriteLine(item.Id);
        }
    }
}

public class ModelsCollection : ObservableCollection<Model>
{
}

public class Model
{
    public int Id { get; set; }
    public string Name { get; set; }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2280, "https://github.com/dotnet/roslyn-analyzers/issues/2280")]
        public async Task ConditionalAssignmentAfterNullCheck()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Node
{
    public Node Parent { get; set; }

    public static Node M(Node node, bool flag)
    {
        if (node == null) { throw new ArgumentNullException(nameof(node)); }

        var parent = flag ? node.Parent : node;
        return parent.Parent; // CA1062 is reported on `parent`
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2276, "https://github.com/dotnet/roslyn-analyzers/issues/2276")]
        public async Task AssignedArrayEmptyOnNullPath()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Data.Common;

public class TableSet
{
    public TableSet(DbDataReader reader, params string[] tableNames)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));
        if (tableNames == null)
            tableNames = Array.Empty<string>();

        var index = 0;
        do
        {
            var tableName = (index < tableNames.Length) ? tableNames[index] : (""Table "" + index);
            index += 1;
        }
        while (reader.NextResult());
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory, WorkItem(2275, "https://github.com/dotnet/roslyn-analyzers/issues/2275")]
        [InlineData("IsNullOrWhiteSpace")]
        [InlineData("IsNullOrEmpty")]
        public async Task StringNullCheckApis(string apiName)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System.Globalization;

public class C
{{
    public static readonly char[] s_InvalidCharacters = new[] {{ 'a', 'b' }};

    public string Name {{ get; }}
    public C(string name)
    {{
        Name = M1(name);
    }}

    private static string M1(string name)
    {{
        if (string.{apiName}(name))
            return null;

        string result = name;
        foreach (char c in s_InvalidCharacters)
        {{
            result = result.Replace(c.ToString(CultureInfo.InvariantCulture), """");
        }}

        if (!char.IsLetter(result[0]) && result[0] != '_')
            result = ""_"" + result;

        return result;
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory, WorkItem(2369, "https://github.com/dotnet/roslyn-analyzers/issues/2369")]
        [InlineData("IsNullOrWhiteSpace")]
        [InlineData("IsNullOrEmpty")]
        public async Task StringNullCheckApis_02(string apiName)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;

public class C
{{
    public static void A(string input)
    {{
        if (string.{apiName}(input))
        {{
            throw new ArgumentException(""Invalid input"", nameof(input));
        }}

        B(input);
    }}

    private static void B(string input)
    {{
        var x = input.Length;
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory, WorkItem(2369, "https://github.com/dotnet/roslyn-analyzers/issues/2369")]
        [InlineData("IsNullOrWhiteSpace")]
        [InlineData("IsNullOrEmpty")]
        public async Task StringNullCheckApis_03(string apiName)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;

public class C
{{
    public static void A(string input)
    {{
        if (!string.{apiName}(input))
        {{
            B(input);
        }}
    }}

    private static void B(string input)
    {{
        var x = input.Length;
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2582, "https://github.com/dotnet/roslyn-analyzers/issues/2582")]
        public async Task StringEmptyFieldIsNonNull()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;

public class Class1
{{
    public Class1(int num) {{ }}

    public Class1(string name)
        : this((name ?? string.Empty).Length) // Ensure no CA1062 here
    {{
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2582, "https://github.com/dotnet/roslyn-analyzers/issues/2582")]
        public async Task ArrayEmptyMethodIsNonNull()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;

public class Class1
{{
    public Class1(int num) {{ }}

    public Class1(int[] arr)
        : this((arr ?? Array.Empty<int>()).Length) // Ensure no CA1062 here
    {{
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2582, "https://github.com/dotnet/roslyn-analyzers/issues/2582")]
        public async Task ImmutableCreationMethodIsNonNull()
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System.Collections.Immutable;

public class Class1
{{
    public Class1(int num) {{ }}

    public Class1(ImmutableDictionary<int, int> map)
        : this((map ?? ImmutableDictionary.Create<int, int>()).Count) // Ensure no CA1062 here
    {{
    }}

    public Class1(ImmutableHashSet<int> set)
        : this((set ?? ImmutableHashSet.CreateRange(new[] {{ 1, 2 }})).Count) // Ensure no CA1062 here
    {{
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        public async Task NamedArgumentInDifferentOrder()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(C c1, C c2)
    {
        if (c1 == null)
        {
            return;
        }

        // We known c1 is non-null.
        // But c2 may still be non-null.
        M2(c2: c2, c1: c1);
    }

    private void M2(C c1, C c2)
    {
        // We known c1 is non-null.
        // But c2 may still be non-null.
        c2.M(null, null);
    }
}",
            // Test0.cs(13,12): warning CA1062: In externally visible method 'void C.M(C c1, C c2)', validate parameter 'c2' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(13, 12, "void C.M(C c1, C c2)", "c2"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact]
        [WorkItem(2528, "https://github.com/dotnet/roslyn-analyzers/issues/2528")]
        [WorkItem(3845, "https://github.com/dotnet/roslyn-analyzers/issues/3845")]
        public async Task ParamArrayIsFlagged()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(params int[] p)
    {
        var x = [|p|].Length;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub M(ParamArray p As Integer())
        Dim x = [|p|].Length
    End Sub
End Class
");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2269, "https://github.com/dotnet/roslyn-analyzers/issues/2269")]
        public async Task ProtectedMemberOfSealedClassNotFlagged()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public abstract class A
{
    public bool CheckMe() => IsType(GetType());

    protected abstract bool IsType(Type type);
}

public sealed class B : A
{
    protected override bool IsType(Type type) => type.Namespace == nameof(System);
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2526, "https://github.com/dotnet/roslyn-analyzers/issues/2526")]
        public async Task CheckedWithConditionalAccess_01()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class C
{
    public bool Flag;
    public void M(C c)
    {
        if (c?.Flag == true)
        {
          var x = c.ToString();
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2526, "https://github.com/dotnet/roslyn-analyzers/issues/2526")]
        public async Task CheckedWithConditionalAccess_02()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class C
{
    public bool M(List<string> list)
    {
        return list?.Count > 5 && list.Count < 10;
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2586, "https://github.com/dotnet/roslyn-analyzers/issues/2586")]
        public async Task CheckedWithConditionalAccess_03()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class C
{
    public bool Flag;
    public void M(C c)
    {
        switch (c?.Flag)
        {
            case true:
                var x = c.ToString();
                break;
            case null:
                var y = c.ToString();
                break;
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2630, "https://github.com/dotnet/roslyn-analyzers/issues/2630")]
        public async Task IsPatternInConditionalExpression_01_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public static void DoSomething(object input)
    {
        // Ensure no diagnostic here.
        SomeMethod(input);
    }

    private static void SomeMethod(object input)
    {
        if (input is Class1)
        {
            var c = (Class1)input;
            c.ToString();
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2630, "https://github.com/dotnet/roslyn-analyzers/issues/2630")]
        public async Task IsPatternInConditionalExpression_01_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public static void DoSomething(object input)
    {
        // Ensure diagnostic here.
        SomeMethod(input);
    }

    private static void SomeMethod(object input)
    {
        if (input is Class1)
        {
            var c = (Class1)input;
            c.ToString();
            return;
        }

        var c2 = (Class1)input;
        c2.ToString();
    }
}",
            // Test0.cs(7,13): warning CA1062: In externally visible method 'void Class1.DoSomething(object input)', validate parameter 'input' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
            GetCSharpResultAt(7, 20, "void Class1.DoSomething(object input)", "input"));
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(2630, "https://github.com/dotnet/roslyn-analyzers/issues/2630")]
        public async Task IsPatternInConditionalExpression_02_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public static void DoSomething(object input)
    {
        // Ensure no diagnostic here.
        SomeMethod(input);
    }

    private static void SomeMethod(object input)
    {
        if (input is Class1 c)
        {
            c.ToString();
        }
    }
}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory, CombinatorialData, WorkItem(3716, "https://github.com/dotnet/roslyn-analyzers/issues/3716")]
        public async Task IsPatternInConditionalExpression_03_NoDiagnostic(bool discardPattern)
        {
            var local = discardPattern ? "_" : "c";
            await VerifyCS.VerifyAnalyzerAsync($@"
public class Class1
{{
    public static void M1(object input)
    {{
        if (input is Class1 {local})
        {{
            input.ToString();
        }}
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory, CombinatorialData, WorkItem(3716, "https://github.com/dotnet/roslyn-analyzers/issues/3716")]
        public async Task IsPatternInConditionalExpression_04_NoDiagnostic(bool discardPattern)
        {
            var local1 = discardPattern ? "_" : "c";
            var local2 = discardPattern ? "_" : "d";

            await VerifyCS.VerifyAnalyzerAsync($@"
public class Class1
{{
    public static void M1(object input)
    {{
        if (input is Class1 {local1})
        {{
            input.ToString();
        }}
        else if (input is Class2 {local2})
        {{
            input.ToString();
        }}
    }}
}}

public class Class2 {{ }}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Theory, CombinatorialData, WorkItem(3716, "https://github.com/dotnet/roslyn-analyzers/issues/3716")]
        public async Task IsPatternInConditionalExpression_05_NoDiagnostic(bool discardPattern)
        {
            var local = discardPattern ? "_" : "c";
            await VerifyCS.VerifyAnalyzerAsync($@"
public class Class1
{{
    public static void M1(object input)
    {{
        if (!(input is Class1 {local}) || input.ToString() == """")
        {{
        }}
    }}
}}");
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(3716, "https://github.com/dotnet/roslyn-analyzers/issues/3716")]
        public async Task RecursivePatternInConditionalExpression_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class Class1
{
    public int X { get; }
    public static void M1(object input)
    {
        if (input is Class1 { X: 0 })
        {
            input.ToString();
        }
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task IsNullPatternInConditionalExpression_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class Class1
{
    public int X { get; }
    public static void M1(object input)
    {
        if (input is null)
        {
            return;
        }

        input.ToString();
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task NegationPatternInConditionalExpression_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class Class1
{
    public int X { get; }
    public static void M1(object input)
    {
        if (input is not null)
        {
            input.ToString();
        }
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Trait(Traits.DataflowAnalysis, Traits.Dataflow.NullAnalysis)]
        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task RelationalPatternInConditionalExpression_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class Class1
{
    public int X { get; }
    public static void M1(object input)
    {
        if (input is > 10)
        {
            input.ToString();
        }
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact, WorkItem(3049, "https://github.com/dotnet/roslyn-analyzers/issues/3049")]
        public async Task SwitchStatement_PatternMatchingNullCheck()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class Test
{
    public static string M(Test test)
    {
        switch (test)
        {
            case null:
                throw new ArgumentNullException(nameof(test));
            default:
                return test.ToString();
        }
    }
}");
        }

        [Fact, WorkItem(3049, "https://github.com/dotnet/roslyn-analyzers/issues/3049")]
        public async Task SwitchExpression_PatternMatchingNullCheck()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;

public class Test
{
    public static string M(Test test)
    {
        return test switch
        {
            null => throw new ArgumentNullException(nameof(test)),
            _ => test.ToString()
        };
    }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task SwitchStatement_PatternMatchingNotNullCheck()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;

public class Test
{
    public static string M(Test test)
    {
        switch (test)
        {
            case not null:
                return test.ToString();
            default:
                throw new ArgumentNullException(nameof(test));
        }
    }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task SwitchExpression_PatternMatchingNotNullCheck()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;

public class Test
{
    public static string M(Test test)
    {
        return test switch
        {
            not null => test.ToString(),
            _ => throw new ArgumentNullException(nameof(test))
        };
    }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Fact, WorkItem(4056, "https://github.com/dotnet/roslyn-analyzers/issues/4056")]
        public async Task SwitchExpression_PatternMatchingRelationalPatternCheck()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;

public class Test
{
    public static string M(object test)
    {
        return test switch
        {
            > 10 => test.ToString(),
            _ => throw new ArgumentNullException(nameof(test))
        };
    }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9
            }.RunAsync();
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.CA1062.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.CA1062.excluded_symbol_names = M*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = M1")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length == 0)
            {
                expected = new[]
                {
                    // Test0.cs(6,17): warning CA1062: In externally visible method 'void Test.M1(string str)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(6, 17, "void Test.M1(string str)", "str")
                };
            }

            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    public void M1(string str)
    {
        var x = str.ToString();
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };
            csTest.ExpectedDiagnostics.AddRange(expected);
            await csTest.RunAsync();

            expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length == 0)
            {
                expected = new[]
                {
                    // Test0.vb(4,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetBasicResultAt(4, 17, "Sub Test.M1(str As String)", "str")
                };
            }

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class Test
    Public Sub M1(str As String)
        Dim x = str.ToString()
    End Sub
End Class
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };
            vbTest.ExpectedDiagnostics.AddRange(expected);
            await vbTest.RunAsync();
        }

        [Theory]
        [WorkItem(2691, "https://github.com/dotnet/roslyn-analyzers/issues/2691")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.exclude_extension_method_this_parameter = true")]
        [InlineData("dotnet_code_quality." + ValidateArgumentsOfPublicMethods.RuleId + ".exclude_extension_method_this_parameter = true")]
        [InlineData("dotnet_code_quality.dataflow.exclude_extension_method_this_parameter = true")]
        public async Task EditorConfigConfiguration_ExcludeExtensionMethodThisParameterOption(string editorConfigText)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length == 0)
            {
                expected = new[]
                {
                    // Test0.cs(6,17): warning CA1062: In externally visible method 'void Test.M1(string str)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetCSharpResultAt(6, 17, "void Test.M1(string str)", "str")
                };
            }

            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public static class Test
{
    public static void M1(this string str)
    {
        var x = str.ToString();
    }
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };
            csTest.ExpectedDiagnostics.AddRange(expected);
            await csTest.RunAsync();

            expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length == 0)
            {
                expected = new[]
                {
                    // Test0.vb(7,17): warning CA1062: In externally visible method 'Sub Test.M1(str As String)', validate parameter 'str' is non-null before using it. If appropriate, throw an ArgumentNullException when the argument is null or add a Code Contract precondition asserting non-null argument.
                    GetBasicResultAt(7, 17, "Sub Test.M1(str As String)", "str")
                };
            }

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System.Runtime.CompilerServices

Public Module Test
    <Extension()>
    Public Sub M1(str As String)
        Dim x = str.ToString()
    End Sub
End Module"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };
            vbTest.ExpectedDiagnostics.AddRange(expected);
            await vbTest.RunAsync();
        }

        [Fact, WorkItem(2919, "https://github.com/dotnet/roslyn-analyzers/issues/2919")]
        public async Task Interprocedural_DelegateInvocation_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace ReproCA1062
{
    public static class Repro
    {
        public static void Test(string foo)
        {
            NotNull(foo, nameof(foo));
            int x = foo.Length; // no warning
            Bar();
            x = foo.Length; // CA1062 on foo
        }

        public static void Bar()
        {
            Action<int> a = x => { };
            a(0);
        }

        public static void NotNull([ValidatedNotNull] object param, string paramName)
        {
            if (param == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }

    [System.AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class ValidatedNotNullAttribute : Attribute { }
}");
        }

        [Fact, WorkItem(3437, "https://github.com/dotnet/roslyn-analyzers/issues/3437")]
        public async Task ReDim_FirstInstruction_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub GetValues(ByRef Values() As String)
        ReDim Values(0)
        Values(0) = ""Test Value""
    End Sub
End Class");
        }

        [Fact, WorkItem(3437, "https://github.com/dotnet/roslyn-analyzers/issues/3437")]
        public async Task ReDim_FirstInstructionMultipleVariables_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub GetValues(ByRef Values() As String, ByRef OtherValues() As String)
        ReDim Values(0), OtherValues(0)
        Values(0) = ""Test Value""
        OtherValues(0) = Values(0)
    End Sub
End Class");
        }

        [Fact, WorkItem(3437, "https://github.com/dotnet/roslyn-analyzers/issues/3437")]
        public async Task ReDim_ParameterAccessFirst_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub GetValues(ByRef Values() As String)
        Values(0) = ""Test Value""
        ReDim Values(0)
        Values(0) = ""Test Value""
    End Sub
End Class",
                GetBasicResultAt(4, 9, "Sub C.GetValues(ByRef Values As String())", "Values"));
        }

        [Fact, WorkItem(3899, "https://github.com/dotnet/roslyn-analyzers/issues/3899")]
        public async Task IsNotNullPattern_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
                TestCode = @"
public class C
{
    public void M(object instance)
    {
        if (instance is { })
        {
            _ = instance.GetHashCode();
        }
    }
}",
            }.RunAsync();
        }

        [Fact, WorkItem(3634, "https://github.com/dotnet/roslyn-analyzers/issues/3634")]
        public async Task NullConditionalAssignmentOperator_NullableEnableContext_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
#nullable enable
using System.Collections.Generic;

public class C
{
    public void ParameterTest(Dictionary<string, string>? dict = null)
    {
        dict ??= new Dictionary<string, string>();
        SetParameter(dict);
    }

    private void SetParameter(Dictionary<string, string> dict)
    {
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8
            }.RunAsync();
        }

        [Fact, WorkItem(3634, "https://github.com/dotnet/roslyn-analyzers/issues/3634")]
        public async Task NullConditionalAssignmentOperator_NonNullableEnableContext_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System.Collections.Generic;

public class C
{
    public void ParameterTest(Dictionary<string, string> dict = null)
    {
        dict ??= new Dictionary<string, string>();
        SetParameter(dict);
    }

    private void SetParameter(Dictionary<string, string> dict)
    {
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8
            }.RunAsync();
        }

    }
}
