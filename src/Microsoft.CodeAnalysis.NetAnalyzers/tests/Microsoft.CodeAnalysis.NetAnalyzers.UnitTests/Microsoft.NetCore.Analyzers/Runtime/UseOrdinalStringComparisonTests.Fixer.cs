// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpUseOrdinalStringComparisonAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpUseOrdinalStringComparisonFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicUseOrdinalStringComparisonAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicUseOrdinalStringComparisonFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseOrdinalStringComparisonFixerTests
    {
        #region Code fix tests

        [Fact]
        public async Task CA1309FixStaticEqualsOverloadCSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
class C
{
    void M(string a, string b)
    {
        if (string.[|Equals|](a, b)) { }
        if (string.Equals(a, b, [|System.StringComparison.CurrentCulture|])) { }
        if (string.Equals(a, b, [|System.StringComparison.CurrentCultureIgnoreCase|])) { }
    }
}
",
                @"
class C
{
    void M(string a, string b)
    {
        if (string.Equals(a, b, System.StringComparison.Ordinal)) { }
        if (string.Equals(a, b, System.StringComparison.Ordinal)) { }
        if (string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase)) { }
    }
}
");
        }

        [Fact]
        public async Task CA1309FixStaticEqualsOverloadBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(
                @"
Class C
    Sub M(a As String, b As String)
        If String.[|Equals|](a, b) Then
        End If
        If String.Equals(a, b, [|System.StringComparison.CurrentCulture|]) Then
        End If
        If String.Equals(a, b, [|System.StringComparison.CurrentCultureIgnoreCase|]) Then
        End If
    End Sub
End Class
",
                @"
Class C
    Sub M(a As String, b As String)
        If String.Equals(a, b, System.StringComparison.Ordinal) Then
        End If
        If String.Equals(a, b, System.StringComparison.Ordinal) Then
        End If
        If String.Equals(a, b, System.StringComparison.OrdinalIgnoreCase) Then
        End If
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1309FixInstanceEqualsOverloadCSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(
                @"
class C
{
    void M(string a, string b)
    {
        if (a.Equals(15)) { }
        if (a.[|Equals|](b)) { }
        if (a.Equals(b, System.StringComparison.Ordinal)) { }
        if (a.Equals(b, [|System.StringComparison.CurrentCulture|])) { }
    }
}
",
                @"
class C
{
    void M(string a, string b)
    {
        if (a.Equals(15)) { }
        if (a.Equals(b, System.StringComparison.Ordinal)) { }
        if (a.Equals(b, System.StringComparison.Ordinal)) { }
        if (a.Equals(b, System.StringComparison.Ordinal)) { }
    }
}
");
        }

        [Fact]
        public async Task CA1309FixInstanceEqualsOverloadBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(
                @"
Class C
    Sub M(a As String, b As String)
        If a.Equals(15) Then
        End If
        If a.[|Equals|](b) Then
        End If
        If a.Equals(b, System.StringComparison.Ordinal) Then
        End If
        If a.Equals(b, [|System.StringComparison.CurrentCulture|]) Then
        End If
    End Sub
End Class
",
                @"
Class C
    Sub M(a As String, b As String)
        If a.Equals(15) Then
        End If
        If a.Equals(b, System.StringComparison.Ordinal) Then
        End If
        If a.Equals(b, System.StringComparison.Ordinal) Then
        End If
        If a.Equals(b, System.StringComparison.Ordinal) Then
        End If
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1309FixStaticCompareOverloadCSharp()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
class C
{
    void M(string a, string b)
    {
        System.Globalization.CultureInfo ci = null;
        System.Globalization.CompareOptions co = System.Globalization.CompareOptions.None;

        // add or correct StringComparison
        if (string.[|Compare|](a, b) == 0) { }
        if (string.[|Compare|](a, 0, b, 0, 0) == 0) { }
        if (string.Compare(a, b, [|System.StringComparison.CurrentCulture|]) == 0) { }
        if (string.Compare(a, b, [|System.StringComparison.CurrentCultureIgnoreCase|]) == 0) { }
        if (string.Compare(a, 0, b, 0, 0, [|System.StringComparison.CurrentCulture|]) == 0) { }

        // these can't be auto-fixed
        if (string.[|Compare|](a, b, true) == 0) { }
        if (string.[|Compare|](a, b, true, ci) == 0) { }
        if (string.[|Compare|](a, b, ci, co) == 0) { }
        if (string.[|Compare|](a, 0, b, 0, 0, true) == 0) { }
        if (string.[|Compare|](a, 0, b, 0, 0, true, ci) == 0) { }
        if (string.[|Compare|](a, 0, b, 0, 0, ci, co) == 0) { }
    }
}
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
class C
{
    void M(string a, string b)
    {
        System.Globalization.CultureInfo ci = null;
        System.Globalization.CompareOptions co = System.Globalization.CompareOptions.None;

        // add or correct StringComparison
        if (string.Compare(a, b, System.StringComparison.Ordinal) == 0) { }
        if (string.Compare(a, 0, b, 0, 0, System.StringComparison.Ordinal) == 0) { }
        if (string.Compare(a, b, System.StringComparison.Ordinal) == 0) { }
        if (string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase) == 0) { }
        if (string.Compare(a, 0, b, 0, 0, System.StringComparison.Ordinal) == 0) { }

        // these can't be auto-fixed
        if (string.[|Compare|](a, b, true) == 0) { }
        if (string.[|Compare|](a, b, true, ci) == 0) { }
        if (string.[|Compare|](a, b, ci, co) == 0) { }
        if (string.[|Compare|](a, 0, b, 0, 0, true) == 0) { }
        if (string.[|Compare|](a, 0, b, 0, 0, true, ci) == 0) { }
        if (string.[|Compare|](a, 0, b, 0, 0, ci, co) == 0) { }
    }
}
",
                    },

                    // Not everything is fixed; we use markup to indicate the remaining ones.
                    MarkupHandling = MarkupMode.Allow,
                },
            }.RunAsync();
        }

        [Fact]
        public async Task CA1309FixStaticCompareOverloadBasic()
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Class C
    Sub M(a As String, b As String)
        Dim ci As System.Globalization.CultureInfo
        Dim co As System.Globalization.CompareOptions

        ' add or correct StringComparison
        If String.[|Compare|](a, b) = 0 Then
        End If
        If String.[|Compare|](a, 0, b, 0, 0) = 0 Then
        End If
        If String.Compare(a, b, [|System.StringComparison.CurrentCulture|]) = 0 Then
        End If
        If String.Compare(a, b, [|System.StringComparison.CurrentCultureIgnoreCase|]) = 0 Then
        End If
        If String.Compare(a, 0, b, 0, 0, [|System.StringComparison.CurrentCulture|]) = 0 Then
        End If

        ' these can't be auto-fixed
        If String.[|Compare|](a, b, True) = 0 Then
        End If
        If String.[|Compare|](a, b, True, ci) = 0 Then
        End If
        If String.[|Compare|](a, b, ci, co) = 0 Then
        End If
        If String.[|Compare|](a, 0, b, 0, 0, True) = 0 Then
        End If
        If String.[|Compare|](a, 0, b, 0, 0, True, ci) = 0 Then
        End If
        If String.[|Compare|](a, 0, b, 0, 0, ci, co) = 0 Then
        End If
    End Sub
