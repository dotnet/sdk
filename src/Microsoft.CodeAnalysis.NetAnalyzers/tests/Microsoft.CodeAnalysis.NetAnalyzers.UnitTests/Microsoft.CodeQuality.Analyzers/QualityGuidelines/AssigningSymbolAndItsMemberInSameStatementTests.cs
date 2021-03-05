// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AssigningSymbolAndItsMemberInSameStatement,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public class AssigningSymbolAndItsMemberInSameStatementTests
    {
        [Fact]
        public async Task CSharpReassignLocalVariableAndReferToItsField()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Field;
}

public class Test
{
    public void Method()
    {
        C a = new C(), b = new C();
        a.Field = a = b;
    }
}
",
            GetCSharpResultAt(12, 9, "a", "Field"));
        }

        [Fact]
        public async Task CSharpReassignLocalVariableAndReferToItsProperty()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    public void Method()
    {
        C a = new C(), b = new C(), c;
        a.Property = c = a = b;
    }
}
",
            GetCSharpResultAt(12, 9, "a", "Property"));
        }

        [Fact]
        public async Task CSharpReassignLocalVariablesPropertyAndReferToItsProperty()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    public void Method()
    {
        C a = new C(), b = new C();
        a.Property.Property = a.Property = b;
    }
}
",
            GetCSharpResultAt(12, 9, "a.Property", "Property"));
        }

        [Fact]
        public async Task CSharpReassignLocalVariableAndItsPropertyAndReferToItsProperty()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    public void Method()
    {
        C a = new C(), b = new C();
        a.Property.Property = a.Property = a = b;
    }
}
",
            GetCSharpResultAt(12, 9, "a.Property", "Property"),
            GetCSharpResultAt(12, 31, "a", "Property"));
        }

        [Fact]
        public async Task CSharpReferToFieldOfReferenceTypeLocalVariableAfterItsReassignment()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Field;
}

public class Test
{
    static C x, y;

    public void Method()
    {
        x.Field = x = y;
    }
}
",
            GetCSharpResultAt(13, 9, "x", "Field"));
        }

        [Fact]
        public async Task CSharpReassignGlobalVariableAndReferToItsField()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    static C x, y;

    public void Method()
    {
        x.Property.Property = x.Property = y;
    }
}
",
            GetCSharpResultAt(13, 9, "x.Property", "Property"));
        }

        [Fact]
        public async Task CSharpReassignGlobalVariableAndItsPropertyAndReferToItsProperty()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    static C x, y;

    public void Method()
    {
        x.Property.Property = x.Property = x = y;
    }
}
",
            GetCSharpResultAt(13, 9, "x.Property", "Property"),
            GetCSharpResultAt(13, 31, "x", "Property"));
        }

        [Fact]
        public async Task CSharpReassignGlobalPropertyAndItsPropertyAndReferToItsProperty()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    static C x { get; set; } 
    static C y { get; set; }

    public void Method()
    {
        x.Property.Property = x.Property = x = y;
    }
}
",
            GetCSharpResultAt(14, 9, "x.Property", "Property"),
            GetCSharpResultAt(14, 31, "x", "Property"));
        }

        [Fact]
        public async Task CSharpReassignSecondLocalVariableAndReferToItsPropertyOfFirstVariable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    public void Method()
    {
        C a = new C(), b;
        a.Property = b = a;
    }
}
");
        }

        [Fact]
        public async Task CSharpReassignPropertyOfFirstLocalVariableWithSecondAndReferToPropertyOfSecondVariable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    public void Method()
    {
        C a = new C(), b = new C(), c;
        b.Property.Property = a.Property = b;
    }
}
");
        }

        [Fact]
        public async Task CSharpReassignPropertyOfFirstLocalVariableWithThirdAndReferToPropertyOfSecondVariable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    public void Method()
    {
        C a = new C(), b = new C(), c = new C();
        b.Property.Property = a.Property = c;
    }
}
");
        }

        [Fact]
        public async Task CSharpReassignMethodParameterAndReferToItsProperty()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public C Property { get; set; }
}

public class Test
{
    public void Method(C b)
    {
        C a = new C();
        b.Property = b = a;
    }
}
",
            GetCSharpResultAt(12, 9, "b", "Property"));
        }

        [Fact]
        public async Task CSharpReassignLocalValueTypeVariableAndReferToItsField()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct S
{
    public S {|CS0523:Field|};
}

public class Test
{
    public void Method()
    {
        S a, b = new S();
        a.Field = a = b;
    }
}
");
        }

        [Fact]
        public async Task CSharpReassignLocalValueTypeVariableAndReferToItsProperty()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct S
{
    public S Property { get => default; set { } }
}

public class Test
{
    public void Method()
    {
        S a, b = new S();
        a.Property = a = b;
    }
}
");
        }

        [Fact]
        public async Task CSharpAssignmentInCodeWithOperationNone()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct Test
{
    public System.IntPtr PtrField;
    public unsafe void Method(Test a, Test *b)
    {
        b->PtrField = a.PtrField;
    }
}
");
        }

        [Fact]
        [WorkItem(2889, "https://github.com/dotnet/roslyn-analyzers/issues/2889")]
        public async Task CSharpAssignmentLocalReferenceOperation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public static class Class1
{
    public static void SomeMethod()
    {
        var u = new System.UriBuilder();
        u.Host = u.Path = string.Empty;
    }
}
");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
