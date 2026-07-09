// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferTypedStringBuilderAppendOverloads,
    Microsoft.NetCore.Analyzers.Runtime.PreferTypedStringBuilderAppendOverloadsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferTypedStringBuilderAppendOverloads,
    Microsoft.NetCore.Analyzers.Runtime.PreferTypedStringBuilderAppendOverloadsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class PreferTypedStringBuilderAppendOverloadsTests
    {
        [TestMethod]
        [DataRow("int", true)]
        [DataRow("Int32", true)]
        [DataRow("string", true)]
        [DataRow("String", true)]
        [DataRow("ulong", true)]
        [DataRow("object", false)]
        [DataRow("char[]", false)]
        [DataRow("DateTime", false)]
        [DataRow("DayOfWeek", false)]
        public async Task ArgumentIsToStringMethodCallOnLocal_CSharpAsync(string receiverType, bool diagnosticExpected)
        {
            string toString = diagnosticExpected ? "[|value.ToString()|]" : "value.ToString()";

            string original = @"
                using System;
                using System.Text;

                class C
                {
                    public void M()
                    {
                        " + receiverType + @" value = default;
                        var sb = new StringBuilder();
                        sb.Append(" + toString + @");
                        sb.Insert(42, " + toString + @");
                    }
                }
                ";

            await VerifyCS.VerifyCodeFixAsync(original, !diagnosticExpected ? original : @"
                using System;
                using System.Text;

                class C
                {
                    public void M()
                    {
                        " + receiverType + @" value = default;
                        var sb = new StringBuilder();
                        sb.Append(value);
                        sb.Insert(42, value);
                    }
                }
                ");
        }

        [TestMethod]
        [DataRow("Integer", true)]
        [DataRow("Int32", true)]
        [DataRow("String", true)]
        [DataRow("Object", false)]
        [DataRow("DateTime", false)]
        [DataRow("DayOfWeek", false)]
        public async Task ArgumentIsToStringMethodCallOnLocal_VBAsync(string receiverType, bool diagnosticExpected)
        {
            string toString = diagnosticExpected ? "[|value.ToString()|]" : "value.ToString()";

            string original = @"
                Imports System
                Imports System.Text

                Class C
                    Public Sub M()
                        Dim value As " + receiverType + @"
                        Dim sb As New StringBuilder()
                        sb.Append(" + toString + @")
                        sb.Insert(42, " + toString + @")
                    End Sub
                End Class";

            await VerifyVB.VerifyCodeFixAsync(original, !diagnosticExpected ? original : @"
                Imports System
                Imports System.Text

                Class C
                    Public Sub M()
                        Dim value As " + receiverType + @"
                        Dim sb As New StringBuilder()
                        sb.Append(value)
                        sb.Insert(42, value)
                    End Sub
                End Class");
        }

        [TestMethod]
        [DataRow("int", true)]
        [DataRow("Int32", true)]
        [DataRow("string", true)]
        [DataRow("String", true)]
        [DataRow("ulong", true)]
        [DataRow("object", false)]
        [DataRow("char[]", false)]
        [DataRow("DateTime", false)]
        [DataRow("DayOfWeek", false)]
        public async Task ArgumentIsToStringMethodCallOnResult_CSharpAsync(string receiverType, bool diagnosticExpected)
        {
            string toString = diagnosticExpected ? "[|Prop.ToString()|]" : "Prop.ToString()";

            string original = @"
                using System;
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        sb.Append(" + toString + @");
                        sb.Insert(42, " + toString + @");
                    }

                    private static " + receiverType + @" Prop => default;
                }
                ";

            await VerifyCS.VerifyCodeFixAsync(original, !diagnosticExpected ? original : @"
                using System;
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        sb.Append(Prop);
                        sb.Insert(42, Prop);
                    }

                    private static " + receiverType + @" Prop => default;
                }
                ");
        }

        [TestMethod]
        [DataRow("42", true)]
        [DataRow("\"hello\"", true)]
        [DataRow("DayOfWeek.Monday", false)]
        [DataRow("DateTime.Now", false)]
        public async Task ArgumentIsToStringMethodCallOnValueAsync(string value, bool diagnosticExpected)
        {
            string toString = value + ".ToString()";
            if (diagnosticExpected)
            {
                toString = "[|" + toString + "|]";
            }

            string original = @"
                using System;
                using System.Text;

                class C
                {
                    public void M1() => new StringBuilder().Append(" + toString + @");
                    public void M2() => new StringBuilder().Insert(42, " + toString + @");
                }
                ";

            await VerifyCS.VerifyCodeFixAsync(original, !diagnosticExpected ? original : @"
                using System;
                using System.Text;

                class C
                {
                    public void M1() => new StringBuilder().Append(" + value + @");
                    public void M2() => new StringBuilder().Insert(42, " + value + @");
                }
                ");
        }

        [TestMethod]
        [DataRow("42")]
        [DataRow("\"hello\"")]
        [DataRow("DayOfWeek.Monday")]
        [DataRow("DateTime.Now")]
        public async Task NoDiagnostic_NoToStringCallAsync(string value)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Text;

                class C
                {
                    public void M1() => new StringBuilder().Append(" + value + @");
                    public void M2() => new StringBuilder().Insert(42, " + value + @");
                }");
        }

        [TestMethod]
        [DataRow("42")]
        [DataRow("DayOfWeek.Monday")]
        [DataRow("DateTime.Now")]
        public async Task NoDiagnostic_FormattedToStringAsync(string value)
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Globalization;
                using System.Text;

                class C
                {
                    public void M1()
                    {
                        new StringBuilder()
                            .Append(" + value + @".ToString(""X4""))
                            .Append(" + value + @".ToString(CultureInfo.CurrentCulture))
                            .Append(" + value + @".ToString(""X4"", CultureInfo.CurrentCulture))
                            .Append(((IFormattable)" + value + @").ToString(""X4"", CultureInfo.CurrentCulture))
                            .Append(((IFormattable)" + value + @").ToString());
                    }

                    public void M2()
                    {
                        new StringBuilder()
                            .Insert(1, " + value + @".ToString(""X4""))
                            .Insert(1, " + value + @".ToString(CultureInfo.CurrentCulture))
                            .Insert(1, " + value + @".ToString(""X4"", CultureInfo.CurrentCulture))
                            .Insert(1, ((IFormattable)" + value + @").ToString(""X4"", CultureInfo.CurrentCulture))
                            .Insert(1, ((IFormattable)" + value + @").ToString());
                    }
                }");
        }

        [TestMethod]
        public async Task NoDiagnostic_NotRelevantMethodAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine(42.ToString());
                        sb.Replace(42.ToString(), ""42"");

                        Console.WriteLine(42.ToString());

                        Append(42.ToString());
                    }

                    private static void Append(string value) { }
                    private static void Append(int value) { }
                }");
        }

        [TestMethod]
        public async Task Diagnostic_StringConstructorInAppend_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        sb.Append([|new string('c', 5)|]);
                    }
                }
                ", @"
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        sb.Append('c', 5);
                    }
                }
                ");
        }

        [TestMethod]
        public async Task Diagnostic_StringConstructorWithVariable_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        char c = 'a';
                        int count = 3;
                        sb.Append([|new string(c, count)|]);
                    }
                }
                ", @"
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        char c = 'a';
                        int count = 3;
                        sb.Append(c, count);
                    }
                }
                ");
        }

        [TestMethod]
        public async Task NoDiagnostic_StringConstructorWithCharArray_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System.Text;

                class C
                {
                    public void M()
                    {
                        var sb = new StringBuilder();
                        char[] chars = new char[] { 'a', 'b', 'c' };
                        sb.Append(new string(chars));
                    }
                }");
        }
    }
}
