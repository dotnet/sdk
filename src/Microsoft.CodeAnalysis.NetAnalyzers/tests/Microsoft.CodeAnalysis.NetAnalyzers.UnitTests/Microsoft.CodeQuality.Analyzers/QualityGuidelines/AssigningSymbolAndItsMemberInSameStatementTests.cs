// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public partial class AssigningSymbolAndItsMemberInSameStatementTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new AssigningSymbolAndItsMemberInSameStatement();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AssigningSymbolAndItsMemberInSameStatement();
        }

        [Fact]
        public void CSharpReassignLocalVariableAndReferToItsField()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(12, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "a", "Field"));
        }

        [Fact]
        public void CSharpReassignLocalVariableAndReferToItsProperty()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(12, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "a", "Property"));
        }

        [Fact]
        public void CSharpReassignLocalVariablesPropertyAndReferToItsProperty()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(12, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "a.Property", "Property"));
        }

        [Fact]
        public void CSharpReassignLocalVariableAndItsPropertyAndReferToItsProperty()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(12, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "a.Property", "Property"),
            GetCSharpResultAt(12, 31, AssigningSymbolAndItsMemberInSameStatement.Rule, "a", "Property"));
        }

        [Fact]
        public void CSharpReferToFieldOfReferenceTypeLocalVariableAfterItsReassignment()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(13, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "x", "Field"));
        }

        [Fact]
        public void CSharpReassignGlobalVariableAndReferToItsField()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(13, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "x.Property", "Property"));
        }

        [Fact]
        public void CSharpReassignGlobalVariableAndItsPropertyAndReferToItsProperty()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(13, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "x.Property", "Property"),
            GetCSharpResultAt(13, 31, AssigningSymbolAndItsMemberInSameStatement.Rule, "x", "Property"));
        }


        [Fact]
        public void CSharpReassignGlobalPropertyAndItsPropertyAndReferToItsProperty()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(14, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "x.Property", "Property"),
            GetCSharpResultAt(14, 31, AssigningSymbolAndItsMemberInSameStatement.Rule, "x", "Property"));
        }

        [Fact]
        public void CSharpReassignSecondLocalVariableAndReferToItsPropertyOfFirstVariable()
        {
            VerifyCSharp(@"
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
        public void CSharpReassignPropertyOfFirstLocalVariableWithSecondAndReferToPropertyOfSecondVariable()
        {
            VerifyCSharp(@"
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
        public void CSharpReassignPropertyOfFirstLocalVariableWithThirdAndReferToPropertyOfSecondVariable()
        {
            VerifyCSharp(@"
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
        public void CSharpReassignMethodParameterAndReferToItsProperty()
        {
            VerifyCSharp(@"
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
            GetCSharpResultAt(12, 9, AssigningSymbolAndItsMemberInSameStatement.Rule, "b", "Property"));
        }

        [Fact]
        public void CSharpReassignLocalValueTypeVariableAndReferToItsField()
        {
            VerifyCSharp(@"
public struct S
{
    public S Field;
}

public class Test
{
    public void Method()
    {
        S a, b;
        a.Field = a = b;
    }
}
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CSharpReassignLocalValueTypeVariableAndReferToItsProperty()
        {
            VerifyCSharp(@"
public struct S
{
    public S Property { get; set; }
}

public class Test
{
    public void Method()
    {
        S a, b;
        a.Property = c = a = b;
    }
}
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void CSharpAssignmentInCodeWithOperationNone()
        {
            VerifyCSharpUnsafeCode(@"
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
        public void CSharpAssignmentLocalReferenceOperation()
        {
            VerifyCSharp(@"
public static class Class1
{
    public static void Foo()
    {
        var u = new System.UriBuilder();
        u.Host = u.Path = string.Empty;
    }
}
");
        }
    }
}
