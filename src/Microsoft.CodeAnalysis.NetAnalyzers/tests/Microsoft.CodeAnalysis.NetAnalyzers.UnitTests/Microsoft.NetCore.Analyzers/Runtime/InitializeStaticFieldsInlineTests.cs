// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpInitializeStaticFieldsInlineAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpInitializeStaticFieldsInlineFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicInitializeStaticFieldsInlineAnalyzer,
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
    static Class1() // No static field initalization
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
        public async Task CA1810_EventSubscriptionInConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    private static string s;

    static C()
    {
        Console.CancelKeyPress += (o, e) => s = string.Empty;
    }
}");
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

        #endregion

        #region Helpers

        private static DiagnosticResult GetCA1810CSharpDefaultResultAt(int line, int column, string typeName) =>
            VerifyCS.Diagnostic(CSharpInitializeStaticFieldsInlineAnalyzer.CA1810Rule)
                .WithLocation(line, column)
                .WithArguments(typeName);

        private static DiagnosticResult GetCA1810BasicDefaultResultAt(int line, int column, string typeName) =>
            VerifyVB.Diagnostic(CSharpInitializeStaticFieldsInlineAnalyzer.CA1810Rule)
                .WithLocation(line, column)
                .WithArguments(typeName);

        private static DiagnosticResult GetCA2207CSharpDefaultResultAt(int line, int column, string typeName) =>
            VerifyCS.Diagnostic(CSharpInitializeStaticFieldsInlineAnalyzer.CA2207Rule)
                .WithLocation(line, column)
                .WithArguments(typeName);

        private static DiagnosticResult GetCA2207BasicDefaultResultAt(int line, int column, string typeName) =>
            VerifyVB.Diagnostic(CSharpInitializeStaticFieldsInlineAnalyzer.CA2207Rule)
                .WithLocation(line, column)
                .WithArguments(typeName);

        #endregion
    }
}