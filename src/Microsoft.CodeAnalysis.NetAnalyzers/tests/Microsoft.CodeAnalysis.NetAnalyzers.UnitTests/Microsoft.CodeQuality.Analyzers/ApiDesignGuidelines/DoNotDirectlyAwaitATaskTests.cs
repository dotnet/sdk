// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotDirectlyAwaitATaskTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotDirectlyAwaitATaskAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotDirectlyAwaitATaskAnalyzer();
        }

        [Fact]
        public async Task CSharpSimpleAwaitTask()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await t;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(9, 15));
        }

        [Fact]
        public async Task BasicSimpleAwaitTask()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task
        Await t
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(7, 15));
        }

        [Fact]
        public async Task CSharpSimpleAwaitTaskOfT()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task<int> t = null;
        int x = await t;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(9, 23));
        }

        [Fact]
        public async Task BasicSimpleAwaitTaskOfT()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task(Of Integer)
        Dim x As Integer = Await t
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(7, 34));
        }

        [Fact]
        public async Task CSharpNoDiagnostic()
        {
            var code = @"
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await t.ConfigureAwait(false);

        SomeAwaitable s = null;
        await s;

        await{|CS1525:;|} // No Argument
    }
}

public class SomeAwaitable
{
    public SomeAwaiter GetAwaiter()
    {
        throw new NotImplementedException();
    }
}

public class SomeAwaiter : INotifyCompletion
{
    public bool IsCompleted => true;

    public void OnCompleted(Action continuation)
    {
    }

