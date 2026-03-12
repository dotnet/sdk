// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SpecifyStringComparisonAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpSpecifyStringComparisonFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SpecifyStringComparisonAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicSpecifyStringComparisonFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class SpecifyStringComparisonTests
    {
        [Fact]
        public async Task CA1307_CA1310_StringCompareTests_CSharpAsync()
        {
#if !NETCOREAPP
            const string StringArgType = "string";
#else
            const string StringArgType = "string?";
#endif

            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public int StringCompare()
    {
        string strA = """";
        string strB = """";
        var x1 = String.Compare(strA, strB);
        var x2 = String.Compare(strA, strB, true);
        var x3 = String.Compare(strA, 0, strB, 0, 1);
        var x4 = String.Compare(strA, 0, strB, 0, 1, true);
        return 0;
    }
}",
GetCA1310CSharpResultsAt(11, 18, $"string.Compare({StringArgType}, {StringArgType})",
                                 "StringComparisonTests.StringCompare()",
                                 $"string.Compare({StringArgType}, {StringArgType}, System.StringComparison)"),
GetCA1310CSharpResultsAt(12, 18, $"string.Compare({StringArgType}, {StringArgType}, bool)",
                                 "StringComparisonTests.StringCompare()",
                                 $"string.Compare({StringArgType}, {StringArgType}, System.StringComparison)"),
GetCA1310CSharpResultsAt(13, 18, $"string.Compare({StringArgType}, int, {StringArgType}, int, int)",
                                 "StringComparisonTests.StringCompare()",
                                 $"string.Compare({StringArgType}, int, {StringArgType}, int, int, System.StringComparison)"),
GetCA1310CSharpResultsAt(14, 18, $"string.Compare({StringArgType}, int, {StringArgType}, int, int, bool)",
                                 "StringComparisonTests.StringCompare()",
                                 $"string.Compare({StringArgType}, int, {StringArgType}, int, int, System.StringComparison)"));
        }

        [Fact]
        public async Task CA1307_CA1310_StringWithStringTests_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public bool StringWith()
    {
        string strA = """";
        string strB = """";
        var x1 = strA.EndsWith(strB);
        return strA.StartsWith(strB);
    }
}",
GetCA1310CSharpResultsAt(11, 18, "string.EndsWith(string)",
                                 "StringComparisonTests.StringWith()",
                                 "string.EndsWith(string, System.StringComparison)"),
GetCA1310CSharpResultsAt(12, 16, "string.StartsWith(string)",
                                 "StringComparisonTests.StringWith()",
                                 "string.StartsWith(string, System.StringComparison)"));
        }

#if NETCOREAPP // EndsWith(char) and StartsWith(char) overloads don't exist in .NET Framework 4.7.2
        [Fact, WorkItem(2581, "https://github.com/dotnet/roslyn-analyzers/issues/2581")]
        public async Task CA1307_CA1310_StringWithCharTests_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public bool StringWith(string strA, char chA, char chB)
    {
        var x = strA.EndsWith(chA);
        return strA.StartsWith(chB);
    }
}");
        }
#endif

        [Theory]
        [InlineData("IndexOf")]
        [InlineData("LastIndexOf")]
        public async Task CA1307_CA1310_StringIndexOfStringTests_CSharpAsync(string method)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;
using System.Globalization;

public class StringComparisonTests
{{
    public int StringIndexOf()
    {{
        string strA = """";
        var x1 = strA.{method}("""");
        var x2 = strA.{method}("""", 0);
        return strA.{method}("""", 0, 1);
    }}
}}",
GetCA1310CSharpResultsAt(10, 18, $"string.{method}(string)",
                                "StringComparisonTests.StringIndexOf()",
                                $"string.{method}(string, System.StringComparison)"),
GetCA1310CSharpResultsAt(11, 18, $"string.{method}(string, int)",
                                 "StringComparisonTests.StringIndexOf()",
                                 $"string.{method}(string, int, System.StringComparison)"),
GetCA1310CSharpResultsAt(12, 16, $"string.{method}(string, int, int)",
                                 "StringComparisonTests.StringIndexOf()",
                                 $"string.{method}(string, int, int, System.StringComparison)"));
        }

        [Fact, WorkItem(2581, "https://github.com/dotnet/roslyn-analyzers/issues/2581")]
        public async Task CA1307_CA1310_StringIndexOfCharTests_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public int StringIndexOf(string strA, char chA)
    {
        var x1 = strA.IndexOf(chA);
        var x2 = strA.IndexOf(chA, 0);
        return strA.IndexOf(chA, 0, 1);
    }
}"
#if NETCOREAPP  // 'string.IndexOf(char, System.StringComparison)' overload does not exist in .NET Framework
, GetCA1307CSharpResultsAt(9, 18, "string.IndexOf(char)",
                                "StringComparisonTests.StringIndexOf(string, char)",
                                "string.IndexOf(char, System.StringComparison)")
