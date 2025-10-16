// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotPassDisposablesIntoUnawaitedTasksAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
   Microsoft.CodeQuality.Analyzers.QualityGuidelines.DoNotPassDisposablesIntoUnawaitedTasksAnalyzer,
   Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class DoNotPassDisposablesIntoUnawaitedTasksTests
    {
        #region Diagnostic

        [Fact]
        public async Task UsingBlockNoConversion_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        using (var ms = new MemoryStream())
        {
            var res = DoAsync({|CA2025:ms|});
        }
    }

    public static Task<string> DoAsync(MemoryStream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Using ms = New MemoryStream()
            Dim res = DoAsync({|CA2025:ms|})
        End Using
    End Function

    Public Shared Function DoAsync(ByVal s As MemoryStream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task UsingBlockWithConversion_DiagnosticAsync()
        {
            // Conversion from MemoryStream to Stream
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        using (var ms = new MemoryStream())
        {
            var res = DoAsync({|CA2025:ms|});
        }
    }

    public static Task<string> DoAsync(Stream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Using ms = New MemoryStream()
            Dim res = DoAsync({|CA2025:ms|})
        End Using
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task MultipleDisposables_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static Task D()
    {
        var ms = new MemoryStream();
        var reader = new StreamReader(ms);
        Task<string> doStuff = DoAsync({|CA2025:ms|}, {|CA2025:reader|});
        ms.Dispose();
        reader.Dispose();
        return doStuff;
    }

    public static Task<string> DoAsync(Stream s, StreamReader r) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Function D() As Task
        Dim ms = New MemoryStream()
        Dim reader = New StreamReader(ms)
        Dim doStuff As Task(Of String) = DoAsync({|CA2025:ms|}, {|CA2025:reader|})
        ms.Dispose()
        reader.Dispose()
        Return doStuff
    End Function

    Public Shared Function DoAsync(ByVal s As Stream, ByVal r As StreamReader) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task MultipleCallsWithSameDisposable_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        using (var ms = new MemoryStream())
        {
            var res = DoAsync({|CA2025:ms|});
            var res1 = DoAsync({|CA2025:ms|});
            await DoAsync(ms);
        }
    }

    public static Task<string> DoAsync(MemoryStream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Using ms = New MemoryStream()
            Dim res = DoAsync({|CA2025:ms|})
            Dim res1 = DoAsync({|CA2025:ms|})
            Await DoAsync(ms)
        End Using
    End Function

    Public Shared Function DoAsync(ByVal s As MemoryStream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task SimpleUsingStatement_DiagnosticAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        using var ms = new MemoryStream();
        var res = DoAsync(ms);
    }

    public static Task<string> DoAsync(MemoryStream s) => Task.FromResult(string.Empty);
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp8,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic().WithSpan(10, 27, 10, 29)
                }
            }.RunAsync();
        }

        [Fact]
        public async Task AwaitedAfterwardsButDisposedBeforeAwait_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        var ms = new MemoryStream();
        var t = DoAsync({|CA2025:ms|});
        ms.Dispose();
        await t.ConfigureAwait(false);
    }

    public static Task DoAsync(Stream s) => Task.CompletedTask;
}
");
        }

        [Fact]
        public async Task ReturnFromMethod_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static Task D()
    {
        using (var ms = new MemoryStream())
        {
            return DoAsync({|CA2025:ms|});
        }
    }

    public static Task<string> DoAsync(Stream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Function D() As Task
        Using ms = New MemoryStream()
            Return DoAsync({|CA2025:ms|})
        End Using
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task NestedUsingStatements_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static Task D()
    {
        using (var ms = new MemoryStream())
        {
            using (var reader = new StreamReader(ms))
            {
                return DoAsync({|CA2025:ms|}, {|CA2025:reader|});
            }
        }
    }

    public static Task<string> DoAsync(Stream s, StreamReader r) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Function D() As Task
        Using ms = New MemoryStream()

            Using reader = New StreamReader(ms)
                Return DoAsync({|CA2025:ms|}, {|CA2025:reader|})
            End Using
        End Using
    End Function

    Public Shared Function DoAsync(ByVal s As Stream, ByVal r As StreamReader) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task ManualDisposeWithTryFinally_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        var ms = new MemoryStream();
        try
        {
            var res = DoAsync({|CA2025:ms|});
        }
        finally
        {
            ms.Dispose();
        }
    }

    public static Task<string> DoAsync(MemoryStream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Dim ms = New MemoryStream()

        Try
            Dim res = DoAsync({|CA2025:ms|})
        Finally
            ms.Dispose()
        End Try
    End Function

    Public Shared Function DoAsync(ByVal s As MemoryStream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task ManualDisposeWithTask_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static Task D()
    {
        var ms = new MemoryStream();
        Task doStuff = DoAsync({|CA2025:ms|});
        ms.Dispose();
        return doStuff;
    }

    public static Task DoAsync(Stream s) => Task.CompletedTask;
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Function D() As Task
        Dim ms = New MemoryStream()
        Dim doStuff As Task = DoAsync({|CA2025:ms|})
        ms.Dispose()
        Return doStuff
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task
        Return Task.CompletedTask
    End Function
End Class
");
        }

        [Fact]
        public async Task ManualDisposeWithTaskString_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static Task D()
    {
        var ms = new MemoryStream();
        Task<string> doStuff = DoAsync({|CA2025:ms|});
        ms.Dispose();
        return doStuff;
    }

    public static Task<string> DoAsync(Stream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Function D() As Task
        Dim ms = New MemoryStream()
        Dim doStuff As Task(Of String) = DoAsync({|CA2025:ms|})
        ms.Dispose()
        Return doStuff
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        #endregion Diagnostic

        #region No Diagnostic

        [Fact]
        public async Task AwaitedTask_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        var ms = new MemoryStream();
        var res = await DoAsync(ms);
        ms.Dispose();
    }

    public static Task<string> DoAsync(Stream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Dim ms = New MemoryStream()
        Dim res = Await DoAsync(ms)
        ms.Dispose()
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task UnawaitedWithNoDispose_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static Task D()
    {
        var ms = new MemoryStream();
        Task<string> doStuff = DoAsync(ms);
        return doStuff;
    }

    public static Task<string> DoAsync(Stream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Function D() As Task
        Dim ms = New MemoryStream()
        Dim doStuff As Task(Of String) = DoAsync(ms)
        Return doStuff
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task TaskWaitedSynchronously_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static void D()
    {
        var ms = new MemoryStream();
        DoAsync(ms).Wait();
        ms.Dispose();
    }

    public static Task DoAsync(Stream s) => Task.CompletedTask;
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Sub D()
        Dim ms = New MemoryStream()
        DoAsync(ms).Wait()
        ms.Dispose()
    End Sub

    Public Shared Function DoAsync(ByVal s As Stream) As Task
        Return Task.CompletedTask
    End Function
End Class
");
        }

        [Fact]
        public async Task TaskResultReceivedSynchronously_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static void D()
    {
        var ms = new MemoryStream();
        var res = DoAsync(ms).Result;
        ms.Dispose();
    }

    public static Task<string> DoAsync(Stream s) => Task.FromResult(string.Empty);
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Sub D()
        Dim ms = New MemoryStream()
        Dim res = DoAsync(ms).Result
        ms.Dispose()
    End Sub

    Public Shared Function DoAsync(ByVal s As Stream) As Task(Of String)
        Return Task.FromResult(String.Empty)
    End Function
End Class
");
        }

        [Fact]
        public async Task AwaitedElsewhereBeforeDispose_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        var ms = new MemoryStream();
        var t = DoAsync(ms);
        var val = ms.Length - ms.Position;
        await t;

        ms.Dispose();
    }

    public static Task DoAsync(Stream s) => Task.CompletedTask;
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Dim ms = New MemoryStream()
        Dim t = DoAsync(ms)
        Dim val = ms.Length - ms.Position
        Await t
        ms.Dispose()
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task
        Return Task.CompletedTask
    End Function
