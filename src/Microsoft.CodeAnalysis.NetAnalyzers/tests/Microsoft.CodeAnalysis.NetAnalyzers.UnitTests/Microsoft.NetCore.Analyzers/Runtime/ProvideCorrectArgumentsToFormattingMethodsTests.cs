// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideCorrectArgumentsToFormattingMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideCorrectArgumentsToFormattingMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class ProvideCorrectArgumentsToFormattingMethodsTests
    {
        #region Diagnostic Tests

        [Fact]
        public async Task CA2241CSharpStringAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            GetCSharpResultAt(8, 17),
            GetCSharpResultAt(9, 17),
            GetCSharpResultAt(10, 17),
            GetCSharpResultAt(11, 17),
            GetCSharpResultAt(12, 17),

            GetCSharpResultAt(15, 17),
            GetCSharpResultAt(16, 17),
            GetCSharpResultAt(17, 17),
            GetCSharpResultAt(18, 17));
        }

        [Fact]
        public async Task CA2241CSharpConsoleWriteAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            GetCSharpResultAt(8, 9),
            GetCSharpResultAt(9, 9),
            GetCSharpResultAt(10, 9),
            GetCSharpResultAt(11, 9),
            GetCSharpResultAt(12, 9));
        }

        [Fact]
        public async Task CA2241CSharpConsoleWriteLineAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            GetCSharpResultAt(8, 9),
            GetCSharpResultAt(9, 9),
            GetCSharpResultAt(10, 9),
            GetCSharpResultAt(11, 9),
            GetCSharpResultAt(12, 9));
        }

        [Fact]
        public async Task CA2241CSharpPassingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        var f = String.Format(""{0} {0} {0} {0}"", 1);

        Console.Write(""{0}"", 1);
        Console.Write(""{0} {1}"", 1, 2);
        Console.Write(""{0} {1} {2}"", 1, 2, 3);
        Console.Write(""{0} {1} {2} {3}"", 1, 2, 3, 4);
        Console.Write(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, 5);
        Console.Write(""{0} {1} {2} {3} {0}"", 1, 2, 3, 4);
        Console.Write(""{0} {0} {0} {0} {0}"", 1);

        Console.WriteLine(""{0}"", 1);
        Console.WriteLine(""{0} {1}"", 1, 2);
        Console.WriteLine(""{0} {1} {2}"", 1, 2, 3);
        Console.WriteLine(""{0} {1} {2} {3}"", 1, 2, 3, 4);
        Console.WriteLine(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, 5);
        Console.WriteLine(""{0} {1} {2} {3} {0}"", 1, 2, 3, 4);
        Console.WriteLine(""{0} {0} {0} {0} {0}"", 1);
    }
}
");
        }

        [Fact]
        public async Task CA2241CSharpDifferentDiagnosticsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class C
{
    void Method()
    {
        var a = String.Format(""{1}"", 1);
        var b = String.Format(""{0} {1}"", 1, 2);
        var c = String.Format(""{0} {1}"", 1, 2, 3);
        var d = String.Format(""{0} {1} {2"", 1, 2, 3);

        Console.Write(""{1}"", 1);
        Console.Write(""{0} {1}"", 1, 2);
        Console.Write(""{0} {1}"", 1, 2, 3);
        Console.Write(""{0} {1} {2"", 1, 2, 3);

        Console.WriteLine(""{1}"", 1);
        Console.WriteLine(""{0} {1}"", 1, 2);
        Console.WriteLine(""{0} {1}"", 1, 2, 3);
        Console.WriteLine(""{0} {1} {2"", 1, 2, 3);
    }
}
",
            GetCSharpResultAt(8, 17, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule),
            GetCSharpResultAt(10, 17, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule),
            GetCSharpResultAt(11, 17, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule),
            GetCSharpResultAt(13, 9, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule),
            GetCSharpResultAt(15, 9, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule),
            GetCSharpResultAt(16, 9, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule),
            GetCSharpResultAt(18, 9, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule),
            GetCSharpResultAt(20, 9, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule),
            GetCSharpResultAt(21, 9, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule));
        }

        [Fact]
        public async Task CA2241CSharpExplicitObjectArraySupportedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            GetCSharpResultAt(8, 17),
            GetCSharpResultAt(9, 9),
            GetCSharpResultAt(10, 9));
        }

        [Fact]
        public async Task CA2241CSharpVarArgsNotSupportedAsync()
        {
            // currently not supported due to "https://github.com/dotnet/roslyn/issues/7346"
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System;

public class C
{
    void Method()
    {
        Console.Write(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, __arglist(5));
        Console.WriteLine(""{0} {1} {2} {3} {4}"", 1, 2, 3, 4, __arglist(5));
    }
}
",
            }.RunAsync();
        }

        [Fact]
        public async Task CA2241VBStringAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
            GetBasicResultAt(6, 17),
            GetBasicResultAt(7, 17),
            GetBasicResultAt(8, 17),
            GetBasicResultAt(9, 17),

            GetBasicResultAt(12, 17),
            GetBasicResultAt(13, 17),
            GetBasicResultAt(14, 17),
            GetBasicResultAt(15, 17));
        }

        [Fact]
        public async Task CA2241VBConsoleWriteAsync()
        {
            // this works in VB
            // Dim s = Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4)
            // since VB bind it to __arglist version where we skip analysis
            // due to a bug - https://github.com/dotnet/roslyn/issues/7346
            // we might skip it only in C# since VB doesn't support __arglist
            await VerifyVB.VerifyAnalyzerAsync(@"
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
            GetBasicResultAt(6, 9),
            GetBasicResultAt(7, 9),
            GetBasicResultAt(8, 9),
#if NETCOREAPP
            GetBasicResultAt(9, 9),
#endif
            GetBasicResultAt(10, 9));
        }

        [Fact]
        public async Task CA2241VBConsoleWriteLineAsync()
        {
            // this works in VB
            // Dim s = Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4)
            // since VB bind it to __arglist version where we skip analysis
            // due to a bug - https://github.com/dotnet/roslyn/issues/7346
            // we might skip it only in C# since VB doesn't support __arglist
            await VerifyVB.VerifyAnalyzerAsync(@"
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
            GetBasicResultAt(6, 9),
            GetBasicResultAt(7, 9),
            GetBasicResultAt(8, 9),
#if NETCOREAPP
            GetBasicResultAt(9, 9),
#endif
            GetBasicResultAt(10, 9));
        }

        [Fact]
        public async Task CA2241VBPassingAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CA2241VBExplicitObjectArraySupportedAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class C
    Sub Method()
        Dim s = String.Format(""{0} {1} {2} {3}"", New Object() {1, 2})
        Console.Write(""{0} {1} {2} {3}"", New Object() {1, 2, 3, 4, 5})
        Console.WriteLine(""{0} {1} {2} {3}"", New Object() {1, 2, 3, 4, 5})
    End Sub
