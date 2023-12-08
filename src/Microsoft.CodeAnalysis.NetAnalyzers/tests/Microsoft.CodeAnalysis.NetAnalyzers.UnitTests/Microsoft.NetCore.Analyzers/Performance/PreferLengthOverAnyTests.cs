// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferLengthCountIsEmptyOverAnyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferLengthCountIsEmptyOverAnyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferLengthCountIsEmptyOverAnyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicPreferLengthCountIsEmptyOverAnyFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class PreferLengthOverAnyTests
    {
        private static readonly DiagnosticResult ExpectedDiagnostic = new DiagnosticResult(PreferLengthCountIsEmptyOverAnyAnalyzer.LengthDescriptor).WithLocation(0);

        [Fact]
        public Task TestLocalDeclarationAsync()
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

            return VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task VbTestLocalDeclarationAsync()
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

            return VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task TestParameterDeclarationAsync()
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

            return VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task VbTestParameterDeclarationAsync()
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

            return VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task TestNegatedAnyAsync()
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

            return VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task VbTestNegatedAnyAsync()
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

            return VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task DontWarnOnChainedLinqWithAnyAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(int[] array) {
        return array.Select(x => x).Any();
    }
}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task VbDontWarnOnChainedLinqWithAnyAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(array As Integer()) As Boolean
        Return array.Select(Function(x) x).Any()
    End Function
End Class";

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task DontWarnOnAnyWithPredicateAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(int[] array) {
        return array.Any(x => x > 5);
    }
}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task VbDontWarnOnAnyWithPredicateAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(array As Integer()) As Boolean
        Return array.Any(Function(x) x > 5)
    End Function
End Class";

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task DontWarnOnCustomType()
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

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(7063, "https://github.com/dotnet/roslyn-analyzers/issues/7063")]
        public Task WhenInExpressionTree_NoDiagnostic()
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

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact, WorkItem(7063, "https://github.com/dotnet/roslyn-analyzers/issues/7063")]
        public Task WhenInFunc_Diagnostic()
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

            return VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }
    }
}