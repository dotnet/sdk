// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class ProvideCorrectArgumentsToFormattingMethodsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ProvideCorrectArgumentsToFormattingMethodsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ProvideCorrectArgumentsToFormattingMethodsAnalyzer();
        }

        #region Diagnostic Tests

        [Fact]
        public void CA2241CSharpString()
        {
            VerifyCSharp(@"
using System;

public class C
{
    void Method()
    {
        var a = String.Format("""", 1);
        var b = String.Format(""{0}"", 1, 2);
        var c = String.Format(""{0} {1}"", 1, 2, 3);
        var d = String.Format(""{0} {1} {2}"", 1, 2, 3, 4);
        var e = string.Format(""{0} {0}"", 1, 2);

        IFormatProvider p = null;
        var f = String.Format(p, """", 1);
        var g = String.Format(p, ""{0}"", 1, 2);
        var h = String.Format(p, ""{0} {1}"", 1, 2, 3);
        var i = String.Format(p, ""{0} {1} {2}"", 1, 2, 3, 4);
    }
}
",
            GetCA2241CSharpResultAt(8, 17),
            GetCA2241CSharpResultAt(9, 17),
            GetCA2241CSharpResultAt(10, 17),
            GetCA2241CSharpResultAt(11, 17),
            GetCA2241CSharpResultAt(12, 17),

            GetCA2241CSharpResultAt(15, 17),
            GetCA2241CSharpResultAt(16, 17),
            GetCA2241CSharpResultAt(17, 17),
            GetCA2241CSharpResultAt(18, 17));
        }

        [Fact]
        public void CA2241CSharpConsoleWrite()
        {
            VerifyCSharp(@"
using System;

public class C
{
    void Method()
    {
        Console.Write("""", 1);
        Console.Write(""{0}"", 1, 2);
        Console.Write(""{0} {1}"", 1, 2, 3);
        Console.Write(""{0} {1} {2}"", 1, 2, 3, 4);
        Console.Write(""{0} {1} {2} {3}"", 1, 2, 3, 4, 5);
    }
}
",
            GetCA2241CSharpResultAt(8, 9),
            GetCA2241CSharpResultAt(9, 9),
            GetCA2241CSharpResultAt(10, 9),
            GetCA2241CSharpResultAt(11, 9),
            GetCA2241CSharpResultAt(12, 9));
        }

        [Fact]
        public void CA2241CSharpConsoleWriteLine()
        {
            VerifyCSharp(@"
using System;

public class C
{
    void Method()
    {
        Console.WriteLine("""", 1);
        Console.WriteLine(""{0}"", 1, 2);
        Console.WriteLine(""{0} {1}"", 1, 2, 3);
        Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4);
        Console.WriteLine(""{0} {1} {2} {3}"", 1, 2, 3, 4, 5);
    }
}
",
            GetCA2241CSharpResultAt(8, 9),
            GetCA2241CSharpResultAt(9, 9),
            GetCA2241CSharpResultAt(10, 9),
            GetCA2241CSharpResultAt(11, 9),
            GetCA2241CSharpResultAt(12, 9));
        }

        [Fact]
        public void CA2241CSharpPassing()
        {
            VerifyCSharp(@"
using System;

public class C
{
    void Method()
    {
        var a = String.Format(""{0}"", 1);
        var b = String.Format(""{0} {1}"", 1, 2);
        var c = String.Format(""{0} {1} {2}"", 1, 2, 3);
        var d = String.Format(""{0} {1} {2} {3}"", 1, 2, 3, 4);
        var e = String.Format(""{0} {1} {2} {0}"", 1, 2, 3);

        Console.Write(""{0}"", 1);
        Console.Write(""{0} {1}"", 1, 2);
        Console.Write(""{0} {1} {2}"", 1, 2, 3);
        Console.Write(""{0} {1} {2} {3}"", 1, 2, 3, 4);
        Console.Write(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, 5);
        Console.Write(""{0} {1} {2} {3} {0}"", 1, 2, 3, 4);

        Console.WriteLine(""{0}"", 1);
        Console.WriteLine(""{0} {1}"", 1, 2);
        Console.WriteLine(""{0} {1} {2}"", 1, 2, 3);
        Console.WriteLine(""{0} {1} {2} {3}"", 1, 2, 3, 4);
        Console.WriteLine(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, 5);
        Console.WriteLine(""{0} {1} {2} {3} {0}"", 1, 2, 3, 4);
    }
}
");
        }

        [Fact]
        public void CA2241CSharpExplicitObjectArraySupported()
        {
            VerifyCSharp(@"
using System;

public class C
{
    void Method()
    {
        var s = String.Format(""{0} {1} {2} {3}"", new object[] {1, 2});
        Console.Write(""{0} {1} {2} {3}"", new object[] {1, 2, 3, 4, 5});
        Console.WriteLine(""{0} {1} {2} {3}"", new object[] {1, 2, 3, 4, 5});
    }
}
",
            GetCA2241CSharpResultAt(8, 17),
            GetCA2241CSharpResultAt(9, 9),
            GetCA2241CSharpResultAt(10, 9));
        }

        [Fact]
        public void CA2241CSharpVarArgsNotSupported()
        {
            // currently not supported due to "https://github.com/dotnet/roslyn/issues/7346"
            VerifyCSharp(@"
using System;

public class C
{
    void Method()
    {
        Console.Write(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, __arglist(5));
        Console.WriteLine(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, __arglist(5));
    }
}
");
        }

        [Fact]
        public void CA2241VBString()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Sub Method()
        Dim a = String.Format("""", 1)
        Dim b = String.Format(""{0}"", 1, 2)
        Dim c = String.Format(""{0} {1}"", 1, 2, 3)
        Dim d = String.Format(""{0} {1} {2}"", 1, 2, 3, 4)

        Dim p as IFormatProvider = Nothing
        Dim e = String.Format(p, """", 1)
        Dim f = String.Format(p, ""{0}"", 1, 2)
        Dim g = String.Format(p, ""{0} {1}"", 1, 2, 3)
        Dim h = String.Format(p, ""{0} {1} {2}"", 1, 2, 3, 4)
    End Sub
End Class
",
            GetCA2241BasicResultAt(6, 17),
            GetCA2241BasicResultAt(7, 17),
            GetCA2241BasicResultAt(8, 17),
            GetCA2241BasicResultAt(9, 17),

            GetCA2241BasicResultAt(12, 17),
            GetCA2241BasicResultAt(13, 17),
            GetCA2241BasicResultAt(14, 17),
            GetCA2241BasicResultAt(15, 17));
        }

        [Fact]
        public void CA2241VBConsoleWrite()
        {
            // this works in VB
            // Dim s = Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4)
            // since VB bind it to __arglist version where we skip analysis
            // due to a bug - https://github.com/dotnet/roslyn/issues/7346
            // we might skip it only in C# since VB doesnt support __arglist
            VerifyBasic(@"
Imports System

Public Class C
    Sub Method()
        Console.Write("""", 1)
        Console.Write(""{0}"", 1, 2)
        Console.Write(""{0} {1}"", 1, 2, 3)
        Console.Write(""{0} {1} {2}"", 1, 2, 3, 4)
        Console.Write(""{0} {1} {2} {3}"", 1, 2, 3, 4, 5)
    End Sub
End Class
",
            GetCA2241BasicResultAt(6, 9),
            GetCA2241BasicResultAt(7, 9),
            GetCA2241BasicResultAt(8, 9),
            GetCA2241BasicResultAt(10, 9));
        }

        [Fact]
        public void CA2241VBConsoleWriteLine()
        {
            // this works in VB
            // Dim s = Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4)
            // since VB bind it to __arglist version where we skip analysis
            // due to a bug - https://github.com/dotnet/roslyn/issues/7346
            // we might skip it only in C# since VB doesnt support __arglist
            VerifyBasic(@"
Imports System

Public Class C
    Sub Method()
        Console.WriteLine("""", 1)
        Console.WriteLine(""{0}"", 1, 2)
        Console.WriteLine(""{0} {1}"", 1, 2, 3)
        Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4)
        Console.WriteLine(""{0} {1} {2} {3}"", 1, 2, 3, 4, 5)
    End Sub
End Class
",
            GetCA2241BasicResultAt(6, 9),
            GetCA2241BasicResultAt(7, 9),
            GetCA2241BasicResultAt(8, 9),
            GetCA2241BasicResultAt(10, 9));
        }

        [Fact]
        public void CA2241VBPassing()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Sub Method()
        Dim a = String.Format(""{0}"", 1)
        Dim b = String.Format(""{0} {1}"", 1, 2)
        Dim c = String.Format(""{0} {1} {2}"", 1, 2, 3)
        Dim d = String.Format(""{0} {1} {2} {3}"", 1, 2, 3, 4)

        Console.Write(""{0}"", 1)
        Console.Write(""{0} {1}"", 1, 2)
        Console.Write(""{0} {1} {2}"", 1, 2, 3)
        Console.Write(""{0} {1} {2} {3}"", 1, 2, 3, 4)
        Console.Write(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, 5)

        Console.WriteLine(""{0}"", 1)
        Console.WriteLine(""{0} {1}"", 1, 2)
        Console.WriteLine(""{0} {1} {2}"", 1, 2, 3)
        Console.WriteLine(""{0} {1} {2} {3}"", 1, 2, 3, 4)
        Console.WriteLine(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, 5)
    End Sub
End Class
");
        }

        [Fact]
        public void CA2241VBExplicitObjectArraySupported()
        {
            VerifyBasic(@"
Imports System

Public Class C
    Sub Method()
        Dim s = String.Format(""{0} {1} {2} {3}"", New Object() {1, 2})
        Console.Write(""{0} {1} {2} {3}"", New Object() {1, 2, 3, 4, 5})
        Console.WriteLine(""{0} {1} {2} {3}"", New Object() {1, 2, 3, 4, 5})
    End Sub
End Class
",
            GetCA2241BasicResultAt(6, 17),
            GetCA2241BasicResultAt(7, 9),
            GetCA2241BasicResultAt(8, 9));
        }

        [Fact]
        public void CA2241CSharpFormatStringParser()
        {
            VerifyCSharp(@"
using System;

public class C
{
    void Method()
    {
        var a = String.Format(""{0,-4 :xd}"", 1);
        var b = String.Format(""{0   ,    5 : d} {1}"", 1, 2);
        var c = String.Format(""{0:d} {1} {2}"", 1, 2, 3);
        var d = String.Format(""{0, 5} {1} {2} {3}"", 1, 2, 3, 4);

        Console.Write(""{0,1}"", 1);
        Console.Write(""{0:   x} {1}"", 1, 2);
        Console.Write(""{{escape}}{0} {1} {2}"", 1, 2, 3);
        Console.Write(""{0: {{escape}} x} {1} {2} {3}"", 1, 2, 3, 4);
        Console.Write(""{0 , -10  :   {{escape}}  y} {1} {2} {3} {4}"", 1, 2, 3, 4, 5);
    }
}
");
        }

        [Theory]
        [WorkItem(2799, "https://github.com/dotnet/roslyn-analyzers/issues/2799")]
        // No configuration - validate no diagnostics in default configuration
        [InlineData("")]
        // Match by method name
        [InlineData("dotnet_code_quality.additional_string_formatting_methods = MyFormat")]
        // Setting only for Rule ID
        [InlineData("dotnet_code_quality." + ProvideCorrectArgumentsToFormattingMethodsAnalyzer.RuleId + ".additional_string_formatting_methods = MyFormat")]
        // Match by documentation ID without "M:" prefix
        [InlineData("dotnet_code_quality.additional_string_formatting_methods = Test.MyFormat(System.String,System.Object[])~System.String")]
        // Match by documentation ID with "M:" prefix
        [InlineData("dotnet_code_quality.additional_string_formatting_methods = M:Test.MyFormat(System.String,System.Object[])~System.String")]
        public void EditorConfigConfiguration_AdditionalStringFormattingMethods(string editorConfigText)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length > 0)
            {
                expected = new DiagnosticResult[]
                {
                    // Test0.cs(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetCA2241CSharpResultAt(8, 17)
                };
            }

            VerifyCSharp(@"
class Test
{
    public static string MyFormat(string format, params object[] args) => format;

    void M1(string param)
    {
        var a = MyFormat("""", 1);
    }
}", GetEditorConfigAdditionalFile(editorConfigText), expected);

            expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length > 0)
            {
                expected = new DiagnosticResult[]
                {
                    // Test0.vb(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetCA2241BasicResultAt(8, 17)
                };
            }

            VerifyBasic(@"
Class Test
    Public Shared Function MyFormat(format As String, ParamArray args As Object()) As String
        Return format
    End Function

    Private Sub M1(ByVal param As String)
        Dim a = MyFormat("""", 1)
    End Sub
End Class", GetEditorConfigAdditionalFile(editorConfigText), expected);
        }

        #endregion

        private static DiagnosticResult GetCA2241CSharpResultAt(int line, int column)
        {
            return GetCSharpResultAt(line, column, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.RuleId, MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentsToFormattingMethodsMessage);
        }

        private static DiagnosticResult GetCA2241BasicResultAt(int line, int column)
        {
            return GetBasicResultAt(line, column, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.RuleId, MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentsToFormattingMethodsMessage);
        }
    }
}