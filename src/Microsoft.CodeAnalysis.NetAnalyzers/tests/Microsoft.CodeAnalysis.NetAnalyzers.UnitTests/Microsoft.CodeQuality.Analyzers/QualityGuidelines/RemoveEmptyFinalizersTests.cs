// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RemoveEmptyFinalizersAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class RemoveEmptyFinalizersTests
    {
        [Fact]
        public async Task CA1821CSharpTestNoWarningAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

public class Class1
{
	// No violation because the finalizer contains a method call
	~Class1()
	{
        System.Console.Write("" "");
	}
}

public class Class2
{
	// No violation because the finalizer contains a local declaration statement
	~Class2()
	{
        var x = 1;
	}
}

public class Class3
{
    bool x = true;

	// No violation because the finalizer contains an expression statement
	~Class3()
	{
        x = false;
	}
}

public class Class4
{
	// No violation because the finalizer's body is not empty
	~Class4()
	{
        var x = false;
		Debug.Fail(""Finalizer called!"");
	}
}

public class Class5
{
	// No violation because the finalizer's body is not empty
	~Class5()
	{
        while (true) ;
	}
}

public class Class6
{
	// No violation because the finalizer's body is not empty
	~Class6()
	{
        System.Console.WriteLine();
		Debug.Fail(""Finalizer called!"");
	}
}

public class Class7
{
	// Violation will not occur because the finalizer's body is not empty.
	~Class7()
	{
        if (true)
        {
		    Debug.Fail(""Finalizer called!"");
        }
    }
}
");
        }

        [Fact]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
	// Violation occurs because the finalizer is empty.
	~Class1()
	{
	}
}

public class Class2
{
	// Violation occurs because the finalizer is empty.
	~Class2()
	{
        //// Comments here
	}
}
",
                GetCA1821CSharpResultAt(5, 3),
                GetCA1821CSharpResultAt(13, 3));
        }

        [Fact]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersWithScopeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
	// Violation occurs because the finalizer is empty.
	~[|Class1|]()
	{
	}
}

public class Class2
{
	// Violation occurs because the finalizer is empty.
	~[|Class2|]()
	{
        //// Comments here
	}
}

");
        }

        [Fact]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersWithDebugFailAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

public class Class1
{
	// Violation occurs because Debug.Fail is a conditional method. 
	// The finalizer will contain code only if the DEBUG directive 
	// symbol is present at compile time. When the DEBUG 
	// directive is not present, the finalizer will still exist, but 
	// it will be empty.
	~Class1()
	{
		Debug.Fail(""Finalizer called!"");
    }
}
",
            GetCA1821CSharpResultAt(11, 3));
        }

        [Fact, WorkItem(1788, "https://github.com/dotnet/roslyn-analyzers/issues/1788")]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersWithDebugFail_ExpressionBodyAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

public class Class1
{
	// Violation occurs because Debug.Fail is a conditional method. 
	// The finalizer will contain code only if the DEBUG directive 
	// symbol is present at compile time. When the DEBUG 
	// directive is not present, the finalizer will still exist, but 
	// it will be empty.
	~Class1() => Debug.Fail(""Finalizer called!"");
}
",
            GetCA1821CSharpResultAt(11, 3));
        }

        [Fact, WorkItem(1241, "https://github.com/dotnet/roslyn-analyzers/issues/1241")]
        public async Task CA1821_DebugFailAndDebugDirective_NoDiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class Class1
{
#if DEBUG
	// Violation will not occur because the finalizer will exist and 
	// contain code when the DEBUG directive is present. When the 
	// DEBUG directive is not present, the finalizer will not exist, 
	// and therefore not be empty.
	~Class1()
	{
		System.Diagnostics.Debug.Fail(""Finalizer called!"");
    }
#endif
}
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var project = solution.GetProject(projectId);
                        var parseOptions = ((CSharpParseOptions)project.ParseOptions).WithPreprocessorSymbols("DEBUG");

                        return project.WithParseOptions(parseOptions)
                            .Solution;
                    },
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestCode = @"
Public Class Class1
#If DEBUG Then
	Protected Overrides Sub Finalize()
		System.Diagnostics.Debug.Fail(""Finalizer called!"")
    End Sub