#endif
                                );
        }

        [Fact, WorkItem(2581, "https://github.com/dotnet/roslyn-analyzers/issues/2581")]
        public async Task CA1307_CA1310_StringLastIndexOfCharTests_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public int StringIndexOf(string strA, char chA)
    {
        var x1 = strA.LastIndexOf(chA);
        var x2 = strA.LastIndexOf(chA, 0);
        return strA.LastIndexOf(chA, 0, 1);
    }
}");
        }

#if NETCOREAPP
        [Theory, WorkItem(2581, "https://github.com/dotnet/roslyn-analyzers/issues/2581")]
        [InlineData("string")]
        [InlineData("char")]
        public async Task CA1307_CA1310_StringContainsTests_CSharpAsync(string firstParamType)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;
using System.Globalization;

public class StringContainsTests
{{
    public bool StringContains(string strA, {firstParamType} p)
    {{
        return strA.Contains(p);
    }}
}}",
GetCA1307CSharpResultsAt(9, 16, $"string.Contains({firstParamType})",
                                 $"StringContainsTests.StringContains(string, {firstParamType})",
                                 $"string.Contains({firstParamType}, System.StringComparison)"));
        }
#endif

        [Fact, WorkItem(2581, "https://github.com/dotnet/roslyn-analyzers/issues/2581")]
        public async Task CA1307_CA1310_StringGetHashCodeTests_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringGetHashCodeTests
{
    public int StringGetHashCode(string strA)
    {
        return strA.GetHashCode();
    }
}"
#if NETCOREAPP  // 'string.GetHashCode(System.StringComparison)' overload does not exist in .NET Framework
, GetCA1307CSharpResultsAt(9, 16, "string.GetHashCode()",
                                 "StringGetHashCodeTests.StringGetHashCode(string)",
                                 "string.GetHashCode(System.StringComparison)")
#endif
                                 );
        }

        [Fact]
        public async Task CA1307_CA1310_StringCompareToTests_CSharpAsync()
        {
#if !NETCOREAPP
            const string ObjectArgType = "object";
            const string StringArgType = "string";
#else
            const string ObjectArgType = "object?";
            const string StringArgType = "string?";
#endif

            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public int StringCompareTo()
    {
            string strA = """";
            string strB = """";
            var x1 = strA.CompareTo(strB);
            return """".CompareTo(new object());
    }
}",
GetCA1310CSharpResultsAt(11, 22, $"string.CompareTo({StringArgType})",
                                 "StringComparisonTests.StringCompareTo()",
                                 $"string.Compare({StringArgType}, {StringArgType}, System.StringComparison)"),
GetCA1310CSharpResultsAt(12, 20, $"string.CompareTo({ObjectArgType})",
                                 "StringComparisonTests.StringCompareTo()",
                                 $"string.Compare({StringArgType}, {StringArgType}, System.StringComparison)"));
        }

        [Fact]
        public async Task CA1307_CA1310_OverloadTests_StringFirstParam_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public void NonString()
    {
        DoNothing("""");
        DoNothing<string>(""""); // No diagnostics since this is generics
    }

    public void DoNothing(string str)
    {
    }

    public void DoNothing<T>(string str)
    {
    }

    public void DoNothing<T>(string str, StringComparison strCompare)
    {
    }
}",
GetCA1307CSharpResultsAt(9, 9, "StringComparisonTests.DoNothing(string)",
                               "StringComparisonTests.NonString()",
                               "StringComparisonTests.DoNothing<T>(string, System.StringComparison)"));
        }

        [Theory, WorkItem(2581, "https://github.com/dotnet/roslyn-analyzers/issues/2581")]
        [InlineData("char")]
        [InlineData("int")]
        [InlineData("object")]
        [InlineData("StringComparisonTests")]
        public async Task CA1307_CA1310_OverloadTests_NonStringFirstParam_CSharpAsync(string firstParamType)
        {
            await VerifyCS.VerifyAnalyzerAsync($@"
using System;
using System.Globalization;

public class StringComparisonTests
{{
    public void NonString({firstParamType} p)
    {{
        DoNothing(p);
    }}

    public void DoNothing({firstParamType} p)
    {{
    }}

    public void DoNothing({firstParamType} p, StringComparison strCompare)
    {{
    }}
}}",
GetCA1307CSharpResultsAt(9, 9, $"StringComparisonTests.DoNothing({firstParamType})",
                                 $"StringComparisonTests.NonString({firstParamType})",
                                 $"StringComparisonTests.DoNothing({firstParamType}, System.StringComparison)"));
        }

        [Fact]
        public async Task CA1307_CA1310_OverloadWithMismatchRefKind_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class StringComparisonTests
{
    public void MyMethod()
    {
        M("""");
    }

    public void M(string str)
    {
    }

    public void M(string str, out StringComparison strCompare)
    {
        strCompare = StringComparison.Ordinal;
    }

    public void M(ref StringComparison strCompare, string str)
    {
        strCompare = StringComparison.Ordinal;
    }

    public void M(ref string str, StringComparison strCompare)
    {
    }
}");
        }

        [Fact]
        public async Task CA1307_CA1310_StringCompareTests_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringCompare() As Integer
        Dim strA As String = """"
        Dim strB As String = """"
        Dim x1 = [String].Compare(strA, strB)
        Dim x2 = [String].Compare(strA, strB, True)
        Dim x3 = [String].Compare(strA, 0, strB, 0, 1)
        Dim x4 = [String].Compare(strA, 0, strB, 0, 1, True)
        Return 0
    End Function
