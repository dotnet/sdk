// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information. 

using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.PreferStringContainsOverIndexOfFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStringContainsOverIndexOfTests
    {
        [Theory]
        [InlineData("This", false, " == ", " -1")]
        [InlineData("a", true, " == ", " -1")]
        [InlineData("This", false, " >= ", " 0")]
        [InlineData("a", true, " >= ", " 0")]
        public async Task TestStringAndChar(string input, bool isCharTest, string operatorKind, string value)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string endQuote = isCharTest ? "'" : "\", System.StringComparison.Ordinal";
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = [|str.IndexOf(" + startQuote + input + endQuote + @")|];
            if (index" + operatorKind + value + @")
            {
            }
        } 
    } 
}";

            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string stringComparison = isCharTest ? "" : ", System.StringComparison.Ordinal";
            operatorKind = operatorKind == " == " ? " = " : operatorKind;

            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index As Integer = [|Str.IndexOf(" + startQuote + input + startQuote + vbCharLiteral + stringComparison + @")|]
            If index" + operatorKind + value + @" Then

            End If
        End Sub
    End Class
End Class
";

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData(" == ", " -1")]
        [InlineData(" >= ", " 0")]
        public async Task TestStringNoComparisonArgument(string operatorKind, string value)
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = [|str.IndexOf(""This"")|];
            if (index" + operatorKind + value + @")
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            operatorKind = operatorKind == " == " ? " = " : operatorKind;
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index As Integer = [|Str.IndexOf(""This"")|]
            If index" + operatorKind + value + @" Then

            End If
        End Sub
    End Class
End Class
";

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData(" == ", " -1")]
        [InlineData(" >= ", " 0")]
        public async Task TestCharAndOrdinal(string operatorKind, string value)
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = [|str.IndexOf('a', System.StringComparison.Ordinal)|];
            if (index" + operatorKind + value + @")
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            operatorKind = operatorKind == " == " ? " = " : operatorKind;
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index As Integer = [|Str.IndexOf(""a""c, System.StringComparison.Ordinal)|]
            If index" + operatorKind + value + @" Then

            End If
        End Sub
    End Class
End Class
";

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData("This", false)]
        [InlineData("a", true)]
        public async Task TestStringAndCharWithMultipleDiagnostics(string input, bool isCharTest)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string endQuote = isCharTest ? "'" : "\", System.StringComparison.Ordinal";
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index1 = [|str.IndexOf(" + startQuote + input + endQuote + @")|];
            int index2 = [|str.IndexOf(" + startQuote + input + endQuote + @")|];
            if (index2 == -1 || -1 == index1)
            {

            }
            if ([|str.IndexOf(" + startQuote + input + endQuote + @") == -1|])
            {

            }
        } 
    } 
}";

            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string stringComparison = isCharTest ? "" : ", System.StringComparison.Ordinal";
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index1 As Integer = [|Str.IndexOf(" + startQuote + input + startQuote + vbCharLiteral + stringComparison + @")|]
            Dim index2 As Integer = [|Str.IndexOf(" + startQuote + input + startQuote + vbCharLiteral + stringComparison + @")|]
            If index2 = -1 OR -1 = index1 Then

            End If
            If [|Str.IndexOf(" + startQuote + input + startQuote + vbCharLiteral + stringComparison + @") = -1|] Then

            End If
        End Sub
    End Class
End Class
";

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData("This", false)]
        [InlineData("a", true)]
        public async Task TestStringAndCharWithComparison(string input, bool isCharTest)
        {
            string quotes = isCharTest ? "'" : "\"";
            string csInput = @" 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = [|str.IndexOf(" + quotes + input + quotes + @", System.StringComparison.InvariantCulture)|];
            if (index == -1)
            {

            }
        } 
    } 
}";

            var test = new VerifyCS.Test
            {
                TestCode = csInput,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await test.RunAsync();

            quotes = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index As Integer = [|Str.IndexOf(" + quotes + input + quotes + vbCharLiteral + @", System.StringComparison.InvariantCulture)|]
            If index = -1 Then

            End If
        End Sub
    End Class
End Class
";

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData("This", false)]
        [InlineData("a", true)]
        public async Task TestLeftAndRightOperandInvocations(string input, bool isCharTest)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string endQuote = isCharTest ? "'" : "\", System.StringComparison.Ordinal";
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if ([|str.IndexOf(" + startQuote + input + endQuote + @") == -1|])
            {

            }
            if ([|-1 == str.IndexOf(" + startQuote + input + endQuote + @")|])
            {

            }
        } 
    } 
}";
            string csFix = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if (!str.Contains(" + startQuote + input + startQuote + @"))
            {

            }
            if (!str.Contains(" + startQuote + input + startQuote + @"))
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string stringComparison = isCharTest ? "" : ", System.StringComparison.Ordinal";
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If [|Str.IndexOf(" + startQuote + input + startQuote + vbCharLiteral + stringComparison + @") = -1|] Then

            End If
            If [|-1 = Str.IndexOf(" + startQuote + input + startQuote + vbCharLiteral + stringComparison + @")|] Then

            End If
        End Sub
    End Class
