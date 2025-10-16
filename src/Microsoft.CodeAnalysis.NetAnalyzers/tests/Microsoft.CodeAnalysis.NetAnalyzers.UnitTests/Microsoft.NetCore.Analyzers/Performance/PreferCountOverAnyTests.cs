// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferLengthCountIsEmptyOverAnyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferLengthCountIsEmptyOverAnyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferLengthCountIsEmptyOverAnyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicPreferLengthCountIsEmptyOverAnyFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class PreferCountOverAnyTests
    {
        private static readonly DiagnosticResult ExpectedDiagnostic = new DiagnosticResult(PreferLengthCountIsEmptyOverAnyAnalyzer.CountDescriptor).WithLocation(0);

        [Fact]
        public Task TestLocalDeclarationAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public void M() {
        var list = new List<int>();
        _ = {|#0:list.Any()|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public void M() {
        var list = new List<int>();
        _ = list.Count != 0;
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
        Dim list = new List(Of Integer)()
        Dim x = {|#0:list.Any()|}
    End Function
End Class";

            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function M()
        Dim list = new List(Of Integer)()
        Dim x = list.Count <> 0
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
    public bool HasContents(List<int> list) {
        return {|#0:list.Any()|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(List<int> list) {
        return list.Count != 0;
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
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return {|#0:list.Any()|}
    End Function
End Class";

            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return list.Count <> 0
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
    public bool IsEmpty(List<int> list) {
        return !{|#0:list.Any()|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool IsEmpty(List<int> list) {
        return list.Count == 0;
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
    Public Function IsEmpty(list As List(Of Integer)) As Boolean
        Return Not {|#0:list.Any()|}
    End Function
End Class";

            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function IsEmpty(list As List(Of Integer)) As Boolean
        Return list.Count = 0
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
    public bool HasContents(List<int> list) {
        return list.Select(x => x).Any();
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
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return list.Select(Function(x) x).Any()
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
    public bool HasContents(List<int> list) {
        return list.Any(x => x > 5);
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
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return list.Any(Function(x) x > 5)
    End Function
End Class";

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task DontWarnOnInvalidCallAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasAny()
    {
        return System.Linq.Enumerable.{|CS1501:Any|}();
    }
}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task VbDontWarnOnInvalidCallAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function M() As Boolean
        Return System.Linq.Enumerable.{|BC30516:Any|}()
    End Function
End Class";

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task TestQualifiedCallAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(List<int> list) {
        return {|#0:Enumerable.Any(list)|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(List<int> list) {
        return list.Count != 0;
    }
}";

            return VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task VbTestQualifiedCallAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return {|#0:Enumerable.Any(list)|}
    End Function
End Class";

            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return list.Count <> 0
    End Function
End Class";

            return VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task TestFullyQualifiedCallAsync()
        {
            const string code = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(List<int> list) {
        return {|#0:System.Linq.Enumerable.Any(list)|};
    }
}";
            const string fixedCode = @"
using System.Collections.Generic;
using System.Linq;

public class Tests {
    public bool HasContents(List<int> list) {
        return list.Count != 0;
    }
}";

            return VerifyCS.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task VbTestFullyQualifiedCallAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return {|#0:System.Linq.Enumerable.Any(list)|}
    End Function
End Class";

            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return list.Count <> 0
    End Function
End Class";

            return VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task VbTestWithoutParenthesesAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return {|#0:list.Any|}
    End Function
End Class";

            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return list.Count <> 0
    End Function
End Class";

            return VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
        }

        [Fact]
        public Task VbTestNegatedWithoutParenthesesAsync()
        {
            const string code = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return Not {|#0:list.Any|}
    End Function
End Class";

            const string fixedCode = @"
Imports System.Collections.Generic
Imports System.Linq

Public Class Tests
    Public Function HasContents(list As List(Of Integer)) As Boolean
        Return list.Count = 0
    End Function
End Class";

            return VerifyVB.VerifyCodeFixAsync(code, ExpectedDiagnostic, fixedCode);
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
    public int Count => throw null;
}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}