End Class
",
            GetBasicResultAt(6, 17),
            GetBasicResultAt(7, 9),
            GetBasicResultAt(8, 9));
        }

        [Fact]
        public async Task CA2241CSharpFormatStringParserAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        [InlineData(null)]
        // Configured but disabled
        [InlineData(false)]
        // Configured and enabled
        [InlineData(true)]
        public async Task EditorConfigConfiguration_HeuristicAdditionalStringFormattingMethodsAsync(bool? editorConfig)
        {
            string editorConfigText = editorConfig == null ? string.Empty :
                "dotnet_code_quality.try_determine_additional_string_formatting_methods_automatically = " + editorConfig.Value;

            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class Test
{
    public static string MyFormat(string format, params object[] args) => format;

    void M1(string param)
    {
        var a = MyFormat("""", 1);
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfig == true)
            {
                csharpTest.ExpectedDiagnostics.Add(
                    // Test0.cs(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetCSharpResultAt(8, 17));
            }

            await csharpTest.RunAsync();

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Class Test
    Public Shared Function MyFormat(format As String, ParamArray args As Object()) As String
        Return format
    End Function

    Private Sub M1(ByVal param As String)
        Dim a = MyFormat("""", 1)
    End Sub
End Class"
},
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfig == true)
            {
                basicTest.ExpectedDiagnostics.Add(
                    // Test0.vb(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetBasicResultAt(8, 17));
            }

            await basicTest.RunAsync();
        }

        [Fact]
        [WorkItem(7023, "https://github.com/dotnet/roslyn-analyzers/issues/7023")]

        public async Task EditorConfigConfiguration_HeuristicAdditionalStringFormattingMethodsShouldNotConsiderIFormattableToString()
        {
            string editorConfigText =
                "dotnet_code_quality.try_determine_additional_string_formatting_methods_automatically = true";

            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class Test
{
    string Formatted(double value1, double value2)
    {
        return value1 >= value2 ?
            value1.ToString(""F1"", System.Globalization.CultureInfo.InvariantCulture) :
            value2.ToString(""F1"", System.Globalization.CultureInfo.InvariantCulture);
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            await csharpTest.RunAsync();

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Class Test
    Private Function M1(ByVal value1 as Double, ByVal value2 as Double) As String
        If value1 > value2
            Return value1.ToString(""F1"", System.Globalization.CultureInfo.InvariantCulture)
        Else
            Return value2.ToString(""F1"", System.Globalization.CultureInfo.InvariantCulture)
        End If
    End Function
End Class"
},
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            await basicTest.RunAsync();
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
        public async Task EditorConfigConfiguration_AdditionalStringFormattingMethodsAsync(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class Test
{
    public static string MyFormat(string format, params object[] args) => format;

    void M1(string param)
    {
        var a = MyFormat("""", 1);
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length > 0)
            {
                csharpTest.ExpectedDiagnostics.Add(
                    // Test0.cs(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetCSharpResultAt(8, 17));
            }

            await csharpTest.RunAsync();

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Class Test
    Public Shared Function MyFormat(format As String, ParamArray args As Object()) As String
        Return format
    End Function

    Private Sub M1(ByVal param As String)
        Dim a = MyFormat("""", 1)
    End Sub
End Class"
},
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };

            if (editorConfigText.Length > 0)
            {
                basicTest.ExpectedDiagnostics.Add(
                    // Test0.vb(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetBasicResultAt(8, 17));
            }

            await basicTest.RunAsync();
        }

        [Fact]
        [WorkItem(6012, "https://github.com/dotnet/roslyn-analyzers/issues/6012")]
        public async Task EditorConfigConfiguration_StringSyntaxAnnotatedMethodsAsync()
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Diagnostics.CodeAnalysis;

class Test
{
    public static string MyFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string specification, params object[] args) => specification;

    void M1(string param)
    {
        var a = MyFormat("""", 1);
    }
}"
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                }
            };

            csharpTest.ExpectedDiagnostics.Add(
                // Test0.cs(10,17): warning CA2241: Provide correct arguments to formatting methods
                GetCSharpResultAt(10, 17));

            await csharpTest.RunAsync();

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System.Diagnostics.CodeAnalysis

Class Test
    Public Shared Function MyFormat(<StringSyntax(StringSyntaxAttribute.CompositeFormat)> specification As String, ParamArray args As Object()) As String
        Return specification
    End Function

    Private Sub M1(ByVal param As String)
        Dim a = MyFormat("""", 1)
    End Sub
End Class"
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                }
            };

            basicTest.ExpectedDiagnostics.Add(
                // Test0.vb(10,17): warning CA2241: Provide correct arguments to formatting methods
                GetBasicResultAt(10, 17));

            await basicTest.RunAsync();
        }

        [Fact]
        [WorkItem(90357, "https://github.com/dotnet/runtime/issues/90357")]
        public async Task CA2241CSharpMethodWithNoPossibleArgumentsOnlyChecksFormat()
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Diagnostics.CodeAnalysis;

class Test
{
    public static int Parse([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format) => -1;

    void M1(string param)
    {
        var a = Parse(""{0} {1}"");
        var b = Parse(""{0 {1}"");
    }
}"
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                }
            };

            csharpTest.ExpectedDiagnostics.Add(
                GetCSharpResultAt(11, 17, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule));

            await csharpTest.RunAsync();
        }

        #endregion

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
            => GetCSharpResultAt(line, column, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule);

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor descriptor)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic(descriptor)
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic(ProvideCorrectArgumentsToFormattingMethodsAnalyzer.ArgumentCountRule)
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not use banned APIs
    }
}