End Class
";
            string vbFix = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If Not Str.Contains(" + startQuote + input + startQuote + vbCharLiteral + @") Then

            End If
            If Not Str.Contains(" + startQuote + input + startQuote + vbCharLiteral + @") Then

            End If
        End Sub
    End Class
End Class
";
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData("This", false)]
        [InlineData("a", true)]
        public async Task TestStringAndCharNamedArgumentCombinationVB(string input, bool isCharTest)
        {
            string startQuote = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If [|Str.IndexOf(value:= " + startQuote + input + startQuote + vbCharLiteral + @", System.StringComparison.Ordinal) = -1|] Then

            End If
        End Sub
    End Class
End Class
";
            string vbFix = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If Not Str.Contains(value:= " + startQuote + input + startQuote + vbCharLiteral + @") Then

            End If
        End Sub
    End Class
End Class
";
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();

            vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If [|Str.IndexOf(value:= " + startQuote + input + startQuote + vbCharLiteral + @", comparisonType:= System.StringComparison.OrdinalIgnoreCase) = -1|] Then

            End If
        End Sub
    End Class
End Class
";
            vbFix = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If Not Str.Contains(value:= " + startQuote + input + startQuote + vbCharLiteral + @", comparisonType:= System.StringComparison.OrdinalIgnoreCase) Then

            End If
        End Sub
    End Class
End Class
";
            testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();

            vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If [|Str.IndexOf(" + startQuote + input + startQuote + vbCharLiteral + @", comparisonType:= System.StringComparison.OrdinalIgnoreCase) = -1|] Then

            End If
        End Sub
    End Class
End Class
";
            vbFix = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If Not Str.Contains(" + startQuote + input + startQuote + vbCharLiteral + @", comparisonType:= System.StringComparison.OrdinalIgnoreCase) Then

            End If
        End Sub
    End Class
End Class
";
            testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();

            vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If [|Str.IndexOf(comparisonType:= System.StringComparison.OrdinalIgnoreCase, value:= " + startQuote + input + startQuote + vbCharLiteral + @") = -1|] Then

            End If
        End Sub
    End Class
End Class
";
            vbFix = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            If Not Str.Contains(value:= " + startQuote + input + startQuote + vbCharLiteral + @", comparisonType:= System.StringComparison.OrdinalIgnoreCase) Then

            End If
        End Sub
    End Class
End Class
";
            testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData("This", false)]
        [InlineData("a", true)]
        public async Task TestStringAndCharNamedArgumentCombinationsCS(string input, bool isCharTest)
        {
            string startQuote = isCharTest ? "'" : "\"";
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if ([|str.IndexOf(value: " + startQuote + input + startQuote + @", System.StringComparison.Ordinal) == -1|])
            {

            }
        } 
    } 
}";
            string csFix = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if (!str.Contains(value: " + startQuote + input + startQuote + @"))
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if ([|str.IndexOf(value: " + startQuote + input + startQuote + @", comparisonType: System.StringComparison.OrdinalIgnoreCase) == -1|])
            {

            }
        } 
    } 
}";
            csFix = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if (!str.Contains(value: " + startQuote + input + startQuote + @", comparisonType: System.StringComparison.OrdinalIgnoreCase))
            {

            }
        } 
    } 
}";
            testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if ([|str.IndexOf(" + startQuote + input + startQuote + @", comparisonType: System.StringComparison.OrdinalIgnoreCase) == -1|])
            {

            }
        } 
    } 
}";
            csFix = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if (!str.Contains(" + startQuote + input + startQuote + @", comparisonType: System.StringComparison.OrdinalIgnoreCase))
            {

            }
        } 
    } 
}";
            testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if ([|str.IndexOf(comparisonType: System.StringComparison.OrdinalIgnoreCase, value: " + startQuote + input + startQuote + @") == -1|])
            {

            }
        } 
    } 
}";
            csFix = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            if (!str.Contains(comparisonType: System.StringComparison.OrdinalIgnoreCase, value: " + startQuote + input + startQuote + @"))
            {

            }
        } 
    } 
}";
            testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();
        }

        [Theory]
        [InlineData("This", false, ", 1")]
        [InlineData("a", true, ", 1")]
        [InlineData("This", false, ", 1", ", 2")]
        [InlineData("a", true, ", 1", ", 2")]
        [InlineData("This", false, ", 1", ", System.StringComparison.OrdinalIgnoreCase")]
        [InlineData("This", false, ", 1", ", 2", ", System.StringComparison.OrdinalIgnoreCase")]
        public async Task TestTooManyArgumentsToIndexOf(string input, bool isCharTest, params string[] inputArguments)
        {
            string quotes = isCharTest ? "'" : "\"";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < inputArguments.Length; i++)
            {
                sb.Append(inputArguments[i]);
            }

            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = str.IndexOf(" + quotes + input + quotes + sb.ToString() + @");
            if (index == -1)
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();
        }

        [Theory]
        [InlineData("This", false)]
        [InlineData("a", true)]
        public async Task TestIndexWrittenTo(string input, bool isCharTest)
        {
            string quotes = isCharTest ? "'" : "\"";
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = str.IndexOf(" + quotes + input + quotes + @");
            index += 2;
            if (index == -1)
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            quotes = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index As Integer = Str.IndexOf(" + quotes + input + quotes + vbCharLiteral + @")
            index += 2
            If index = -1 Then

            End If
        End Sub
    End Class
End Class
";
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData("This", false)]
        [InlineData("a", true)]
        public async Task TestIndexWrittenToAfter(string input, bool isCharTest)
        {
            string quotes = isCharTest ? "'" : "\"";
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = str.IndexOf(" + quotes + input + quotes + @");
            if (index == -1)
            {

            }
            index += 2;
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            quotes = "\"";
            string vbCharLiteral = isCharTest ? "c" : "";
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index As Integer = Str.IndexOf(" + quotes + input + quotes + vbCharLiteral + @")
            If index = -1 Then

            End If
            index += 2
        End Sub
    End Class
End Class
";
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Fact]
        public async Task TestNonSupportedTarget()
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private bool TestMethod() 
        { 
            string str = ""This is a string"";
            return str.IndexOf(""This"") == -1;
        } 
    } 
}";
            var test = new VerifyCS.Test
            {
                TestCode = csInput,
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
            };
            await test.RunAsync();

            string csInputStringAndArgument = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private bool TestMethod() 
        { 
            string str = ""This is a string"";
            return str.IndexOf(""a"", System.StringComparison.InvariantCulture) == -1;
        } 
    } 
}";
            test = new VerifyCS.Test
            {
                TestCode = csInputStringAndArgument,
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
            };
            await test.RunAsync();

            string csCharInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private bool TestMethod() 
        { 
            string str = ""This is a string"";
            return str.IndexOf('T') == -1;
        } 
    } 
}";
            test = new VerifyCS.Test
            {
                TestCode = csCharInput,
                ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20
            };
            await test.RunAsync();
        }

        [Theory]
        [InlineData(" != ", "-1")]
        [InlineData(" != ", "3")]
        [InlineData(" > ", "2")]
        [InlineData(" >= ", "2")]
        public async Task TestNonSupportedOperationKind(string operatorKind, string right)
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            string str = ""This is a string"";
            int index = str.IndexOf(""This"");
            if (index" + operatorKind + right + @")
            {
            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();
        }

        [Fact]
        public async Task TestRightOperandIsVariable()
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            string str = ""This is a string"";
            int compare = 5;
            int index = str.IndexOf(""This"");
            if (index == compare)
            {
            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();
        }

        [Fact]
        public async Task TestReadOutside()
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            string str = ""This is a string"";
            int index = str.IndexOf(""This"");
            if (index == -1)
            {
            }
            int foo = index;
        } 
    } 
}";
            var test = new VerifyCS.Test
            {
                TestCode = csInput,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            await test.RunAsync();
        }

        [Theory]
        [InlineData(" == ", " -1", "!")]
        [InlineData(" >= ", " 0", "")]
        public async Task TestFunctionParameter(string operatorKind, string value, string notString)
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod(string str) 
        { 
            if ([|str.IndexOf(""This"")" + operatorKind + value + @"|])
            {
            }
        } 
    } 
}";
            string csFix = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod(string str) 
        { 
            if (" + notString + @"str.Contains(""This"", System.StringComparison.CurrentCulture)" + @")
            {
            }
        } 
    } 
}";
            var testCulture = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testCulture.RunAsync();

            operatorKind = operatorKind == " == " ? " = " : operatorKind;
            notString = notString == "!" ? " Not" : notString;
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub AMethod(arg As String)
            If [|arg.IndexOf(""This"")" + operatorKind + value + @"|] Then

            End If
        End Sub
    End Class
