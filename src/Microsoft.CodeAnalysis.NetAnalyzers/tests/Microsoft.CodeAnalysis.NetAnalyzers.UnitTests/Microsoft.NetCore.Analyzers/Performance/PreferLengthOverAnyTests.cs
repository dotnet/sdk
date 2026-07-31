// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferLengthCountIsEmptyOverAnyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferLengthCountIsEmptyOverAnyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferLengthCountIsEmptyOverAnyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicPreferLengthCountIsEmptyOverAnyFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    [TestClass]
    public class PreferLengthOverAnyTests
    {
        private static readonly DiagnosticResult ExpectedDiagnostic = new DiagnosticResult(PreferLengthCountIsEmptyOverAnyAnalyzer.LengthDescriptor).WithLocation(0);

        [TestMethod]
        public async Task TestLocalDeclarationAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public void M() {
        var array = new int[0];
        _ = {|#0:array.Any()|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public void M() {
        var array = new int[0];
        _ = array.Length != 0;
    }
}";

            await VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [TestMethod]
        public async Task VbTestLocalDeclarationAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function M()
        Dim array = new Integer() {}
        Dim x = {|#0:array.Any()|}
    End Function
End Class";
            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function M()
        Dim array = new Integer() {}
        Dim x = array.Length <> 0
    End Function
End Class";

            await VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [TestMethod]
        public async Task TestParameterDeclarationAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContent(int[] array) {
        return {|#0:array.Any()|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContent(int[] array) {
        return array.Length != 0;
    }
}";

            await VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [TestMethod]
        public async Task VbTestParameterDeclarationAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(array As Integer()) As Boolean
        Return {|#0:array.Any()|}
    End Function
End Class";
            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(array As Integer()) As Boolean
        Return array.Length <> 0
    End Function
End Class";

            await VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [TestMethod]
        public async Task TestNegatedAnyAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool IsEmpty(int[] array) {
        return !{|#0:array.Any()|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool IsEmpty(int[] array) {
        return array.Length == 0;
    }
}";

            await VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [TestMethod]
        public async Task VbTestNegatedAnyAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function IsEmpty(array As Integer()) As Boolean
        Return Not {|#0:array.Any()|}
    End Function
End Class";
            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function IsEmpty(array As Integer()) As Boolean
        Return array.Length = 0
    End Function
End Class";

            await VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [TestMethod]
        public async Task DontWarnOnChainedLinqWithAnyAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(int[] array) {
        return array.Select(x => x).Any();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestMethod]
        public async Task VbDontWarnOnChainedLinqWithAnyAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(array As Integer()) As Boolean
        Return array.Select(Function(x) x).Any()
    End Function
End Class";

            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [TestMethod]
        public async Task DontWarnOnAnyWithPredicateAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(int[] array) {
        return array.Any(x => x > 5);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestMethod]
        public async Task VbDontWarnOnAnyWithPredicateAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(array As Integer()) As Boolean
        Return array.Any(Function(x) x > 5)
    End Function
End Class";

            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [TestMethod]
        public async Task DontWarnOnCustomType()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(MyCollection collection) {
        return collection.Any();
    }
}

public class MyCollection {
    public bool Any() => throw null;
    public int Length => throw null;
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestMethod, WorkItem(7063, "https://github.com/dotnet/roslyn-analyzers/issues/7063")]
        public async Task WhenInExpressionTree_NoDiagnostic()
        {
            const string code = """
                                using System;
                                using System.Collections.Generic;
                                using System.Linq;
                                using System.Linq.Expressions;

                                public class Tests {
                                    public void M() {
                                        var array = new int[0];
                                        Evaluate(() => array.Any());
                                    }
                                
                                    private void Evaluate(Expression<Func<bool>> expression)
                                    {
                                    }
                                }
                """;

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [TestMethod, WorkItem(7063, "https://github.com/dotnet/roslyn-analyzers/issues/7063")]
        public async Task WhenInFunc_Diagnostic()
        {
            const string code = """
                                using System;
                                using System.Collections.Generic;
                                using System.Linq;
                                using System.Linq.Expressions;

                                public class Tests {
                                    public void M() {
                                        var array = new int[0];
                                        Evaluate(() => {|#0:array.Any()|});
                                    }
                                
                                    private void Evaluate(Func<bool> func)
                                    {
                                    }
                                }
                """;
            const string fixedCode = """
                                using System;
                                using System.Collections.Generic;
                                using System.Linq;
                                using System.Linq.Expressions;

                                public class Tests {
                                    public void M() {
                                        var array = new int[0];
                                        Evaluate(() => array.Length != 0);
                                    }
                                
                                    private void Evaluate(Func<bool> func)
                                    {
                                    }
                                }
                """;

            await VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [TestMethod]
        public async Task CS_NestedAny_FixAllRewritesBothAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool M(int[] outer, int[] inner) {
        return {|#0:({|#1:inner.Any()|} ? outer : inner).Any()|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool M(int[] outer, int[] inner) {
        return (inner.Length != 0 ? outer : inner).Length != 0;
    }
}";

            await VerifyCS.VerifyCodeFixAsync(
                code,
                new[]
                {
                    ExpectedDiagnostic,
                    new DiagnosticResult(PreferLengthCountIsEmptyOverAnyAnalyzer.LengthDescriptor).WithLocation(1),
                },
                fixedCode);
        }

        [TestMethod]
        public async Task VB_NestedAny_FixAllRewritesBothAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function M(outer As Integer(), inner As Integer()) As Boolean
        Return {|#0:If({|#1:inner.Any()|}, outer, inner).Any()|}
    End Function
End Class";
            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function M(outer As Integer(), inner As Integer()) As Boolean
        Return If(inner.Length <> 0, outer, inner).Length <> 0
    End Function
End Class";

            await VerifyVB.VerifyCodeFixAsync(
                code,
                new[]
                {
                    ExpectedDiagnostic,
                    new DiagnosticResult(PreferLengthCountIsEmptyOverAnyAnalyzer.LengthDescriptor).WithLocation(1),
                },
                fixedCode);
        }
    }
}