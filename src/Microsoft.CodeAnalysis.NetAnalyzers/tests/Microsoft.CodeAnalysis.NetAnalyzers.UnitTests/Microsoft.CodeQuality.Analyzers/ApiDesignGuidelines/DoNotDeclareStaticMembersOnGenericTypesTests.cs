// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareStaticMembersOnGenericTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDeclareStaticMembersOnGenericTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotDeclareStaticMembersOnGenericTypesTests
    {
        [Fact]
        public async Task CSharp_CA1000_ShouldGenerate()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    public class GenericType1<T>
    {
        private GenericType1()
        {
        }
 
        public static void Output(T data)
        {
            Console.Write(data);
        }
 
        public static string Test
        {
            get { return string.Empty; }
        }        
    }
 
    public static class GenericType2<T>
    {
        public static void Output(T data)
        {
            Console.Write(data);
        }
 
        public static string Test
        {
            get { return string.Empty; }
        }
    }",
    GetCSharpResultAt(10, 28),
    GetCSharpResultAt(15, 30),
    GetCSharpResultAt(23, 28),
    GetCSharpResultAt(28, 30)
    );
        }

        [Fact]
        public async Task Basic_CA1000_ShouldGenerate()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"Imports System
Public Class GenericType1(Of T)
    Private Sub New()
    End Sub

    Public Shared Sub Output(data As T)
        Console.Write(data)
    End Sub

    Public Shared ReadOnly Property Test() As String
        Get
            Return String.Empty
        End Get
    End Property
End Class

Public NotInheritable Class GenericType2(Of T)
    Public Shared Sub Output(data As T)
        Console.Write(data)
    End Sub

    Public Shared ReadOnly Property Test() As String
        Get
            Return String.Empty
        End Get
    End Property
End Class",
    GetBasicResultAt(6, 23),
    GetBasicResultAt(10, 37),
    GetBasicResultAt(18, 23),
    GetBasicResultAt(22, 37)
    );
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_CA1000_ShouldNotGenerate_ContainingTypeIsNotExternallyVisible()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    using System;

    internal class GenericType1<T>
    {
        private GenericType1()
        {
        }
 
        public static void Output(T data)
        {
            Console.Write(data);
        }
 
        public static string Test
        {
            get { return string.Empty; }
        }        
    }
 
    internal static class GenericType2<T>
    {
        public static void Output(T data)
        {
            Console.Write(data);
        }
 
        public static string Test
        {
            get { return string.Empty; }
        }
    }"
    );
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task Basic_CA1000_ShouldNotGenerate_ContainingTypeIsNotExternallyVisible()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"Imports System
Friend Class GenericType1(Of T)
    Private Sub New()
    End Sub

    Public Shared Sub Output(data As T)
        Console.Write(data)
    End Sub

    Public Shared ReadOnly Property Test() As String
        Get
            Return String.Empty
        End Get
    End Property
End Class

Friend NotInheritable Class GenericType2(Of T)
    Public Shared Sub Output(data As T)
        Console.Write(data)
    End Sub

    Public Shared ReadOnly Property Test() As String
        Get
            Return String.Empty
        End Get
    End Property
