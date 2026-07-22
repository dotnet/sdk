// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotSerializeTypeWithPointerFields,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [TestClass]
    public class DoNotSerializeTypeWithPointerFieldsTests
    {
        [TestMethod]
        public async Task TestChildPointerToStructureDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private TestStructB* pointer;
}

[Serializable()]
struct TestStructB
{
}",
            GetCSharpResultAt(7, 26, "pointer"));
        }

        [TestMethod]
        public async Task TestChildPointerToIntegerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private int* pointer;
}",
            GetCSharpResultAt(7, 18, "pointer"));
        }

        [TestMethod]
        public async Task TestChildPointerToBooleanDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private bool* pointer;
}",
            GetCSharpResultAt(7, 19, "pointer"));
        }

        [TestMethod]
        public async Task TestChildPointerToPointerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private int** pointer;
}",
            GetCSharpResultAt(7, 19, "pointer"));
        }

        [TestMethod]
        public async Task TestChildPointerPropertyToPointerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private int** pointer { get; set; }
}",
            GetCSharpResultAt(7, 19, "pointer"));
        }

        [TestMethod]
        public async Task TestChildPointerInArrayDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private int*[] pointers;
}",
            GetCSharpResultAt(7, 20, "pointers"));
        }

        [TestMethod]
        public async Task TestChildArrayOfChildPointerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private TestClassB[] testClassBs;
}

[Serializable()]
unsafe class TestClassB
{
    private int* pointer;
}",
            GetCSharpResultAt(13, 18, "pointer"));
        }

        [TestMethod]
        public async Task TestChildListOfChildPointerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

[Serializable()]
unsafe class TestClassA
{
    private List<TestClassB> testClassBs;
}

[Serializable()]
unsafe class TestClassB
{
    private int* pointer;
}",
            GetCSharpResultAt(14, 18, "pointer"));
        }

        [TestMethod]
        public async Task TestChildListOfListOfChildPointerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Collections.Generic;

[Serializable()]
unsafe class TestClassA
{
    private List<List<TestClassB>> testClassBs;
}

[Serializable()]
unsafe class TestClassB
{
    private int* pointer;
}",
            GetCSharpResultAt(14, 18, "pointer"));
        }

        [TestMethod]
        public async Task TestGrandchildPointerToIntegerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private TestClassB testClassB;
}

[Serializable()]
unsafe class TestClassB
{
    public int* pointer;
}",
            GetCSharpResultAt(13, 17, "pointer"));
        }

        [TestMethod]
        public async Task TestGrandchildPointerInArrayDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private TestClassB testClassB;
}

[Serializable()]
unsafe class TestClassB
{
    private int*[] pointers;
}",
            GetCSharpResultAt(13, 20, "pointers"));
        }

        [TestMethod]
        public async Task TestChildPointerAndGrandchildPointerDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private TestStructB* pointer1;
}

[Serializable()]
unsafe struct TestStructB
{
    public int* pointer2;
}",
            GetCSharpResultAt(7, 26, "pointer1"),
            GetCSharpResultAt(13, 17, "pointer2"));
        }

        [TestMethod]
        public async Task TestMultiChildPointersDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private int* pointer1;
    
    private int* pointer2;
}",
            GetCSharpResultAt(7, 18, "pointer1"),
            GetCSharpResultAt(9, 18, "pointer2"));
        }

        [TestMethod]
        public async Task TestChildPointerToSelfDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe struct TestStructA
{
    private TestStructA* pointer;
}",
            GetCSharpResultAt(7, 26, "pointer"));
        }

        [TestMethod]
        public async Task TestGrandchildPointerToSelfDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    public TestStructB testStructB;
}

[Serializable()]
unsafe struct TestStructB
{
    public TestStructB* pointer;
}",
            GetCSharpResultAt(13, 25, "pointer"));
        }

        [TestMethod]
        public async Task TestSubclassWithPointerFieldsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    protected TestStructB* pointer1;
}

[Serializable()]
struct TestStructB
{
}

[Serializable()]
unsafe class TestClassC : TestClassA
{
    private int* pointer2;
}",
            GetCSharpResultAt(7, 28, "pointer1"),
            GetCSharpResultAt(18, 18, "pointer2"));
        }

        [TestMethod]
        public async Task TestGenericTypeWithPointerFieldDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA<T>
{
    private T[] normalField;

    private int* pointer;
}",
            GetCSharpResultAt(9, 18, "pointer"));
        }

        [TestMethod]
        public async Task TestGenericTypeWithoutPointerFieldNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
class TestClassA<T>
{
    private T[] normalField;
}");
        }

        [TestMethod]
        public async Task TestWithoutPointerFieldNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private int normalField;
}");
        }

        [TestMethod]
        public async Task TestWithoutSerializableAttributeNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

unsafe class TestClassA
{
    private int* pointer;
}");
        }

        [TestMethod]
        public async Task TestChildPointerWithNonSerializedNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    [NonSerialized]
    private int* pointer;
}");
        }

        [TestMethod]
        public async Task TestChildPointerWithStaticNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[Serializable()]
unsafe class TestClassA
{
    private static int* pointer;
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
