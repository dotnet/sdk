// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.TestForEmptyStringsUsingStringLengthAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpTestForEmptyStringsUsingStringLengthFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.TestForEmptyStringsUsingStringLengthAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicTestForEmptyStringsUsingStringLengthFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class TestForEmptyStringsUsingStringLengthFixerTests
    {
        private const int c_StringLengthCodeActionIndex = 1;

        [Fact, WorkItem(3686, "https://github.com/dotnet/roslyn-analyzers/pull/3686")]
        public async Task CA1820_FixTestEmptyStringsUsingIsNullOrEmpty_WhenStringIsLiteralAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return [|s == """"|];
    }

    public bool CompareEmptyIsLeft(string s)
    {
        return [|"""" == s|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return string.IsNullOrEmpty(s);
    }

    public bool CompareEmptyIsLeft(string s)
    {
        return string.IsNullOrEmpty(s);
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s = """"|]
    End Function

    Public Function CompareEmptyIsLeft(s As String) As Boolean
        Return [|"""" = s|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return String.IsNullOrEmpty(s)
    End Function

    Public Function CompareEmptyIsLeft(s As String) As Boolean
        Return String.IsNullOrEmpty(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingStringLength_WhenStringIsLiteralAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class A
{
    public bool Compare(string s)
    {
        return [|s == """"|];
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
public class A
{
    public bool Compare(string s)
    {
        return s.Length == 0;
    }
}
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s = """"|]
    End Function
End Class
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return s.Length = 0
    End Function
End Class
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingIsNullOrEmptyAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return [|s == string.Empty|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return string.IsNullOrEmpty(s);
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s = String.Empty|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return String.IsNullOrEmpty(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingStringLengthAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class A
{
    public bool Compare(string s)
    {
        return [|s == string.Empty|];
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
public class A
{
    public bool Compare(string s)
    {
        return s.Length == 0;
    }
}
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s = String.Empty|]
    End Function
End Class
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return s.Length = 0
    End Function
End Class
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingIsNullOrEmptyComparisonOnRightAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return [|string.Empty == s|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return string.IsNullOrEmpty(s);
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|String.Empty = s|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return String.IsNullOrEmpty(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingStringLengthComparisonOnRightAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class A
{
    public bool Compare(string s)
    {
        return [|string.Empty == s|];
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
public class A
{
    public bool Compare(string s)
    {
        return 0 == s.Length;
    }
}
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|String.Empty = s|]
    End Function
End Class
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return 0 = s.Length
    End Function
End Class
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1820_FixInequalityTestEmptyStringsUsingIsNullOrEmptyAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return [|s != string.Empty|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return !string.IsNullOrEmpty(s);
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s <> String.Empty|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return Not String.IsNullOrEmpty(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixInequalityTestEmptyStringsUsingStringLengthAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class A
{
    public bool Compare(string s)
    {
        return [|s != string.Empty|];
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
public class A
{
    public bool Compare(string s)
    {
        return s.Length != 0;
    }
}
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s <> String.Empty|]
    End Function
End Class
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return s.Length <> 0
    End Function
End Class
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1820_FixInequalityTestEmptyStringsUsingIsNullOrEmptyComparisonOnRightAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return [|string.Empty != s|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return !string.IsNullOrEmpty(s);
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|String.Empty <> s|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return Not String.IsNullOrEmpty(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixInequalityTestEmptyStringsUsingStringLengthComparisonOnRightAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class A
{
    public bool Compare(string s)
    {
        return [|string.Empty != s|];
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
public class A
{
    public bool Compare(string s)
    {
        return 0 != s.Length;
    }
}
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|String.Empty <> s|]
    End Function
End Class
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return 0 <> s.Length
    End Function
End Class
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInFunctionArgumentAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        G([|_s == string.Empty|]);
    }

    public void G(bool comparison) {}
}
", @"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        G(string.IsNullOrEmpty(_s));
    }

    public void G(bool comparison) {}
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Private _s As String = String.Empty

    Public Sub F()
        G([|_s = String.Empty|])
    End Sub

    Public Sub G(comparison As Boolean)
    End Sub
End Class
", @"
Public Class A
    Private _s As String = String.Empty

    Public Sub F()
        G(String.IsNullOrEmpty(_s))
    End Sub

    Public Sub G(comparison As Boolean)
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInTernaryOperatorAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public int F()
    {
        return [|_s == string.Empty|] ? 1 : 0;
    }
}
", @"
public class A
{
    string _s = string.Empty;

    public int F()
    {
        return string.IsNullOrEmpty(_s) ? 1 : 0;
    }
}
");

            // VB doesn't have the ternary operator, but we add this test for symmetry.
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Private _s As String = String.Empty

    Public Function F() As Integer
        Return If([|_s = String.Empty|], 1, 0)
    End Function
End Class
", @"
Public Class A
    Private _s As String = String.Empty

    Public Function F() As Integer
        Return If(String.IsNullOrEmpty(_s), 1, 0)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInThrowStatementAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        throw [|_s != string.Empty|] ? new System.Exception() : new System.ArgumentException();
    }
}
", @"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        throw !string.IsNullOrEmpty(_s) ? new System.Exception() : new System.ArgumentException();
    }
}
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInCatchFilterClauseAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        try { }
        catch (System.Exception ex) when ([|_s != string.Empty|]) { }
    }
}
", @"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        try { }
        catch (System.Exception ex) when (!string.IsNullOrEmpty(_s)) { }
    }
}
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInYieldReturnStatementAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.Collections.Generic;

public class A
{
    string _s = string.Empty;

    public IEnumerable<bool> F()
    {
        yield return [|_s != string.Empty|];
    }
}
", @"
using System.Collections.Generic;

public class A
{
    string _s = string.Empty;

    public IEnumerable<bool> F()
    {
        yield return !string.IsNullOrEmpty(_s);
    }
}
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInSwitchStatementAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        switch ([|_s != string.Empty|])
        {
            default:
                throw new System.NotImplementedException();
        }
    }
}
", @"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        switch (!string.IsNullOrEmpty(_s))
        {
            default:
                throw new System.NotImplementedException();
        }
    }
}
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInForLoopAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        for (; [|_s != string.Empty|]; )
        {
            throw new System.Exception();
        }
    }
}
", @"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        for (; !string.IsNullOrEmpty(_s); )
        {
            throw new System.Exception();
        }
    }
}
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInWhileLoopAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        while ([|_s != string.Empty|])
        {
        }
    }
}
", @"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        while (!string.IsNullOrEmpty(_s))
        {
        }
    }
}
");
        }

        [Fact]
        public async Task CA1820_FixForComparisonWithEmptyStringInDoWhileLoopAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        do
        {
        }
        while ([|_s != string.Empty|]);
    }
}
", @"
public class A
{
    string _s = string.Empty;

