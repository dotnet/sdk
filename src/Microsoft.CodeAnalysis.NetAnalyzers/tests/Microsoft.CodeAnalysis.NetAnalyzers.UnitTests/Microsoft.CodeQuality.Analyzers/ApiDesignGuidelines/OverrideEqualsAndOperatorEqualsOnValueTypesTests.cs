// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpOverrideEqualsAndOperatorEqualsOnValueTypesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicOverrideEqualsAndOperatorEqualsOnValueTypesFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OverrideEqualsAndOperatorEqualsOnValueTypesTests
    {
        [Fact]
        public async Task CSharpDiagnosticForBothEqualsAndOperatorEqualsOnStructAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct A
{
    public int X;
}",
                GetCSharpOverrideEqualsDiagnostic(2, 15, "A"),
                GetCSharpOperatorEqualsDiagnostic(2, 15, "A"));
        }

        [WorkItem(895, "https://github.com/dotnet/roslyn-analyzers/issues/895")]
        [Fact]
        public async Task CSharpNoDiagnosticForInternalAndPrivateStructAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal struct A
{
    public int X;
}

public class B
{
    private struct C
    {
        public int X;
    }
}
");
        }

        [WorkItem(899, "https://github.com/dotnet/roslyn-analyzers/issues/899")]
        [Fact]
        public async Task CSharpNoDiagnosticForEnumAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public enum E
{
    F = 0
}
");
        }

        [WorkItem(899, "https://github.com/dotnet/roslyn-analyzers/issues/899")]
        [Fact]
        public async Task CSharpNoDiagnosticForStructsWithoutMembersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct EmptyStruct
{
}
");
        }

        [WorkItem(899, "https://github.com/dotnet/roslyn-analyzers/issues/899")]
        [Fact]
        public async Task CSharpNoDiagnosticForEnumeratorsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections;

public struct MyEnumerator : System.Collections.IEnumerator
{
    public object Current
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    public bool MoveNext()
    {
        throw new NotImplementedException();
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }
}

public struct MyGenericEnumerator<T> : System.Collections.Generic.IEnumerator<T>
{
    public T Current
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    object IEnumerator.Current
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public bool MoveNext()
    {
        throw new NotImplementedException();
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForEqualsOrOperatorEqualsOnClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public int X;
}");
        }

        [Fact]
        public async Task CSharpNoDiagnosticWhenStructImplementsEqualsAndOperatorEqualsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct A
{
    public override bool Equals(object other)
    {
        return true;
    }

    public static bool operator==(A left, A right)
    {
        return true;
    }

    public static bool operator!=(A left, A right)
    {
        return true;
    }
}");
        }

        [Fact]
        public async Task CSharpDiagnosticWhenEqualsHasWrongSignatureAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct A
{
    public bool Equals(A other)
    {
        return true;
    }

    public static bool operator==(A left, A right)
    {
        return true;
    }

    public static bool operator!=(A left, A right)
    {
        return true;
    }
}",
                GetCSharpOverrideEqualsDiagnostic(2, 15, "A"));
        }

        [Fact]
        public async Task CSharpDiagnosticWhenEqualsIsNotAnOverrideAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct A
{
    public new bool Equals(object other)
    {
        return true;
    }

    public static bool operator==(A left, A right)
    {
        return true;
    }

    public static bool operator!=(A left, A right)
    {
        return true;
    }
}",
                GetCSharpOverrideEqualsDiagnostic(2, 15, "A"));
        }

        [Fact]
        public async Task BasicDiagnosticsForEqualsOnStructureAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure A
    Public X As Integer
End Structure
",
                GetBasicOverrideEqualsDiagnostic(2, 18, "A"),
                GetBasicOperatorEqualsDiagnostic(2, 18, "A"));
        }

        [WorkItem(895, "https://github.com/dotnet/roslyn-analyzers/issues/895")]
        [Fact]
        public async Task BasicNoDiagnosticsForInternalAndPrivateStructureAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Structure A
    Public X As Integer
