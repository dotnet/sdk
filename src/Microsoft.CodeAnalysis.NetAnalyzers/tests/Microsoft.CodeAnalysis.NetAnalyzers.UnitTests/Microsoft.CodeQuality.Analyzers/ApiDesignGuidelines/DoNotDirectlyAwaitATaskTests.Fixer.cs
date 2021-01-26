// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class DoNotDirectlyAwaitATaskFixerTests
    {
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
        await [|t|];
    }
}
";
            var fixedCode = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await t.ConfigureAwait(false);
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [Fact, WorkItem(1962, "https://github.com/dotnet/roslyn-analyzers/issues/1962")]
        public async Task CSharpSimpleAwaitTask_ConfigureAwaitTrue()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await [|t|];
    }
}
";
            var fixedCode = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await t.ConfigureAwait(true);
    }
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode } },
#pragma warning disable CS0618 // Type or member is obsolete
                CodeFixIndex = 1,
                CodeFixEquivalenceKey = MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitTrue,
#pragma warning restore CS0618 // Type or member is obsolete
            }.RunAsync();
        }

        [Fact]
        public async Task BasicSimpleAwaitTask()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task
        Await [|t|]
    End Function
End Class
";

            var fixedCode = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task
        Await t.ConfigureAwait(False)
    End Function
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, fixedCode);
        }

        [Fact, WorkItem(1962, "https://github.com/dotnet/roslyn-analyzers/issues/1962")]
        public async Task BasicSimpleAwaitTask_ConfigureAwaitTrue()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task
        Await [|t|]
    End Function
End Class
";

            var fixedCode = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task
        Await t.ConfigureAwait(True)
    End Function
End Class
";

            await new VerifyVB.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode } },
#pragma warning disable CS0618 // Type or member is obsolete
                CodeFixIndex = 1,
                CodeFixEquivalenceKey = MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitTrue,
#pragma warning restore CS0618 // Type or member is obsolete
            }.RunAsync();
        }

        [Fact]
        public async Task CSharpSimpleAwaitTaskWithTrivia()
        {
            var code = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await /*leading */ [|t|] /*trailing*/; //Shouldn't matter
    }
}
";
            var fixedCode = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task t = null;
        await /*leading */ t.ConfigureAwait(false) /*trailing*/; //Shouldn't matter
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [Fact]
        public async Task BasicSimpleAwaitTaskWithTrivia()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task
        Await      [|t|] ' trailing
    End Function
End Class
";

            var fixedCode = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task
        Await      t.ConfigureAwait(False) ' trailing
    End Function
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, fixedCode);
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
        await [|await [|t|]|]; // both have warnings.
        await [|await t.ConfigureAwait(false)|]; // outer await is wrong.
        await (await [|t|]).ConfigureAwait(false); // inner await is wrong.
    }
}
";

            var fixedCode = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task<Task> t = null;
        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // both have warnings.
        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // outer await is wrong.
        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // inner await is wrong.
    }
}
";

            // 🐛 the Fix All should not be producing this invalid code
            var fixAllCode = @"
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        Task<Task> t = null;
        await (await t.ConfigureAwait(false)).ConfigureAwait(false).{|CS1061:ConfigureAwait|}(false); // both have warnings.
        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // outer await is wrong.
        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // inner await is wrong.
    }
}
";

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode } },
                BatchFixedState = { Sources = { fixAllCode } },
                NumberOfFixAllIterations = 2,
            }.RunAsync();
        }

        [Fact]
        public async Task BasicAwaitAwaitTask()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task(Of Task)
        Await [|Await [|t|]|] ' both have warnings.
        Await [|Await t.ConfigureAwait(False)|] ' outer await is wrong.
        Await (Await [|t|]).ConfigureAwait(False) ' inner await is wrong.
    End Function
End Class
";
            var fixedCode = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task(Of Task)
        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' both have warnings.
        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' outer await is wrong.
        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' inner await is wrong.
    End Function
End Class
";

            // 🐛 the Fix All should not be producing this invalid code
            var fixAllCode = @"
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim t As Task(Of Task)
        Await {|BC30456:(Await t.ConfigureAwait(False)).ConfigureAwait(False).ConfigureAwait|}(False) ' both have warnings.
        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' outer await is wrong.
        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' inner await is wrong.
    End Function
End Class
";

            await new VerifyVB.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode } },
                BatchFixedState = { Sources = { fixAllCode } },
                NumberOfFixAllIterations = 2,
            }.RunAsync();
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
        int x = 10 + await [|GetTask()|];
        Func<Task<int>> a = async () => await [|GetTask()|];
        Console.WriteLine(await [|GetTask()|]);
    }

    public Task<int> GetTask() { throw new NotImplementedException(); }
}
";
            var fixedCode = @"
using System;
using System.Threading.Tasks;

public class C
{
    public async Task M()
    {
        int x = 10 + await GetTask().ConfigureAwait(false);
        Func<Task<int>> a = async () => await GetTask().ConfigureAwait(false);
        Console.WriteLine(await GetTask().ConfigureAwait(false));
    }

    public Task<int> GetTask() { throw new NotImplementedException(); }
}
";
            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [Fact]
        public async Task BasicComplexeAwaitTask()
        {
            var code = @"
Imports System
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim x As Integer = 10 + Await [|GetTask()|]
        Dim a As Func(Of Task(Of Integer)) = Async Function() Await [|GetTask()|]
        Console.WriteLine(Await [|GetTask()|])
    End Function
    Public Function GetTask() As Task(Of Integer)
        Throw New NotImplementedException()
    End Function
End Class
";
            var fixedCode = @"
Imports System
Imports System.Threading.Tasks

Public Class C
    Public Async Function M() As Task
        Dim x As Integer = 10 + Await GetTask().ConfigureAwait(False)
        Dim a As Func(Of Task(Of Integer)) = Async Function() Await GetTask().ConfigureAwait(False)
        Console.WriteLine(Await GetTask().ConfigureAwait(False))
    End Function
    Public Function GetTask() As Task(Of Integer)
        Throw New NotImplementedException()
    End Function
End Class
";
            await VerifyVB.VerifyCodeFixAsync(code, fixedCode);
        }
    }
}