End Class",
GetCA1310BasicResultsAt(9, 18, "String.Compare(String, String)",
                               "StringComparisonTests.StringCompare()",
                               "String.Compare(String, String, System.StringComparison)"),
GetCA1310BasicResultsAt(10, 18, "String.Compare(String, String, Boolean)",
                                "StringComparisonTests.StringCompare()",
                                "String.Compare(String, String, System.StringComparison)"),
GetCA1310BasicResultsAt(11, 18, "String.Compare(String, Integer, String, Integer, Integer)",
                                "StringComparisonTests.StringCompare()",
                                "String.Compare(String, Integer, String, Integer, Integer, System.StringComparison)"),
GetCA1310BasicResultsAt(12, 18, "String.Compare(String, Integer, String, Integer, Integer, Boolean)",
                                "StringComparisonTests.StringCompare()",
                                "String.Compare(String, Integer, String, Integer, Integer, System.StringComparison)"));
        }

        [Fact]
        public async Task CA1307_CA1310_StringWithTests_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringWith() As Boolean
        Dim strA As String = """"
        Dim strB As String = """"
        Dim x = strA.EndsWith(strB)
        Return strA.StartsWith(strB)
    End Function
End Class",
GetCA1310BasicResultsAt(9, 17, "String.EndsWith(String)",
                               "StringComparisonTests.StringWith()",
                               "String.EndsWith(String, System.StringComparison)"),