#End If
End Class
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var project = solution.GetProject(projectId);
                        var parseOptions = ((VisualBasicParseOptions)project.ParseOptions).WithPreprocessorSymbols(new KeyValuePair<string, object>("DEBUG", true));

                        return project.WithParseOptions(parseOptions)
                            .Solution;
                    },
                }
            }.RunAsync();
        }

        [Fact, WorkItem(1241, "https://github.com/dotnet/roslyn-analyzers/issues/1241")]
        public async Task CA1821_DebugFailAndReleaseDirective_NoDiagnostic_FalsePositiveAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class Class1
{
#if RELEASE
	// There should be a diagnostic because in release the finalizer will be empty.
	~Class1()
	{
		System.Diagnostics.Debug.Fail(""Finalizer called!"");
    }
#endif
}
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var project = solution.GetProject(projectId);
                        var parseOptions = ((CSharpParseOptions)project.ParseOptions).WithPreprocessorSymbols("RELEASE");

                        return project.WithParseOptions(parseOptions)
                            .Solution;
                    },
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestCode = @"
Public Class Class1
#If RELEASE Then
    ' There should be a diagnostic because in release the finalizer will be empty.
	Protected Overrides Sub Finalize()
		System.Diagnostics.Debug.Fail(""Finalizer called!"")
    End Sub
#End If
End Class
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var project = solution.GetProject(projectId);
                        var parseOptions = ((VisualBasicParseOptions)project.ParseOptions).WithPreprocessorSymbols(new KeyValuePair<string, object>("RELEASE", true));

                        return project.WithParseOptions(parseOptions)
                            .Solution;
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersWithDebugFailAndDirectiveAroundStatementsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

public class Class1
{
	// Violation occurs because Debug.Fail is a conditional method.
	~Class1()
	{
		Debug.Fail(""Class1 finalizer called!"");
    }
}

public class Class2
{
	// Violation will not occur because the finalizer's body is not empty.
	~Class2()
	{
		Debug.Fail(""Class2 finalizer called!"");
        SomeMethod();
    }

    void SomeMethod()
    {
    }
}
",
            GetCA1821CSharpResultAt(7, 3));
        }

        [WorkItem(820941, "DevDiv")]
        [Fact]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersWithNonInvocationBodyAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    ~Class1()
	{
		Class2.SomeFlag = true;
    }
}

public class Class2
{
    public static bool SomeFlag;
}
");
        }

        [Fact]
        public async Task CA1821BasicTestNoWarningAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Diagnostics

Public Class Class1
	' No violation because the finalizer contains a method call
    Protected Overrides Sub Finalize()
        System.Console.Write("" "")
    End Sub
End Class

Public Class Class2
	' No violation because the finalizer's body is not empty.
    Protected Overrides Sub Finalize()
        Dim a = true
    End Sub
End Class

Public Class Class3
    Dim a As Boolean = True

	' No violation because the finalizer's body is not empty.
    Protected Overrides Sub Finalize()
        a = False
    End Sub
End Class

Public Class Class4
	' No violation because the finalizer's body is not empty.
    Protected Overrides Sub Finalize()
        Dim a = False
        Debug.Fail(""Finalizer called!"")
    End Sub
End Class

Public Class Class5
	' No violation because the finalizer's body is not empty.
    Protected Overrides Sub Finalize()
        If True Then
            Debug.Fail(""Finalizer called!"")
        End If
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1821BasicTestRemoveEmptyFinalizersAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Diagnostics

Public Class Class1
    '  Violation occurs because the finalizer is empty.
    Protected Overrides Sub Finalize()

    End Sub
End Class

Public Class Class2
    '  Violation occurs because the finalizer is empty.
    Protected Overrides Sub Finalize()
        ' Comments
    End Sub
