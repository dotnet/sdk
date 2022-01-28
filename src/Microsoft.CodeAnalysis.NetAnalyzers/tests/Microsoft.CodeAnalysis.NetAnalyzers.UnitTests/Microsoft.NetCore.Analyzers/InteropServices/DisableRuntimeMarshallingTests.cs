// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.InteropServices.DisableRuntimeMarshallingAnalyzer,
    Microsoft.NetCore.Analyzers.InteropServices.CSharpDisableRuntimeMarshallingFixer>;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests.Microsoft.NetCore.Analyzers.InteropServices
{
    public class DisableRuntimeMarshallingTests
    {
        [Fact]
        public async Task PInvokeWithSetLastError_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"", SetLastError = true)]
    public static extern void {|CA1420:Bar|}();
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithLCIDConversion_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    [LCIDConversion(0)]
    public static extern void {|CA1420:Bar|}();
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithClassParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void Bar(string {|CA1420:param|});
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithClassReturnValue_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern string {|CA1420:Bar|}();
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithManagedValueTypeReturnValue_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern ValueType {|CA1420:Bar|}();
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithManagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void Bar(ValueType {|CA1420:param|});
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithByRefManagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void Bar(ref ValueType {|CA1420:{|CA1420:param|}|});
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithByRefUnmanagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void Bar(ref ValueType {|CA1420:param|});
}

struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithUnmnagedValueTypeReturnValue_Does_Not_Emit_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern ValueType Bar();
}

struct ValueType
{
    char s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithUnmanagedValueTypeParameter_Does_Not_Emit_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void Bar(ValueType param);
}

struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithUnmanagedValueTypeParameter_WithAutoLayout_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void Bar(ValueType {|CA1420:param|});
}

[StructLayout(LayoutKind.Auto)]
struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithUnmanagedValueTypeParameter_WithAutoLayoutField_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void Bar(ValueType {|CA1420:param|});
}

struct ValueType
{
    ValueType2 v;
}

[StructLayout(LayoutKind.Auto)]
struct ValueType2
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task PInvokeWithVarargs_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [DllImport(""Foo"")]
    public static extern void {|CA1420:Bar|}(__arglist);
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithClassParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void Bar(string {|CA1420:param|});
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithClassReturnValue_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate string {|CA1420:Bar|}();
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithManagedValueTypeReturnValue_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate ValueType {|CA1420:Bar|}();
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithManagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void Bar(ValueType {|CA1420:param|});
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithByRefManagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void Bar(ref ValueType {|CA1420:{|CA1420:param|}|});
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithByRefUnmanagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void Bar(ref ValueType {|CA1420:param|});
}

struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithUnmnagedValueTypeReturnValue_Does_Not_Emit_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate ValueType Bar();
}

struct ValueType
{
    char s;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithUnmanagedValueTypeParameter_Does_Not_Emit_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void Bar(ValueType param);
}

struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithUnmanagedValueTypeParameter_WithAutoLayout_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void Bar(ValueType {|CA1420:param|});
}

[StructLayout(LayoutKind.Auto)]
struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task DelegateWithUnmanagedValueTypeParameter_WithAutoLayoutField_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate void Bar(ValueType {|CA1420:param|});
}

struct ValueType
{
    ValueType2 v;
}