End Class
";
            string vbFix = @"
Public Class StringOf
    Class TestClass
        Public Sub AMethod(arg As String)
            If" + notString + @" arg.Contains(""This"", System.StringComparison.CurrentCulture)" + @" Then

            End If
        End Sub
    End Class
End Class
";
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData(" == ", " -1", "!")]
        [InlineData(" >= ", " 0", "")]
        public async Task TestFunctionParameterWithStringComparisonArgument(string operatorKind, string value, string notString)
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod(string str, System.StringComparison comparison) 
        { 
            if ([|str.IndexOf(""This"", comparison)" + operatorKind + value + @"|])
            {
            }
        } 
    } 
}";
            string csFix = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod(string str, System.StringComparison comparison) 
        { 
            if (" + notString + @"str.Contains(""This"", comparison)" + @")
            {
            }
        } 
    } 
}";
            var testCulture = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                FixedState = { Sources = { csFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testCulture.RunAsync();

            operatorKind = operatorKind == " == " ? " = " : operatorKind;
            notString = notString == "!" ? " Not" : notString;
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub AMethod(arg As String, comparison As System.StringComparison)
            If [|arg.IndexOf(""This"", comparison)" + operatorKind + value + @"|] Then

            End If
        End Sub
    End Class
End Class
";
            string vbFix = @"
Public Class StringOf
    Class TestClass
        Public Sub AMethod(arg As String, comparison As System.StringComparison)
            If" + notString + @" arg.Contains(""This"", comparison)" + @" Then

            End If
        End Sub
    End Class
End Class
";
            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                FixedState = { Sources = { vbFix } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData(" == ", " -1")]
        [InlineData(" >= ", " 0")]
        public async Task TestReversedMultipleDeclarations(string operatorKind, string value)
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int a = 5, index = [|str.IndexOf('a', System.StringComparison.Ordinal)|];
            if (index" + operatorKind + value + @")
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            operatorKind = operatorKind == " == " ? " = " : operatorKind;
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim a As Integer = 5, index = [|Str.IndexOf(""a""c, System.StringComparison.Ordinal)|]
            If index" + operatorKind + value + @" Then

            End If
        End Sub
    End Class
End Class
";

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }

        [Theory]
        [InlineData(" == ", " -1")]
        [InlineData(" >= ", " 0")]
        public async Task TestMultipleDeclarations(string operatorKind, string value)
        {
            string csInput = @"
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            const string str = ""This is a string"";
            int index = [|str.IndexOf('a', System.StringComparison.Ordinal)|], aa = 5;
            if (index" + operatorKind + value + @")
            {

            }
        } 
    } 
}";
            var testOrdinal = new VerifyCS.Test
            {
                TestState = { Sources = { csInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal.RunAsync();

            operatorKind = operatorKind == " == " ? " = " : operatorKind;
            string vbInput = @"
Public Class StringOf
    Class TestClass
        Public Sub Main()
            Dim Str As String = ""This is a statement""
            Dim index As Integer = [|Str.IndexOf(""a""c, System.StringComparison.Ordinal)|], aa = 5
            If index" + operatorKind + value + @" Then

            End If
        End Sub
    End Class
End Class
";

            var testOrdinal_vb = new VerifyVB.Test
            {
                TestState = { Sources = { vbInput } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            await testOrdinal_vb.RunAsync();
        }
    }
}