    public void F()
    {
        do
        {
        }
        while (!string.IsNullOrEmpty(_s));
    }
}
");
        }

        [Fact]
        public async Task CA1820_MultilineFixTestEmptyStringsUsingIsNullOrEmptyAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    string _s = string.Empty;
    public bool Compare(string s)
    {
        return [|s == string.Empty|] ||
               s == _s;
    }
}
", @"
public class A
{
    string _s = string.Empty;
    public bool Compare(string s)
    {
        return string.IsNullOrEmpty(s) ||
               s == _s;
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Private _s As String = String.Empty
    Public Function Compare(s As String) As Boolean
        Return [|s = String.Empty|] Or
               s = _s
    End Function
End Class
", @"
Public Class A
    Private _s As String = String.Empty
    Public Function Compare(s As String) As Boolean
        Return String.IsNullOrEmpty(s) Or
               s = _s
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_MultilineFixTestEmptyStringsUsingStringLengthAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class A
{
    string _s = string.Empty;
    public bool Compare(string s)
    {
        return [|s == string.Empty|] ||
               s == _s;
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
public class A
{
    string _s = string.Empty;
    public bool Compare(string s)
    {
        return s.Length == 0 ||
               s == _s;
    }
}
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Private _s As String = String.Empty
    Public Function Compare(s As String) As Boolean
        Return [|s = String.Empty|] Or
               s = _s
    End Function
End Class
",
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Public Class A
    Private _s As String = String.Empty
    Public Function Compare(s As String) As Boolean
        Return s.Length = 0 Or
               s = _s
    End Function
End Class
",
                    },
                },
                CodeActionIndex = c_StringLengthCodeActionIndex,
                CodeActionEquivalenceKey = "TestForEmptyStringCorrectlyUsingStringLength",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingStringLength_WhenStringEqualsMethodIsUsedWithStringEmptyAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return [|s.Equals(string.Empty)|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return string.IsNullOrEmpty(s);
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s.Equals(String.Empty)|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return String.IsNullOrEmpty(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingStringLength_WhenStringEqualsMethodIsUsedWithEmptyLiteralAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return [|s.Equals("""")|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return string.IsNullOrEmpty(s);
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return [|s.Equals("""")|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return String.IsNullOrEmpty(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA1820_FixTestEmptyStringsUsingStringLength_WhenNotStringEqualsMethodIsUsedAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(string s)
    {
        return ![|s.Equals(string.Empty)|];
    }
}
", @"
public class A
{
    public bool Compare(string s)
    {
        return !string.IsNullOrEmpty(s);
    }
}
");
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return Not [|s.Equals(String.Empty)|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As String) As Boolean
        Return Not String.IsNullOrEmpty(s)
    End Function
End Class
");
        }
    }
}