[StructLayout(LayoutKind.Auto)]
struct ValueType2
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task FunctionPointerWithClassParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<string, void> cb)
    {
        {|CA1420:cb("""")|};
    }
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithClassReturnValue_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<string> cb)
    {
        _ = {|CA1420:cb()|};
    }
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithManagedValueTypeReturnValue_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ValueType> cb)
    {
        _ = {|CA1420:cb()|};
    }
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithManagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ValueType, void> cb)
    {
        {|CA1420:cb(default)|};
    }
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithByRefManagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ref ValueType, void> cb)
    {
        ValueType vt = default;
        {|CA1420:{|CA1420:cb(ref vt)|}|};
    }
}

struct ValueType
{
    string s;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithByRefUnmanagedValueTypeParameter_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ref ValueType, void> cb)
    {
        ValueType vt = default;
        {|CA1420:cb(ref vt)|};
    }
}

struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithUnmnagedValueTypeReturnValue_Does_Not_Emit_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ValueType> cb)
    {
        _ = cb();
    }
}

struct ValueType
{
    char s;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithUnmanagedValueTypeParameter_Does_Not_Emit_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ValueType, void> cb)
    {
        cb(default);
    }
}

struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithUnmanagedValueTypeParameter_WithAutoLayout_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ValueType, void> cb)
    {
        {|CA1420:cb(default)|};
    }
}

[StructLayout(LayoutKind.Auto)]
struct ValueType
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task FunctionPointerWithUnmanagedValueTypeParameter_WithAutoLayoutField_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate* unmanaged<ValueType, void> cb)
    {
        {|CA1420:cb(default)|};
    }
}

struct ValueType
{
    ValueType2 v;
}

[StructLayout(LayoutKind.Auto)]
struct ValueType2
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task ManagedFunctionPointerWithUnmanagedValueTypeParameter_WithAutoLayoutField_Does_Not_Emit_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public static unsafe void Test(delegate*<ValueType, void> cb)
    {
        cb(default);
    }
}

struct ValueType
{
    ValueType2 v;
}

[StructLayout(LayoutKind.Auto)]
struct ValueType2
{
    char c;
}
";
            await VerifyCSAnalyzerAsync(source, allowUnsafeBlocks: true);
        }

        [Fact]
        public async Task MarshalOffsetOf_Emits_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public void Test()
    {
        IntPtr offset = {|CA1421:Marshal.OffsetOf(typeof(ValueType), ""field"")|};
        IntPtr offsetGeneric = {|CA1421:Marshal.OffsetOf<ValueType>(""field"")|};
    }
}

struct ValueType
{
    int field;
}
";
            await VerifyCSAnalyzerAsync(source);
        }

        [Fact]
        public async Task MarshalSizeOf_Emits_Diagnostic()
        {
            string source = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public void Test<T, U>(System.Type type)
        where U : unmanaged
    {
        object obj = default(ValueType);
        int instanceSize = {|CA1421:Marshal.SizeOf(obj)|};
        int size = {|CA1421:Marshal.SizeOf(typeof(ValueType))|};
        int sizeGeneric = {|CA1421:Marshal.SizeOf<ValueType>()|};
        int sizePassedInType = {|CA1421:Marshal.SizeOf(type)|};
        int sizePassedInGeneric = {|CA1421:Marshal.SizeOf<T>()|};
        int sizePassedInGenericUnmanaged = {|CA1421:Marshal.SizeOf<U>()|};
    }
}

struct ValueType
{
    int field;
}
";
            string codeFix = @"
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public unsafe void Test<T, U>(System.Type type)
        where U : unmanaged
    {
        object obj = default(ValueType);
        int instanceSize = {|CA1421:Marshal.SizeOf(obj)|};
        int size = sizeof(ValueType);
        int sizeGeneric = sizeof(ValueType);
        int sizePassedInType = {|CA1421:Marshal.SizeOf(type)|};
        int sizePassedInGeneric = {|CA1421:Marshal.SizeOf<T>()|};
        int sizePassedInGenericUnmanaged = sizeof(U);
    }
}

struct ValueType
{
    int field;
}
";
            await VerifyCSCodeFixAsync(source, codeFix, allowUnsafeBlocks: true);

            // The code fix is not applicable when unsafe blocks are not allowed.
            await VerifyCSCodeFixAsync(source, source, allowUnsafeBlocks: false);
        }

        [Fact]
        public async Task MarshalStructureToPtr_Emits_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public void Test(IntPtr ptr)
    {
        {|CA1421:Marshal.StructureToPtr((object)default(ValueType), ptr, false)|};
        {|CA1421:Marshal.StructureToPtr((object)default(ValueType), ptr, true)|};
        {|CA1421:Marshal.StructureToPtr(default(ValueType), ptr, false)|};
        {|CA1421:Marshal.StructureToPtr(default(ValueType), ptr, true)|};
        {|CA1421:Marshal.StructureToPtr(default(ManagedValueType), ptr, false)|};
        {|CA1421:Marshal.StructureToPtr(default(ManagedValueType), ptr, true)|};
    }
}

struct ValueType
{
    int field;
}
struct ManagedValueType
{
    string field;
}
";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public unsafe void Test(IntPtr ptr)
    {
        {|CA1421:Marshal.StructureToPtr((object)default(ValueType), ptr, false)|};
        {|CA1421:Marshal.StructureToPtr((object)default(ValueType), ptr, true)|};
        *(ValueType*)ptr = default(ValueType);
        *(ValueType*)ptr = default(ValueType);
        {|CA1421:Marshal.StructureToPtr(default(ManagedValueType), ptr, false)|};
        {|CA1421:Marshal.StructureToPtr(default(ManagedValueType), ptr, true)|};
    }
}

struct ValueType
{
    int field;
}
struct ManagedValueType
{
    string field;
}
";
            await VerifyCSCodeFixAsync(source, codeFix, allowUnsafeBlocks: true);

            // The code fix is not applicable when unsafe blocks are not allowed.
            await VerifyCSCodeFixAsync(source, source, allowUnsafeBlocks: false);
        }

        [Fact]
        public async Task MarshalPtrStructure_Emits_Diagnostic()
        {
            string source = @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public void Test(IntPtr ptr, Type t)
    {
        {|CA1421:Marshal.PtrToStructure(ptr, (object)default(ValueType))|};
        {|CA1421:Marshal.PtrToStructure(ptr, new ClassType())|};
        _ = {|CA1421:Marshal.PtrToStructure(ptr, typeof(ManagedValueType))|};
        _ = {|CA1421:Marshal.PtrToStructure(ptr, typeof(ValueType))|};
        _ = {|CA1421:Marshal.PtrToStructure(ptr, typeof(ValueType?))|};
        _ = {|CA1421:Marshal.PtrToStructure(ptr + 1, typeof(ValueType?))|};
        _ = {|CA1421:Marshal.PtrToStructure(ptr, t)|};
        _ = {|CA1421:Marshal.PtrToStructure<ValueType>(ptr)|};
        _ = {|CA1421:Marshal.PtrToStructure<ValueType?>(ptr)|};
        {|CA1421:Marshal.PtrToStructure<ValueType>(ptr, default(ValueType))|};
        {|CA1421:Marshal.PtrToStructure<ClassType>(ptr, new ClassType())|};
    }
}

struct ValueType
{
    int field;
}
struct ManagedValueType
{
    string field;
}
class ClassType
{
    int field;
}
";
            string codeFix = @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly:DisableRuntimeMarshalling]

class Foo
{
    public unsafe void Test(IntPtr ptr, Type t)
    {
        {|CA1421:Marshal.PtrToStructure(ptr, (object)default(ValueType))|};
        {|CA1421:Marshal.PtrToStructure(ptr, new ClassType())|};
        _ = {|CA1421:Marshal.PtrToStructure(ptr, typeof(ManagedValueType))|};
        _ = (object)(*(ValueType*)ptr);
        _ = (object)((ValueType*)ptr is not null and var ptrPtr ? *ptrPtr : (ValueType?)null);
        _ = (ValueType*)(ptr + 1) is not null and var ptrPtr1 ? (object)(*ptrPtr1) : null;
        _ = {|CA1421:Marshal.PtrToStructure(ptr, t)|};
        _ = (*(ValueType*)ptr);
        _ = (ValueType*)ptr is not null and var ptrPtr2 ? *ptrPtr2 : (ValueType?)null;
        {|CA1421:Marshal.PtrToStructure<ValueType>(ptr, default(ValueType))|};
        {|CA1421:Marshal.PtrToStructure<ClassType>(ptr, new ClassType())|};
    }
}

struct ValueType
{
    int field;
}
struct ManagedValueType
{
    string field;
}
class ClassType
{
    int field;
}
";
            await VerifyCSCodeFixAsync(source, codeFix, allowUnsafeBlocks: true);

            // The code fix is not applicable when unsafe blocks are not allowed.
            await VerifyCSCodeFixAsync(source, source, allowUnsafeBlocks: false);
        }

        private static Task VerifyCSAnalyzerAsync(string source, bool allowUnsafeBlocks = false)
        {
            return VerifyCSCodeFixAsync(source, source, allowUnsafeBlocks);
        }

        private static async Task VerifyCSCodeFixAsync(string source, string codeFix, bool allowUnsafeBlocks = false)
        {
            var test = new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp9,
                ReferenceAssemblies = new ReferenceAssemblies("net7.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "7.0.0-preview.1.22075.6"), Path.Combine("ref", "net7.0"))
                    .WithNuGetConfigFilePath(Path.Combine(Path.GetDirectoryName(typeof(DisableRuntimeMarshallingTests).Assembly.Location), "NuGet.config")),
                TestCode = source,
                FixedCode = codeFix,
                SolutionTransforms =
                {
                    (solution, projectId) => solution.WithProjectCompilationOptions(projectId, (solution.GetProject(projectId).CompilationOptions as CSharpCompilationOptions)?.WithAllowUnsafe(allowUnsafeBlocks))
                },
                NumberOfFixAllIterations = -2
            };

            // Verify that there are some instances of the diagnostic that we can't fix.
            test.FixedState.MarkupHandling = MarkupMode.Allow;

            await test.RunAsync();
        }
    }
}
