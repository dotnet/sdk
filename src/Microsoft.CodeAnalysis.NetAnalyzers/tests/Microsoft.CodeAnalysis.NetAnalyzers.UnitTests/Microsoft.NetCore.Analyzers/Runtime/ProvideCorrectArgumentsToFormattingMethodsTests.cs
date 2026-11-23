// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideCorrectArgumentsToFormattingMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideCorrectArgumentsToFormattingMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class ProvideCorrectArgumentsToFormattingMethodsTests
    {
        #region Diagnostic Tests

        [TestMethod]
        public async Task CA2241CSharpStringAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                public class C
                {
                    void Method()
                    {
                        var a = String.Format("", 1);
                        var b = String.Format("{0}", 1, 2);
                        var c = String.Format("{0} {1}", 1, 2, 3);
                        var d = String.Format("{0} {1} {2}", 1, 2, 3, 4);
                        var e = string.Format("{0} {0}", 1, 2);

                        IFormatProvider p = null;
                        var f = String.Format(p, "", 1);
                        var g = String.Format(p, "{0}", 1, 2);
                        var h = String.Format(p, "{0} {1}", 1, 2, 3);
                        var i = String.Format(p, "{0} {1} {2}", 1, 2, 3, 4);
                        var j = string.Format(arg0: 1, format: "{0}");
                        var k = string.Format(arg0: 1, format: "{0} {1}");
                    }
                }

                """,
            GetCSharpResultAt(8, 17),
            GetCSharpResultAt(9, 17),
            GetCSharpResultAt(10, 17),
            GetCSharpResultAt(11, 17),
            GetCSharpResultAt(12, 17),

            GetCSharpResultAt(15, 17),
            GetCSharpResultAt(16, 17),
            GetCSharpResultAt(17, 17),
            GetCSharpResultAt(18, 17),
            GetCSharpResultAt(20, 17));
        }

        [TestMethod]
        public async Task CA2241CSharpConsoleWriteAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                public class C
                {
                    void Method()
                    {
                        Console.Write("", 1);
                        Console.Write("{0}", 1, 2);
                        Console.Write("{0} {1}", 1, 2, 3);
                        Console.Write("{0} {1} {2}", 1, 2, 3, 4);
                        Console.Write("{0} {1} {2} {3}", 1, 2, 3, 4, 5);
                    }
                }

                """,
            GetCSharpResultAt(8, 9),
            GetCSharpResultAt(9, 9),
            GetCSharpResultAt(10, 9),
            GetCSharpResultAt(11, 9),
            GetCSharpResultAt(12, 9));
        }

        [TestMethod]
        public async Task CA2241CSharpConsoleWriteLineAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                public class C
                {
                    void Method()
                    {
                        Console.WriteLine("", 1);
                        Console.WriteLine("{0}", 1, 2);
                        Console.WriteLine("{0} {1}", 1, 2, 3);
                        Console.WriteLine("{0} {1} {2}", 1, 2, 3, 4);
                        Console.WriteLine("{0} {1} {2} {3}", 1, 2, 3, 4, 5);
                    }
                }

                """,
            GetCSharpResultAt(8, 9),
            GetCSharpResultAt(9, 9),
            GetCSharpResultAt(10, 9),
            GetCSharpResultAt(11, 9),
            GetCSharpResultAt(12, 9));
        }

        [TestMethod]
        public async Task CA2241CSharpPassingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                public class C
                {
                    void Method()
                    {
                        var a = String.Format("{0}", 1);
                        var b = String.Format("{0} {1}", 1, 2);
                        var c = String.Format("{0} {1} {2}", 1, 2, 3);
                        var d = String.Format("{0} {1} {2} {3}", 1, 2, 3, 4);
                        var e = String.Format("{0} {1} {2} {0}", 1, 2, 3);
                        var f = String.Format("{0} {0} {0} {0}", 1);

                        Console.Write("{0}", 1);
                        Console.Write("{0} {1}", 1, 2);
                        Console.Write("{0} {1} {2}", 1, 2, 3);
                        Console.Write("{0} {1} {2} {3}", 1, 2, 3, 4);
                        Console.Write("{0} {1} {2} {3} {4}", 1, 2, 3, 4, 5);
                        Console.Write("{0} {1} {2} {3} {0}", 1, 2, 3, 4);
                        Console.Write("{0} {0} {0} {0} {0}", 1);

                        Console.WriteLine("{0}", 1);
                        Console.WriteLine("{0} {1}", 1, 2);
                        Console.WriteLine("{0} {1} {2}", 1, 2, 3);
                        Console.WriteLine("{0} {1} {2} {3}", 1, 2, 3, 4);
                        Console.WriteLine("{0} {1} {2} {3} {4}", 1, 2, 3, 4, 5);
                        Console.WriteLine("{0} {1} {2} {3} {0}", 1, 2, 3, 4);
                        Console.WriteLine("{0} {0} {0} {0} {0}", 1);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task CA2241CSharpDifferentDiagnosticsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                public class C
                {
                    void Method()
                    {
                        var a = String.Format("{1}", 1);
                        var b = String.Format("{0} {1}", 1, 2);
                        var c = String.Format("{0} {1}", 1, 2, 3);
                        var d = String.Format("{0} {1} {2", 1, 2, 3);

                        Console.Write("{1}", 1);
                        Console.Write("{0} {1}", 1, 2);
                        Console.Write("{0} {1}", 1, 2, 3);
                        Console.Write("{0} {1} {2", 1, 2, 3);

                        Console.WriteLine("{1}", 1);
                        Console.WriteLine("{0} {1}", 1, 2);
                        Console.WriteLine("{0} {1}", 1, 2, 3);
                        Console.WriteLine("{0} {1} {2", 1, 2, 3);
                    }
                }

                """,
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

        [TestMethod]
        public async Task CA2241CSharpExplicitObjectArraySupportedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""

                using System;

                public class C
                {
                    void Method()
                    {
                        var s = String.Format("{0} {1} {2} {3}", new object[] {1, 2});
                        Console.Write("{0} {1} {2} {3}", new object[] {1, 2, 3, 4, 5});
                        Console.WriteLine("{0} {1} {2} {3}", new object[] {1, 2, 3, 4, 5});
                    }
                }

                """,
            GetCSharpResultAt(8, 17),
            GetCSharpResultAt(9, 9),
            GetCSharpResultAt(10, 9));
        }

        [TestMethod]
        public async Task CA2241CSharpVarArgsNotSupportedAsync()
        {
            // currently not supported due to "https://github.com/dotnet/roslyn/issues/7346"
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = """
                    using System;

                    public class C
                    {
                        void Method()
                        {
                            Console.Write("{0} {1} {2} {3} {4}", 1, 2, 3, 4, __arglist(5));
                            Console.WriteLine("{0} {1} {2} {3} {4}", 1, 2, 3, 4, __arglist(5));
                        }
                    }
                    """,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task CA2241VBStringAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""

                Imports System

                Public Class C
                    Sub Method()
                        Dim a = String.Format("", 1)
                        Dim b = String.Format("{0}", 1, 2)
                        Dim c = String.Format("{0} {1}", 1, 2, 3)
                        Dim d = String.Format("{0} {1} {2}", 1, 2, 3, 4)

                        Dim p as IFormatProvider = Nothing
                        Dim e = String.Format(p, "", 1)
                        Dim f = String.Format(p, "{0}", 1, 2)
                        Dim g = String.Format(p, "{0} {1}", 1, 2, 3)
                        Dim h = String.Format(p, "{0} {1} {2}", 1, 2, 3, 4)
                        Dim i = String.Format(arg0:=1, format:="{0}")
                        Dim j = String.Format(arg0:=1, format:="{0} {1}")
                    End Sub
                End Class

                """,
            GetBasicResultAt(6, 17),
            GetBasicResultAt(7, 17),
            GetBasicResultAt(8, 17),
            GetBasicResultAt(9, 17),

            GetBasicResultAt(12, 17),
            GetBasicResultAt(13, 17),
            GetBasicResultAt(14, 17),
            GetBasicResultAt(15, 17),
            GetBasicResultAt(17, 17));
        }

        [TestMethod]
        public async Task CA2241VBConsoleWriteAsync()
        {
            // this works in VB
            // Dim s = Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4)
            // since VB bind it to __arglist version where we skip analysis
            // due to a bug - https://github.com/dotnet/roslyn/issues/7346
            // we might skip it only in C# since VB doesn't support __arglist
            await VerifyVB.VerifyAnalyzerAsync("""

                Imports System

                Public Class C
                    Sub Method()
                        Console.Write("", 1)
                        Console.Write("{0}", 1, 2)
                        Console.Write("{0} {1}", 1, 2, 3)
                        Console.Write("{0} {1} {2}", 1, 2, 3, 4)
                        Console.Write("{0} {1} {2} {3}", 1, 2, 3, 4, 5)
                    End Sub
                End Class

                """,
            GetBasicResultAt(6, 9),
            GetBasicResultAt(7, 9),
            GetBasicResultAt(8, 9),
#if NETCOREAPP
            GetBasicResultAt(9, 9),
#endif
            GetBasicResultAt(10, 9));
        }

        [TestMethod]
        public async Task CA2241VBConsoleWriteLineAsync()
        {
            // this works in VB
            // Dim s = Console.WriteLine(""{0} {1} {2}"", 1, 2, 3, 4)
            // since VB bind it to __arglist version where we skip analysis
            // due to a bug - https://github.com/dotnet/roslyn/issues/7346
            // we might skip it only in C# since VB doesn't support __arglist
            await VerifyVB.VerifyAnalyzerAsync("""

                Imports System

                Public Class C
                    Sub Method()
                        Console.WriteLine("", 1)
                        Console.WriteLine("{0}", 1, 2)
                        Console.WriteLine("{0} {1}", 1, 2, 3)
                        Console.WriteLine("{0} {1} {2}", 1, 2, 3, 4)
                        Console.WriteLine("{0} {1} {2} {3}", 1, 2, 3, 4, 5)
                    End Sub
                End Class

                """,
            GetBasicResultAt(6, 9),
            GetBasicResultAt(7, 9),
            GetBasicResultAt(8, 9),
#if NETCOREAPP
            GetBasicResultAt(9, 9),
#endif
            GetBasicResultAt(10, 9));
        }

        [TestMethod]
        public async Task CA2241VBPassingAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System

                Public Class C
                    Sub Method()
                        Dim a = String.Format("{0}", 1)
                        Dim b = String.Format("{0} {1}", 1, 2)
                        Dim c = String.Format("{0} {1} {2}", 1, 2, 3)
                        Dim d = String.Format("{0} {1} {2} {3}", 1, 2, 3, 4)

                        Console.Write("{0}", 1)
                        Console.Write("{0} {1}", 1, 2)
                        Console.Write("{0} {1} {2}", 1, 2, 3)
                        Console.Write("{0} {1} {2} {3}", 1, 2, 3, 4)
                        Console.Write("{0} {1} {2} {3} {4}", 1, 2, 3, 4, 5)

                        Console.WriteLine("{0}", 1)
                        Console.WriteLine("{0} {1}", 1, 2)
                        Console.WriteLine("{0} {1} {2}", 1, 2, 3)
                        Console.WriteLine("{0} {1} {2} {3}", 1, 2, 3, 4)
                        Console.WriteLine("{0} {1} {2} {3} {4}", 1, 2, 3, 4, 5)
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task CA2241VBExplicitObjectArraySupportedAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync("""

                Imports System

                Public Class C
                    Sub Method()
                        Dim s = String.Format("{0} {1} {2} {3}", New Object() {1, 2})
                        Console.Write("{0} {1} {2} {3}", New Object() {1, 2, 3, 4, 5})
                        Console.WriteLine("{0} {1} {2} {3}", New Object() {1, 2, 3, 4, 5})
                    End Sub
                End Class

                """,
            GetBasicResultAt(6, 17),
            GetBasicResultAt(7, 9),
            GetBasicResultAt(8, 9));
        }

        [TestMethod]
        public async Task CA2241CSharpFormatStringParserAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                public class C
                {
                    void Method()
                    {
                        var a = String.Format("{0,-4 :xd}", 1);
                        var b = String.Format("{0   ,    5 : d} {1}", 1, 2);
                        var c = String.Format("{0:d} {1} {2}", 1, 2, 3);
                        var d = String.Format("{0, 5} {1} {2} {3}", 1, 2, 3, 4);

                        Console.Write("{0,1}", 1);
                        Console.Write("{0:   x} {1}", 1, 2);
                        Console.Write("{{escape}}{0} {1} {2}", 1, 2, 3);
                        Console.Write("{0: {{escape}} x} {1} {2} {3}", 1, 2, 3, 4);
                        Console.Write("{0 , -10  :   {{escape}}  y} {1} {2} {3} {4}", 1, 2, 3, 4, 5);
                    }
                }
                """);
        }

        [TestMethod]
        [WorkItem(2799, "https://github.com/dotnet/roslyn-analyzers/issues/2799")]
        // No configuration - validate no diagnostics in default configuration
        [DataRow(null)]
        // Configured but disabled
        [DataRow(false)]
        // Configured and enabled
        [DataRow(true)]
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
                        """

                            class Test
                            {
                                public static string MyFormat(string format, params object[] args) => format;

                                void M1(string param)
                                {
                                    var a = MyFormat("", 1);
                                }
                            }
                            """
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}

                        """) }
                }
            };

            if (editorConfig == true)
            {
                csharpTest.ExpectedDiagnostics.Add(
                    // Test0.cs(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetCSharpResultAt(8, 17));
            }

            await csharpTest.RunAsync(CancellationToken.None);

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """

                            Class Test
                                Public Shared Function MyFormat(format As String, ParamArray args As Object()) As String
                                    Return format
                                End Function

                                Private Sub M1(ByVal param As String)
                                    Dim a = MyFormat("", 1)
                                End Sub
                            End Class
                            """
},
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}

                        """) }
                }
            };

            if (editorConfig == true)
            {
                basicTest.ExpectedDiagnostics.Add(
                    // Test0.vb(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetBasicResultAt(8, 17));
            }

            await basicTest.RunAsync(CancellationToken.None);
        }

        [TestMethod]
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
                        """
                            class Test
                            {
                                string Formatted(double value1, double value2)
                                {
                                    return value1 >= value2 ?
                                        value1.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) :
                                        value2.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                                }
                            }
                            """
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}
                        """) }
                }
            };

            await csharpTest.RunAsync(CancellationToken.None);

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """
                            Class Test
                                Private Function M1(ByVal value1 as Double, ByVal value2 as Double) As String
                                    If value1 > value2
                                        Return value1.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                                    Else
                                        Return value2.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                                    End If
                                End Function
                            End Class
                            """
},
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}
                        """) }
                }
            };

            await basicTest.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [WorkItem(2799, "https://github.com/dotnet/roslyn-analyzers/issues/2799")]
        // No configuration - validate no diagnostics in default configuration
        [DataRow("")]
        // Match by method name
        [DataRow("dotnet_code_quality.additional_string_formatting_methods = MyFormat")]
        // Setting only for Rule ID
        [DataRow("dotnet_code_quality." + ProvideCorrectArgumentsToFormattingMethodsAnalyzer.RuleId + ".additional_string_formatting_methods = MyFormat")]
        // Match by documentation ID without "M:" prefix
        [DataRow("dotnet_code_quality.additional_string_formatting_methods = Test.MyFormat(System.String,System.Object[])~System.String")]
        // Match by documentation ID with "M:" prefix
        [DataRow("dotnet_code_quality.additional_string_formatting_methods = M:Test.MyFormat(System.String,System.Object[])~System.String")]
        public async Task EditorConfigConfiguration_AdditionalStringFormattingMethodsAsync(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """

                            class Test
                            {
                                public static string MyFormat(string format, params object[] args) => format;

                                void M1(string param)
                                {
                                    var a = MyFormat("", 1);
                                }
                            }
                            """
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}

                        """) }
                }
            };

            if (editorConfigText.Length > 0)
            {
                csharpTest.ExpectedDiagnostics.Add(
                    // Test0.cs(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetCSharpResultAt(8, 17));
            }

            await csharpTest.RunAsync(CancellationToken.None);

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """

                            Class Test
                                Public Shared Function MyFormat(format As String, ParamArray args As Object()) As String
                                    Return format
                                End Function

                                Private Sub M1(ByVal param As String)
                                    Dim a = MyFormat("", 1)
                                End Sub
                            End Class
                            """
},
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}

                        """) }
                }
            };

            if (editorConfigText.Length > 0)
            {
                basicTest.ExpectedDiagnostics.Add(
                    // Test0.vb(8,17): warning CA2241: Provide correct arguments to formatting methods
                    GetBasicResultAt(8, 17));
            }

            await basicTest.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [WorkItem(6012, "https://github.com/dotnet/roslyn-analyzers/issues/6012")]
        public async Task EditorConfigConfiguration_StringSyntaxAnnotatedMethodsAsync()
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """

                            using System.Diagnostics.CodeAnalysis;

                            class Test
                            {
                                public static string MyFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string specification, params object[] args) => specification;

                                void M1(string param)
                                {
                                    var a = MyFormat("", 1);
                                }
                            }
                            """
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                }
            };

            csharpTest.ExpectedDiagnostics.Add(
                // Test0.cs(10,17): warning CA2241: Provide correct arguments to formatting methods
                GetCSharpResultAt(10, 17));

            await csharpTest.RunAsync(CancellationToken.None);

            var basicTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """

                            Imports System.Diagnostics.CodeAnalysis

                            Class Test
                                Public Shared Function MyFormat(<StringSyntax(StringSyntaxAttribute.CompositeFormat)> specification As String, ParamArray args As Object()) As String
                                    Return specification
                                End Function

                                Private Sub M1(ByVal param As String)
                                    Dim a = MyFormat("", 1)
                                End Sub
                            End Class
                            """
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                }
            };

            basicTest.ExpectedDiagnostics.Add(
                // Test0.vb(10,17): warning CA2241: Provide correct arguments to formatting methods
                GetBasicResultAt(10, 17));

            await basicTest.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task EditorConfigConfiguration_StringSyntaxAnnotatedMethodsMultipleFrameworksAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """
                        using System.Diagnostics.CodeAnalysis;

                        class Test
                        {
                            public static string MyFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string specification, params object[] args) => specification;

                            void M1(string param)
                            {
                                var a = [|MyFormat("", 1)|];
                                var b = [|NetStandardTest.MyFormat("", 1)|];
                            }
                        }
                        """
                    },
                    AdditionalProjects =
                    {
                        ["NetstandardReference"] =
                        {
                            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
                            Sources =
                            {
                                """
                                using System.Diagnostics.CodeAnalysis;

                                public class NetStandardTest
                                {
                                    public static string MyFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string specification, params object[] args) => specification;
                                }
                                """,
                                """
                                namespace System.Diagnostics.CodeAnalysis
                                {
                                    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
                                    internal sealed class StringSyntaxAttribute : Attribute
                                    {
                                        public StringSyntaxAttribute(string syntax)
                                        {
                                            Syntax = syntax;
                                            Arguments = Array.Empty<object?>();
                                        }

                                        public StringSyntaxAttribute(string syntax, params object?[] arguments)
                                        {
                                            Syntax = syntax;
                                            Arguments = arguments;
                                        }

                                        public string Syntax { get; }
                                        public object?[] Arguments { get; }
                                        public const string CompositeFormat = nameof(CompositeFormat);
                                    }
                                }
                                """
                            },
                        }
                    },
                    AdditionalProjectReferences =
                    {
                        "NetstandardReference"
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            }.RunAsync(CancellationToken.None);

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """
                        Imports System.Diagnostics.CodeAnalysis

                        Class Test
                            Public Shared Function MyFormat(<StringSyntax(StringSyntaxAttribute.CompositeFormat)> specification As String, ParamArray args As Object()) As String
                                Return specification
                            End Function

                            Private Sub M1(ByVal param As String)
                                Dim a = [|MyFormat("", 1)|]
                                Dim b = [|NetStandardTest.MyFormat("", 1)|]
                            End Sub
                        End Class
                        """
                    },
                    AdditionalProjects =
                    {
                        ["NetstandardReference", LanguageNames.CSharp] =
                        {
                            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
                            Sources =
                            {
                                """
                                using System.Diagnostics.CodeAnalysis;

                                public class NetStandardTest
                                {
                                    public static string MyFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string specification, params object[] args) => specification;
                                }
                                """,
                                """
                                namespace System.Diagnostics.CodeAnalysis
                                {
                                    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
                                    internal sealed class StringSyntaxAttribute : Attribute
                                    {
                                        public StringSyntaxAttribute(string syntax)
                                        {
                                            Syntax = syntax;
                                            Arguments = Array.Empty<object?>();
                                        }

                                        public StringSyntaxAttribute(string syntax, params object?[] arguments)
                                        {
                                            Syntax = syntax;
                                            Arguments = arguments;
                                        }

                                        public string Syntax { get; }
                                        public object?[] Arguments { get; }
                                        public const string CompositeFormat = nameof(CompositeFormat);
                                    }
                                }
                                """
                            },
                        }
                    },
                    AdditionalProjectReferences =
                    {
                        "NetstandardReference"
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [WorkItem(57259, "https://github.com/dotnet/sdk/issues/57259")]
        public async Task CSharpExtensionBlock_AnnotatedReceiver_ChecksArgumentCount()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp14,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                MarkupOptions = MarkupOptions.UseFirstDescriptor,
                TestCode = """
                    using System;
                    using System.Diagnostics.CodeAnalysis;

                    static class Extensions
                    {
                        extension([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
                        {
                            public string Fmt(object arg) => string.Format(format, arg);
                            public string Fmt(params object[] args) => string.Format(format, args);
                            public string FmtP(IFormatProvider provider, object arg) => string.Format(provider, format, arg);
                            public string FmtP(IFormatProvider provider, params object[] args) => string.Format(provider, format, args);
                            public string Append([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string other, params object[] args) => format + string.Format(other, args);
                        }

                        static void M(IFormatProvider provider, string unknownFormat, object[] unknownArgs)
                        {
                            [|"{0} {1}".Fmt(1)|];
                            [|"{0}".Fmt(1, 2)|];
                            [|"{0} {1}".FmtP(provider, 1)|];
                            [|"{0}".FmtP(provider, 1, 2)|];

                            "{0}".FmtP(provider, 1);
                            "{0} {1}".FmtP(provider, 1, 2);
                            [|"{0}".FmtP(args: new object[] { 1, 2 }, provider: provider)|];
                            unknownFormat.Fmt(1);
                            "{0}".FmtP(provider, unknownArgs);
                            [|unknownFormat.Append("{0}", 1, 2)|];
                            "{0} {1}"?[|.Fmt(1)|];
                            "{0}"?.Fmt(1);
                            unknownFormat?.Fmt(1);
                        }
                    }
                    """,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task CSharpExtensionBlock_AnnotatedReceiver_OnlyChecksFormatWhenNoArguments()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp14,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                TestCode = """
                    using System.Diagnostics.CodeAnalysis;

                    static class Extensions
                    {
                        extension([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
                        {
                            public void Parse() { }
                        }

                        extension(string text)
                        {
                            public void Ignore() { }
                        }

                        static void M()
                        {
                            "{0}".Parse();
                            {|#0:"{".Parse()|};
                            "{"?{|#1:.Parse()|};
                            "{".Ignore();
                        }
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule).WithLocation(0),
                    VerifyCS.Diagnostic(ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule).WithLocation(1),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task CSharpExtensionBlock_MixedFixedAndParamsArguments_OnlyChecksFormat()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp14,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                TestCode = """
                    using System.Diagnostics.CodeAnalysis;

                    static class Extensions
                    {
                        extension([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
                        {
                            public string Fmt(object first, params object[] rest) =>
                                string.Format(format, (object[])[first, .. rest]);
                        }

                        static void M(object[] unknownArgs)
                        {
                            "{0}".Fmt(1);
                            "{0} {1} {2}".Fmt(1, 2, 3);
                            "{0}".Fmt(1, unknownArgs);
                            {|#0:"{".Fmt(1)|};
                        }
                    }
                    """,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule).WithLocation(0) },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [WorkItem(90357, "https://github.com/dotnet/runtime/issues/90357")]
        public async Task CA2241CSharpMethodWithNoPossibleArgumentsOnlyChecksFormat()
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        """

                            using System.Diagnostics.CodeAnalysis;

                            class Test
                            {
                                public static int Parse([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format) => -1;

                                void M1(string param)
                                {
                                    var a = Parse("{0} {1}");
                                    var b = Parse("{0 {1}");
                                }
                            }
                            """
                    },
                    ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
                }
            };

            csharpTest.ExpectedDiagnostics.Add(
                GetCSharpResultAt(11, 17, ProvideCorrectArgumentsToFormattingMethodsAnalyzer.InvalidFormatRule));

            await csharpTest.RunAsync(CancellationToken.None);
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