End Class

Public Class Class3
    '  Violation occurs because Debug.Fail is a conditional method.
    Protected Overrides Sub Finalize()
        Debug.Fail(""Finalizer called!"")
    End Sub
End Class
",
                GetCA1821BasicResultAt(6, 29),
                GetCA1821BasicResultAt(13, 29),
                GetCA1821BasicResultAt(20, 29));
        }

        [Fact]
        public async Task CA1821BasicTestRemoveEmptyFinalizersWithScopeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Diagnostics

Public Class Class1
    '  Violation occurs because the finalizer is empty.
    Protected Overrides Sub [|Finalize|]()

    End Sub
End Class

Public Class Class2
    '  Violation occurs because the finalizer is empty.
    Protected Overrides Sub [|Finalize|]()
        ' Comments
    End Sub
End Class

Public Class Class3
    '  Violation occurs because Debug.Fail is a conditional method.
    Protected Overrides Sub [|Finalize|]()
        Debug.Fail(""Finalizer called!"")
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1821BasicTestRemoveEmptyFinalizersWithDebugFailAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Diagnostics

Public Class Class1
	' Violation occurs because Debug.Fail is a conditional method.
    Protected Overrides Sub Finalize()
        Debug.Fail(""Finalizer called!"")
    End Sub
End Class

Public Class Class2
	' Violation occurs because Debug.Fail is a conditional method.
    Protected Overrides Sub Finalize()
        Dim a = False
        Debug.Fail(""Finalizer called!"")
    End Sub
End Class
",
                GetCA1821BasicResultAt(6, 29));
        }

        [Fact]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersWithThrowStatementAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    ~Class1()
    {
        throw new System.Exception();
    }
}",
                GetCA1821CSharpResultAt(4, 6));
        }

        [Fact, WorkItem(1788, "https://github.com/dotnet/roslyn-analyzers/issues/1788")]
        public async Task CA1821CSharpTestRemoveEmptyFinalizersWithThrowExpressionAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    ~Class1() => throw new System.Exception();
}",
                GetCA1821CSharpResultAt(4, 6));
        }

        [Fact]
        public async Task CA1821BasicTestRemoveEmptyFinalizersWithThrowStatementAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
	' Violation occurs because Debug.Fail is a conditional method.
    Protected Overrides Sub Finalize()
        Throw New System.Exception()
    End Sub
End Class
",
                GetCA1821BasicResultAt(4, 29));
        }

        [Fact, WorkItem(1211, "https://github.com/dotnet/roslyn-analyzers/issues/1211")]
        public async Task CA1821CSharpRemoveEmptyFinalizersInvalidInvocationExpressionAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C1
{
    ~C1()
    {
        {|CS0103:a|}{|CS1002:|}
    }
}
");
        }

        [Fact, WorkItem(1788, "https://github.com/dotnet/roslyn-analyzers/issues/1788")]
        public async Task CA1821CSharpRemoveEmptyFinalizers_ErrorCodeWithBothBlockAndExpressionBodyAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C1
{
    {|CS8057:~C1() { }
    => {|CS1525:;|}|}
}
");
        }

        [Fact, WorkItem(1211, "https://github.com/dotnet/roslyn-analyzers/issues/1211")]
        public async Task CA1821BasicRemoveEmptyFinalizersInvalidInvocationExpressionAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Protected Overrides Sub Finalize()
        {|BC30451:a|}
    End Sub
End Class
");
        }

        [Fact, WorkItem(1788, "https://github.com/dotnet/roslyn-analyzers/issues/1788")]
        public async Task CA1821CSharpRemoveEmptyFinalizers_ExpressionBodiedImplAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

public class SomeTestClass : IDisposable
{
    private readonly Stream resource = new MemoryStream(1024);

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool dispose)
    {
        if (dispose)
        {
            this.resource.Dispose();
        }
    }

    ~SomeTestClass() => this.Dispose(false);
}");
        }

        private static DiagnosticResult GetCA1821CSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA1821BasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
