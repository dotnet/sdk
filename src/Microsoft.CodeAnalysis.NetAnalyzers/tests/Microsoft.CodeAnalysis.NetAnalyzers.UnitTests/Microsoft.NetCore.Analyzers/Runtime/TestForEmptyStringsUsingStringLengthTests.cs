// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.TestForEmptyStringsUsingStringLengthAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class TestForEmptyStringsUsingStringLengthTests
    {
        #region Helper methods

        private DiagnosticResult CSharpResult(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        #endregion

        #region Diagnostic tests

        [Fact]
        public async Task CA1820StaticEqualsTestCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    void Method()
    {
        string a = null;

        // equality with empty string
        string.Equals(a, """");
        string.Equals(a, """", StringComparison.CurrentCulture);
        string.Equals("""", a, StringComparison.Ordinal);

        // equality with string.Empty
        string.Equals(a, string.Empty);
        string.Equals(a, string.Empty, StringComparison.CurrentCulture);
        string.Equals(string.Empty, a, StringComparison.Ordinal);
    }
}
",
                CSharpResult(11, 9),
                CSharpResult(12, 9),
                CSharpResult(13, 9),
                CSharpResult(16, 9),
                CSharpResult(17, 9),
                CSharpResult(18, 9));
        }

        [Fact]
        public async Task CA1820InstanceEqualsTestCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    void Method()
    {
        string a = null;

        // equality with empty string
        a.Equals("""");
        a.Equals("""", StringComparison.CurrentCulture);

        // equality with string.Empty
        a.Equals(string.Empty);
        a.Equals(string.Empty, StringComparison.CurrentCulture);
    }
}
",
                CSharpResult(11, 9),
                CSharpResult(12, 9),
                CSharpResult(15, 9),
                CSharpResult(16, 9));
        }

        [Fact]
        public async Task CA1820OperatorOverloadTestCSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class C
{
    void Method()
    {
        string a = null;
        if (a == """") { }
        if ("""" != a) { }
        if (a == string.Empty) { }
        if (string.Empty != a) { }
    }
}
",
                CSharpResult(9, 13),
                CSharpResult(10, 13),
                CSharpResult(11, 13),
                CSharpResult(12, 13));
        }

        #endregion

        [Fact, WorkItem(1508, "https://github.com/dotnet/roslyn-analyzers/issues/1508")]
        public async Task CA1820_ExpressionTree_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Linq;

class C
{
    void M(IQueryable<string> strings)
    {
        var q1 = from s in strings
                where s == """"
                select s;

        var q2 = strings.Where(s => s.Equals(""""));
    }
}");
        }
    }
}
