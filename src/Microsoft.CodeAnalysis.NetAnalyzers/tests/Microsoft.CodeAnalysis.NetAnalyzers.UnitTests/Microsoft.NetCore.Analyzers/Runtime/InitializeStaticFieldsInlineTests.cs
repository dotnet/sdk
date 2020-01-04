// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Microsoft.NetCore.VisualBasic.Analyzers.Runtime;
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
    public class InitializeStaticFieldsInlineTests : DiagnosticAnalyzerTestBase
    {
        #region Unit tests for no analyzer diagnostic

        [Fact]
        public void CA1810_EmptyStaticConstructor()
        {
            VerifyCSharp(@"
public class Class1
{
    private readonly static int field = 1;
    static Class1() // Empty
    {
    }
}
");
            VerifyBasic(@"
Public Class Class1
	Private Shared ReadOnly field As Integer = 1
	Shared Sub New() ' Empty
	End Sub
End Class
");
        }

        [Fact]
        public void CA2207_EmptyStaticConstructor()
        {
            VerifyCSharp(@"
public struct Struct1
{
    private readonly static int field = 1;
    static Struct1() // Empty
    {
    }
}
");
            VerifyBasic(@"
Public Structure Struct1
	Private Shared ReadOnly field As Integer = 1
	Shared Sub New() ' Empty
	End Sub
End Structure
");
        }

        [Fact]
        public void CA1810_NoStaticFieldInitializedInStaticConstructor()
        {
            VerifyCSharp(@"
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
            VerifyBasic(@"
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
        public void CA1810_StaticPropertyInStaticConstructor()
        {
            VerifyCSharp(@"
public class Class1
{
    private static int Property { get; set; }

    static Class1() // Static property initalization
    {
        Property = 1;
    }
}
");

            VerifyBasic(@"
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
        public void CA1810_InitializionInNonStaticConstructor()
        {
            VerifyCSharp(@"
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
            VerifyBasic(@"
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

        #endregion

        #region Unit tests for analyzer diagnostic(s)

        [Fact]
        public void CA1810_InitializationInStaticConstructor()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void CA2207_InitializationInStaticConstructor()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void CA1810_NoDuplicateDiagnostics()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void CA2207_NoDuplicateDiagnostics()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicInitializeStaticFieldsInlineAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpInitializeStaticFieldsInlineAnalyzer();
        }

        private static DiagnosticResult GetCA1810CSharpDefaultResultAt(int line, int column, string typeName)
        {
            string message = string.Format(MicrosoftNetCoreAnalyzersResources.InitializeStaticFieldsInlineMessage, typeName);
            return GetCSharpResultAt(line, column, CSharpInitializeStaticFieldsInlineAnalyzer.CA1810RuleId, message);
        }

        private static DiagnosticResult GetCA1810BasicDefaultResultAt(int line, int column, string typeName)
        {
            string message = string.Format(MicrosoftNetCoreAnalyzersResources.InitializeStaticFieldsInlineMessage, typeName);
            return GetBasicResultAt(line, column, BasicInitializeStaticFieldsInlineAnalyzer.CA1810RuleId, message);
        }

        private static DiagnosticResult GetCA2207CSharpDefaultResultAt(int line, int column, string typeName)
        {
            string message = string.Format(MicrosoftNetCoreAnalyzersResources.InitializeStaticFieldsInlineMessage, typeName);
            return GetCSharpResultAt(line, column, CSharpInitializeStaticFieldsInlineAnalyzer.CA2207RuleId, message);
        }

        private static DiagnosticResult GetCA2207BasicDefaultResultAt(int line, int column, string typeName)
        {
            string message = string.Format(MicrosoftNetCoreAnalyzersResources.InitializeStaticFieldsInlineMessage, typeName);
            return GetBasicResultAt(line, column, BasicInitializeStaticFieldsInlineAnalyzer.CA2207RuleId, message);
        }

        #endregion
    }
}