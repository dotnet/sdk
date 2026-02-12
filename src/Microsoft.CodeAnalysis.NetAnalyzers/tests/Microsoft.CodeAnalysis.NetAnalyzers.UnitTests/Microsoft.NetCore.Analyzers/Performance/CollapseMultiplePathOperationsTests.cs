// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.CollapseMultiplePathOperationsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpCollapseMultiplePathOperationsFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class CollapseMultiplePathOperationsTests
    {
        [Fact]
        public async Task NoDiagnostic_SingleCombineCall()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b");
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(csCode);
        }

        [Fact]
        public async Task NoDiagnostic_SingleJoinCall()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Join("a", "b");
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(csCode);
        }

        [Fact]
        public async Task Diagnostic_NestedCombineCalls()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Combine(Path.Combine("a", "b"), "c")|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b", "c");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_NestedJoinCalls()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Join(Path.Join("a", "b"), "c")|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Join("a", "b", "c");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_DeeplyNestedCombineCalls()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Combine(Path.Combine(Path.Combine("a", "b"), "c"), "d")|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b", "c", "d");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_FullyQualifiedName()
        {
            var csCode = """
                public class Test
                {
                    public void M()
                    {
                        string path = [|System.IO.Path.Combine(System.IO.Path.Combine("a", "b"), "c")|];
                    }
                }
                """;
            var fixedCode = """
                public class Test
                {
                    public void M()
                    {
                        string path = System.IO.Path.Combine("a", "b", "c");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task NoDiagnostic_DifferentMethods()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine(Path.Join("a", "b"), "c");
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(csCode);
        }

        [Fact]
        public async Task Diagnostic_WithMultipleArgumentsInInnerCall()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Combine(Path.Combine("a", "b", "c"), "d")|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b", "c", "d");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_MultipleNestedLevels()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Combine(Path.Combine("a", "b"), Path.Combine("c", "d"))|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b", "c", "d");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_LargeNumberOfArguments()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Combine(
                            Path.Combine(
                                Path.Combine("a", "b"), 
                                Path.Combine("c", "d")), 
                            Path.Combine(
                                Path.Combine("e", "f"), 
                                Path.Combine("g", "h")))|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b", "c", "d", "e", "f", "g", "h");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_NestedInMiddlePosition()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Combine("a", Path.Combine("b", "c"), "d")|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b", "c", "d");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_NestedInLastPosition()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = [|Path.Combine("a", "b", Path.Combine("c", "d"))|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine("a", "b", "c", "d");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task Diagnostic_WithNonConstArguments()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    private string GetPath() => "path";
                    private string MyProperty => "prop";

                    public void M(bool condition)
                    {
                        string path = [|Path.Combine(Path.Combine(GetPath(), MyProperty), condition ? "a" : "b")|];
                    }
                }
                """;
            var fixedCode = """
                using System.IO;

                public class Test
                {
                    private string GetPath() => "path";
                    private string MyProperty => "prop";

                    public void M(bool condition)
                    {
                        string path = Path.Combine(GetPath(), MyProperty, condition ? "a" : "b");
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(csCode, fixedCode);
        }

        [Fact]
        public async Task NoDiagnostic_JoinNestedInCombine()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Join(Path.Combine("a", "b"), "c");
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(csCode);
        }

        [Fact]
        public async Task NoDiagnostic_CombineNestedInJoinMultipleLevels()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine(Path.Join(Path.Combine("a", "b"), "c"), "d");
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(csCode);
        }

        [Fact]
        public async Task NoDiagnostic_DeeplyMixedCombineAndJoinNesting()
        {
            var csCode = """
                using System.IO;

                public class Test
                {
                    public void M()
                    {
                        string path = Path.Combine(Path.Join(Path.Combine(Path.Combine("a", "b"), "c"), "d"), "e");
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(csCode);
        }
    }
}
