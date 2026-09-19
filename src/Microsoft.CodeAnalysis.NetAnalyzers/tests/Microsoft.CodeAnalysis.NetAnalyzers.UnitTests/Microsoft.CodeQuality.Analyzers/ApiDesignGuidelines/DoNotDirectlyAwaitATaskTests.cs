// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotDirectlyAwaitATaskFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class DoNotDirectlyAwaitATaskTests
    {
        [TestMethod]
        [WorkItem(1962, "https://github.com/dotnet/roslyn-analyzers/issues/1962")]
        [DataRow("Task")]
        [DataRow("Task<int>")]
        [DataRow("ValueTask")]
        [DataRow("ValueTask<int>")]
        public async Task CSharpSimpleAwaitTaskAsync(string typeName)
        {
            var code = $$"""
                using System.Threading.Tasks;

                public class C
                {
                    public async Task M()
                    {
                        {{typeName}} t = default;
                        await [|t|];
                    }
                }
                """;
            string fixedCode(bool configureAwait) => $$"""
                using System.Threading.Tasks;

                public class C
                {
                    public async Task M()
                    {
                        {{typeName}} t = default;
                        await t.ConfigureAwait({{(configureAwait ? "true" : "false")}});
                    }
                }
                """;

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode(configureAwait: false) } },
                CodeActionIndex = 0,
                CodeActionEquivalenceKey = nameof(MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitFalse),
            }.RunAsync(CancellationToken.None);

            await new VerifyCS.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode(configureAwait: true) } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = nameof(MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitTrue),
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task CSharpSimpleAwaitTaskWithTriviaAsync()
        {
            var code = """
                using System.Threading.Tasks;

                public class C
                {
                    public async Task M()
                    {
                        Task t = null;
                        await /*leading */ [|t|] /*trailing*/; //Shouldn't matter
                    }
                }
                """;
            var fixedCode = """
                using System.Threading.Tasks;

                public class C
                {
                    public async Task M()
                    {
                        Task t = null;
                        await /*leading */ t.ConfigureAwait(false) /*trailing*/; //Shouldn't matter
                    }
                }
                """;
            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod, WorkItem(4888, "https://github.com/dotnet/roslyn-analyzers/issues/4888")]
        public Task CSharpAsyncDisposable_DiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using System.Threading.Tasks;

                    public class C
                    {
                        private static IAsyncDisposable Create() => throw null;
                        private static Task<IAsyncDisposable> CreateAsync() => throw null;

                        public async Task M1()
                        {
                            await using var resource = [|Create()|];
                        }

                        public async Task M2()
                        {
                            await using var resource = [|await [|CreateAsync()|]|];
                        }

                        public async Task M3()
                        {
                            await using (var resource = [|Create()|])
                            {
                            }
                        }

                        public async Task M4()
                        {
                            await using (var resource = [|await [|CreateAsync()|]|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = """
                    using System;
                    using System.Threading.Tasks;

                    public class C
                    {
                        private static IAsyncDisposable Create() => throw null;
                        private static Task<IAsyncDisposable> CreateAsync() => throw null;

                        public async Task M1()
                        {
                            await using var resource = Create().ConfigureAwait(false);
                        }

                        public async Task M2()
                        {
                            await using var resource = (await CreateAsync().ConfigureAwait(false)).ConfigureAwait(false);
                        }

                        public async Task M3()
                        {
                            await using (var resource = Create().ConfigureAwait(false))
                            {
                            }
                        }

                        public async Task M4()
                        {
                            await using (var resource = (await CreateAsync().ConfigureAwait(false)).ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(4888, "https://github.com/dotnet/roslyn-analyzers/issues/4888")]
        [DataRow("class")]
        [DataRow("struct")]
        [DataRow("record")]
        [DataRow("record class")]
        [DataRow("record struct")]
        public Task CSharpAsyncDisposableImplementation_DiagnosticAsync(string implKind)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System;
                    using System.Threading.Tasks;

                    public {{implKind}} MyAsyncDisposable : IAsyncDisposable
                    {
                        public ValueTask DisposeAsync() => default;
                    }

                    public class C
                    {
                        private static MyAsyncDisposable Create() => throw null;
                        private static Task<MyAsyncDisposable> CreateAsync() => throw null;

                        public async Task M1()
                        {
                            await using var resource = [|Create()|];
                        }

                        public async Task M2()
                        {
                            await using var resource = [|await [|CreateAsync()|]|];
                        }

                        public async Task M3()
                        {
                            await using (var resource = [|Create()|])
                            {
                            }
                        }

                        public async Task M4()
                        {
                            await using (var resource = [|await [|CreateAsync()|]|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = $$"""
                    using System;
                    using System.Threading.Tasks;

                    public {{implKind}} MyAsyncDisposable : IAsyncDisposable
                    {
                        public ValueTask DisposeAsync() => default;
                    }

                    public class C
                    {
                        private static MyAsyncDisposable Create() => throw null;
                        private static Task<MyAsyncDisposable> CreateAsync() => throw null;

                        public async Task M1()
                        {
                            await using var resource = Create().ConfigureAwait(false);
                        }

                        public async Task M2()
                        {
                            await using var resource = (await CreateAsync().ConfigureAwait(false)).ConfigureAwait(false);
                        }

                        public async Task M3()
                        {
                            await using (var resource = Create().ConfigureAwait(false))
                            {
                            }
                        }

                        public async Task M4()
                        {
                            await using (var resource = (await CreateAsync().ConfigureAwait(false)).ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(4888, "https://github.com/dotnet/roslyn-analyzers/issues/4888")]
        public Task CSharpAsyncDisposableInheritingInterface_DiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using System.Threading.Tasks;

                    public interface IMyAsyncDisposable : IAsyncDisposable
                    {
                    }

                    public class C
                    {
                        private static IMyAsyncDisposable Create() => throw null;
                        private static Task<IMyAsyncDisposable> CreateAsync() => throw null;

                        public async Task M1()
                        {
                            await using var resource = [|Create()|];
                        }

                        public async Task M2()
                        {
                            await using var resource = [|await [|CreateAsync()|]|];
                        }

                        public async Task M3()
                        {
                            await using (var resource = [|Create()|])
                            {
                            }
                        }

                        public async Task M4()
                        {
                            await using (var resource = [|await [|CreateAsync()|]|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = """
                    using System;
                    using System.Threading.Tasks;

                    public interface IMyAsyncDisposable : IAsyncDisposable
                    {
                    }

                    public class C
                    {
                        private static IMyAsyncDisposable Create() => throw null;
                        private static Task<IMyAsyncDisposable> CreateAsync() => throw null;

                        public async Task M1()
                        {
                            await using var resource = Create().ConfigureAwait(false);
                        }

                        public async Task M2()
                        {
                            await using var resource = (await CreateAsync().ConfigureAwait(false)).ConfigureAwait(false);
                        }

                        public async Task M3()
                        {
                            await using (var resource = Create().ConfigureAwait(false))
                            {
                            }
                        }

                        public async Task M4()
                        {
                            await using (var resource = (await CreateAsync().ConfigureAwait(false)).ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(4888, "https://github.com/dotnet/roslyn-analyzers/issues/4888")]
        public Task CSharpAsyncDisposableConstrainedGenericParameter_DiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using System.Threading.Tasks;

                    public class C
                    {
                        private static TAsyncDisposable Create<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable => throw null;
                        private static Task<TAsyncDisposable> CreateAsync<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable => throw null;

                        public async Task M1<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using var resource = [|Create<TAsyncDisposable>()|];
                        }

                        public async Task M2<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using var resource = [|await [|CreateAsync<TAsyncDisposable>()|]|];
                        }

                        public async Task M3<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using (var resource = [|Create<TAsyncDisposable>()|])
                            {
                            }
                        }

                        public async Task M4<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using (var resource = [|await [|CreateAsync<TAsyncDisposable>()|]|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = """
                    using System;
                    using System.Threading.Tasks;

                    public class C
                    {
                        private static TAsyncDisposable Create<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable => throw null;
                        private static Task<TAsyncDisposable> CreateAsync<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable => throw null;
                    
                        public async Task M1<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using var resource = Create<TAsyncDisposable>().ConfigureAwait(false);
                        }
                    
                        public async Task M2<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using var resource = (await CreateAsync<TAsyncDisposable>().ConfigureAwait(false)).ConfigureAwait(false);
                        }
                    
                        public async Task M3<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using (var resource = Create<TAsyncDisposable>().ConfigureAwait(false))
                            {
                            }
                        }
                    
                        public async Task M4<TAsyncDisposable>()
                            where TAsyncDisposable : IAsyncDisposable
                        {
                            await using (var resource = (await CreateAsync<TAsyncDisposable>().ConfigureAwait(false)).ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(4888, "https://github.com/dotnet/sdk/issues/4888")]
        public Task CSharpAsyncDisposableImplementationRefStruct_UsingStatement_NoDiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using System.Threading.Tasks;

                    public ref struct MyAsyncDisposableLikeStruct : IAsyncDisposable
                    {
                        public ValueTask DisposeAsync() => default;
                    }

                    public class C
                    {
                        public async Task M()
                        {
                            await using (var resource = new MyAsyncDisposableLikeStruct())
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp13,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(4888, "https://github.com/dotnet/sdk/issues/4888")]
        public Task CSharpAsyncDisposableImplementationRefStruct_UsingDeclaration_NoDiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System;
                    using System.Threading.Tasks;

                    public ref struct MyAsyncDisposableLikeStruct : IAsyncDisposable
                    {
                        public ValueTask DisposeAsync() => default;
                    }

                    public class C
                    {
                        public async Task M()
                        {
                            await using var resource = new MyAsyncDisposableLikeStruct();
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp13,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(53461, "https://github.com/dotnet/sdk/issues/53461")]
        public Task CSharpPatternBasedAwaitUsing_UsingStatement_NoDiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System.Runtime.CompilerServices;
                    using System.Threading.Tasks;

                    public struct CustomAsyncDisposable
                    {
                        public ConfiguredValueTaskAwaitable DisposeAsync() => default;
                    }

                    public class C
                    {
                        public async Task M()
                        {
                            await using (var cad = new CustomAsyncDisposable())
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(53461, "https://github.com/dotnet/sdk/issues/53461")]
        public Task CSharpPatternBasedAwaitUsing_UsingDeclaration_NoDiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System.Runtime.CompilerServices;
                    using System.Threading.Tasks;

                    public struct CustomAsyncDisposable
                    {
                        public ConfiguredValueTaskAwaitable DisposeAsync() => default;
                    }

                    public class C
                    {
                        public async Task M()
                        {
                            await using var cad = new CustomAsyncDisposable();
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        [WorkItem(1962, "https://github.com/dotnet/roslyn-analyzers/issues/1962")]
        [DataRow("Task")]
        [DataRow("Task(Of Integer)")]
        [DataRow("ValueTask")]
        [DataRow("ValueTask(Of Integer)")]
        public async Task BasicSimpleAwaitTaskAsync(string typeName)
        {
            var code = $"""
                Imports System.Threading.Tasks

                Public Class C
                    Public Async Function M() As Task
                        Dim t As {typeName}
                        Await [|t|]
                    End Function
                End Class
                """;
            string fixedCode(bool configureAwait) => $"""
                Imports System.Threading.Tasks

                Public Class C
                    Public Async Function M() As Task
                        Dim t As {typeName}
                        Await t.ConfigureAwait({(configureAwait ? "True" : "False")})
                    End Function
                End Class
                """;

            await new VerifyVB.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode(configureAwait: false) } },
                CodeActionIndex = 0,
                CodeActionEquivalenceKey = nameof(MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitFalse),
            }.RunAsync(CancellationToken.None);

            await new VerifyVB.Test
            {
                TestState = { Sources = { code } },
                FixedState = { Sources = { fixedCode(configureAwait: true) } },
                CodeActionIndex = 1,
                CodeActionEquivalenceKey = nameof(MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitTrue),
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task BasicSimpleAwaitTaskWithTriviaAsync()
        {
            var code = """
                Imports System.Threading.Tasks

                Public Class C
                    Public Async Function M() As Task
                        Dim t As Task
                        Await      [|t|] ' trailing
                    End Function
                End Class
                """;

            var fixedCode = """
                Imports System.Threading.Tasks

                Public Class C
                    Public Async Function M() As Task
                        Dim t As Task
                        Await      t.ConfigureAwait(False) ' trailing
                    End Function
                End Class
                """;
            await VerifyVB.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod]
        public async Task CSharpNoDiagnosticAsync()
        {
            var code = """
                using System;
                using System.Runtime.CompilerServices;
                using System.Threading.Tasks;

                public class C
                {
                    public async Task M()
                    {
                        Task t = null;
                        await t.ConfigureAwait(false);

                        Task<int> tg = null;
                        await tg.ConfigureAwait(false);

                        ValueTask vt = default;
                        await vt.ConfigureAwait(false);

                        ValueTask<int> vtg = default;
                        await vtg.ConfigureAwait(false);

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
                """;
            await VerifyCS.VerifyCodeFixAsync(code, code);
        }

        [TestMethod]
        public async Task BasicNoDiagnosticAsync()
        {
            var code = """
                Imports System
                Imports System.Runtime.CompilerServices
                Imports System.Threading.Tasks

                Public Class C
                    Public Async Function M() As Task
                        Dim t As Task = Nothing
                        Await t.ConfigureAwait(False)

                        Dim tg As Task(Of Integer) = Nothing
                        Await tg.ConfigureAwait(False)

                        Dim vt As ValueTask
                        Await vt.ConfigureAwait(False)

                        Dim vtg As ValueTask(Of Integer) = Nothing
                        Await vtg.ConfigureAwait(False)

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
                """;
            await VerifyVB.VerifyCodeFixAsync(code, code);
        }

        [TestMethod]
        public async Task CSharpAwaitAwaitTaskAsync()
        {
            var code = """
                using System.Threading.Tasks;

                public class C
                {
                    public async Task M()
                    {
                        Task<Task> t = null;
                        await [|await [|t|]|]; // both have warnings.
                        await [|await t.ConfigureAwait(false)|]; // outer await is wrong.
                        await (await [|t|]).ConfigureAwait(false); // inner await is wrong.
                        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // both correct.

                        ValueTask<ValueTask> vt = default;
                        await [|await [|vt|]|]; // both have warnings.
                        await [|await vt.ConfigureAwait(false)|]; // outer await is wrong.
                        await (await [|vt|]).ConfigureAwait(false); // inner await is wrong.
                        await (await vt.ConfigureAwait(false)).ConfigureAwait(false); // both correct.
                    }
                }
                """;

            var fixedCode = """
                using System.Threading.Tasks;

                public class C
                {
                    public async Task M()
                    {
                        Task<Task> t = null;
                        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // both have warnings.
                        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // outer await is wrong.
                        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // inner await is wrong.
                        await (await t.ConfigureAwait(false)).ConfigureAwait(false); // both correct.

                        ValueTask<ValueTask> vt = default;
                        await (await vt.ConfigureAwait(false)).ConfigureAwait(false); // both have warnings.
                        await (await vt.ConfigureAwait(false)).ConfigureAwait(false); // outer await is wrong.
                        await (await vt.ConfigureAwait(false)).ConfigureAwait(false); // inner await is wrong.
                        await (await vt.ConfigureAwait(false)).ConfigureAwait(false); // both correct.
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod]
        public async Task BasicAwaitAwaitTaskAsync()
        {
            var code = """
                Imports System.Threading.Tasks

                Public Class C
                    Public Async Function M() As Task
                        Dim t As Task(Of Task)
                        Await [|Await [|t|]|] ' both have warnings.
                        Await [|Await t.ConfigureAwait(False)|] ' outer await is wrong.
                        Await (Await [|t|]).ConfigureAwait(False) ' inner await is wrong.
                        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' both correct.

                        Dim vt As ValueTask(Of ValueTask)
                        Await [|Await [|vt|]|] ' both have warnings.
                        Await [|Await vt.ConfigureAwait(False)|] ' outer await is wrong.
                        Await (Await [|vt|]).ConfigureAwait(False) ' inner await is wrong.
                        Await (Await vt.ConfigureAwait(False)).ConfigureAwait(False) ' both correct.
                    End Function
                End Class
                """;
            var fixedCode = """
                Imports System.Threading.Tasks

                Public Class C
                    Public Async Function M() As Task
                        Dim t As Task(Of Task)
                        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' both have warnings.
                        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' outer await is wrong.
                        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' inner await is wrong.
                        Await (Await t.ConfigureAwait(False)).ConfigureAwait(False) ' both correct.

                        Dim vt As ValueTask(Of ValueTask)
                        Await (Await vt.ConfigureAwait(False)).ConfigureAwait(False) ' both have warnings.
                        Await (Await vt.ConfigureAwait(False)).ConfigureAwait(False) ' outer await is wrong.
                        Await (Await vt.ConfigureAwait(False)).ConfigureAwait(False) ' inner await is wrong.
                        Await (Await vt.ConfigureAwait(False)).ConfigureAwait(False) ' both correct.
                    End Function
                End Class
                """;

            await VerifyVB.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod]
        public async Task CSharpComplexAwaitTaskAsync()
        {
            var code = """
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
                """;
            var fixedCode = """
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
                """;

            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod]
        public async Task BasicComplexAwaitTaskAsync()
        {
            var code = """
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
                """;
            var fixedCode = """
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
                """;

            await VerifyVB.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        public async Task CSharpAsyncVoidMethod_DiagnosticAsync()
        {
            var code = """
                using System.Threading.Tasks;

                public class C
                {
                    private Task t;
                    public async void M()
                    {
                        await [|M1Async()|];
                    }

                    private async Task M1Async()
                    {
                        await t.ConfigureAwait(false);
                    }
                }
                """;
            var fixedCode = """
                using System.Threading.Tasks;

                public class C
                {
                    private Task t;
                    public async void M()
                    {
                        await M1Async().ConfigureAwait(false);
                    }

                    private async Task M1Async()
                    {
                        await t.ConfigureAwait(false);
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        [DataRow("dotnet_code_quality.exclude_async_void_methods = true")]
        [DataRow("dotnet_code_quality.CA2007.exclude_async_void_methods = true")]
        public async Task CSharpAsyncVoidMethod_AnalyzerOption_NoDiagnosticAsync(string editorConfigText)
        {
            var code = """
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
                }
                """;
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}
                        """) }
                }
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        [DataRow("dotnet_code_quality.exclude_async_void_methods = false")]
        [DataRow("dotnet_code_quality.CA2007.exclude_async_void_methods = false")]
        public async Task CSharpAsyncVoidMethod_AnalyzerOption_DiagnosticAsync(string editorConfigText)
        {
            var code = """
                using System.Threading.Tasks;

                public class C
                {
                    private Task t;
                    public async void M()
                    {
                        await [|M1Async()|];
                    }

                    private async Task M1Async()
                    {
                        await t.ConfigureAwait(false);
                    }
                }
                """;
            var fixedCode = """
                using System.Threading.Tasks;

                public class C
                {
                    private Task t;
                    public async void M()
                    {
                        await M1Async().ConfigureAwait(false);
                    }

                    private async Task M1Async()
                    {
                        await t.ConfigureAwait(false);
                    }
                }
                """;

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { code },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}
                        """) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                },
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(1953, "https://github.com/dotnet/roslyn-analyzers/issues/1953")]
        [DataRow("", true)]
        [DataRow("dotnet_code_quality.output_kind = ConsoleApplication", false)]
        [DataRow("dotnet_code_quality.CA2007.output_kind = ConsoleApplication, WindowsApplication", false)]
        [DataRow("dotnet_code_quality.output_kind = DynamicallyLinkedLibrary", true)]
        [DataRow("dotnet_code_quality.CA2007.output_kind = ConsoleApplication, DynamicallyLinkedLibrary", true)]
        public async Task CSharpSimpleAwaitTask_AnalyzerOption_OutputKindAsync(string editorConfigText, bool isExpectingDiagnostic)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                         """
                             using System.Threading.Tasks;

                             public class C
                             {
                                 public async Task M()
                                 {
                                     Task t = null;
                                     await {|#0:t|};
                                 }
                             }
                             """
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $"""
                        root = true

                        [*]
                        {editorConfigText}
                        """) }
                }
            };

            if (isExpectingDiagnostic)
            {
                csharpTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic().WithLocation(0));
            }

            await csharpTest.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(2393, "https://github.com/dotnet/roslyn-analyzers/issues/2393")]
        public async Task CSharpSimpleAwaitTaskInLocalFunctionAsync()
        {
            var code = """
                using System.Threading.Tasks;

                public class C
                {
                    public void M()
                    {
                        async Task CoreAsync()
                        {
                            Task t = null;
                            await [|t|];
                        }
                    }
                }
                """;
            var fixedCode = """
                using System.Threading.Tasks;

                public class C
                {
                    public void M()
                    {
                        async Task CoreAsync()
                        {
                            Task t = null;
                            await t.ConfigureAwait(false);
                        }
                    }
                }
                """;

            await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
        }

        [TestMethod, CombinatorialData]
        [WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerable_DiagnosticAsync(bool genericMethod)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading.Tasks;

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}(IAsyncEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            await foreach (var i in [|enumerable|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading.Tasks;

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}(IAsyncEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            await foreach (var i in enumerable.ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, CombinatorialData]
        [WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableImplementation_Concrete_DiagnosticAsync(
            [CombinatorialValues("class", "struct", "record", "record class", "record struct")] string implKind)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public {{implKind}} MyAsyncEnumerable : IAsyncEnumerable<int>
                    {
                        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
                    }

                    public class C
                    {
                        public async Task Test(MyAsyncEnumerable enumerable)
                        {
                            await foreach (var i in [|enumerable|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public {{implKind}} MyAsyncEnumerable : IAsyncEnumerable<int>
                    {
                        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
                    }

                    public class C
                    {
                        public async Task Test(MyAsyncEnumerable enumerable)
                        {
                            await foreach (var i in enumerable.ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableInheritingInterface_Concrete_DiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public interface IMyAsyncEnumerable : IAsyncEnumerable<int>
                    {
                    }

                    public class C
                    {
                        public async Task Test(IMyAsyncEnumerable enumerable)
                        {
                            await foreach (var i in [|enumerable|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = """
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public interface IMyAsyncEnumerable : IAsyncEnumerable<int>
                    {
                    }

                    public class C
                    {
                        public async Task Test(IMyAsyncEnumerable enumerable)
                        {
                            await foreach (var i in enumerable.ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, CombinatorialData]
        [WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableImplementation_Generic_DiagnosticAsync(
            bool genericMethod,
            [CombinatorialValues("class", "struct", "record", "record class", "record struct")] string implKind)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public {{implKind}} MyAsyncEnumerable<T> : IAsyncEnumerable<T>
                    {
                        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
                    }

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}(MyAsyncEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            await foreach (var i in [|enumerable|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public {{implKind}} MyAsyncEnumerable<T> : IAsyncEnumerable<T>
                    {
                        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
                    }

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}(MyAsyncEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            await foreach (var i in enumerable.ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, CombinatorialData]
        [WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableInheritingInterface_Generic_DiagnosticAsync(bool genericMethod)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public interface IMyAsyncEnumerable<T> : IAsyncEnumerable<T>
                    {
                    }

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}(IMyAsyncEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            await foreach (var i in [|enumerable|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public interface IMyAsyncEnumerable<T> : IAsyncEnumerable<T>
                    {
                    }

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}(IMyAsyncEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            await foreach (var i in enumerable.ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableConstrainedGenericParameter_Concrete_DiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public class C
                    {
                        public async Task Test<TEnumerable>(TEnumerable enumerable)
                            where TEnumerable : IAsyncEnumerable<int>
                        {
                            await foreach (var i in [|enumerable|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = """
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public class C
                    {
                        public async Task Test<TEnumerable>(TEnumerable enumerable)
                            where TEnumerable : IAsyncEnumerable<int>
                        {
                            await foreach (var i in enumerable.ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableConstrainedGenericParameter_Generic_DiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public class C
                    {
                        public async Task Test<TEnumerable, TElement>(TEnumerable enumerable)
                            where TEnumerable : IAsyncEnumerable<TElement>
                        {
                            await foreach (var i in [|enumerable|])
                            {
                            }
                        }
                    }
                    """,
                FixedCode = """
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public class C
                    {
                        public async Task Test<TEnumerable, TElement>(TEnumerable enumerable)
                            where TEnumerable : IAsyncEnumerable<TElement>
                        {
                            await foreach (var i in enumerable.ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp10
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableImplementationRefStruct_Concrete_NoDiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public ref struct MyAsyncEnumerableLikeStruct : IAsyncEnumerable<int>
                    {
                        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
                    }

                    public class C
                    {
                        public async Task Test()
                        {
                            await foreach (var i in new MyAsyncEnumerableLikeStruct())
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp13
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, CombinatorialData]
        [WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpAwaitIAsyncEnumerableImplementationRefStruct_Generic_NoDiagnosticAsync(bool genericMethod)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading;
                    using System.Threading.Tasks;

                    public ref struct MyAsyncEnumerableLikeStruct<T> : IAsyncEnumerable<T>
                    {
                        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => null;
                    }

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}()
                        {
                            await foreach (var i in new MyAsyncEnumerableLikeStruct<{{(genericMethod ? "T" : "int")}}>())
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp13
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(53461, "https://github.com/dotnet/sdk/issues/53461")]
        public Task CSharpAwaitPatternBasedAsyncEnumerable_Concrete_NoDiagnosticAsync()
        {
            return new VerifyCS.Test
            {
                TestCode = """
                    using System.Runtime.CompilerServices;
                    using System.Threading.Tasks;

                    public struct CustomAsyncEnumerable
                    {
                        public ConfiguredCancelableAsyncEnumerable<int>.Enumerator GetAsyncEnumerator() => default;
                    }

                    public class C
                    {
                        public async Task Test(CustomAsyncEnumerable enumerable)
                        {
                            await foreach (var i in enumerable)
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, CombinatorialData]
        [WorkItem(53461, "https://github.com/dotnet/sdk/issues/53461")]
        public Task CSharpAwaitPatternBasedAsyncEnumerable_Generic_NoDiagnosticAsync(bool genericMethod)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System.Runtime.CompilerServices;
                    using System.Threading.Tasks;

                    public struct CustomAsyncEnumerable<T>
                    {
                        public ConfiguredCancelableAsyncEnumerable<T>.Enumerator GetAsyncEnumerator() => default;
                    }

                    public class C
                    {
                        public async Task Test{{(genericMethod ? "<T>" : "")}}(CustomAsyncEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            await foreach (var i in enumerable)
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, CombinatorialData]
        [WorkItem(6652, "https://github.com/dotnet/roslyn-analyzers/issues/6652")]
        public Task CSharpForEachEnumerable_NoDiagnosticAsync(bool genericMethod)
        {
            return new VerifyCS.Test
            {
                TestCode = $$"""
                    using System.Collections.Generic;
                    using System.Threading.Tasks;

                    public class C
                    {
                        public void Test{{(genericMethod ? "<T>" : "")}}(IEnumerable<{{(genericMethod ? "T" : "int")}}> enumerable)
                        {
                            foreach(var i in enumerable)
                            {
                            }
                        }
                    }
                    """,
                LanguageVersion = LanguageVersion.CSharp8
            }.RunAsync(CancellationToken.None);
        }
    }
}