End Structure

Public Class B
    Private Structure C
        Public X As Integer
    End Structure
End Class
");
        }

        [WorkItem(899, "https://github.com/dotnet/roslyn-analyzers/issues/899")]
        [Fact]
        public async Task BasicNoDiagnosticForEnumAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Enum E
    F = 0
End Enum
");
        }

        [WorkItem(899, "https://github.com/dotnet/roslyn-analyzers/issues/899")]
        [Fact]
        public async Task BasicNoDiagnosticForStructsWithoutMembersAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure EmptyStruct
End Structure
");
        }

        [WorkItem(899, "https://github.com/dotnet/roslyn-analyzers/issues/899")]
        [Fact]
        public async Task BasicNoDiagnosticForEnumeratorsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Public Structure MyEnumerator
	Implements IEnumerator
	Public ReadOnly Property Current As Object Implements IEnumerator.Current
		Get
			Throw New NotImplementedException()
		End Get
	End Property

	Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
		Throw New NotImplementedException()
	End Function

	Public Sub Reset() Implements IEnumerator.Reset
		Throw New NotImplementedException()
	End Sub
End Structure

Public Structure MyGenericEnumerator(Of T)
	Implements IEnumerator(Of T)
	Public ReadOnly Property Current As T Implements IEnumerator(Of T).Current
		Get
			Throw New NotImplementedException()
		End Get
	End Property

	Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
		Get
			Throw New NotImplementedException()
		End Get
	End Property

	Public Sub Dispose() Implements IEnumerator(Of T).Dispose
		Throw New NotImplementedException()
	End Sub

	Public Function MoveNext() As Boolean Implements IEnumerator(Of T).MoveNext
		Throw New NotImplementedException()
	End Function

	Public Sub Reset() Implements IEnumerator(Of T).Reset
		Throw New NotImplementedException()
	End Sub
End Structure
");
        }

        [Fact]
        public async Task BasicNoDiagnosticForEqualsOnClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class A
End Class
");
        }

        [Fact]
        public async Task BasicNoDiagnosticWhenStructureImplementsEqualsAndOperatorEqualsAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure A
    Public Overrides Overloads Function Equals(obj As Object) As Boolean
        Return True
     End Function

    Public Shared Operator =(left As A, right As A)
        Return True
    End Operator

    Public Shared Operator <>(left As A, right As A)
        Return False
    End Operator
End Structure
");
        }

        [Fact]
        public async Task BasicDiagnosticWhenEqualsHasWrongSignatureAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure A
    Public Overrides Overloads Function {|BC30284:Equals|}(obj As A) As Boolean
        Return True
    End Function

    Public Shared Operator =(left As A, right As A)
        Return True
    End Operator

    Public Shared Operator <>(left As A, right As A)
        Return False
    End Operator
End Structure
",
                GetBasicOverrideEqualsDiagnostic(2, 18, "A"));
        }

        [Fact]
        public async Task BasicDiagnosticWhenEqualsIsNotAnOverrideAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure A
   Public Shadows Function Equals(obj As Object) As Boolean
      Return True
   End Function

    Public Shared Operator =(left As A, right As A)
        Return True
    End Operator

    Public Shared Operator <>(left As A, right As A)
        Return False
    End Operator
End Structure
",
                GetBasicOverrideEqualsDiagnostic(2, 18, "A"));
        }

        [Fact, WorkItem(2324, "https://github.com/dotnet/roslyn-analyzers/issues/2324")]
        public async Task CSharp_RefStruct_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public ref struct A
{
    public int X;
}",
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpOverrideEqualsDiagnostic(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.EqualsRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCSharpOperatorEqualsDiagnostic(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetBasicOverrideEqualsDiagnostic(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.EqualsRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetBasicOperatorEqualsDiagnostic(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.OpEqualityRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);
    }
}