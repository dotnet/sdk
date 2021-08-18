// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UsePropertiesWhereAppropriateAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpUsePropertiesWhereAppropriateFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UsePropertiesWhereAppropriateAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicUsePropertiesWhereAppropriateFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class UsePropertiesWhereAppropriateTests
    {
        [Fact]
        public async Task CSharp_CA1024NoDiagnosticCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections;

public class GenericType<T>
{
}

public class Base
{
    public virtual int GetSomething()
    {
        return 0;
    }

    public virtual int GetOverloadedMethod()
    {
        return 1;
    }

    public virtual int GetOverloadedMethod(int i)
    {
        return i;
    }
}

public class Class1 : Base
{
    private string fileName = """";

    // 1) Returns void
    public void GetWronglyNamedMethod()
    {
    }

    // 2) Not a method
    public string LogFile
    {
        get { return fileName; }
    }

    // 3) Returns an array type
    public int[] GetValues()
    {
        return null;
    }

    // 4) Has parameters
    public int[] GetMethodWithParameters(int p)
    {
        return new int[] { p };
    }

    // 5a) Name doesn't start with a 'Get'
    public int SomeMethod()
    {
        return 0;
    }

    // 5b) First compound word is not 'Get'
    public int GetterMethod()
    {
        return 0;
    }

    // 6) Generic method
    public object GetGenericMethod<T>()
    {
        return new GenericType<T>();
    }

    // 7) Override
    public override int GetSomething()
    {
        return 1;
    }

    // 8) Method with overloads
    public override int GetOverloadedMethod()
    {
        return 1;
    }

    public override int GetOverloadedMethod(int i)
    {
        return i;
    }

    // 9) Methods with special name
    public override int GetHashCode()
    {
        return 0;
    }

    public IEnumerator GetEnumerator()
    {
        return null;
    }

    public ref string GetPinnableReference() // If the method isn't ref-returning, there will be a diagnostic.
    {
        return ref fileName;
    }

    // 10) Method with invocation expressions
    public int GetSomethingWithInvocation()
    {
        Console.WriteLine(this);
        return 0;
    }

    // 11) Method named 'Get'
    public string Get()
    {
        return fileName;
    }

    // 12) Private method
    private string GetSomethingPrivate()
    {
        return fileName;
    }

    // 13) Internal method
    internal string GetSomethingInternal()
    {
        return fileName;
    }
}

public class Class2
{
    private string fileName = """";

    public ref readonly string GetPinnableReference() // If the method isn't ref-returning, there will be a diagnostic.
    {
        return ref fileName;
    }
}
");
        }

        [Fact]
        public async Task CSharp_CA1024DiagnosticCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class
{
    private string fileName = ""data.txt"";

    public string GetFileName()
    {
        return fileName;
    }

    public string Get_FileName2()
    {
        return fileName;
    }

    public string Get123()
    {
        return fileName;
    }

    protected string GetFileNameProtected()
    {
        return fileName;
    }

    public int GetPinnableReference() // Not a ref-return method.
    {
        return 0;
    }
}
",
            GetCA1024CSharpResultAt(6, 19, "GetFileName"),
            GetCA1024CSharpResultAt(11, 19, "Get_FileName2"),
            GetCA1024CSharpResultAt(16, 19, "Get123"),
            GetCA1024CSharpResultAt(21, 22, "GetFileNameProtected"),
            GetCA1024CSharpResultAt(26, 16, "GetPinnableReference"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharp_CA1024NoDiagnosticCases_InternalAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class
{
    private string fileName = ""data.txt"";

    internal string GetFileName()
    {
        return fileName;
    }

    private string Get_FileName2()
    {
        return fileName;
    }

    private class InnerClass
    {
        private string fileName = ""data.txt"";

        public string Get123()
        {
            return fileName;
        }
    }
}
");
        }

        [Fact]
        public async Task VisualBasic_CA1024NoDiagnosticCasesAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections

Public Class Base
    Public Overridable Function GetSomething() As Integer
        Return 0
    End Function
End Class

Public Class Class1
    Inherits Base
    Private fileName As String

    ' 1) Returns void
    Public Sub GetWronglyNamedMethod()
    End Sub

    ' 2) Not a method
    Public ReadOnly Property LogFile() As String
        Get
            Return fileName
        End Get
    End Property

    ' 3) Returns an array type
    Public Function GetValues() As Integer()
        Return Nothing
    End Function

    ' 4) Has parameters
    Public Function GetMethodWithParameters(p As Integer) As Integer()
        Return New Integer() {p}
    End Function

    ' 5a) Name doesn't start with a 'Get'
    Public Function SomeMethod() As Integer
        Return 0
    End Function

    ' 5b) First compound word is not 'Get'
    Public Function GetterMethod() As Integer
        Return 0
    End Function

    ' 6) Generic method
    Public Function GetGenericMethod(Of T)() As Object
        Return New GenericType(Of T)()
    End Function

    ' 7) Override
    Public Overrides Function GetSomething() As Integer
        Return 1
    End Function

    ' 8) Method with overloads
    Public Function GetOverloadedMethod() As Integer
        Return 1
    End Function

    Public Function GetOverloadedMethod(i As Integer) As Integer
        Return i
    End Function

    ' 9) Methods with special name
    Public Overloads Function GetHashCode() As Integer
        Return 0
    End Function

    Public Function GetEnumerator() As IEnumerator
        Return Nothing
    End Function

    ' 10) Method with invocation expressions
    Public Function GetSomethingWithInvocation() As Integer
        System.Console.WriteLine(Me)
        Return 0
    End Function

    ' 11) Method named 'Get'
    Public Function [Get]() As String
        Return fileName
    End Function

    ' 12) Private method
    Private Function GetSomethingPrivate() As String
        Return fileName
    End Function

    ' 13) Friend method
    Friend Function GetSomethingInternal() As String
        Return fileName
    End Function