End Class");
        }

        [Fact]
        public async Task CSharp_CA1000_ShouldNotGenerate_MemberIsNotPublicStatic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class GenericType1<T>
{
    private GenericType1()
    {
    }
 
    static GenericType1()
    {
    }

    protected static string TestProtected
    {
        get { return string.Empty; }
    }

    protected internal static string TestProtectedInternal
    {
        get { return string.Empty; }
    }

    internal static string TestInternal
    {
        get { return string.Empty; }
    }

    private static string TestPrivate
    {
        get { return string.Empty; }
    }

    protected static void OutputProtected(T data)
    {
        Console.Write(data);
    }

    protected internal static void OutputProtectedInternal(T data)
    {
        Console.Write(data);
    }

    internal static void OutputInternal(T data)
    {
        Console.Write(data);
    }

    private static void OutputPrivate(T data)
    {
        Console.Write(data);
    }

    public override bool Equals(object obj)
    {
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static bool operator ==(GenericType1<T> left, GenericType1<T> right)
    {
        return object.Equals(left, right);
    }

    public static bool operator !=(GenericType1<T> left, GenericType1<T> right)
    {
        return !object.Equals(left, right);
    }
}

public class OpenType<T>
{
}

public sealed class ClosedType : OpenType<String>
{

    public static void OutputProtected()
    {
    }

    public static string Test
    {
        get { return string.Empty; }
    }
}");
        }

        [Fact]
        public async Task Basic_CA1000_ShouldNotGenerate_MemberIsNotPublicStatic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class GenericType1(Of T)
    Private Sub New()
    End Sub

    Shared Sub New()
    End Sub

    Protected Shared ReadOnly Property TestProtected() As String
        Get
            Return String.Empty
        End Get
    End Property

    Protected Friend Shared ReadOnly Property TestProtectedInternal() As String
        Get
            Return String.Empty
        End Get
    End Property

    Friend Shared ReadOnly Property TestInternal() As String
        Get
            Return String.Empty
        End Get
    End Property

    Private Shared ReadOnly Property TestPrivate() As String
        Get
            Return String.Empty
        End Get
    End Property

    Protected Shared Sub OutputProtected(data As T)
        Console.Write(data)
    End Sub

    Protected Friend Shared Sub OutputProtectedInternal(data As T)
        Console.Write(data)
    End Sub

    Friend Shared Sub OutputInternal(data As T)
        Console.Write(data)
    End Sub

    Private Shared Sub OutputPrivate(data As T)
        Console.Write(data)
    End Sub

    Public Overrides Function Equals(obj As Object) As Boolean
        Return MyBase.Equals(obj)
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return MyBase.GetHashCode()
    End Function

    Public Shared Operator =(left As GenericType1(Of T), right As GenericType1(Of T)) As Boolean
        Return Object.Equals(left, right)
    End Operator

    Public Shared Operator <>(left As GenericType1(Of T), right As GenericType1(Of T)) As Boolean
        Return Not Object.Equals(left, right)
    End Operator
End Class

Public Class OpenType(Of T)
End Class

Public NotInheritable Class ClosedType
    Inherits OpenType(Of [String])

    Public Shared Sub OutputProtected()
    End Sub

    Public Shared ReadOnly Property Test() As String
        Get
            Return String.Empty
        End Get
    End Property
End Class");
        }

        [Fact]
        public async Task CSharp_CA1000_ShouldNotGenerate_ConversionOperator()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1<T>
{
    public static implicit operator Class1<T>(T value) => new Class1<T>();
    public static explicit operator T(Class1<T> value) => default(T);
}
");
        }

        [Fact]
        public async Task Basic_CA1000_ShouldNotGenerate_ConversionOperator()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1(Of T)
    Public Shared Narrowing Operator CType(value As T) As Class1(Of T)
        Return New Class1(Of T)()
    End Operator

    Public Shared Widening Operator CType(value As Class1(Of T)) As T
        Return Nothing
    End Operator
End Class
");
        }

        [Fact, WorkItem(1791, "https://github.com/dotnet/roslyn-analyzers/issues/1791")]
        public async Task CSharp_CA1000_ShouldNotGenerate_OperatorOverloads()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public abstract class TestObject<T2> : IEquatable<TestObject<T2>>, IComparable<TestObject<T2>>
        where T2 : TestObject<T2>, new()
{
    public static bool operator ==(TestObject<T2> first, TestObject<T2> second) => first.Equals(second);

    public static bool operator !=(TestObject<T2> first, TestObject<T2> second) => !(first == second);

    public static bool operator <(TestObject<T2> first, TestObject<T2> second) => first.CompareTo(second) < 0;

    public static bool operator >(TestObject<T2> first, TestObject<T2> second) => first.CompareTo(second) > 0;

    public static bool operator <=(TestObject<T2> first, TestObject<T2> second) => first.CompareTo(second) <= 0;

    public static bool operator >=(TestObject<T2> first, TestObject<T2> second) => first.CompareTo(second) >= 0;

    public int CompareTo(TestObject<T2> other)
    {
        return 0;
    }

    public bool Equals(TestObject<T2> other)
    {
        return true;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is null)
        {
            return false;
        }

        throw new NotImplementedException();
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}