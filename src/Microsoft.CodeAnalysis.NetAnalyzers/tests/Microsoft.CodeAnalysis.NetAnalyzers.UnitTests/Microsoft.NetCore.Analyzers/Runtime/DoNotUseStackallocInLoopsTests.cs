// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDoNotUseStackallocInLoopsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotUseStackallocInLoopsTests
    {
        [Fact]
        public async Task NoDiagnostics_StackallocNotInLoop()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                unsafe class TestClass {
                    private static void NoLoop() {
                        byte* ptr1 = stackalloc byte[1];
                        Span<char> ptr2 = stackalloc char[2];

                        Label1:
                        Span<byte> ptr3 = stackalloc byte[3];
                        goto Label1; // false negative, but too difficult to track well with few false positives
                    }
                }");
        }

        [Fact]
        public async Task NoDiagnostics_StackallocInLoopWithBreak()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                unsafe class TestClass {
                    private static void ForLoop() {
                        for (int i = 0; i < 10; i++)
                        {
                            if (i == 5)
                            {
                                byte* ptr = stackalloc byte[1024];
                            }
                            break;
                        }
                    }

                    private static void WhileLoop() {
                        while (true)
                        {
                            byte* ptr = stackalloc byte[1024];
                            break;
                        }
                    }

                    private static void DoWhile() {
                        do
                        {
                            byte* ptr = stackalloc byte[1024];
                            return;
                        }
                        while (true);
                    }
                }");
        }

        [Fact]
        public async Task NoDiagnostics_StackallocInLoopButInsideALocalFunction()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp8,
                TestCode = @"
using System;
class TestClass {
    private static void StackAllocInLoopButInsideLocalFunction() {
        while (true) {
            XX();

            static void XX()
            {
                Span<int> tmp = stackalloc int[10];
                Console.WriteLine(tmp[0]);
            }
        }
    }
}"
            }.RunAsync();
        }

        [Fact]
        public async Task NoDiagnostics_StackallocInLoopButInsideALambda()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp8,
                TestCode = @"
using System;
class TestClass {
    private static void StackallocInLoopButInsideALambda() {
        while (true) {
            Action a = () => {
                Span<int> tmp = stackalloc int[10];
                Console.WriteLine(tmp[0]);
            };
            a();
        }
    }
}"
            }.RunAsync();
        }

        [Fact]
        public async Task NoDiagnostics_StackallocInLoopButInsideALambda2()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp8,
                TestCode = @"
using System;
class TestClass {
    private static void StackallocInLoopButInsideALambda2() {
        while (true) {
            Action<int> a = _ => Console.Write((stackalloc int[10]).Length);
        }
    }
}"
            }.RunAsync();
        }

        [Fact]
        public async Task NoDiagnostics_StackallocInLoopButInsideFunc()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = LanguageVersion.CSharp8,
                TestCode = @"
using System;
class TestClass {
    private static void StackallocInLoopButInsideAction() {
        while (true) {
            Func<int> a = delegate()
            {
                Span<int> tmp = stackalloc int[10];
                return 0;
            };
            a();
        }
    }
}"
            }.RunAsync();
        }

        [Fact]
        public async Task Diagnostics_LoopsWithStackallocPtr()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                unsafe class TestClass {
                    private static void ForLoop() {
                        for (int i = 0; i < 10; i++)
                        {
                            byte* ptr = {|CA2014:stackalloc byte[1024]|};
                        }
                    }

                    private static void WhileLoop() {
                        while (true)
                        {
                            byte* ptr = {|CA2014:stackalloc byte[1024]|};
                        }
                    }

                    private static void DoWhile() {
                        do
                        {
                            byte* ptr = {|CA2014:stackalloc byte[1024]|};
                        }
                        while (true);
                    }
                }");
        }

        [Fact]
        public async Task Diagnostics_LoopsWithStackallocSpan()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                unsafe class TestClass {
                    private static void ForLoop() {
                        for (int i = 0; i < 10; i++)
                        {
                            Span<byte> span = {|CA2014:stackalloc byte[1024]|};
                        }
                    }

                    private static void WhileLoop() {
                        while (true)
                        {
                            Span<char> span = {|CA2014:stackalloc char[1024]|};
                        }
                    }

                    private static void DoWhile() {
                        do
                        {
                            Span<int> span = {|CA2014:stackalloc int[1024]|};
                        }
                        while (true);
                    }
                }");
        }

        [Fact]
        public async Task Diagnostics_LoopInLoopWithOuterBreak()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                unsafe class TestClass {
                    private static void LoopInLoopWithOuterBreak() {
                        while (true)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                Span<byte> span = {|CA2014:stackalloc byte[1024]|};
                            }
                            break;
                        }
                    }
                }");
        }

        [Fact]
        public async Task Diagnostics_LoopWithBreakInConditional()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                unsafe class TestClass {
                    private static void LoopWithBreakInConditional() {
                        for (int i = 0; i < 10; i++)
                        {
                            Span<byte> span1 = {|CA2014:stackalloc byte[1024]|};
                            if (i == 5)
                                break;

                            Span<char> span2 = {|CA2014:stackalloc char[1024]|};
                            if (i == 3)
                                break;
                        }
                    }
                }");
        }
    }
}
