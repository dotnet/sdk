// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.OperatorsShouldHaveSymmetricalOverloadsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OperatorsShouldHaveSymmetricalOverloadsTests
    {
        [Fact]
        public async Task CSharpTestMissingEquality()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static bool operator{|CS0216:==|}(A a1, A a2) { return false; }
}",
                GetCSharpResultAt(4, 32, "A", "==", "!="));
        }

        [Fact]
        public async Task CSharpTestMissingInequality()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static bool operator{|CS0216:!=|}(A a1, A a2) { return false; }
}",
                GetCSharpResultAt(4, 32, "A", "!=", "=="));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharpTestMissingEquality_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class A
{
    public static bool operator{|CS0216:==|}(A a1, A a2) { return false; }
}

public class B
{
    private class C
    {
        public static bool operator{|CS0216:==|}(C a1, C a2) { return false; }
    }

    public class D
    {
        internal static bool operator{|CS0216:{|CS0558:==|}|}(D a1, D a2) { return false; }
    }
}

");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CSharpTestMissingInequality_Internal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class A
{
    public static bool operator{|CS0216:!=|}(A a1, A a2) { return false; }
}

public class B
{
    private class C
    {
        public static bool operator{|CS0216:!=|}(C a1, C a2) { return false; }
    }

    public class D
    {
        internal static bool operator{|CS0216:{|CS0558:!=|}|}(D a1, D a2) { return false; }
    }
}
");
        }

        [Fact]
        public async Task CSharpTestBothEqualityOperators()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static bool operator==(A a1, A a2) { return false; }
    public static bool operator!=(A a1, A a2) { return false; }
}");
        }

        [Fact]
        public async Task CSharpTestMissingLessThan()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static bool operator{|CS0216:<|}(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '>' to also be defined
}",
                GetCSharpResultAt(4, 32, "A", "<", ">"));
        }

        [Fact]
        public async Task CSharpTestNotMissingLessThan()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static bool operator<(A a1, A a2) { return false; }
    public static bool operator>(A a1, A a2) { return false; }
}");
        }

        [Fact]
        public async Task CSharpTestMissingLessThanOrEqualTo()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static bool operator{|CS0216:<=|}(A a1, A a2) { return false; }
}",
                GetCSharpResultAt(4, 32, "A", "<=", ">="));
        }

        [Fact]
        public async Task CSharpTestNotMissingLessThanOrEqualTo()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    public static bool operator<=(A a1, A a2) { return false; }
    public static bool operator>=(A a1, A a2) { return false; }
}");
        }

        [Fact]
        public async Task CSharpTestOperatorType()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class A
{
    /* We intentionally declare invalid methods for this test */

    public static bool operator{|CS0216:==|}(A a1, int a2) { return false; }
    public static bool operator{|CS0216:!=|}(A a1, string a2) { return false; }
}",
                GetCSharpResultAt(6, 32, "A", "==", "!="),
                GetCSharpResultAt(7, 32, "A", "!=", "=="));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