End Class
");
        }

        [Fact]
        public async Task AwaitedElsewhereBeforeDisposeMultipleArgs_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        var ms = new MemoryStream();
        var reader = new StreamReader(ms);

        var t = DoAsync(ms, reader);
        var val = ms.Length - ms.Position;
        await t;

        ms.Dispose();
    }

    public static Task DoAsync(Stream s, StreamReader r) => Task.CompletedTask;
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Dim ms = New MemoryStream()
        Dim reader = New StreamReader(ms)
        Dim t = DoAsync(ms, reader)
        Dim val = ms.Length - ms.Position
        Await t
        ms.Dispose()
    End Function

    Public Shared Function DoAsync(ByVal s As Stream, ByVal r As StreamReader) As Task
        Return Task.CompletedTask
    End Function
End Class
");
        }

        [Fact]
        public async Task AwaitedElsewhereBeforeDisposeConfigureAwait_NoDiagnosticAsync()
        {
            // Ensures we register the await even when it's not the direct parent of the local invocation
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Threading.Tasks;

public class C
{
    public static async Task D()
    {
        var ms = new MemoryStream();
        var t = DoAsync(ms);
        var val = ms.Length - ms.Position;
        await t.ConfigureAwait(false);

        ms.Dispose();
    }

    public static Task DoAsync(Stream s) => Task.CompletedTask;
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
Imports System.Threading.Tasks

Public Class C
    Public Shared Async Function D() As Task
        Dim ms = New MemoryStream()
        Dim t = DoAsync(ms)
        Dim val = ms.Length - ms.Position
        Await t.ConfigureAwait(False)
        ms.Dispose()
    End Function

    Public Shared Function DoAsync(ByVal s As Stream) As Task
        Return Task.CompletedTask
    End Function
End Class
");
        }

        #endregion No Diagnostic
    }
}