    public void GetResult()
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnostic()
        {
            var code = @"
Imports System
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task = Nothing
        Await t.ConfigureAwait(False)

        Dim s As SomeAwaitable = Nothing
        Await s

        Await {|BC30201:|}'No Argument
    End Function
End Class

Public Class SomeAwaitable
    Public Function GetAwaiter As SomeAwaiter
        Throw New NotImplementedException()
    End Function
End Class

Public Class SomeAwaiter
    Implements INotifyCompletion
    Public ReadOnly Property IsCompleted() As Boolean
	    Get
		    Throw New NotImplementedException()
	    End Get
    End Property

    Public Sub OnCompleted(continuation As Action) Implements INotifyCompletion.OnCompleted
    End Sub

    Public Sub GetResult()
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpAwaitAwaitTask()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task<Task> t = null;
        await await t; // both have warnings.
        await await t.ConfigureAwait(false); // outer await is wrong.
        await (await t).ConfigureAwait(false); // inner await is wrong.
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(9, 15),
                GetCSharpResultAt(9, 21),
                GetCSharpResultAt(10, 15),
                GetCSharpResultAt(11, 22));
        }

        [Fact]
        public async Task BasicAwaitAwaitTask()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task(Of Task)
        Await Await t ' both have warnings.
        Await Await t.ConfigureAwait(False) ' outer await is wrong.
        Await (Await t).ConfigureAwait(False) ' inner await is wrong.
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicResultAt(7, 15),
                GetBasicResultAt(7, 21),
                GetBasicResultAt(8, 15),
                GetBasicResultAt(9, 22));
        }

        [Fact]
        public async Task CSharpComplexAwaitTask()
        {
            var code = @"
using System;
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        int x = 10 + await GetTask();
        Func<Task<int>> a = async () => await GetTask();
        Console.WriteLine(await GetTask());
    }

    public Task<int> GetTask() { throw new NotImplementedException(); }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code,
                GetCSharpResultAt(9, 28),
                GetCSharpResultAt(10, 47),
                GetCSharpResultAt(11, 33));
        }

        [Fact]
        public async Task BasicComplexeAwaitTask()
        {
            var code = @"
Imports System
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim x As Integer = 10 + Await GetTask()
        Dim a As Func(Of Task(Of Integer)) = Async Function() Await GetTask()
        Console.WriteLine(Await GetTask())
    End Function
    Public Function GetTask() As Task(Of Integer)
        Throw New NotImplementedException()
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code,
                GetBasicResultAt(7, 39),
                GetBasicResultAt(8, 69),
                GetBasicResultAt(9, 33));
        }

        [Fact, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        public async Task CSharpAsyncVoidMethod_Diagnostic()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    private Task t;
    public async void M()
    {
        await M1Async();
    }

    private async Task M1Async()
    {
        await t.ConfigureAwait(false);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(9, 15));
        }

        [Theory, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        [InlineData("dotnet_code_quality.exclude_async_void_methods = true")]
        [InlineData("dotnet_code_quality.CA2007.exclude_async_void_methods = true")]
        public async Task CSharpAsyncVoidMethod_AnalyzerOption_NoDiagnostic(string editorConfigText)
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    private Task t;
    public async void M()
    {
        await M1Async();
    }

    private async Task M1Async()
    {
        await t.ConfigureAwait(false);
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    AdditionalFiles = { (".editorconfig", editorConfigText) }
                }
            }.RunAsync();
        }

        [Theory, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        [InlineData("dotnet_code_quality.exclude_async_void_methods = false")]
        [InlineData("dotnet_code_quality.CA2007.exclude_async_void_methods = false")]
        public async Task CSharpAsyncVoidMethod_AnalyzerOption_Diagnostic(string editorConfigText)
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    private Task t;
    public async void M()
    {
        await M1Async();
    }

    private async Task M1Async()
    {
        await t.ConfigureAwait(false);
    }
}";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    AdditionalFiles = { (".editorconfig", editorConfigText) }
                },
                ExpectedDiagnostics = { GetCSharpResultAt(9, 15) }
            }.RunAsync();
        }

        [Fact, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        public void CSharpSimpleAwaitTask_AnalyzerOption_OutputKind()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await t;
    }
}
";
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            // Verify diagnostic with no editor config options.
            VerifyCSharp(code, compilationOptions, parseOptions: null, expected: GetCSharpResultAt(9, 15));

            // Verify no diagnostic with editor config options that exclude OutputKind.DynamicallyLinkedLibrary.
            var editorConfigText = "dotnet_code_quality.output_kind = ConsoleApplication";
            VerifyCSharp(code, GetEditorConfigAdditionalFile(editorConfigText), compilationOptions, parseOptions: null);

            editorConfigText = "dotnet_code_quality.CA2007.output_kind = ConsoleApplication, WindowsApplication";
            VerifyCSharp(code, GetEditorConfigAdditionalFile(editorConfigText), compilationOptions, parseOptions: null);

            // Verify diagnostic with editor config options that include OutputKind.DynamicallyLinkedLibrary.
            editorConfigText = "dotnet_code_quality.output_kind = DynamicallyLinkedLibrary";
            VerifyCSharp(code, GetEditorConfigAdditionalFile(editorConfigText), compilationOptions, parseOptions: null, expected: GetCSharpResultAt(9, 15));

            editorConfigText = "dotnet_code_quality.CA2007.output_kind = ConsoleApplication, DynamicallyLinkedLibrary";
            VerifyCSharp(code, GetEditorConfigAdditionalFile(editorConfigText), compilationOptions, parseOptions: null, expected: GetCSharpResultAt(9, 15));
        }

        [Fact, WorkItem(2393, "https://github.com/dotnet/roslyn-analyzers/issues/2393")]
        public async Task CSharpSimpleAwaitTaskInLocalFunction()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    public void M()
    {
        async Task CoreAsync()
        {
            Task t = null;
            await t;
        }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(11, 19));
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column)
            => new DiagnosticResult(DoNotDirectlyAwaitATaskAnalyzer.Rule)
                .WithLocation(line, column)
                .WithMessage(MicrosoftCodeQualityAnalyzersResources.DoNotDirectlyAwaitATaskMessage);

        private DiagnosticResult GetBasicResultAt(int line, int column)
            => new DiagnosticResult(DoNotDirectlyAwaitATaskAnalyzer.Rule)
                .WithLocation(line, column)
                .WithMessage(MicrosoftCodeQualityAnalyzersResources.DoNotDirectlyAwaitATaskMessage);
    }
}