// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.InitializeStaticFieldsInlineAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpInitializeStaticFieldsInlineFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.InitializeStaticFieldsInlineAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicInitializeStaticFieldsInlineFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class InitializeStaticFieldsInlineTests
    {
        #region Unit tests for no analyzer diagnostic

        [Fact]
        public async Task CA1810_EmptyStaticConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private readonly static int field = 1;
    static Class1() // Empty
    {
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
	Private Shared ReadOnly field As Integer = 1
	Shared Sub New() ' Empty
	End Sub
End Class
");
        }

        [Fact]
        public async Task CA2207_EmptyStaticConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct Struct1
{
    private readonly static int field = 1;
    static Struct1() // Empty
    {
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure Struct1
	Private Shared ReadOnly field As Integer = 1
	Shared Sub New() ' Empty
	End Sub
End Structure
");
        }

        [Fact]
        public async Task CA1810_NoStaticFieldInitializedInStaticConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private readonly static int field = 1;
    static Class1() // No static field initialization
    {
        Class1_Method();
        var field2 = 1;
    }

    private static void Class1_Method() { throw new System.NotImplementedException(); }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
	Private Shared ReadOnly field As Integer = 1
	Shared Sub New() ' No static field initalization
		Class1_Method()
		Dim field2 = 1
	End Sub

    Private Shared Sub Class1_Method()
        Throw New System.NotImplementedException()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1810_StaticPropertyInStaticConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private static int Property { get; set; }

    static Class1() // Static property initalization
    {
        Property = 1;
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
	Private Shared Property [Property]() As Integer
		Get
			Return 0
		End Get
		Set
		End Set
	End Property

	Shared Sub New()
		' Static property initalization
		[Property] = 1
	End Sub
End Class
");
        }

        [Fact]
        public async Task CA1810_InitializionInNonStaticConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private static int field = 1;
    public Class1() // Non static constructor
    {
        field = 0;
    }

    public static void Class1_Method() // Non constructor
    {
        field = 0;
    }
}
");
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
	Private Shared field As Integer = 1
	Public Sub New() ' Non static constructor
		field = 0
	End Sub

	Public Shared Sub Class1_Method() ' Non constructor
		field = 0
	End Sub
End Class
");
        }

        [Fact, WorkItem(3138, "https://github.com/dotnet/roslyn-analyzers/issues/3138")]
        public async Task CA1810_EventLambdas_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    private static string s;

    static C()
    {
        Console.CancelKeyPress += (o, e) => s = string.Empty;
        Console.CancelKeyPress -= (o, e) => s = string.Empty;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class C
    Private Shared s As String

    Shared Sub New()
        AddHandler Console.CancelKeyPress,
            Sub(o, e)
                s = string.Empty
            End Sub

        RemoveHandler Console.CancelKeyPress,
            Sub(o, e)
                s = string.Empty
            End Sub
    End Sub
End Class");
        }

        [Fact, WorkItem(3138, "https://github.com/dotnet/roslyn-analyzers/issues/3138")]
        public async Task CA1810_EventDelegate_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    private static string s;

    static C()
    {
        Console.CancelKeyPress += delegate { s = string.Empty; };
    }
}");
        }

        [Fact, WorkItem(3138, "https://github.com/dotnet/roslyn-analyzers/issues/3138")]
        public async Task CA1810_TaskRunActionAndFunc_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;

class C
{
    private static int s;

    static C()
    {
        Task.Run(() => s = 3);

        Task.Run(() =>
        {
            s = 3;
            return 42;
        });
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks

Class C
    Private Shared s As Integer

    Shared Sub New()
        Task.Run(Sub()
                    s = 3
                 End Sub)

        Task.Run(Function()
                    s = 3
                    Return 42
                 End Function)
    End Sub
End Class");
        }

        [Fact, WorkItem(3138, "https://github.com/dotnet/roslyn-analyzers/issues/3138")]
        public async Task CA1810_EnumerableWhere_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;
using System.Linq;

class C
{
    private static int s;

    static C()
    {
        var result = new List<int>().Where(x =>
        {
            if (x > 10)
            {
                s = x;
                return true;
            }

            return false;
        });
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic
Imports System.Linq

Class C
    Private Shared s As Integer

    Shared Sub New()
        Dim list = New List(Of Integer)
        Dim result = list.Where(Function(x)
                                    If x > 10 Then
                                        s = x
                                        Return True
                                    End if

                                    Return False
                                End Function)
    End Sub
End Class");
        }

        [Fact, WorkItem(3138, "https://github.com/dotnet/roslyn-analyzers/issues/3138")]
        public async Task CA1810_MixedFieldInitialization_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    private static string s1;
    private static string s2;

