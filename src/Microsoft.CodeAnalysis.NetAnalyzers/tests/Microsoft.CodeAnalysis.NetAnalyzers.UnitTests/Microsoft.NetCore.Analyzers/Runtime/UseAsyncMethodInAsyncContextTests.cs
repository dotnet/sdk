// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseAsyncMethodInAsyncContext,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseAsyncMethodInAsyncContext,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseAsyncMethodInAsyncContextTests
    {

        [Fact]
        public async Task TaskWaitInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Task t = null;
        [|t.Wait()|];
        return Task.FromResult(1);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim t As Task = Nothing
        [|t.Wait()|]
        Return Task.FromResult(1)
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskWaitInValueTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    ValueTask T() {
        Task t = null;
        [|t.Wait()|];
        return default;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As ValueTask
        Dim t As Task = Nothing
        [|t.Wait()|]
        Return CType(Nothing, ValueTask)
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

#if NETCOREAPP
        [Fact]
        public async Task TaskWait_InIAsyncEnumerableAsyncMethod_ShouldReportWarning()
        {
            var testCS = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Test {
    async IAsyncEnumerable<int> FooAsync()
    {
        [|Task.Delay(TimeSpan.FromSeconds(5)).Wait()|];
        yield return 1;
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks

Module Program
    Sub Main()
        FooAsync()
    End Sub
    Function FooAsync() As IAsyncEnumerable(Of Integer)
        [|Task.Delay(TimeSpan.FromSeconds(5)).Wait()|]
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }
#endif

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task<int> T() {
        Task<int> t = null;
        int result = [|t.Result|];
        return Task.FromResult(result);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task(Of Integer)
        Dim t As Task(Of Integer) = Nothing
        Dim result = [|t.Result()|]
        Return Task.FromResult(result)
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodGeneratesWarning_FixPreservesCall()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Task<int> t = null;
        Assert.NotNull([|t.Result|]);
        return Task.CompletedTask;
    }
}

static class Assert {
    internal static void NotNull(object value) => throw null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim t As Task(Of Integer) = Nothing
        Assert.NotNull([|t.Result|])
        Return Task.CompletedTask
    End Function
End Module

Module Assert
    Friend Sub NotNull(value As Object)
        Throw New System.Exception()
    End Sub
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningAnonymousMethodWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Func<Task<int>> f = delegate {
            Task<int> t = null;
            int result = [|t.Result|];
            return Task.FromResult(result);
        };
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim f As Func(Of Task(Of Integer)) = Function()
                                                 Dim t As Task(Of Integer) = Nothing
                                                 Dim result As Integer = [|t.Result|]
                                                 Return Task.FromResult(result)
                                             End Function
    End Sub
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningSimpleLambdaWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Func<int, Task<int>> f = a => {
            Task<int> t = null;
            int result = [|t.Result|];
            return Task.FromResult(result);
        };
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim f = Function(a)
                    Dim t As Task(Of Integer) = Nothing
                    Dim result As Integer = [|t.Result|]
                    Return Task.FromResult(result)
                End Function
    End Sub
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningSimpleLambdaExpressionWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Task<int> b = null;
        Func<int, Task<int>> f = a => Task.FromResult([|b.Result|]);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim b As Task(Of Integer) = Nothing
        Dim f = Function(a) Task.FromResult([|b.Result|])
    End Sub
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodGeneratesWarning_FixRewritesCorrectExpression()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    async Task T() {
        await [|Task.Run(() => Console.Error).Result|].WriteLineAsync();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Async Function Test() As Task
        Await [|Task.Run(Function() Console.Error).Result|].WriteLineAsync()
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningParentheticalLambdaWithinSyncMethod_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void T() {
        Func<Task<int>> f = () => {
            Task<int> t = null;
            int result = [|t.Result|];
            return Task.FromResult(result);
        };
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Sub Test()
        Dim f As Func(Of Task(Of Integer)) = Function()
                                                 Dim t As Task(Of Integer) = Nothing
                                                 Dim result As Integer = [|t.Result|]
                                                 Return Task.FromResult(result)
                                             End Function
    End Sub
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskOfTResultInTaskReturningMethodAnonymousDelegate_GeneratesNoWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    Task<int> T() {
        Task<int> task = null;
        task.ContinueWith(t => { Console.WriteLine(t.Result); });
        return Task.FromResult(1);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task(Of Integer)
        Dim task As Task(Of Integer) = Nothing
        task.ContinueWith(Sub(t) Console.WriteLine(t.Result))
        Return task.FromResult(1)
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task TaskGetAwaiterGetResultInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Task t = null;
        [|t.GetAwaiter().GetResult()|];
        return Task.FromResult(1);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim t As Task = Nothing
        [|t.GetAwaiter().GetResult()|]
        Return task.FromResult(1)
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInSameTypeGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
class Test {
    Task T() {
        [|Foo(10, 15)|];
        return Task.FromResult(1);
    }
    internal static void Foo(int x, int y) { }
    internal static Task FooAsync(int x, int y) => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        [|Foo(10, 15)|]
        Return Task.FromResult(1)
    End Function

    Friend Sub Foo(x As Integer, y As Integer)
    End Sub
    Friend Function FooAsync(x As Integer, y As Integer) As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionIsObsolete_GeneratesNoWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    Task T() {
        Foo(10, 15);
        return Task.FromResult(1);
    }

    internal static void Foo(int x, int y) { }
    [Obsolete]
    internal static Task FooAsync(int x, int y) => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Foo(10, 15)
        Return Task.FromResult(1)
    End Function

    Friend Sub Foo(x As Integer, y As Integer)
    End Sub
    <Obsolete>
    Friend Function FooAsync(x As Integer, y As Integer) As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionIsPartlyObsolete_GeneratesWarning()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    Task T() {
        [|Foo(10, 15.0)|];
        return Task.FromResult(1);
    }

    internal static void Foo(int x, int y) { }
    internal static void Foo(int x, double y) { }
    [Obsolete]
    internal static Task FooAsync(int x, int y) => null;
    internal static Task FooAsync(int x, double y) => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        [|Foo(10, 15.0)|]
        Return Task.FromResult(1)
    End Function

    Friend Sub Foo(x As Integer, y As Integer)
    End Sub
    Friend Sub Foo(x As Integer, y As Double)
    End Sub
    <Obsolete>
    Friend Function FooAsync(x As Integer, y As Integer) As Task
        Return Nothing
    End Function
    Friend Function FooAsync(x As Integer, y As Double) As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInSubExpressionGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        int r = [|Foo()|].CompareTo(1);
        return Task.FromResult(1);
    }

    internal static int Foo() => 5;
    internal static Task<int> FooAsync() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim r = [|Foo()|].CompareTo(1)
        Return Task.FromResult(1)
    End Function

    Friend Function Foo() As Integer
        Return 5
    End Function
    Friend Function FooAsync() As Task(Of Integer)
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInOtherTypeGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        [|Util.Foo()|];
        return Task.FromResult(1);
    }
}

class Util {
    internal static void Foo() { }
    internal static Task FooAsync() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        [|Util.Foo()|]
        Return Task.FromResult(1)
    End Function
End Module

Module Util
    Friend Sub Foo()
    End Sub
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsAsPrivateInOtherTypeGeneratesNoWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Util.Foo();
        return Task.FromResult(1);
    }
}

class Util {
    internal static void Foo() { }
    private static Task FooAsync() => null;
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Util.Foo()
        Return Task.FromResult(1)
    End Function
End Module

Module Util
    Friend Sub Foo()
    End Sub
    Private Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInOtherBaseTypeGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Apple a = null;
        [|a.Foo()|];
        return Task.FromResult(1);
    }
}

class Fruit {
    internal Task FooAsync() => null;
}

class Apple : Fruit {
    internal void Foo() { }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim a As Apple = Nothing
        [|a.Foo()|]
        Return Task.FromResult(1)
    End Function
End Module

Class Fruit
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Class
Class Apple
    Inherits Fruit
    Friend Sub Foo()
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationWhereAsyncOptionExistsInExtensionMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task T() {
        Fruit f = null;
        [|f.Foo()|];
        return Task.FromResult(1);
    }
}

class Fruit {
    internal void Foo() { }
}

static class FruitUtils {
    internal static Task FooAsync(this Fruit f) => null;
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks
Imports System.Runtime.CompilerServices
Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Dim f As Fruit = Nothing
        [|f.Foo()|]
        Return Task.FromResult(1)
    End Function
End Module

Class Fruit
    Friend Sub Foo()
    End Sub
End Class
Module FruitUtils
    <Extension()>
    Friend Function FooAsync(f As Fruit) As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationUsingStaticGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using static FruitUtils;

class Test {
    Task T() {
        [|Foo()|];
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo() { }
    internal static Task FooAsync() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        [|Foo()|]
        Return Task.FromResult(1)
    End Function
End Module

Module FruitUtils
    Friend Sub Foo()
    End Sub
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task SyncInvocationUsingStaticGeneratesNoWarningAcrossTypes()
        {
            var testCS = @"
using System.Threading.Tasks;
using static FruitUtils;
using static PlateUtils;

class Test {
    Task T() {
        // Foo and FooAsync are totally different methods (on different types).
        // The use of Foo should therefore not produce a recommendation to use FooAsync,
        // despite their name similarities.
        Foo();
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo() { }
}

static class PlateUtils {
    internal static Task FooAsync() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        Foo()
        Return Task.FromResult(1)
    End Function
End Module

Module FruitUtils
    Friend Sub Foo()
    End Sub
End Module

Module PlateUtils
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task AwaitingAsyncMethodWithoutSuffixProducesNoWarningWhereSuffixVersionExists()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    Task Foo() => null;
    Task FooAsync() => null;

    async Task BarAsync() {
       await Foo();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub
    Function Foo() As Task
        Return Nothing
    End Function
    Function FooAsync() As Task
        Return Nothing
    End Function
    Async Function BarAsync() As Task
        Await Foo()
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        /// <summary>
        /// Verifies that when method invocations and member access happens in properties
        /// (which can never be async), nothing bad happens.
        /// </summary>
        [Fact]
        public async Task NoDiagnosticAndNoExceptionForProperties()
        {
            var testCS = @"
using System.Threading.Tasks;

class Test {
    string Foo => string.Empty;
    string Bar => string.Join(""a"", string.Empty);
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()

    End Sub
    Function Foo() As String
        Return String.Empty
    End Function

    Function Bar() As String
        Return String.Join(""a"", String.Empty)
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task GenericMethodName()
        {
            var testCS = @"
using System.Threading.Tasks;
using static FruitUtils;

class Test {
    Task T() {
        [|Foo<int>()|];
        return Task.FromResult(1);
    }
}

static class FruitUtils {
    internal static void Foo<T>() { }
    internal static Task FooAsync<T>() => null;
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
        Test()
    End Sub
    Function Test() As Task
        [|Foo(Of Integer)()|]
        Return Task.FromResult(1)
    End Function
End Module

Module FruitUtils
    Friend Sub Foo(Of t)()
    End Sub
    Friend Function FooAsync() As Task
        Return Nothing
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task AsyncAlternative_CodeFixRespectsTrivia()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void Foo() { }
    Task FooAsync() => Task.CompletedTask;

    async Task DoWorkAsync()
    {
        await Task.Yield();
        Console.WriteLine(""Foo"");

        // Some comment
        [|Foo(/*argcomment*/)|]; // another comment
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub
    Friend Sub Foo()
    End Sub

    Friend Function FooAsync() As Task
        Return Task.CompletedTask
    End Function
    Async Function DoWorkAsync() As Task
        Await Task.Yield()
        Console.WriteLine(""Foo"")

        'Some comment
        [|Foo()|] 'another comment
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task AwaitRatherThanWait_CodeFixRespectsTrivia()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void Foo() { }
    Task FooAsync() => Task.CompletedTask;

    async Task DoWorkAsync()
    {
        await Task.Yield();
        System.Console.WriteLine(""Foo"");

        // Some comment
        [|FooAsync(/*argcomment*/).Wait()|];
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub
    Friend Sub Foo()
    End Sub

    Friend Function FooAsync() As Task
        Return Task.CompletedTask
    End Function
    Async Function DoWorkAsync() As Task
        Await Task.Yield()
        Console.WriteLine(""Foo"")

        'Some comment
        [|FooAsync().Wait()|] 'another comment
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task DoNotSuggestAsyncAlternativeWhenItIsSelf()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    public async Task CallMainAsync()
    {
        // do stuff
        CallMain();
        // do stuff
    }

    public void CallMain()
    {
        // more stuff
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub

    Async Function CallMainAsync() As Task
        CallMain()
    End Function
    Sub CallMain()
        'more stuff
    End Sub
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task DoNotSuggestAsyncAlternativeWhenItReturnsVoid()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;

class Test {
    void LogInformation() { }
    void LogInformationAsync() { }

    Task MethodAsync()
    {
        LogInformation();
        return Task.CompletedTask;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCS);

            var testVB = @"
Imports System.Threading.Tasks

Module Program
    Sub Main()
    End Sub

    Sub LogInformation()
    End Sub

    Sub LogInformationAsync()
    End Sub
    Function MethodAsync() As Task
        LogInformation()
        Return Task.CompletedTask
    End Function
End Module
";
            await VerifyVB.VerifyAnalyzerAsync(testVB);
        }

        [Fact]
        public async Task JTFRunInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    Task T() {
        JoinableTaskFactory jtf = null;
        [|jtf.Run(() => TplExtensions.CompletedTask)|];
        this.Run();
        return Task.FromResult(1);
    }

    void Run() { }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Threading

Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Dim jtf As JoinableTaskFactory = Nothing
        [|jtf.Run(Function() TplExtensions.CompletedTask)|]
        Run()
        Return Task.FromResult(1)
    End Function
    Sub Run()
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task JTFRunInTaskReturningMethod_WithExtraReturn_GeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    Task T() {
        JoinableTaskFactory jtf = null;
        [|jtf.Run(() => TplExtensions.CompletedTask)|];
        if (false) {
            return Task.FromResult(2);
        }

        this.Run();
        return Task.FromResult(1);
    }

    void Run() { }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Threading
Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Dim jtf As JoinableTaskFactory = Nothing
        [|jtf.Run(Function() TplExtensions.CompletedTask)|]
        If (False) Then
            Return Task.FromResult(2)
        End If
        Run()
        Return Task.FromResult(1)
    End Function

    Sub Run()
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task JTFRunInAsyncMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    async Task T() {
        JoinableTaskFactory jtf = null;
        [|jtf.Run(() => TplExtensions.CompletedTask)|];
        this.Run();
    }

    void Run() { }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Threading

Module Program
    Sub Main()
        Test()
    End Sub

    Async Function Test() As Task
        Dim jtf As JoinableTaskFactory = Nothing
        [|jtf.Run(Function() TplExtensions.CompletedTask)|]
        Program.Run()
    End Function
    Sub Run()
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task JTFRunOfTInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    Task T() {
        JoinableTaskFactory jtf = null;
        int result = [|jtf.Run(() => Task.FromResult(1))|];
        this.Run();
        return Task.FromResult(2);
    }

    void Run() { }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Threading

Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Dim jtf As JoinableTaskFactory = Nothing
        Dim result As Integer = [|jtf.Run(Function() Task.FromResult(1))|]
        Program.Run()
        Return Task.FromResult(2)
    End Function
    Sub Run()
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task JTJoinOfTInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    Task T() {
        JoinableTaskFactory jtf = null;
        JoinableTask<int> jt = jtf.RunAsync(() => Task.FromResult(1));
        [|jt.Join()|];
        this.Join();
        return Task.FromResult(2);
    }

    void Join() { }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Threading

Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Dim jtf As JoinableTaskFactory = Nothing
        Dim jt As JoinableTask(Of Integer) = jtf.RunAsync(Function() Task.FromResult(1))
        [|jt.Join()|]
        Program.Join()
        Return Task.FromResult(2)
    End Function
    Sub Join()
    End Sub
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task XunitThrowAsyncNotSuggestedInAsyncTestMethod()
        {
            var testCS = @"
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class Test {
    Task T() {
        Throws<Exception>(() => { });
        return Task.FromResult(1);
    }

    void Throws<T>(Action action) { }
    Task ThrowsAsync<T>(Func<Task> action) { return TplExtensions.CompletedTask; }
}
";

            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Threading

Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Throws(Of Exception)(Sub()
                             End Sub)
        Return Task.FromResult(1)
    End Function

    Sub Throws(Of t)(action As Action)
    End Sub
    Function ThrowAsync(Of t)(action As Func(Of Task)) As Task
        Return TplExtensions.CompletedTask
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioThreading,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task IVsTaskWaitInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

class Test {
    Task T() {
        IVsTask t = null;
        [|t.Wait()|];
        return Task.FromResult(1);
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioShellInterop,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Shell
Imports Microsoft.VisualStudio.Shell.Interop
Imports Task = System.Threading.Tasks.Task

Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Dim t As IVsTask = Nothing
        [|t.Wait()|]
        Return Task.FromResult(1)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioShellInterop,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task IVsTaskGetResultInTaskReturningMethodGeneratesWarning()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

class Test {
    Task T() {
        IVsTask t = null;
        object result = [|t.GetResult()|];
        return Task.FromResult(1);
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioShellInterop,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Shell
Imports Microsoft.VisualStudio.Shell.Interop
Imports Task = System.Threading.Tasks.Task

Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Dim t As IVsTask = Nothing
        Dim result As Object = [|t.GetResult()|]
        Return Task.FromResult(1)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioShellInterop,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }

        [Fact]
        public async Task IVsTaskGetResultInTaskReturningMethod_WithoutUsing_OffersNoFix()
        {
            var testCS = @"
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;

class Test {
    Task T() {
        IVsTask t = null;
        object result = [|t.GetResult()|];
        return Task.FromResult(1);
    }
}
";
            var csTestVerify = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioShellInterop,
                TestCode = testCS,
            };
            await csTestVerify.RunAsync();

            var testVB = @"
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.Shell.Interop
Module Program
    Sub Main()
        Test()
    End Sub

    Function Test() As Task
        Dim t As IVsTask = Nothing
        Dim result As Object = [|t.GetResult()|]
        Return Task.FromResult(1)
    End Function
End Module
";
            var vbTestVerify = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithVisualStudioShellInterop,
                TestCode = testVB,
            };
            await vbTestVerify.RunAsync();
        }
    }
}