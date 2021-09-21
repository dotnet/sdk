// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.PreferJaggedArraysOverMultidimensionalAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpPreferJaggedArraysOverMultidimensionalFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.PreferJaggedArraysOverMultidimensionalAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicPreferJaggedArraysOverMultidimensionalFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class PreferJaggedArraysOverMultidimensionalTests
    {
        [Fact]
        public async Task CSharpSimpleMembersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public int[,] MultidimensionalArrayField;
    
    public int[,] MultidimensionalArrayProperty
    {
        get { return null; }
    }

    public int[,] MethodReturningMultidimensionalArray()
    {
        return null;
    }

    public void MethodWithMultidimensionalArrayParameter(int[,] multidimensionalParameter) { }

    public void MethodWithMultidimensionalArrayCode()
    {
        int[,] multiDimVariable = new int[5, 5];
        multiDimVariable[1, 1] = 3;
    }

    public int[,][] JaggedMultidimensionalField;
}

public interface IInterface
{
    int[,] InterfaceMethod(int[,] array);
}
",
            GetCSharpDefaultResultAt(4, 19, "MultidimensionalArrayField"),
            GetCSharpDefaultResultAt(6, 19, "MultidimensionalArrayProperty"),
            GetCSharpReturnResultAt(11, 19, "MethodReturningMultidimensionalArray", "int[*,*]"),
            GetCSharpDefaultResultAt(16, 65, "multidimensionalParameter"),
            GetCSharpBodyResultAt(20, 35, "MethodWithMultidimensionalArrayCode", "int[*,*]"),
            GetCSharpDefaultResultAt(24, 21, "JaggedMultidimensionalField"),
            GetCSharpReturnResultAt(29, 12, "InterfaceMethod", "int[*,*]"),
            GetCSharpDefaultResultAt(29, 35, "array"));
        }

        [Fact]
        public async Task BasicSimpleMembersAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Public MultidimensionalArrayField As Integer(,)

    Public ReadOnly Property MultidimensionalArrayProperty As Integer(,)
        Get
            Return Nothing
        End Get
    End Property

    Public Function MethodReturningMultidimensionalArray() As Integer(,)
        Return Nothing
    End Function

    Public Sub MethodWithMultidimensionalArrayParameter(multidimensionalParameter As Integer(,))
    End Sub

    Public Sub MethodWithMultidimensionalArrayCode()
        Dim multiDimVariable(5, 5) As Integer
        multiDimVariable(1, 1) = 3
    End Sub

    Public JaggedMultidimensionalField As Integer(,)()
End Class

Public Interface IInterface
    Function InterfaceMethod(array As Integer(,)) As Integer(,)
End Interface
",
            GetBasicDefaultResultAt(3, 12, "MultidimensionalArrayField"),
            GetBasicDefaultResultAt(5, 30, "MultidimensionalArrayProperty"),
            GetBasicReturnResultAt(11, 21, "MethodReturningMultidimensionalArray", "Integer(*,*)"),
            GetBasicDefaultResultAt(15, 57, "multidimensionalParameter"),
            GetBasicBodyResultAt(19, 13, "MethodWithMultidimensionalArrayCode", "Integer(*,*)"),
            GetBasicDefaultResultAt(23, 12, "JaggedMultidimensionalField"),
            GetBasicReturnResultAt(27, 14, "InterfaceMethod", "Integer(*,*)"),
            GetBasicDefaultResultAt(27, 30, "array"));
        }

        [Fact]
        public async Task CSharpNoDiagosticsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public int[][] JaggedArrayField;
    
    public int[][] JaggedArrayProperty
    {
        get { return null; }
    }

    public int[][] MethodReturningJaggedArray()
    {
        return null;
    }

    public void MethodWithJaggedArrayParameter(int[][] jaggedParameter) { }
}
");
        }

        [Fact]
        public async Task BasicNoDiangnosticsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Public JaggedArrayField As Integer()()

    Public ReadOnly Property JaggedArrayProperty As Integer()()
        Get
            Return Nothing
        End Get
    End Property

    Public Function MethodReturningJaggedArray() As Integer()()
        Return Nothing
    End Function

    Public Sub MethodWithJaggedArrayParameter(jaggedParameter As Integer()())
    End Sub
