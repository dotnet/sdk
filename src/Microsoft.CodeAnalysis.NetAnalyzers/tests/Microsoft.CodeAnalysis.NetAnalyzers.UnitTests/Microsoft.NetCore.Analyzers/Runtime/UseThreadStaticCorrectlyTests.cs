// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseThreadStaticCorrectly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseThreadStaticCorrectly,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class UseThreadStaticsCorrectlyTests
    {
        [TestMethod]
        [DataRow("public", "object")]
        [DataRow("public", "int")]
        [DataRow("internal", "object")]
        [DataRow("internal", "int")]
        [DataRow("private", "object")]
        [DataRow("private", "int")]
        [DataRow("", "object")]
        [DataRow("", "int")]
        public async Task ValidThreadStatic_NoDiagnostics_CSharp(string visibility, string type)
        {
            await VerifyCS.VerifyAnalyzerAsync(
                $$"""
                                    using System;

                                    class C
                                    {
                                        [ThreadStatic]
                                        {{visibility}} static {{type}} t_value;

                                        [field: ThreadStatic]
                                        {{visibility}} static {{type}} Prop { get; set; }

                                        [field: ThreadStatic]
                                        {{visibility}} static event EventHandler Ev;
                                    }

                    """);
        }

        [TestMethod]
        [DataRow("Public", "Object")]
        [DataRow("Public", "Integer")]
        [DataRow("Friend", "Object")]
        [DataRow("Friend", "Integer")]
        [DataRow("Private", "Object")]
        [DataRow("Private", "Integer")]
        [DataRow("", "Object")]
        [DataRow("", "Integer")]
        public async Task ValidThreadStatic_NoDiagnostics_VB(string visibility, string type)
        {
            await VerifyVB.VerifyAnalyzerAsync(
                $"""
                                    Imports System

                                    Class C
                                        <ThreadStatic>
                                        {visibility} Shared t_value As {type}
                                    End Class

                    """);
        }

        [TestMethod]
        [DataRow("public", "object")]
        [DataRow("public", "int")]
        [DataRow("internal", "object")]
        [DataRow("internal", "int")]
        [DataRow("private", "object")]
        [DataRow("private", "int")]
        [DataRow("", "object")]
        [DataRow("", "int")]
        public async Task InstanceField_Diagnostic_CSharp(string visibility, string type)
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = $$"""
                                    using System;

                                    class C
                                    {
                                        [ThreadStatic]
                                        {{visibility}} {{type}} {|#0:t_value|};

                                        [field: ThreadStatic]
                                        string {|#1:Prop|} { get; set; }
                                    }

                                    record R([field: ThreadStatic] string {|#2:Value|});

                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(0).WithArguments("ThreadStatic", "t_value"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(1).WithArguments("ThreadStatic", "Prop"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(2).WithArguments("ThreadStatic", "Value"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [DataRow("Public", "Object")]
        [DataRow("Public", "Integer")]
        [DataRow("Friend", "Object")]
        [DataRow("Friend", "Integer")]
        [DataRow("Private", "Object")]
        [DataRow("Private", "Integer")]
        public async Task InstanceField_Diagnostic_VB(string visibility, string type)
        {
            await VerifyVB.VerifyAnalyzerAsync(
                $$"""
                                    Imports System

                                    Class C
                                        <ThreadStatic>
                                        {{visibility}} {|#0:t_value|} As {{type}}
                                    End Class

                    """,
                VerifyVB.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(0).WithArguments("ThreadStatic", "t_value"));
        }

        [TestMethod]
        [DataRow("object", "new object()")]
        [DataRow("object", "default")]
        [DataRow("object", "null")]
        [DataRow("int", "42")]
        [DataRow("int", "default")]
        [DataRow("int", "0")]
        public async Task FieldInitializer_Diagnostic_CSharp(string type, string initializer)
        {
            await VerifyCS.VerifyAnalyzerAsync(
                $$"""
                                    using System;

                                    class C
                                    {
                                        [ThreadStatic]
                                        private static {{type}} t_value {|CA2019:= {{initializer}}|};
                                    }

                    """);
        }

        [TestMethod]
        [DataRow("Object", "New Object()")]
        [DataRow("Object", "Nothing")]
        [DataRow("Integer", "42")]
        [DataRow("Integer", "0")]
        public async Task FieldInitializer_Diagnostic_VB(string type, string initializer)
        {
            await VerifyVB.VerifyAnalyzerAsync(
                $$"""
                                    Imports System

                                    Class C
                                        <ThreadStatic>
                                        Private Shared t_value As {{type}} {|CA2019:= {{initializer}}|}
                                    End Class

                    """);
        }

        [TestMethod]
        public async Task AttributeOnInstanceField_Diagnostic_CSharp()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    using System;
                    using System.Runtime.CompilerServices;

                    class C
                    {
                        [ContextStatic]
                        object {|#0:_context|};

                        [FixedAddressValueType]
                        Int128 {|#1:_fixed|};

                        [field: ContextStatic]
                        object {|#2:Prop|} { get; set; }

                        [ThreadStatic]
                        object {|#3:_thread|};

                        [ContextStatic]
                        static object s_context;

                        [FixedAddressValueType]
                        static Int128 s_fixed;
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(0).WithArguments("ContextStatic", "_context"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(1).WithArguments("FixedAddressValueType", "_fixed"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(2).WithArguments("ContextStatic", "Prop"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(3).WithArguments("ThreadStatic", "_thread"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task AttributeOnInstanceField_Diagnostic_VB()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    Imports System
                    Imports System.Runtime.CompilerServices

                    Class C
                        <ContextStatic>
                        Private {|#0:_context|} As Object

                        <FixedAddressValueType>
                        Private {|#1:_fixed|} As Int128

                        <ContextStatic>
                        Private Shared s_context As Object

                        <FixedAddressValueType>
                        Private Shared s_fixed As Int128
                    End Class
                    """,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(0).WithArguments("ContextStatic", "_context"),
                    VerifyVB.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(1).WithArguments("FixedAddressValueType", "_fixed"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task AttributeOnFieldLikeEvent_Diagnostic_CSharp()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    using System;
                    using System.Runtime.CompilerServices;

                    class C
                    {
                        [field: ThreadStatic]
                        event EventHandler {|#0:Thread|};

                        [field: ContextStatic]
                        event EventHandler {|#1:Context|};

                        [field: FixedAddressValueType]
                        event EventHandler {|#2:Fixed|};

                        [field: FixedAddressValueType]
                        static event EventHandler {|#3:StaticFixed|};

                        [field: ThreadStatic]
                        static event EventHandler StaticThread;

                        [field: ContextStatic]
                        static event EventHandler StaticContext;

                        [field: ThreadStatic]
                        event EventHandler {|#4:First|}, {|#5:Second|};

                        [{|CS0592:ThreadStatic|}]
                        event EventHandler NoTarget;
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(0).WithArguments("ThreadStatic", "Thread"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(1).WithArguments("ContextStatic", "Context"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(2).WithArguments("FixedAddressValueType", "Fixed"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(3).WithArguments("FixedAddressValueType", "StaticFixed"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(4).WithArguments("ThreadStatic", "First"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(5).WithArguments("ThreadStatic", "Second"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task FixedAddressValueTypeOnUnsupportedStaticField_Diagnostic_CSharp()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    using System;
                    using System.Runtime.CompilerServices;

                    class C<TClass, TAny, TStruct, TEnum>
                        where TClass : class
                        where TStruct : struct
                        where TEnum : struct, Enum
                    {
                        [FixedAddressValueType]
                        static object {|#0:s_object|};

                        [FixedAddressValueType]
                        static string {|#1:s_string|};

                        [FixedAddressValueType]
                        static int[] {|#2:s_array|};

                        [FixedAddressValueType]
                        static TClass {|#3:s_class|};

                        [field: FixedAddressValueType]
                        static object {|#4:Prop|} { get; set; }

                        [FixedAddressValueType]
                        static Int128 s_int128;

                        [FixedAddressValueType]
                        static int {|#5:s_int|};

                        [FixedAddressValueType]
                        static DayOfWeek {|#7:s_enum|};

                        [FixedAddressValueType]
                        static IntPtr {|#6:s_intPtr|};

                        [FixedAddressValueType]
                        static decimal s_decimal;

                        [FixedAddressValueType]
                        static TAny s_any;

                        [FixedAddressValueType]
                        static TStruct s_struct;

                        [FixedAddressValueType]
                        static TEnum {|#8:s_typeParameterEnum|};

                        [FixedAddressValueType]
                        static int? s_nullable;
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(0).WithArguments("FixedAddressValueType", "s_object"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(1).WithArguments("FixedAddressValueType", "s_string"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(2).WithArguments("FixedAddressValueType", "s_array"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(3).WithArguments("FixedAddressValueType", "s_class"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(4).WithArguments("FixedAddressValueType", "Prop"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(5).WithArguments("FixedAddressValueType", "s_int"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(6).WithArguments("FixedAddressValueType", "s_intPtr"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(7).WithArguments("FixedAddressValueType", "s_enum"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(8).WithArguments("FixedAddressValueType", "s_typeParameterEnum"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task FixedAddressValueTypeOnPointerStaticField_Diagnostic_CSharp()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    using System.Runtime.CompilerServices;

                    unsafe class C
                    {
                        [FixedAddressValueType]
                        static int* {|#0:s_pointer|};

                        [FixedAddressValueType]
                        static delegate*<void> {|#1:s_functionPointer|};
                    }
                    """,
                SolutionTransforms =
                {
                    (solution, projectId) => solution.WithProjectCompilationOptions(projectId,
                        ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true))
                },
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(0).WithArguments("FixedAddressValueType", "s_pointer"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(1).WithArguments("FixedAddressValueType", "s_functionPointer"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task FixedAddressValueTypeOnReferenceTypeInstanceField_ReportsOnlyNonStatic_CSharp()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    using System.Runtime.CompilerServices;

                    class C
                    {
                        [FixedAddressValueType]
                        object {|#0:_object|};
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NonStaticFieldRule).WithLocation(0).WithArguments("FixedAddressValueType", "_object"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task FixedAddressValueTypeOnUnsupportedStaticField_Diagnostic_VB()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    Imports System
                    Imports System.Runtime.CompilerServices

                    Class C
                        <FixedAddressValueType>
                        Private Shared {|#0:s_object|} As Object

                        <FixedAddressValueType>
                        Private Shared {|#1:s_string|} As String

                        <FixedAddressValueType>
                        Private Shared s_int128 As Int128

                        <FixedAddressValueType>
                        Private Shared {|#2:s_int|} As Integer
                    End Class
                    """,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(0).WithArguments("FixedAddressValueType", "s_object"),
                    VerifyVB.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(1).WithArguments("FixedAddressValueType", "s_string"),
                    VerifyVB.Diagnostic(UseThreadStaticCorrectly.UnsupportedTypeFieldRule).WithLocation(2).WithArguments("FixedAddressValueType", "s_int"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task SuppressGCTransition_NotPInvoke_Diagnostic_CSharp()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    using System.Runtime.InteropServices;
                    using System.Runtime.InteropServices.Marshalling;

                    [GeneratedComInterface]
                    [Guid("5C1A6F38-3F5B-4D2E-9B7A-1E2C3D4F5A6B")]
                    partial interface IComApi
                    {
                        [SuppressGCTransition]
                        void ComMethod();

                        [SuppressGCTransition]
                        void {|#1:DefaultMethod|}() { }

                        [SuppressGCTransition]
                        static void {|#2:StaticHelper|}() { }
                    }

                    partial class C
                    {
                        [DllImport("native")]
                        [SuppressGCTransition]
                        static extern int DllImportMethod();

                        [LibraryImport("native")]
                        [SuppressGCTransition]
                        internal static partial int LibraryImportMethod();

                        internal static partial int LibraryImportMethod() => 0;

                        [SuppressGCTransition]
                        static int {|#0:Managed|}() => 0;

                        [SuppressGCTransition]
                        static partial void {|#3:ManagedPartial|}();

                        static partial void ManagedPartial() { }
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NotPInvokeRule).WithLocation(0).WithArguments("SuppressGCTransition", "Managed"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NotPInvokeRule).WithLocation(1).WithArguments("SuppressGCTransition", "DefaultMethod"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NotPInvokeRule).WithLocation(2).WithArguments("SuppressGCTransition", "StaticHelper"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NotPInvokeRule).WithLocation(3).WithArguments("SuppressGCTransition", "ManagedPartial"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task SuppressGCTransition_LocalFunctionAndLambda_Diagnostic_CSharp()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    using System;
                    using System.Runtime.InteropServices;

                    class C
                    {
                        void M()
                        {
                            [DllImport("native")]
                            [SuppressGCTransition]
                            static extern int NativeLocal();

                            [SuppressGCTransition]
                            static int {|#0:ManagedLocal|}() => 0;

                            Func<int> lambda = {|#1:[SuppressGCTransition] () => 0|};
                        }
                    }
                    """,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NotPInvokeRule).WithLocation(0).WithArguments("SuppressGCTransition", "ManagedLocal"),
                    VerifyCS.Diagnostic(UseThreadStaticCorrectly.NotPInvokeRule).WithLocation(1).WithArguments("SuppressGCTransition", "lambda expression"),
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task SuppressGCTransition_NotPInvoke_Diagnostic_VB()
        {
            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                TestCode = """
                    Imports System.Runtime.InteropServices

                    Class C
                        <SuppressGCTransition>
                        Private Declare Function DeclareMethod Lib "native" () As Integer

                        <DllImport("native")>
                        <SuppressGCTransition>
                        Private Shared Function DllImportMethod() As Integer
                        End Function

                        <SuppressGCTransition>
                        Private Shared Function {|#0:Managed|}() As Integer
                            Return 0
                        End Function
                    End Class
                    """,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(UseThreadStaticCorrectly.NotPInvokeRule).WithLocation(0).WithArguments("SuppressGCTransition", "Managed"),
                },
            }.RunAsync(CancellationToken.None);
        }
    }
}