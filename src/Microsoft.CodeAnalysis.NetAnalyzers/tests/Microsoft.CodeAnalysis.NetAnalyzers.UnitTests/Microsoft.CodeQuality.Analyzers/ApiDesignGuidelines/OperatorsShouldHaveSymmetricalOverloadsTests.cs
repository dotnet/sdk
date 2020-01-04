// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class OperatorsShouldHaveSymmetricalOverloadsTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new OperatorsShouldHaveSymmetricalOverloadsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OperatorsShouldHaveSymmetricalOverloadsAnalyzer();
        }

        [Fact]
        public void CSharpTestMissingEquality()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator==(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '!=' to also be defined
}", TestValidationMode.AllowCompileErrors,
GetCSharpResultAt(4, 32, OperatorsShouldHaveSymmetricalOverloadsAnalyzer.Rule, "A", "==", "!="));
        }

        [Fact]
        public void CSharpTestMissingInequality()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator!=(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '==' to also be defined
}", TestValidationMode.AllowCompileErrors,
GetCSharpResultAt(4, 32, OperatorsShouldHaveSymmetricalOverloadsAnalyzer.Rule, "A", "!=", "=="));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharpTestMissingEquality_Internal()
        {
            VerifyCSharp(@"
class A
{
    public static bool operator==(A a1, A a2) { return false; }
}

public class B
{
    private class C
    {
        public static bool operator==(C a1, C a2) { return false; }
    }

    public class D
    {
        internal static bool operator==(D a1, D a2) { return false; }
    }
}

", TestValidationMode.AllowCompileErrors);
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void CSharpTestMissingInequality_Internal()
        {
            VerifyCSharp(@"
class A
{
    public static bool operator!=(A a1, A a2) { return false; }
}

public class B
{
    private class C
    {
        public static bool operator!=(C a1, C a2) { return false; }
    }

    public class D
    {
        internal static bool operator!=(D a1, D a2) { return false; }
    }
}
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CSharpTestBothEqualityOperators()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator==(A a1, A a2) { return false; }
    public static bool operator!=(A a1, A a2) { return false; }
}");
        }

        [Fact]
        public void CSharpTestMissingLessThan()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator<(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '>' to also be defined
}", TestValidationMode.AllowCompileErrors,
GetCSharpResultAt(4, 32, OperatorsShouldHaveSymmetricalOverloadsAnalyzer.Rule, "A", "<", ">"));
        }

        [Fact]
        public void CSharpTestNotMissingLessThan()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator<(A a1, A a2) { return false; }
    public static bool operator>(A a1, A a2) { return false; }
}");
        }

        [Fact]
        public void CSharpTestMissingLessThanOrEqualTo()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator<=(A a1, A a2) { return false; }   // error CS0216: The operator requires a matching operator '>=' to also be defined
}", TestValidationMode.AllowCompileErrors,
GetCSharpResultAt(4, 32, OperatorsShouldHaveSymmetricalOverloadsAnalyzer.Rule, "A", "<=", ">="));
        }

        [Fact]
        public void CSharpTestNotMissingLessThanOrEqualTo()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator<=(A a1, A a2) { return false; }
    public static bool operator>=(A a1, A a2) { return false; }
}");
        }

        [Fact]
        public void CSharpTestOperatorType()
        {
            VerifyCSharp(@"
public class A
{
    public static bool operator==(A a1, int a2) { return false; }
    public static bool operator!=(A a1, string a2) { return false; }
}", TestValidationMode.AllowCompileErrors,
GetCSharpResultAt(4, 32, OperatorsShouldHaveSymmetricalOverloadsAnalyzer.Rule, "A", "==", "!="),
GetCSharpResultAt(5, 32, OperatorsShouldHaveSymmetricalOverloadsAnalyzer.Rule, "A", "!=", "=="));
        }
    }
}