End Class
");
        }

        [Fact]
        public async Task CSharpOverridenMembersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public virtual int[,] MultidimensionalArrayProperty
    {
        get { return null; }
    }

    public virtual int[,] MethodReturningMultidimensionalArray()
    {
        return null;
    }
}

public class Class2 : Class1
{
    public override int[,] MultidimensionalArrayProperty
    {
        get { return null; }
    }

    public override int[,] MethodReturningMultidimensionalArray()
    {
        return null;
    }
}
",
            GetCSharpDefaultResultAt(4, 27, "MultidimensionalArrayProperty"),
            GetCSharpReturnResultAt(9, 27, "MethodReturningMultidimensionalArray", "int[*,*]"));
        }

        [Fact]
        public async Task BasicOverriddenMembersAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Public Overridable ReadOnly Property MultidimensionalArrayProperty As Integer(,)
        Get
            Return Nothing
        End Get
    End Property

    Public Overridable Function MethodReturningMultidimensionalArray() As Integer(,)
        Return Nothing
    End Function
End Class

Public Class Class2
    Inherits Class1
    Public Overrides ReadOnly Property MultidimensionalArrayProperty As Integer(,)
        Get
            Return Nothing
        End Get
    End Property

    Public Overrides Function MethodReturningMultidimensionalArray() As Integer(,)
        Return Nothing
    End Function
End Class
",
            GetBasicDefaultResultAt(3, 42, "MultidimensionalArrayProperty"),
            GetBasicReturnResultAt(9, 33, "MethodReturningMultidimensionalArray", "Integer(*,*)"));
        }

        [Fact, WorkItem(3650, "https://github.com/dotnet/roslyn-analyzers/issues/3650")]
        public async Task Method_WhenInterfaceImplementation_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface IC
{
    int[,] {|#0:MethodReturningMultidimensionalArray|}(int[,] {|#1:array|});
}

public class C : IC
{
    public int[,] MethodReturningMultidimensionalArray(int[,] array)
        => null;
}",
                VerifyCS.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.ReturnRule).WithLocation(0).WithArguments("MethodReturningMultidimensionalArray", "int[*,*]"),
                VerifyCS.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.DefaultRule).WithLocation(1).WithArguments("array"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface IC
    Function {|#0:MethodReturningMultidimensionalArray|}(ByVal {|#1:array|} As Integer(,)) As Integer(,)
End Interface

Public Class C
    Implements IC

    Public Function MethodReturningMultidimensionalArray(ByVal array As Integer(,)) As Integer(,) Implements IC.MethodReturningMultidimensionalArray
        Return Nothing
    End Function
End Class
",
                VerifyVB.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.ReturnRule).WithLocation(0).WithArguments("MethodReturningMultidimensionalArray", "Integer(*,*)"),
                VerifyVB.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.DefaultRule).WithLocation(1).WithArguments("array"));
        }

        [Fact, WorkItem(3650, "https://github.com/dotnet/roslyn-analyzers/issues/3650")]
        public async Task Property_WhenInterfaceImplementation_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface IC
{
    int[,] {|#0:MultidimensionalArrayProperty|} { get; }
}

public class C : IC
{
    public int[,] MultidimensionalArrayProperty { get; set; }
}",
                VerifyCS.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.DefaultRule).WithLocation(0).WithArguments("MultidimensionalArrayProperty"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface IC
    ReadOnly Property {|#0:MultidimensionalArrayProperty|} As Integer(,)
End Interface

Public Class C
    Implements IC

    Public Property MultidimensionalArrayProperty As Integer(,) Implements IC.MultidimensionalArrayProperty
End Class
",
                VerifyVB.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.DefaultRule).WithLocation(0).WithArguments("MultidimensionalArrayProperty"));
        }

        private static DiagnosticResult GetCSharpDefaultResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private static DiagnosticResult GetCSharpReturnResultAt(int line, int column, string symbolName, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.ReturnRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName, typeName);

        private static DiagnosticResult GetCSharpBodyResultAt(int line, int column, string symbolName, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.BodyRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName, typeName);

        private static DiagnosticResult GetBasicDefaultResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private static DiagnosticResult GetBasicReturnResultAt(int line, int column, string symbolName, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.ReturnRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName, typeName);

        private static DiagnosticResult GetBasicBodyResultAt(int line, int column, string symbolName, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(PreferJaggedArraysOverMultidimensionalAnalyzer.BodyRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName, typeName);
    }
}