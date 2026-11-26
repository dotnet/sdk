// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

 

using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class PreferStringContainsOverIndexOfTests
    {
        [TestMethod]
        [DataRow("This", false, " == ", " -1")]
        [DataRow("a", true, " == ", " -1")]
        [DataRow("This", false, " >= ", " 0")]
        [DataRow("a", true, " >= ", " 0")]
        [DataRow("This", false, " != ", " -1")]
        [DataRow("a", true, " != ", " -1")]
        public async Task TestStringAndCharAsync(string input, bool isCharTest, string operatorKind, string value)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string endQuote = isCharTest ? "'" : "\", System.StringComparison.Ordinal";
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = [|str.IndexOf(
                """ + startQuote + input + endQuote + """
                )|];
                            if (index
                """ + operatorKind + value + """
                )
                            {
                            }
                        }
                    }
                }
                """;

            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string stringComparison = isCharTest ? "" : ", System.StringComparison.Ordinal";
            operatorKind = ToBasicOperator(operatorKind);

            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index As Integer = [|Str.IndexOf(
                """ + startQuote + input + startQuote + vbCharLiteral + stringComparison + """
                )|]
                            If index
                """ + operatorKind + value + """
                 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(" == ", " -1")]
        [DataRow(" >= ", " 0")]
        [DataRow(" != ", " -1")]
        public async Task TestStringNoComparisonArgumentAsync(string operatorKind, string value)
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = [|str.IndexOf("This")|];
                            if (index
                """ + operatorKind + value + """
                )
                            {

                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            operatorKind = ToBasicOperator(operatorKind);
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index As Integer = [|Str.IndexOf("This")|]
                            If index
                """ + operatorKind + value + """
                 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(" == ", " -1")]
        [DataRow(" >= ", " 0")]
        [DataRow(" != ", " -1")]
        public async Task TestCharAndOrdinalAsync(string operatorKind, string value)
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = [|str.IndexOf('a', System.StringComparison.Ordinal)|];
                            if (index
                """ + operatorKind + value + """
                )
                            {

                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            operatorKind = ToBasicOperator(operatorKind);
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index As Integer = [|Str.IndexOf("a"c, System.StringComparison.Ordinal)|]
                            If index
                """ + operatorKind + value + """
                 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false)]
        [DataRow("a", true)]
        public async Task TestStringAndCharWithMultipleDiagnosticsAsync(string input, bool isCharTest)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string endQuote = isCharTest ? "'" : "\", System.StringComparison.Ordinal";
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index1 = [|str.IndexOf(
                """ + startQuote + input + endQuote + """
                )|];
                            int index2 = [|str.IndexOf(
                """ + startQuote + input + endQuote + """
                )|];
                            if (index2 == -1 || -1 == index1)
                            {

                            }
                            if ([|str.IndexOf(
                """ + startQuote + input + endQuote + """
                ) == -1|])
                            {

                            }
                        }
                    }
                }
                """;

            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string stringComparison = isCharTest ? "" : ", System.StringComparison.Ordinal";
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index1 As Integer = [|Str.IndexOf(
                """ + startQuote + input + startQuote + vbCharLiteral + stringComparison + """
                )|]
                            Dim index2 As Integer = [|Str.IndexOf(
                """ + startQuote + input + startQuote + vbCharLiteral + stringComparison + """
                )|]
                            If index2 = -1 OR -1 = index1 Then

                            End If
                            If [|Str.IndexOf(
                """ + startQuote + input + startQuote + vbCharLiteral + stringComparison + """
                ) = -1|] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false)]
        [DataRow("a", true)]
        public async Task TestStringAndCharWithComparisonAsync(string input, bool isCharTest)
        {
            string quotes = isCharTest ? "'" : "\"";
            string csInput = """

                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = [|str.IndexOf(
                """ + quotes + input + quotes + """
                , System.StringComparison.InvariantCulture)|];
                            if (index == -1)
                            {

                            }
                        }
                    }
                }
                """;

            var test = new VerifyCS.Test
            {
                TestCode = csInput,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await test.RunAsync(CancellationToken.None);

            quotes = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index As Integer = [|Str.IndexOf(
                """ + quotes + input + quotes + vbCharLiteral + """
                , System.StringComparison.InvariantCulture)|]
                            If index = -1 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false)]
        [DataRow("a", true)]
        public async Task TestLeftAndRightOperandInvocationsAsync(string input, bool isCharTest)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string endQuote = isCharTest ? "'" : "\", System.StringComparison.Ordinal";
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            if ([|str.IndexOf(
                """ + startQuote + input + endQuote + """
                ) == -1|])
                            {

                            }
                            if ([|-1 == str.IndexOf(
                """ + startQuote + input + endQuote + """
                )|])
                            {

                            }
                        }
                    }
                }
                """;
            string csFix = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            if (!str.Contains(
                """ + startQuote + input + startQuote + """
                ))
                            {

                            }
                            if (!str.Contains(
                """ + startQuote + input + startQuote + """
                ))
                            {

                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string stringComparison = isCharTest ? "" : ", System.StringComparison.Ordinal";
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If [|Str.IndexOf(
                """ + startQuote + input + startQuote + vbCharLiteral + stringComparison + """
                ) = -1|] Then

                            End If
                            If [|-1 = Str.IndexOf(
                """ + startQuote + input + startQuote + vbCharLiteral + stringComparison + """
                )|] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            string vbFix = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If Not Str.Contains(
                """ + startQuote + input + startQuote + vbCharLiteral + """
                ) Then

                            End If
                            If Not Str.Contains(
                """ + startQuote + input + startQuote + vbCharLiteral + """
                ) Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false)]
        [DataRow("a", true)]
        public async Task TestStringAndCharNamedArgumentCombinationVBAsync(string input, bool isCharTest)
        {
            string startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If [|Str.IndexOf(value:=
                """ + startQuote + input + startQuote + vbCharLiteral + """
                , System.StringComparison.Ordinal) = -1|] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            string vbFix = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If Not Str.Contains(value:=
                """ + startQuote + input + startQuote + vbCharLiteral + """
                ) Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);

            vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If [|Str.IndexOf(value:=
                """ + startQuote + input + startQuote + vbCharLiteral + """
                , comparisonType:= System.StringComparison.OrdinalIgnoreCase) = -1|] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            vbFix = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If Not Str.Contains(value:=
                """ + startQuote + input + startQuote + vbCharLiteral + """
                , comparisonType:= System.StringComparison.OrdinalIgnoreCase) Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);

            vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If [|Str.IndexOf(
                """ + startQuote + input + startQuote + vbCharLiteral + """
                , comparisonType:= System.StringComparison.OrdinalIgnoreCase) = -1|] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            vbFix = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If Not Str.Contains(
                """ + startQuote + input + startQuote + vbCharLiteral + """
                , comparisonType:= System.StringComparison.OrdinalIgnoreCase) Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);

            vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If [|Str.IndexOf(comparisonType:= System.StringComparison.OrdinalIgnoreCase, value:=
                """ + startQuote + input + startQuote + vbCharLiteral + """
                ) = -1|] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            vbFix = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            If Not Str.Contains(value:=
                """ + startQuote + input + startQuote + vbCharLiteral + """
                , comparisonType:= System.StringComparison.OrdinalIgnoreCase) Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false)]
        [DataRow("a", true)]
        public async Task TestStringAndCharNamedArgumentCombinationsCSAsync(string input, bool isCharTest)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string csInput = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if ([|str.IndexOf(value: 
                """ + startQuote + input + startQuote + """
                , System.StringComparison.Ordinal) == -1|])
                            {

                            }
                        } 
                    } 
                }
                """;
            string csFix = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if (!str.Contains(value: 
                """ + startQuote + input + startQuote + """
                ))
                            {

                            }
                        } 
                    } 
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            csInput = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if ([|str.IndexOf(value: 
                """ + startQuote + input + startQuote + """
                , comparisonType: System.StringComparison.OrdinalIgnoreCase) == -1|])
                            {

                            }
                        } 
                    } 
                }
                """;
            csFix = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if (!str.Contains(value: 
                """ + startQuote + input + startQuote + """
                , comparisonType: System.StringComparison.OrdinalIgnoreCase))
                            {

                            }
                        } 
                    } 
                }
                """;
            testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            csInput = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if ([|str.IndexOf(
                """ + startQuote + input + startQuote + """
                , comparisonType: System.StringComparison.OrdinalIgnoreCase) == -1|])
                            {

                            }
                        } 
                    } 
                }
                """;
            csFix = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if (!str.Contains(
                """ + startQuote + input + startQuote + """
                , comparisonType: System.StringComparison.OrdinalIgnoreCase))
                            {

                            }
                        } 
                    } 
                }
                """;
            testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            csInput = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if ([|str.IndexOf(comparisonType: System.StringComparison.OrdinalIgnoreCase, value: 
                """ + startQuote + input + startQuote + """
                ) == -1|])
                            {

                            }
                        } 
                    } 
                }
                """;
            csFix = """
                namespace TestNamespace 
                { 
                    class TestClass 
                    { 
                        private void TestMethod() 
                        { 
                            const string str = "This is a string";
                            if (!str.Contains(comparisonType: System.StringComparison.OrdinalIgnoreCase, value: 
                """ + startQuote + input + startQuote + """
                ))
                            {

                            }
                        } 
                    } 
                }
                """;
            testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false, ", 1")]
        [DataRow("a", true, ", 1")]
        [DataRow("This", false, ", 1", ", 2")]
        [DataRow("a", true, ", 1", ", 2")]
        [DataRow("This", false, ", 1", ", System.StringComparison.OrdinalIgnoreCase")]
        [DataRow("This", false, ", 1", ", 2", ", System.StringComparison.OrdinalIgnoreCase")]
        public async Task TestTooManyArgumentsToIndexOfAsync(string input, bool isCharTest, params string[] inputArguments)
        {
            string quotes = isCharTest ? "'" : "\"";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < inputArguments.Length; i++)
            {
                sb.Append(inputArguments[i]);
            }

            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = str.IndexOf(
                """ + quotes + input + quotes + sb.ToString() + """
                );
                            if (index == -1)
                            {

                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false)]
        [DataRow("a", true)]
        public async Task TestIndexWrittenToAsync(string input, bool isCharTest)
        {
            string quotes = isCharTest ? "'" : "\"";
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = str.IndexOf(
                """ + quotes + input + quotes + """
                );
                            index += 2;
                            if (index == -1)
                            {

                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            quotes = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index As Integer = Str.IndexOf(
                """ + quotes + input + quotes + vbCharLiteral + """
                )
                            index += 2
                            If index = -1 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("This", false)]
        [DataRow("a", true)]
        public async Task TestIndexWrittenToAfterAsync(string input, bool isCharTest)
        {
            string quotes = isCharTest ? "'" : "\"";
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = str.IndexOf(
                """ + quotes + input + quotes + """
                );
                            if (index == -1)
                            {

                            }
                            index += 2;
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            quotes = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index As Integer = Str.IndexOf(
                """ + quotes + input + quotes + vbCharLiteral + """
                )
                            If index = -1 Then

                            End If
                            index += 2
                        End Sub
                    End Class
                End Class
                """;
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestNonSupportedTargetAsync()
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private bool TestMethod()
                        {
                            string str = "This is a string";
                            return str.IndexOf("This") == -1;
                        }
                    }
                }
                """;
            var test = new VerifyCS.Test
            {
                TestCode = csInput,
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
            };
            await test.RunAsync(CancellationToken.None);

            string csInputStringAndArgument = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private bool TestMethod()
                        {
                            string str = "This is a string";
                            return str.IndexOf("a", System.StringComparison.InvariantCulture) == -1;
                        }
                    }
                }
                """;
            test = new VerifyCS.Test
            {
                TestCode = csInputStringAndArgument,
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
            };
            await test.RunAsync(CancellationToken.None);

            string csCharInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private bool TestMethod()
                        {
                            string str = "This is a string";
                            return str.IndexOf('T') == -1;
                        }
                    }
                }
                """;
            test = new VerifyCS.Test
            {
                TestCode = csCharInput,
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
            };
            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(" != ", "3")]
        [DataRow(" > ", "2")]
        [DataRow(" >= ", "2")]
        public async Task TestNonSupportedOperationKindAsync(string operatorKind, string right)
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            string str = "This is a string";
                            int index = str.IndexOf("This");
                            if (index
                """ + operatorKind + right + """
                )
                            {
                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestRightOperandIsVariableAsync()
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            string str = "This is a string";
                            int compare = 5;
                            int index = str.IndexOf("This");
                            if (index == compare)
                            {
                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestReadOutsideAsync()
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            string str = "This is a string";
                            int index = str.IndexOf("This");
                            if (index == -1)
                            {
                            }
                            int foo = index;
                        }
                    }
                }
                """;
            var test = new VerifyCS.Test
            {
                TestCode = csInput,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            await test.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(" == ", " -1", "!")]
        [DataRow(" >= ", " 0", "")]
        [DataRow(" != ", " -1", "")]
        public async Task TestFunctionParameterAsync(string operatorKind, string value, string notString)
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod(string str)
                        {
                            if ([|str.IndexOf("This")
                """ + operatorKind + value + """
                |])
                            {
                            }
                        }
                    }
                }
                """;
            string csFix = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod(string str)
                        {
                            if (
                """ + notString + """"str.Contains("This", System.StringComparison.CurrentCulture)"""" + """
                )
                            {
                            }
                        }
                    }
                }
                """;
            var testCulture = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testCulture.RunAsync(CancellationToken.None);

            operatorKind = ToBasicOperator(operatorKind);
            notString = notString == "!" ? " Not" : notString;
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub AMethod(arg As String)
                            If [|arg.IndexOf("This")
                """ + operatorKind + value + """
                |] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            string vbFix = """
                Public Class StringOf
                    Class TestClass
                        Public Sub AMethod(arg As String)
                            If
                """ + notString + """" arg.Contains("This", System.StringComparison.CurrentCulture)"""" + """
                 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(" == ", " -1", "!")]
        [DataRow(" >= ", " 0", "")]
        [DataRow(" != ", " -1", "")]
        public async Task TestFunctionParameterWithStringComparisonArgumentAsync(string operatorKind, string value, string notString)
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod(string str, System.StringComparison comparison)
                        {
                            if ([|str.IndexOf("This", comparison)
                """ + operatorKind + value + """
                |])
                            {
                            }
                        }
                    }
                }
                """;
            string csFix = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod(string str, System.StringComparison comparison)
                        {
                            if (
                """ + notString + """"str.Contains("This", comparison)"""" + """
                )
                            {
                            }
                        }
                    }
                }
                """;
            var testCulture = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testCulture.RunAsync(CancellationToken.None);

            operatorKind = ToBasicOperator(operatorKind);
            notString = notString == "!" ? " Not" : notString;
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub AMethod(arg As String, comparison As System.StringComparison)
                            If [|arg.IndexOf("This", comparison)
                """ + operatorKind + value + """
                |] Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            string vbFix = """
                Public Class StringOf
                    Class TestClass
                        Public Sub AMethod(arg As String, comparison As System.StringComparison)
                            If
                """ + notString + """" arg.Contains("This", comparison)"""" + """
                 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(" == ", " -1")]
        [DataRow(" >= ", " 0")]
        [DataRow(" != ", " -1")]
        public async Task TestReversedMultipleDeclarationsAsync(string operatorKind, string value)
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int a = 5, index = [|str.IndexOf('a', System.StringComparison.Ordinal)|];
                            if (index
                """ + operatorKind + value + """
                )
                            {

                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            operatorKind = ToBasicOperator(operatorKind);
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim a As Integer = 5, index = [|Str.IndexOf("a"c, System.StringComparison.Ordinal)|]
                            If index
                """ + operatorKind + value + """
                 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow(" == ", " -1")]
        [DataRow(" >= ", " 0")]
        [DataRow(" != ", " -1")]
        public async Task TestMultipleDeclarationsAsync(string operatorKind, string value)
        {
            string csInput = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod()
                        {
                            const string str = "This is a string";
                            int index = [|str.IndexOf('a', System.StringComparison.Ordinal)|], aa = 5;
                            if (index
                """ + operatorKind + value + """
                )
                            {

                            }
                        }
                    }
                }
                """;
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync(CancellationToken.None);

            operatorKind = ToBasicOperator(operatorKind);
            string vbInput = """
                Public Class StringOf
                    Class TestClass
                        Public Sub Main()
                            Dim Str As String = "This is a statement"
                            Dim index As Integer = [|Str.IndexOf("a"c, System.StringComparison.Ordinal)|], aa = 5
                            If index
                """ + operatorKind + value + """
                 Then

                            End If
                        End Sub
                    End Class
                End Class
                """;

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync(CancellationToken.None);
        }

        #region Helpers

        private string ToBasicOperator(string op)
        {
            return op switch
            {
                " == " => " = ",
                " != " => " <> ",
                _ => op
            };
        }

        #endregion

        [TestMethod]
        public async Task NestedIndexOfComparison_FixAllRewritesBoth_CSharpAsync()
        {
            string source = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod(string str)
                        {
                            if ([|str.IndexOf(([|str.IndexOf("a", System.StringComparison.Ordinal) != -1|]).ToString(), System.StringComparison.Ordinal) != -1|])
                            {
                            }
                        }
                    }
                }
                """;
            string fixedSource = """
                namespace TestNamespace
                {
                    class TestClass
                    {
                        private void TestMethod(string str)
                        {
                            if (str.Contains((str.Contains("a")).ToString()))
                            {
                            }
                        }
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        public async Task NestedIndexOfComparison_FixAllRewritesBoth_BasicAsync()
        {
            string source = """
                Namespace TestNamespace
                    Class TestClass
                        Private Sub TestMethod(Str As String)
                            If [|Str.IndexOf(([|Str.IndexOf("a", System.StringComparison.Ordinal) <> -1|]).ToString(), System.StringComparison.Ordinal) <> -1|] Then
                            End If
                        End Sub
                    End Class
                End Namespace
                """;
            string fixedSource = """
                Namespace TestNamespace
                    Class TestClass
                        Private Sub TestMethod(Str As String)
                            If Str.Contains((Str.Contains("a")).ToString()) Then
                            End If
                        End Sub
                    End Class
                End Namespace
                """;
            await VerifyVB.VerifyCodeFixAsync(source, fixedSource);
        }

        [TestMethod]
        [DataRow("span.IndexOf('a') >= 0", "span.Contains('a')")]
        [DataRow("span.IndexOf('a') != -1", "span.Contains('a')")]
        [DataRow("span.IndexOf('a') < 0", "!span.Contains('a')")]
        [DataRow("span.IndexOf('a') == -1", "!span.Contains('a')")]
        [DataRow("span.IndexOf('a') is >= 0", "span.Contains('a')")]
        [DataRow("span.IndexOf('a') is < 0", "!span.Contains('a')")]
        [DataRow("span.IndexOfAny('a', 'b') >= 0", "span.ContainsAny('a', 'b')")]
        [DataRow("span.IndexOfAny('a', 'b') < 0", "!span.ContainsAny('a', 'b')")]
        [DataRow("span.IndexOfAny(\"abc\") >= 0", "span.ContainsAny(\"abc\")")]
        [DataRow("span.IndexOfAnyExcept('a', 'b') >= 0", "span.ContainsAnyExcept('a', 'b')")]
        [DataRow("span.IndexOfAnyExcept('a', 'b') < 0", "!span.ContainsAnyExcept('a', 'b')")]
        [DataRow("span.IndexOf(\"ab\", System.StringComparison.OrdinalIgnoreCase) >= 0", "span.Contains(\"ab\", System.StringComparison.OrdinalIgnoreCase)")]
        [DataRow("str.AsSpan().IndexOf('a') >= 0", "str.AsSpan().Contains('a')")]
        [DataRow("ints.IndexOf(5) < 0", "!ints.Contains(5)")]
        [DataRow("MemoryExtensions.IndexOf(span, 'a') >= 0", "span.Contains('a')")]
        [DataRow("span.IndexOf(value: 'a') >= 0", "span.Contains(value: 'a')")]
        public async Task SpanIndexOfComparedWithZero_OffersContainsAsync(string condition, string fixedCondition)
        {
            const string template = """
                using System;

                class TestClass
                {
                    void TestMethod(ReadOnlySpan<char> span, Span<int> ints, string str)
                    {
                        if ({0})
                        {
                        }
                    }
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = template.Replace("{0}", "{|CA2249:" + condition + "|}"),
                FixedCode = template.Replace("{0}", fixedCondition),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("str.AsSpan().IndexOf(\"a\"c) >= 0", "str.AsSpan().Contains(\"a\"c)")]
        [DataRow("str.AsSpan().IndexOf(\"a\"c) <> -1", "str.AsSpan().Contains(\"a\"c)")]
        [DataRow("str.AsSpan().IndexOf(\"a\"c) < 0", "Not str.AsSpan().Contains(\"a\"c)")]
        [DataRow("str.AsSpan().IndexOf(\"a\"c) = -1", "Not str.AsSpan().Contains(\"a\"c)")]
        [DataRow("str.AsSpan().IndexOfAny(\"a\"c, \"b\"c) >= 0", "str.AsSpan().ContainsAny(\"a\"c, \"b\"c)")]
        public async Task SpanIndexOfComparedWithZero_OffersContainsVBAsync(string condition, string fixedCondition)
        {
            const string template = """
                Imports System

                Class TestClass
                    Sub TestMethod(str As String)
                        If {0} Then
                        End If
                    End Sub
                End Class
                """;

            await new VerifyVB.Test
            {
                TestCode = template.Replace("{0}", "{|CA2249:" + condition + "|}"),
                FixedCode = template.Replace("{0}", fixedCondition),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("span.IndexOf('a') > 0")]
        [DataRow("span.IndexOf('a') >= 1")]
        [DataRow("span.IndexOf('a') <= 0")]
        [DataRow("span.IndexOf('a') is > 0")]
        [DataRow("span.LastIndexOf('a') >= 0")]
        [DataRow("span.IndexOfAnyInRange('a', 'z') >= 0")]
        [DataRow("span.IndexOf(\"ab\".AsSpan()) >= 0")]
        public async Task SpanIndexOfNotAPresenceCheck_NoDiagnosticAsync(string condition)
        {
            string source = """
                using System;

                class TestClass
                {
                    void TestMethod(ReadOnlySpan<char> span)
                    {
                        if (
                """ + condition + """
                )
                        {
                        }
                    }
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task SpanContainsImplementedWithIndexOf_NoDiagnosticAsync()
        {
            string source = """
                namespace System
                {
                    public static class MemoryExtensions
                    {
                        public static int IndexOf(this ReadOnlySpan<char> span, char value) => 0;

                        public static bool Contains(this ReadOnlySpan<char> span, char value) => IndexOf(span, value) >= 0;

                        public static bool Other(this ReadOnlySpan<char> span, char value) => {|CA2249:IndexOf(span, value) >= 0|};
                    }
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source.Replace("{|CA2249:IndexOf(span, value) >= 0|}", "span.Contains(value)"),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task SpanIndexOfStoredInLocal_DiagnosticWithoutFixAsync()
        {
            string source = """
                using System;

                class TestClass
                {
                    void TestMethod(ReadOnlySpan<char> span)
                    {
                        int index = {|CA2249:span.IndexOf('a')|};
                        if (index >= 0)
                        {
                        }
                    }
                }
                """;

            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task SpanIndexOf_MessageNamesTheContainsMethodAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = """
                    using System;

                    class TestClass
                    {
                        bool TestMethod(ReadOnlySpan<char> span) => span.IndexOfAnyExcept('a', 'b') >= 0;
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic().WithSpan(5, 49, 5, 85).WithArguments("ContainsAnyExcept", "IndexOfAnyExcept"),
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }
    }
}