End Class

Public Class GenericType(Of T)
End Class
");
        }

        [Fact]
        public async Task CSharp_CA1024NoDiagnosticOnUnboundMethodCallerAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class class1
{
    public int GetSomethingWithUnboundInvocation()
    {
        Console.WriteLine(this);
        return 0;
    }
}
");
        }

        [Fact]
        public async Task VisualBasic_CA1024NoDiagnosticOnUnboundMethodCallerAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class class1
    Public Function GetSomethingWithUnboundInvocation() As Integer
        Console.WriteLine(Me)
        Return 0
    End Function
End Class
");
        }

        [Fact]
        public async Task VisualBasic_CA1024DiagnosticCasesAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Private fileName As String

    Public Function GetFileName() As String
        Return filename
    End Function

    Public Function Get_FileName2() As String
        Return filename
    End Function

    Public Function Get123() As String
        Return filename
    End Function

    Protected Function GetFileNameProtected() As String
        Return filename
    End Function
End Class
",
            GetCA1024BasicResultAt(5, 21, "GetFileName"),
            GetCA1024BasicResultAt(9, 21, "Get_FileName2"),
            GetCA1024BasicResultAt(13, 21, "Get123"),
            GetCA1024BasicResultAt(17, 24, "GetFileNameProtected"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VisualBasic_CA1024NoDiagnosticCases_InternalAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Private fileName As String

    Friend Function GetFileName() As String
        Return filename
    End Function

    Private Function Get_FileName2() As String
        Return filename
    End Function

    Private Class InnerClass
        Private fileName As String

        Public Function Get123() As String
            Return filename
        End Function
    End Class
End Class
");
        }

        [Fact, WorkItem(1551, "https://github.com/dotnet/roslyn-analyzers/issues/1551")]
        public async Task CA1024_ExplicitInterfaceImplementation_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface ISomething
{
    object GetContent();
}

public class Something : ISomething
{
    object ISomething.GetContent()
    {
        return null;
    }
}
");
        }

        [Fact, WorkItem(1551, "https://github.com/dotnet/roslyn-analyzers/issues/1551")]
        public async Task CA1024_ImplicitInterfaceImplementation_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface ISomething
{
    object GetContent();
}

public class Something : ISomething
{
    public object GetContent()
    {
        return null;
    }
}
");
        }

        [Fact, WorkItem(3877, "https://github.com/dotnet/roslyn-analyzers/issues/3877")]
        public async Task CA1024_ReturnsTask_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;

public class Something
{
    public Task GetTask() => default(Task);
    public Task<int> GetGenericTask() => default(Task<int>);

    public ValueTask GetValueTask() => default(ValueTask);
    public ValueTask<int> GetGenericValueTask() => default(ValueTask<int>);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks

Public Class Something
    Public Function GetTask() As Task
        Return Nothing
    End Function

    Public Function GetGenericTask() As Task(Of Integer)
        Return Nothing
    End Function

    Public Function GetValueTask() As ValueTask
        Return Nothing
    End Function

    Public Function GetGenericValueTask() As ValueTask(Of Integer)
        Return Nothing
    End Function
End Class
");
        }

        [Fact, WorkItem(4623, "https://github.com/dotnet/roslyn-analyzers/issues/4623")]
        public async Task AwaiterPattern_INotifyCompletion_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;

public class DummyAwaiter : INotifyCompletion
{
    public object GetResult() => null;

    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) => throw null;
}");
        }

        [Fact, WorkItem(4623, "https://github.com/dotnet/roslyn-analyzers/issues/4623")]
        public async Task AwaiterPattern_ICriticalNotifyCompletion_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;

public class DummyAwaiter : ICriticalNotifyCompletion
{
    public object GetResult() => null;

    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) => throw null;
    public void UnsafeOnCompleted(Action continuation) => throw null;
}");
        }

        [Fact, WorkItem(4623, "https://github.com/dotnet/roslyn-analyzers/issues/4623")]
        public async Task AwaitablePattern_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;

public class DummyAwaitable
{
    public DummyAwaiter GetAwaiter() => new DummyAwaiter();
}

public class DummyAwaiter : INotifyCompletion
{
    public void GetResult()
    {
    }

    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) => throw null;
}");
        }

        private static DiagnosticResult GetCA1024CSharpResultAt(int line, int column, string methodName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(methodName);

        private static DiagnosticResult GetCA1024BasicResultAt(int line, int column, string methodName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(methodName);
    }
}