GetCA1310BasicResultsAt(10, 16, "String.StartsWith(String)",
                                "StringComparisonTests.StringWith()",
                                "String.StartsWith(String, System.StringComparison)"));
        }

        [Fact]
        public async Task CA1307_CA1310_StringIndexOfTests_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringIndexOf() As Integer
        Dim strA As String = """"
        Dim x1 = strA.IndexOf("""")
        Dim x2 = strA.IndexOf("""", 0)
        Return strA.IndexOf("""", 0, 1)
    End Function
End Class",
GetCA1310BasicResultsAt(8, 18, "String.IndexOf(String)",
                               "StringComparisonTests.StringIndexOf()",
                               "String.IndexOf(String, System.StringComparison)"),
GetCA1310BasicResultsAt(9, 18, "String.IndexOf(String, Integer)",
                                "StringComparisonTests.StringIndexOf()",
                                "String.IndexOf(String, Integer, System.StringComparison)"),
GetCA1310BasicResultsAt(10, 16, "String.IndexOf(String, Integer, Integer)",
                                "StringComparisonTests.StringIndexOf()",
                                "String.IndexOf(String, Integer, Integer, System.StringComparison)"));
        }

        [Fact]
        public async Task CA1307_CA1310_StringCompareToTests_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Function StringCompareTo() As Integer
        Dim strA As String = """"
        Dim strB As String = """"
        Dim x1 = strA.CompareTo(strB)
        Return """".CompareTo(New Object())
    End Function
End Class",
GetCA1310BasicResultsAt(9, 18, "String.CompareTo(String)",
                               "StringComparisonTests.StringCompareTo()",
                               "String.Compare(String, String, System.StringComparison)"),
GetCA1310BasicResultsAt(10, 16, "String.CompareTo(Object)",
                                "StringComparisonTests.StringCompareTo()",
                                "String.Compare(String, String, System.StringComparison)"));
        }

        [Fact]
        public async Task CA1307_CA1310_OverloadTests_VisualBasicAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class StringComparisonTests
    Public Sub NonString()
        DoNothing("")
        ' No diagnostics since this is generics
        DoNothing(Of String)("")
    End Sub

    Public Sub DoNothing(str As String)
    End Sub

    Public Sub DoNothing(Of T)(str As String)
    End Sub

    Public Sub DoNothing(Of T)(str As String, strCompare As StringComparison)
    End Sub
End Class",
GetCA1307BasicResultsAt(7, 9, "StringComparisonTests.DoNothing(String)",
                              "StringComparisonTests.NonString()",
                              "StringComparisonTests.DoNothing(Of T)(String, System.StringComparison)"));
        }

        [Fact, WorkItem(3492, "https://github.com/dotnet/roslyn-analyzers/issues/3492")]
        public async Task CA1307_CA1310_SimpleIQueryable_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Linq;

public class C
{
    public string Name { get; }

    public void DoSomething(IQueryable<C> data)
    {
        var result1 = data.Where(c => c.Name.StartsWith(""Hello""));
        var result2 = data.Where(c => c.M(""Hello""));
    }

    public bool M(string s) => false;
    public bool M(string s, StringComparison sc) => false;
}");
        }

        [Fact, WorkItem(3492, "https://github.com/dotnet/roslyn-analyzers/issues/3492")]
        public async Task CA1307_CA1310_IQueryableOfIEnumerable_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;

public class C
{
    public string Name { get; }

    public void DoSomething(IQueryable<IEnumerable<C>> data)
    {
        var result1 = data.Where(x => x.Any(y => y.Name.StartsWith(""Hello"")));
        var result2 = data.Where(x => x.Any(y => y.M(""Hello"")));
    }

    public bool M(string s) => false;
    public bool M(string s, StringComparison sc) => false;
}",
                GetCA1310CSharpResultsAt(12, 50,
                    "string.StartsWith(string)",
                    "C.DoSomething(System.Linq.IQueryable<System.Collections.Generic.IEnumerable<C>>)",
                    "string.StartsWith(string, System.StringComparison)"),
                GetCA1307CSharpResultsAt(13, 50,
                    "C.M(string)",
                    "C.DoSomething(System.Linq.IQueryable<System.Collections.Generic.IEnumerable<C>>)",
                    "C.M(string, System.StringComparison)"));
        }

        [Fact, WorkItem(3492, "https://github.com/dotnet/roslyn-analyzers/issues/3492")]
        public async Task CA1307_CA1310_IQueryableAsEnumerable_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;

public class C
{
    public string Name { get; }

    public void DoSomething(IQueryable<C> data)
    {
        var result1 = data.AsEnumerable().Where(c => c.Name.StartsWith(""Hello""));
        var result2 = data.AsEnumerable().Where(c => c.M(""Hello""));
    }

    public bool M(string s) => false;
    public bool M(string s, StringComparison sc) => false;
}",
                GetCA1310CSharpResultsAt(12, 54,
                    "string.StartsWith(string)",
                    "C.DoSomething(System.Linq.IQueryable<C>)",
                    "string.StartsWith(string, System.StringComparison)"),
                GetCA1307CSharpResultsAt(13, 54,
                    "C.M(string)",
                    "C.DoSomething(System.Linq.IQueryable<C>)",
                    "C.M(string, System.StringComparison)"));
        }

        [Fact, WorkItem(3492, "https://github.com/dotnet/roslyn-analyzers/issues/3492")]
        public async Task CA1307_CA1310_ExpressionFunc_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Linq;
using System.Linq.Expressions;

public class C
{
    public string Name { get; }

    public void DoSomething()
    {
        F(c => c.Name.StartsWith(""Hello""));
        F(c => c.M(""Hello""));
    }

    public bool M(string s) => false;
    public bool M(string s, StringComparison sc) => false;

    private void F(Expression<Func<C, bool>> e) {}
}");
        }

        [Fact, WorkItem(6943, "https://github.com/dotnet/roslyn-analyzers/issues/6943")]
        public async Task CA1307_StaticMethodWithPrivateOverload_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class G
{
    public static void DoSomething()
    {
        F.M("""");
    }
}

