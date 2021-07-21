// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferConstCharOverConstUnitStringAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.PreferConstCharOverConstUnitStringFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferConstCharOverConstUnitStringAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.PreferConstCharOverConstUnitStringFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferConstCharOverConstUnitStringForStringBuilderAppendTests
    {
        [Fact]
        public async Task TestRegularCase()
        {
            string csInput = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            const string ch = ""a"";
            sb.Append([|ch|]);
        } 
    } 
}";
            string csFix = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            const char ch = 'a';
            sb.Append(ch);
        } 
    } 
}";

            await VerifyCS.VerifyCodeFixAsync(csInput, csFix);

            string vbInput = @" 
Imports System

Module Program
    Sub Main(args As String())
        Const aa As String = ""a""
        Dim builder As New System.Text.StringBuilder
        builder.Append([|aa|])

    End Sub
End Module
";

            string vbFix = @" 
Imports System

Module Program
    Sub Main(args As String())
        Const aa As Char = ""a""c
        Dim builder As New System.Text.StringBuilder
        builder.Append(aa)

    End Sub
End Module
";

            await VerifyVB.VerifyCodeFixAsync(vbInput, vbFix);
        }

        [Fact]
        public async Task TestMultipleDeclarations()
        {
            const string multipleDeclarations_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            const string ch = ""a"", bb = ""b"";
            sb.Append([|ch|]);
        } 
    } 
}";
            await VerifyCS.VerifyCodeFixAsync(multipleDeclarations_cs, multipleDeclarations_cs);
            const string multipleDeclarations_vb = @" 
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Const aa As String = ""a"", bb As String = ""b""
            Dim builder As New System.Text.StringBuilder
            builder.Append([|aa|])
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyCodeFixAsync(multipleDeclarations_vb, multipleDeclarations_vb);
        }

        [Fact]
        public async Task TestClassField()
        {
            const string classFieldInAppend_cs = @"
using System;
using System.Text;

namespace RosylnScratch
{
    public class Program
    {
        public const string SS = ""a"";

        static void Main(string[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append([|SS|]);
        }
    }
}";
            await VerifyCS.VerifyCodeFixAsync(classFieldInAppend_cs, classFieldInAppend_cs);
            const string classFieldInAppend_vb = @"
Imports System

Module Program
    Class TestClass
        Public Const str As String = ""a""
        Public Sub Main(args As String())
            Dim builder As New System.Text.StringBuilder
            builder.Append([|str|])
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyCodeFixAsync(classFieldInAppend_vb, classFieldInAppend_vb);
        }

        [Fact]
        public async Task TestNullInitializer()
        {
            const string nullInitializer_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            const string ch = null;
            sb.Append(ch);
        } 
    } 
}";
            await VerifyCS.VerifyAnalyzerAsync(nullInitializer_cs);
            const string nullInitializer_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Const ch As String = Nothing
            Dim builder As New System.Text.StringBuilder
            builder.Append(ch)
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(nullInitializer_vb);
        }

        [Fact]
        public async Task TestNonUnitString()
        {
            const string nonUnitString_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            const string ch = ""ab"";
            sb.Append(ch);
        } 
    } 
}";
            await VerifyCS.VerifyAnalyzerAsync(nonUnitString_cs);
            const string nonUnitString_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Const ch As String = ""ab""
            Dim builder As New System.Text.StringBuilder
            builder.Append(ch)
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(nonUnitString_vb);
        }

        [Fact]
        public async Task TestNoCallToStringAppend()
        {
            const string noCallToStringAppend_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            const string ch = ""a"";
        } 
    } 
}";
            await VerifyCS.VerifyAnalyzerAsync(noCallToStringAppend_cs);

            const string noCallToStringAppend_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Const ch As String = ""a""
            Dim builder As New System.Text.StringBuilder
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(noCallToStringAppend_vb);
        }

        [Fact]
        public async Task TestNonConstUnitString()
        {
            const string nonConstUnitString_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            string ch = ""ab"";
            sb.Append(ch);
        } 
    } 
}";
            await VerifyCS.VerifyAnalyzerAsync(nonConstUnitString_cs);

            const string nonConstUnitString_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Dim ch As String = ""a""
            Dim builder As New System.Text.StringBuilder
            builder.Append(ch)
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(nonConstUnitString_vb);
        }

        [Fact]
        public async Task TestAppendLiteralWithFix()
        {
            const string appendLiteralInput_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            sb.Append([|"",""|]);
        } 
    } 
}";
            const string appendLiteralFix_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            sb.Append(',');
        } 
    } 
}";
            await VerifyCS.VerifyCodeFixAsync(appendLiteralInput_cs, appendLiteralFix_cs);

            const string appendLiteralInput_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Dim builder As New System.Text.StringBuilder
            builder.Append([|"",""|])
        End Sub
    End Class
