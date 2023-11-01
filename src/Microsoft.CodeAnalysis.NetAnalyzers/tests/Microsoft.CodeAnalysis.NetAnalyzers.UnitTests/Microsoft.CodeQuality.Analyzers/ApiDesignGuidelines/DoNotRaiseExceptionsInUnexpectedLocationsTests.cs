// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotRaiseExceptionsInUnexpectedLocationsTests
    {
        #region Property and Event Tests

        [Fact]
        public async Task CSharpPropertyNoDiagnosticsAsync()
        {
            var code = @"
using System;

public class C
{
    public int PropWithNoException { get { return 10; } set { } }
    public int PropWithSetterException { get { return 10; } set { throw new NotSupportedException(); } }
    public int PropWithAllowedException { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
    public int this[int x] { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
}

class NonPublic
{
    public int PropWithException { get { throw new Exception(); } set { throw new NotSupportedException(); } }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpPropertyWithDerivedExceptionNoDiagnosticsAsync()
        {
            var code = @"
using System;

public class C
{
    public int this[int x] { get { throw new ArgumentOutOfRangeException(); } set { throw new ArgumentOutOfRangeException(); } }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicPropertyNoDiagnosticsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Property PropWithNoException As Integer
        Get
           Return 10
        End Get
        Set 
        End Set
    End Property
    Public Property PropWithSetterException As Integer
        Get
           Return 10
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
    Public Property PropWithAllowedException As Integer
        Get
           Throw New NotSupportedException()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
    Default Public Property Item(x As Integer) As Integer
        Get
           Throw New NotSupportedException()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
End Class

Class NonPublic
    Public Property PropWithInvalidException As Integer
        Get
           Throw New Exception() 'Doesn't fire because it's not visible outside assembly
        End Get
        Set 
        End Set
    End Property
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicPropertyWithDerivedExceptionNoDiagnosticsAsync()
        {
            var code = @"
Imports System

Public Class C
    Default Public Property Item(x As Integer) As Integer
        Get
           Throw New ArgumentOutOfRangeException()
        End Get
        Set
            Throw New ArgumentOutOfRangeException()
        End Set
    End Property
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpPropertyWithInvalidExceptionsAsync()
        {
            var code = @"
using System;

public class C
{
    public int Prop1 { get { throw new Exception(); } set { throw new NotSupportedException(); } }
    public int this[int x] { get { throw new Exception(); } set { throw new NotSupportedException(); } }
    public event EventHandler Event1 { add { throw new Exception(); } remove { throw new Exception(); } }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpPropertyResultAt(6, 30, "get_Prop1", "Exception"),
                         GetCSharpPropertyResultAt(7, 36, "get_Item", "Exception"),
                         GetCSharpAllowedExceptionsResultAt(8, 46, "add_Event1", "Exception"),
                         GetCSharpAllowedExceptionsResultAt(8, 80, "remove_Event1", "Exception"));
        }

        [Fact]
        public async Task BasicPropertyWithInvalidExceptionsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Property Prop1 As Integer
        Get
           Throw New Exception()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property
    Default Public Property Item(x As Integer) As Integer
        Get
           Throw New Exception()
        End Get
        Set
            Throw New NotSupportedException()
        End Set
    End Property

    Public Custom Event Event1 As EventHandler
        AddHandler(ByVal value As EventHandler)
            Throw New Exception()
        End AddHandler
 
        RemoveHandler(ByVal value As EventHandler)
            Throw New Exception()
        End RemoveHandler
 
        ' RaiseEvent accessors are considered private and we won't flag this exception.
        RaiseEvent(ByVal sender As Object, ByVal e As System.EventArgs)
            Throw New Exception() 
        End RaiseEvent
    End Event
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicPropertyResultAt(7, 12, "get_Prop1", "Exception"),
                        GetBasicPropertyResultAt(15, 12, "get_Item", "Exception"),
                        GetBasicAllowedExceptionsResultAt(24, 13, "add_Event1", "Exception"),
                        GetBasicAllowedExceptionsResultAt(28, 13, "remove_Event1", "Exception"));
        }

        [Fact, WorkItem(1842, "https://github.com/dotnet/roslyn-analyzers/issues/1842")]
        public async Task CSharpIndexer_KeyNotFoundException_NoDiagnosticsAsync()
        {
            var code = @"
using System.Collections.Generic;

public class C
{
    public int this[int x] { get { throw new KeyNotFoundException(); } }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        #endregion

        #region Equals, GetHashCode, Dispose and ToString Tests

        [Fact]
        public async Task CSharpEqualsAndGetHashCodeWithExceptionsAsync()
        {
            var code = @"
using System;

public class C
{
    public override bool Equals(object obj)
    {
        throw new Exception();
    }
    public override int GetHashCode()
    {
        throw new ArgumentException("""");
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Equals", "Exception"),
                         GetCSharpNoExceptionsResultAt(12, 9, "GetHashCode", "ArgumentException"));
        }

        [Fact]
        public async Task BasicEqualsAndGetHashCodeWithExceptionsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Overrides Function Equals(obj As Object) As Boolean
        Throw New Exception()
    End Function
    Public Overrides Function GetHashCode() As Integer
        Throw New ArgumentException("""")
    End Function
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(6, 9, "Equals", "Exception"),
                        GetBasicNoExceptionsResultAt(9, 9, "GetHashCode", "ArgumentException"));
        }

        [Fact]
        public async Task CSharpEqualsAndGetHashCodeNoDiagnosticsAsync()
        {
            var code = @"
using System;

public class C
{
    public new bool Equals(object obj)
    {
        throw new Exception();
    }
    public new int GetHashCode()
    {
        throw new ArgumentException("""");
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicEqualsAndGetHashCodeNoDiagnosticsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Shadows Function Equals(obj As Object) As Boolean
        Throw New Exception()
    End Function
    Public Shadows Function GetHashCode() As Integer
        Throw New ArgumentException("""")
    End Function
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpIEquatableEqualsWithExceptionsAsync()
        {
            var code = @"
using System;

public class C : IEquatable<C>
{
    public bool Equals(C obj)
    {
        throw new Exception();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Equals", "Exception"));
        }

        [Fact]
        public async Task BasicIEquatableEqualsExceptionsAsync()
        {
            var code = @"
Imports System

Public Class C
    Implements IEquatable(Of C)
    Public Function Equals(obj As C) As Boolean Implements IEquatable(Of C).Equals
        Throw New Exception()
    End Function
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(7, 9, "Equals", "Exception"));
        }

        [Fact]
        public async Task CSharpIHashCodeProviderGetHashCodeAsync()
        {
            var code = @"
using System;
using System.Collections;
public class C : IHashCodeProvider
{
    public int GetHashCode(object obj)
    {
        throw new Exception();
    }
}

public class D : IHashCodeProvider
{
    public int GetHashCode(object obj)
    {
        throw new ArgumentException(""obj""); // this is fine.
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpAllowedExceptionsResultAt(8, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public async Task BasicIHashCodeProviderGetHashCodeAsync()
        {
            var code = @"
Imports System
Imports System.Collections
Public Class C
    Implements IHashCodeProvider
    Public Function GetHashCode(obj As Object) As Integer Implements IHashCodeProvider.GetHashCode
        Throw New Exception()
    End Function
End Class

Public Class D
    Implements IHashCodeProvider
    Public Function GetHashCode(obj As Object) As Integer Implements IHashCodeProvider.GetHashCode
        Throw New ArgumentException() ' This is fine.
    End Function
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicAllowedExceptionsResultAt(7, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public async Task CSharpIEqualityComparerAsync()
        {
            var code = @"
using System;
using System.Collections.Generic;
public class C : IEqualityComparer<C>
{
    public bool Equals(C obj1, C obj2)
    {
        throw new Exception();
    }
    public int GetHashCode(C obj)
    {
        throw new Exception();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Equals", "Exception"),
                         GetCSharpAllowedExceptionsResultAt(12, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public async Task BasicIEqualityComparerAsync()
        {
            var code = @"
Imports System
Imports System.Collections.Generic
Public Class C
    Implements IEqualityComparer(Of C)
    Public Function Equals(obj1 As C, obj2 As C) As Boolean Implements IEqualityComparer(Of C).Equals
        Throw New Exception()
    End Function
    Public Function GetHashCode(obj As C) As Integer Implements IEqualityComparer(Of C).GetHashCode
        Throw New Exception()
    End Function
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(7, 9, "Equals", "Exception"),
                        GetBasicAllowedExceptionsResultAt(10, 9, "GetHashCode", "Exception"));
        }

        [Fact]
        public async Task CSharpIDisposableAsync()
        {
            var code = @"
using System;

public class C : IDisposable
{
    public void Dispose()
    {
        throw new Exception();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Dispose", "Exception"));
        }

        [Fact]
        public async Task BasicIDisposableAsync()
        {
            var code = @"
Imports System

Public Class C
    Implements IDisposable
    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New Exception()
    End Sub
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(7, 9, "Dispose", "Exception"));
        }

        [Fact]
        public async Task CSharpToStringWithExceptionsAsync()
        {
            var code = @"
using System;

public class C
{
    public override string ToString()
    {
        throw new Exception();
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "ToString", "Exception"));
        }

        [Fact]
        public async Task BasicToStringWithExceptionsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Overrides Function ToString() As String
        Throw New Exception()
    End Function
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(6, 9, "ToString", "Exception"));
        }

        #endregion

        #region Constructor and Destructor tests
        [Fact]
        public async Task CSharpStaticConstructorWithExceptionsAsync()
        {
            var code = @"
using System;
    
class NonPublic
{
    static NonPublic()
    {
        throw new Exception();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, ".cctor", "Exception"));
        }

        [Fact]
        public async Task BasicStaticConstructorWithExceptionsAsync()
        {
            var code = @"
Imports System

Class NonPublic
    Shared Sub New()
        Throw New Exception()
    End Sub
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(6, 9, ".cctor", "Exception"));
        }

        [Fact]
        public async Task CSharpFinalizerWithExceptionsAsync()
        {
            var code = @"
using System;
    
class NonPublic
{
    ~NonPublic()
    {
        throw new Exception();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "Finalize", "Exception"));
        }

        [Fact]
        public async Task BasicFinalizerWithExceptionsAsync()
        {
            var code = @"
Imports System

Class NonPublic
    Protected Overrides Sub Finalize()
        Throw New Exception()
    End Sub
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(6, 9, "Finalize", "Exception"));
        }

        [Fact, WorkItem(6963, "https://github.com/dotnet/roslyn-analyzers/issues/6963")]
        public Task Lambda_NoDiagnostic()
        {
            const string code = """
                                using System;

                                public class ShouldNotViolate
                                {
                                    static readonly Action a;
                                
                                    static ShouldNotViolate()
                                    {
                                        a = () => throw new DivideByZeroException();
                                    }
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(6963, "https://github.com/dotnet/roslyn-analyzers/issues/6963")]
        public Task VB_Lambda_NoDiagnostic()
        {
            const string code = """
                                Imports System

                                Public Class ShouldNotViolate
                                    Shared ReadOnly a As Action
                                
                                    Shared Sub New()
                                        a = Sub () Throw New DivideByZeroException()
                                    End Sub
                                End Class
                                """;

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        #endregion

        #region Operator tests
        [Fact]
        public async Task CSharpEqualityOperatorWithExceptionsAsync()
        {
            var code = @"
using System;

public class C
{
    public static C operator ==(C c1, C c2)
    {
        throw new Exception();
    }
    public static C operator !=(C c1, C c2)
    {
        throw new Exception();
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "op_Equality", "Exception"),
                         GetCSharpNoExceptionsResultAt(12, 9, "op_Inequality", "Exception"));
        }

        [Fact]
        public async Task BasicEqualityOperatorWithExceptionsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Shared Operator =(c1 As C, c2 As C) As C
        Throw New Exception()
    End Operator
    Public Shared Operator <>(c1 As C, c2 As C) As C
        Throw New Exception()
    End Operator
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(6, 9, "op_Equality", "Exception"),
                        GetBasicNoExceptionsResultAt(9, 9, "op_Inequality", "Exception"));
        }

        [Fact]
        [WorkItem(5021, "https://github.com/dotnet/roslyn-analyzers/issues/5021")]
        public async Task CSharpComparisonOperatorWithExceptionsAsync()
        {
            var code = @"
using System;

public class C
{
    public static bool operator <=(C left, C right)
    {
        {|#0:throw new Exception();|}
    }
    public static bool operator >=(C left, C right)
    {
        {|#1:throw new Exception();|}
    }
    public static bool operator <(C left, C right)
    {
        {|#2:throw new Exception();|}
    }
    public static bool operator >(C left, C right)
    {
        {|#3:throw new Exception();|}
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(
                code,
                GetCSharpNoExceptionsResultAt(0, "op_LessThanOrEqual", "Exception"),
                GetCSharpNoExceptionsResultAt(1, "op_GreaterThanOrEqual", "Exception"),
                GetCSharpNoExceptionsResultAt(2, "op_LessThan", "Exception"),
                GetCSharpNoExceptionsResultAt(3, "op_GreaterThan", "Exception"));
        }

        [Fact]
        public async Task BasicComparisonOperatorWithExceptionsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Shared Operator <=(left As C, right As C) As Boolean
        {|#0:Throw New Exception()|}
    End Operator
    Public Shared Operator >=(left As C, right As C) As Boolean
        {|#1:Throw New Exception()|}
    End Operator
    Public Shared Operator <(left As C, right As C) As Boolean
        {|#2:Throw New Exception()|}
    End Operator
    Public Shared Operator >(left As C, right As C) As Boolean
        {|#3:Throw New Exception()|}
    End Operator
End Class";
            await VerifyVB.VerifyAnalyzerAsync(
                code,
                GetBasicNoExceptionsResultAt(0, "op_LessThanOrEqual", "Exception"),
                GetBasicNoExceptionsResultAt(1, "op_GreaterThanOrEqual", "Exception"),
                GetBasicNoExceptionsResultAt(2, "op_LessThan", "Exception"),
                GetBasicNoExceptionsResultAt(3, "op_GreaterThan", "Exception"));
        }

        [Fact]
        public async Task CSharpImplicitOperatorWithExceptionsAsync()
        {
            var code = @"
using System;

public class C
{
    public static implicit operator int(C c1)
    {   
        throw new Exception();
    }
    public static explicit operator double(C c1)
    {
        throw new Exception(); // This is fine.
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code,
                         GetCSharpNoExceptionsResultAt(8, 9, "op_Implicit", "Exception"));
        }

        [Fact]
        public async Task BasicImplicitOperatorWithExceptionsAsync()
        {
            var code = @"
Imports System

Public Class C
    Public Shared Widening Operator CType(x As Integer) As C
        Throw New Exception()
    End Operator
    Public Shared Narrowing Operator CType(x As Double) As C
        Throw New Exception()
    End Operator
End Class
";

            await VerifyVB.VerifyAnalyzerAsync(code,
                        GetBasicNoExceptionsResultAt(6, 9, "op_Implicit", "Exception"));
        }
        #endregion

        private static DiagnosticResult GetCSharpPropertyResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.PropertyGetterRule, methodName, exceptionName);
        }
        private static DiagnosticResult GetCSharpAllowedExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.HasAllowedExceptionsRule, methodName, exceptionName);
        }
        private static DiagnosticResult GetCSharpNoExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetCSharpResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.NoAllowedExceptionsRule, methodName, exceptionName);
        }
        private static DiagnosticResult GetCSharpNoExceptionsResultAt(int markupKey, string methodName, string exceptionName)
        {
            return VerifyCS.Diagnostic(DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.NoAllowedExceptionsRule)
                .WithLocation(markupKey)
                .WithArguments(methodName, exceptionName);
        }

        private static DiagnosticResult GetBasicPropertyResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetBasicResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.PropertyGetterRule, methodName, exceptionName);
        }
        private static DiagnosticResult GetBasicAllowedExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetBasicResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.HasAllowedExceptionsRule, methodName, exceptionName);
        }
        private static DiagnosticResult GetBasicNoExceptionsResultAt(int line, int column, string methodName, string exceptionName)
        {
            return GetBasicResultAt(line, column, DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.NoAllowedExceptionsRule, methodName, exceptionName);
        }
        private static DiagnosticResult GetBasicNoExceptionsResultAt(int markupKey, string methodName, string exceptionName)
        {
            return VerifyVB.Diagnostic(DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer.NoAllowedExceptionsRule)
                .WithLocation(markupKey)
                .WithArguments(methodName, exceptionName);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
