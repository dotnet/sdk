// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotUseAsParallelInForEachLoopAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.DoNotUseAsParallelInForEachLoopAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class DoNotUseAsParallelInForEachLoopTests
    {
        [Fact]
        public Task CSharp_AsParallelDirectlyInForeachLoop_ReportsDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var list = new List<int> { 1, 2, 3 };
                        foreach (var item in [|list.AsParallel()|])
                        {
                        }
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task VB_AsParallelDirectlyInForeachLoop_ReportsDiagnostic()
        {
            const string code = """
                Imports System.Collections.Generic
                Imports System.Linq

                Public Class Tests
                    Public Sub M()
                        Dim list = New List(Of Integer) From {1, 2, 3}
                        For Each item In [|list.AsParallel()|]
                        Next
                    End Sub
                End Class
                """;

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task CSharp_AsParallelWithSelectInForeachLoop_ReportsDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var list = new List<int> { 1, 2, 3 };
                        foreach (var item in [|list.Select(x => x * 2).AsParallel()|])
                        {
                        }
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task CSharp_AsParallelWithWhereInForeachLoop_ReportsDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var list = new List<int> { 1, 2, 3 };
                        foreach (var item in [|list.Where(x => x > 1).AsParallel()|])
                        {
                        }
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task CSharp_AsParallelEarlyInChain_NoDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var list = new List<int> { 1, 2, 3 };
                        var result = list.AsParallel().Select(x => x * 2).ToList();
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task CSharp_AsParallelWithToList_NoDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var list = new List<int> { 1, 2, 3 };
                        var result = list.Select(x => x * 2).AsParallel().ToList();
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task CSharp_AsParallelWithToArray_NoDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var list = new List<int> { 1, 2, 3 };
                        var result = list.Select(x => x * 2).AsParallel().ToArray();
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task CSharp_RegularForeachWithoutAsParallel_NoDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var list = new List<int> { 1, 2, 3 };
                        foreach (var item in list)
                        {
                        }
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task CSharp_AsParallelOnArrayDirectlyInForeachLoop_ReportsDiagnostic()
        {
            const string code = """
                using System.Collections.Generic;
                using System.Linq;

                public class Tests
                {
                    public void M()
                    {
                        var array = new int[] { 1, 2, 3 };
                        foreach (var item in [|array.AsParallel()|])
                        {
                        }
                    }
                }
                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task VB_AsParallelWithSelectInForeachLoop_ReportsDiagnostic()
        {
            const string code = """
                Imports System.Collections.Generic
                Imports System.Linq

                Public Class Tests
                    Public Sub M()
                        Dim list = New List(Of Integer) From {1, 2, 3}
                        For Each item In [|list.Select(Function(x) x * 2).AsParallel()|]
                        Next
                    End Sub
                End Class
                """;

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task VB_AsParallelWithToList_NoDiagnostic()
        {
            const string code = """
                Imports System.Collections.Generic
                Imports System.Linq

                Public Class Tests
                    Public Sub M()
                        Dim list = New List(Of Integer) From {1, 2, 3}
                        Dim result = list.Select(Function(x) x * 2).AsParallel().ToList()
                    End Sub
                End Class
                """;

            return VerifyVB.VerifyAnalyzerAsync(code);
        }
    }
}