End Class
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Class C
    Sub M(a As String, b As String)
        Dim ci As System.Globalization.CultureInfo
        Dim co As System.Globalization.CompareOptions

        ' add or correct StringComparison
        If String.Compare(a, b, System.StringComparison.Ordinal) = 0 Then
        End If
        If String.Compare(a, 0, b, 0, 0, System.StringComparison.Ordinal) = 0 Then
        End If
        If String.Compare(a, b, System.StringComparison.Ordinal) = 0 Then
        End If
        If String.Compare(a, b, System.StringComparison.OrdinalIgnoreCase) = 0 Then
        End If
        If String.Compare(a, 0, b, 0, 0, System.StringComparison.Ordinal) = 0 Then
        End If

        ' these can't be auto-fixed
        If String.[|Compare|](a, b, True) = 0 Then
        End If
        If String.[|Compare|](a, b, True, ci) = 0 Then
        End If
        If String.[|Compare|](a, b, ci, co) = 0 Then
        End If
        If String.[|Compare|](a, 0, b, 0, 0, True) = 0 Then
        End If
        If String.[|Compare|](a, 0, b, 0, 0, True, ci) = 0 Then
        End If
        If String.[|Compare|](a, 0, b, 0, 0, ci, co) = 0 Then
        End If
    End Sub
End Class
",
                    },

                    // Not everything is fixed; we use markup to indicate the remaining ones.
                    MarkupHandling = MarkupMode.Allow,
                },
            }.RunAsync();
        }

        #endregion
    }
}