    static C()
    {
        Console.CancelKeyPress += (o, e) => s1 = string.Empty;
        s2 = string.Empty;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Class C
    Private Shared s1 As String
    Private Shared s2 As String

    Shared Sub New()
        AddHandler Console.CancelKeyPress,
            Sub(o, e)
                s1 = string.Empty
            End Sub
        s2 = string.Empty
    End Sub
End Class");
        }

        [Fact, WorkItem(3852, "https://github.com/dotnet/roslyn-analyzers/issues/3852")]
        public async Task CA1810_EventSubscriptionInStaticCtorPreventsDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithWinForms,
                TestCode = @"
using System;
using System.Windows.Forms;

public class C1
{
    private static readonly int field;

    static C1()
    {
        Application.ThreadExit += new EventHandler(OnThreadExit);
        field = 42;
    }

    private static void OnThreadExit(object sender, EventArgs e) {}
}

public class C2
{
    private static readonly int field;

    static C2()
    {
        Application.ThreadExit -= new EventHandler(OnThreadExit);
        field = 42;
    }

    private static void OnThreadExit(object sender, EventArgs e) {}
}
",
            }.RunAsync();
        }

        #endregion

        #region Unit tests for analyzer diagnostic(s)

        [Fact]
        public async Task CA1810_InitializationInStaticConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private readonly static int field;
    static Class1() // Non static constructor
    {
        field = 0;
    }
}
",
    GetCA1810CSharpDefaultResultAt(5, 12, "Class1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
	Private Shared ReadOnly field As Integer
	Shared Sub New()
		' Non static constructor
		field = 0
	End Sub
End Class
",
    GetCA1810BasicDefaultResultAt(4, 13, "Class1"));
        }

        [Fact]
        public async Task CA2207_InitializationInStaticConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct Struct1
{
    private readonly static int field;
    static Struct1() // Non static constructor
    {
        field = 0;
    }
}
",
    GetCA2207CSharpDefaultResultAt(5, 12, "Struct1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure Struct1
	Private Shared ReadOnly field As Integer
	Shared Sub New()
		' Non static constructor
		field = 0
	End Sub
End Structure
",
    GetCA2207BasicDefaultResultAt(4, 13, "Struct1"));
        }

        [Fact]
        public async Task CA1810_NoDuplicateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    private readonly static int field, field2;
    static Class1() // Non static constructor
    {
        field = 0;
        field2 = 0;
    }
}
",
    GetCA1810CSharpDefaultResultAt(5, 12, "Class1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
	Private Shared ReadOnly field As Integer, field2 As Integer
	Shared Sub New()
		' Non static constructor
		field = 0
		field2 = 0
	End Sub
End Class",
    GetCA1810BasicDefaultResultAt(4, 13, "Class1"));
        }

        [Fact]
        public async Task CA2207_NoDuplicateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct Struct1
{
    private readonly static int field, field2;
    static Struct1() // Non static constructor
    {
        field = 0;
        field2 = 0;
    }
}
",
    GetCA2207CSharpDefaultResultAt(5, 12, "Struct1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure Struct1
	Private Shared ReadOnly field As Integer, field2 As Integer
	Shared Sub New()
		' Non static constructor
		field = 0
		field2 = 0
	End Sub
End Structure",
    GetCA2207BasicDefaultResultAt(4, 13, "Struct1"));
        }

        [Fact, WorkItem(3138, "https://github.com/dotnet/roslyn-analyzers/issues/3138")]
        public async Task CA1810_LocalFunc_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;
using System.Linq;

class C
{
    private static int s;

    static C()
    {
        void LocalFunc()
        {
            s = 1;
        }
    }
}",
                GetCA1810CSharpDefaultResultAt(9, 12, "C"));
        }

        [Fact, WorkItem(3138, "https://github.com/dotnet/roslyn-analyzers/issues/3138")]
        public async Task CA1810_StaticLocalFunc_Diagnostic()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
                TestCode = @"
using System.Collections.Generic;
using System.Linq;

class C
{
    private static int s;

    static C()
    {
        static void StaticLocalFunc()
        {
            s = 2;
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCA1810CSharpDefaultResultAt(9, 12, "C")
                },
            }
            .RunAsync();
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCA1810CSharpDefaultResultAt(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(InitializeStaticFieldsInlineAnalyzer.CA1810Rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCA1810BasicDefaultResultAt(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(InitializeStaticFieldsInlineAnalyzer.CA1810Rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCA2207CSharpDefaultResultAt(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(InitializeStaticFieldsInlineAnalyzer.CA2207Rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCA2207BasicDefaultResultAt(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(InitializeStaticFieldsInlineAnalyzer.CA2207Rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(typeName);

        #endregion
    }
}