End Module
";

            const string appendLiteralFix_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Dim builder As New System.Text.StringBuilder
            builder.Append("",""c)
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyCodeFixAsync(appendLiteralInput_vb, appendLiteralFix_vb);
        }

        [Fact]
        public async Task TestMethodCallInAppend()
        {
            const string methodCallInAppend_cs = @" 
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private string AString() => ""A"";

        private void TestMethod() 
        { 
            StringBuilder sb = new StringBuilder();
            sb.Append(AString());
        } 
    } 
}";
            await VerifyCS.VerifyAnalyzerAsync(methodCallInAppend_cs);

            const string methodCallInAppend_vb = @"
Imports System

Module Program
    Class TestClass
        Public Function AString() As String
            Return ""A""
        End Function

        Public Sub Main(args As String())
            Dim builder As New System.Text.StringBuilder
            builder.Append(AString())
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(methodCallInAppend_vb);
        }

        [Fact]
        public async Task TestMethodParameterInAppend()
        {
            const string methodParameterInAppend = @"
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod(int value) 
        { 
            StringBuilder sb = new StringBuilder();
            sb.Append(value.ToString());
        } 
    } 
}";
            await VerifyCS.VerifyAnalyzerAsync(methodParameterInAppend);

            const string methodParameterInAppend_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(arg As Int32)
            Dim builder As New System.Text.StringBuilder
            builder.Append(arg)
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(methodParameterInAppend_vb);
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("(string)null")]
        public async Task TestAppendLiteral(string input)
        {
            string quotes = input == "(string)null" ? "" : "\"";
            string methodParameterInAppend = @"
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod(int value) 
        { 
            StringBuilder sb = new StringBuilder();
            sb.Append(" + quotes + input + quotes + @");
        } 
    } 
}";
            await VerifyCS.VerifyAnalyzerAsync(methodParameterInAppend);

            if (input == "(string)null")
            {
                input = "CType(Nothing, String)";
            }
            string methodParameterInAppend_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(arg As Int32)
            Dim builder As New System.Text.StringBuilder
            builder.Append(" + quotes + input + quotes + @")
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(methodParameterInAppend_vb);
        }

        [Fact]
        public async Task TestInterpolatedString()
        {
            const string interpolatedString_cs = @"
using System; 
using System.Text;
 
namespace TestNamespace 
{ 
    class TestClass 
    { 
        private void TestMethod(int value) 
        { 
            StringBuilder sb = new StringBuilder();
            const string ch = ""a"";
            sb.Append([|$""{ch}""|]);
        } 
    } 
}";

            await VerifyCS.VerifyCodeFixAsync(interpolatedString_cs, interpolatedString_cs);
            const string interpolatedString_vb = @"
Imports System

Module Program
    Class TestClass
        Public Sub Main(args As String())
            Const ch As String = ""a""
            Dim builder As New System.Text.StringBuilder
            builder.Append($""{ch}"")
        End Sub
    End Class
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(interpolatedString_vb);
        }
    }
}