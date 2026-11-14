// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotSetApartmentStateOfRunningThread,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Tasks.DoNotSetApartmentStateOfRunningThread,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Tasks.UnitTests
{
    [TestClass]
    public class DoNotSetApartmentStateOfRunningThreadTests
    {
        [TestMethod]
        [DataRow("STAThread")]
        [DataRow("STAThreadAttribute")]
        [DataRow("System.STAThread")]
        public async Task STAThreadOnAsyncMain_ReportsDiagnostic_CS(string attribute)
        {
            await VerifyConsoleAppAsync($$"""
                using System;
                using System.Threading.Tasks;

                class Program
                {
                    [{|CA2029:{{attribute}}|}]
                    static async Task Main()
                    {
                        await Task.Yield();
                    }
                }
                """);
        }

        [TestMethod]
        public async Task STAThreadOnAsyncMethod_ReportsDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Threading.Tasks;

                class C
                {
                    [{|CA2029:STAThread|}]
                    async Task M()
                    {
                        await Task.Yield();
                    }

                    [{|CA2029:STAThread|}]
                    async void N()
                    {
                        await Task.Yield();
                    }
                }
                """);
        }

        [TestMethod]
        public async Task STAThreadOnMethodThatIsNotTheEntryPoint_ReportsDiagnostic_CS()
        {
            await VerifyConsoleAppAsync("""
                using System;

                class Program
                {
                    [STAThread]
                    static void Main()
                    {
                    }

                    [{|CA2029:STAThread|}]
                    static void Run()
                    {
                    }
                }
                """);
        }

        [TestMethod]
        public async Task STAThreadOnTaskReturningMain_NoDiagnostic_CS()
        {
            await VerifyConsoleAppAsync("""
                using System;
                using System.Threading.Tasks;

                class Program
                {
                    [STAThread]
                    static Task Main() => Task.CompletedTask;
                }
                """);
        }

        [TestMethod]
        public async Task STAThreadOnSynchronousMethodInLibrary_NoDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;

                class C
                {
                    [STAThread]
                    static void Main()
                    {
                    }

                    [STAThread]
                    void Run()
                    {
                    }
                }
                """);
        }

        [TestMethod]
        public async Task SetApartmentStateOnCurrentThreadInAsyncMethod_ReportsDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    async Task M()
                    {
                        {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.STA)|};
                        {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.MTA)|};
                        _ = {|CA2029:Thread.CurrentThread.TrySetApartmentState(ApartmentState.STA)|};
                        await Task.Yield();
                    }
                }
                """);
        }

        [TestMethod]
        public async Task SetApartmentStateOnCurrentThreadInAsyncLambdaAndLocalFunction_ReportsDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    void M()
                    {
                        Func<Task> lambda = async () =>
                        {
                            {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.MTA)|};
                            await Task.Yield();
                        };

                        async Task LocalFunction()
                        {
                            {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.MTA)|};
                            await Task.Yield();
                        }
                    }
                }
                """);
        }

        [TestMethod]
        public async Task SetApartmentStateToSTAOnCurrentThread_ReportsDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System;
                using System.Threading;

                class C
                {
                    void M()
                    {
                        {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.STA)|};
                        {|CA2029:Thread.CurrentThread.SetApartmentState(state: ApartmentState.STA)|};
                        _ = {|CA2029:Thread.CurrentThread.TrySetApartmentState(ApartmentState.STA)|};
                        Action action = () => {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.STA)|};
                    }
                }
                """);
        }

        [TestMethod]
        public async Task SetApartmentStateOnCurrentThreadToOtherValue_NoDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading;

                class C
                {
                    void M(ApartmentState state)
                    {
                        Thread.CurrentThread.SetApartmentState(ApartmentState.MTA);
                        Thread.CurrentThread.SetApartmentState(state);
                        _ = Thread.CurrentThread.TrySetApartmentState(ApartmentState.Unknown);
                    }
                }
                """);
        }

        [TestMethod]
        public async Task SetApartmentStateOnNewThread_NoDiagnostic_CS()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Threading;
                using System.Threading.Tasks;

                class C
                {
                    void M()
                    {
                        var thread = new Thread(() => { });
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.Start();
                    }

                    async Task N()
                    {
                        var thread = new Thread(() => { });
                        thread.SetApartmentState(ApartmentState.STA);
                        _ = thread.TrySetApartmentState(ApartmentState.STA);
                        thread.Start();
                        await Task.Yield();
                    }
                }
                """);
        }

        [TestMethod]
        public async Task SetApartmentStateInTopLevelStatementsWithAwait_ReportsDiagnostic_CS()
        {
            await VerifyConsoleAppAsync("""
                using System.Threading;
                using System.Threading.Tasks;

                {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.MTA)|};
                await Task.Yield();
                """, LanguageVersion.CSharp9);
        }

        [TestMethod]
        public async Task SetApartmentStateInTopLevelStatementsWithoutAwait_ReportsOnlySTA_CS()
        {
            await VerifyConsoleAppAsync("""
                using System.Threading;

                {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.STA)|};
                Thread.CurrentThread.SetApartmentState(ApartmentState.MTA);
                """, LanguageVersion.CSharp9);
        }

        [TestMethod]
        public async Task STAThread_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System
                Imports System.Threading.Tasks

                Class C
                    <{|CA2029:STAThread|}>
                    Async Function M() As Task
                        Await Task.Yield()
                    End Function

                    <STAThread>
                    Shared Sub Main()
                    End Sub

                    <STAThread>
                    Sub Run()
                    End Sub
                End Class
                """);
        }

        [TestMethod]
        public async Task SetApartmentStateOnCurrentThread_VB()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System.Threading
                Imports System.Threading.Tasks

                Class C
                    Async Function M() As Task
                        {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.MTA)|}
                        Await Task.Yield()
                    End Function

                    Sub N()
                        {|CA2029:Thread.CurrentThread.SetApartmentState(ApartmentState.STA)|}
                        Thread.CurrentThread.SetApartmentState(ApartmentState.MTA)
                    End Sub
                End Class
                """);
        }

        private static Task VerifyConsoleAppAsync(string source, LanguageVersion languageVersion = LanguageVersion.CSharp7_3)
        {
            return new VerifyCS.Test
            {
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                    Sources = { source },
                },
                LanguageVersion = languageVersion,
            }.RunAsync(CancellationToken.None);
        }
    }
}