public class F
{
    private static void M(string s, StringComparison c) { }
    public static void M(string s) { }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class G
    Public Shared Sub DoSomething()
        F.M("""")
    End Sub
End Class

Public Class F
    Private Shared Sub M(s As String, c As StringComparison)
    End Sub

    Public Shared Sub M(s As String)
    End Sub
End Class");
        }

        [Fact, WorkItem(6943, "https://github.com/dotnet/roslyn-analyzers/issues/6943")]
        public async Task CA1307_StaticMethodWithAccessibleInstanceOverload_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class G
{
    public static void DoSomething()
    {
        F.M("""");
    }
}

public class F
{
    public void M(string s, StringComparison c) { }
    public static void M(string s) { }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class G
    Public Shared Sub DoSomething()
        F.M("""")
    End Sub
End Class

Public Class F
    Public Sub M(s As String, c As StringComparison)
    End Sub

    Public Shared Sub M(s As String)
    End Sub
End Class");
        }

        [Fact, WorkItem(6943, "https://github.com/dotnet/roslyn-analyzers/issues/6943")]
        public async Task CA1307_StaticMethodWithProtectedStaticOverloadOnBaseClass_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class G : F
{
    public static void DoSomething()
    {
        F.M("""");
    }
}

public class F
{
    protected static void M(string s, StringComparison c) { }
    public static void M(string s) { }
}",
                GetCA1307CSharpResultsAt(8, 9, "F.M(string)",
                    "G.DoSomething()",
                    "F.M(string, System.StringComparison)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class G
    Inherits F

    Public Shared Sub DoSomething()
        F.M("""")
    End Sub
End Class

Public Class F
    Protected Shared Sub M(s As String, c As StringComparison)
    End Sub

    Public Shared Sub M(s As String)
    End Sub
End Class",
                GetCA1307BasicResultsAt(8, 9, "F.M(String)",
                "G.DoSomething()",
                "F.M(String, System.StringComparison)"));
        }

        [Fact, WorkItem(6943, "https://github.com/dotnet/roslyn-analyzers/issues/6943")]
        public async Task CA1307_PrivateOverloadOnBaseClass_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class G : F
{
    public void DoSomething()
    {
        M("""");
    }
}

public class F
{
    private void M(string s, StringComparison c) { }
    public void M(string s) { }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class G
    Inherits F

    Public Sub DoSomething()
        M("""")
    End Sub
End Class

Public Class F
    Private Sub M(s As String, c As StringComparison)
    End Sub

    Public Sub M(s As String)
    End Sub
End Class");
        }

        [Fact, WorkItem(6943, "https://github.com/dotnet/roslyn-analyzers/issues/6943")]
        public async Task CA1307_ProtectedOverloadOnBaseClass_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class G : F
{
    public void DoSomething()
    {
        M("""");
    }
}

public class F
{
    protected void M(string s, StringComparison c) { }
    public void M(string s) { }
}",
                GetCA1307CSharpResultsAt(8, 9, "F.M(string)",
                    "G.DoSomething()",
                    "F.M(string, System.StringComparison)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class G
    Inherits F

    Public Sub DoSomething()
        M("""")
    End Sub
End Class

Public Class F
    Protected Sub M(s As String, c As StringComparison)
    End Sub

    Public Sub M(s As String)
    End Sub
End Class",
                GetCA1307BasicResultsAt(8, 9, "F.M(String)",
                    "G.DoSomething()",
                    "F.M(String, System.StringComparison)"));
        }

        [Fact, WorkItem(6943, "https://github.com/dotnet/roslyn-analyzers/issues/6943")]
        public async Task CA1307_StaticOverloadOnBaseClass_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

public class G : F
{
    public void DoSomething()
    {
        M("""");
    }
}

public class F
{
    public static void M(string s, StringComparison c) { }
    public void M(string s) { }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Public Class G
    Inherits F

    Public Sub DoSomething()
        M("""")
    End Sub
End Class

Public Class F
    Public Shared Sub M(s As String, c As StringComparison)
    End Sub

    Public Sub M(s As String)
    End Sub
End Class");
        }

        private static DiagnosticResult GetCA1307CSharpResultsAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(SpecifyStringComparisonAnalyzer.Rule_CA1307)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private static DiagnosticResult GetCA1307BasicResultsAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(SpecifyStringComparisonAnalyzer.Rule_CA1307)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private static DiagnosticResult GetCA1310CSharpResultsAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(SpecifyStringComparisonAnalyzer.Rule_CA1310)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);

        private static DiagnosticResult GetCA1310BasicResultsAt(int line, int column, string arg1, string arg2, string arg3) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(SpecifyStringComparisonAnalyzer.Rule_CA1310)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arg1, arg2, arg3);
    }
}