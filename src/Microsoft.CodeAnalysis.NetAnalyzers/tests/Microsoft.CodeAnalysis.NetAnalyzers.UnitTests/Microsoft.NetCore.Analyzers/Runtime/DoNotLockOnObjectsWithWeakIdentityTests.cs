// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotLockOnObjectsWithWeakIdentityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotLockOnObjectsWithWeakIdentityAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class DoNotLockOnObjectsWithWeakIdentityTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotLockOnObjectsWithWeakIdentityAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotLockOnObjectsWithWeakIdentityAnalyzer();
        }

        [Fact]
        public void CA2002TestLockOnStrongType()
        {
            VerifyCSharp(@"
            using System;
            public class foo {
                public void Test() {
                    object o = new object();
                    lock (o) {
                        Console.WriteLine();
                    }
                }
            }
");
            VerifyBasic(@"
            Imports System
            Public Class foo
                Public Sub Test()
                    Dim o As new Object()
                    SyncLock o
                        Console.WriteLine()
                    End SyncLock
                End Sub
            End Class
");
        }

        [Fact]
        public void CA2002TestLockOnWeakIdentities()
        {
            VerifyCSharp(@"
            using System;
            public class foo
            {
                public void Test()
                {
                    string s1 = """";
                    lock (s1) { }
                    lock (""Hello"") { }

                    var o1 = new OutOfMemoryException();
                    lock (o1) { }
                    var o2 = new StackOverflowException();
                    lock (o2) { }
                    var o3 = new ExecutionEngineException();
                    lock (o3) { }

                    lock (System.Threading.Thread.CurrentThread) { }

                    lock (typeof(foo)) { }

                    System.Reflection.MemberInfo mi = null;
                    lock (mi) { }

                    System.Reflection.ConstructorInfo ci = null;
                    lock (ci) { }

                    System.Reflection.ParameterInfo pi = null;
                    lock (pi) { }

                    int[] values = { 1, 2, 3 };
                    lock (values) { }

                    System.Reflection.MemberInfo[] values1 = null;
                    lock (values1) { }

                    lock (this) { }
                }
            }
            ",
            GetCA2002CSharpResultAt(8, 27, "string"),
            GetCA2002CSharpResultAt(9, 27, "string"),
            GetCA2002CSharpResultAt(12, 27, "System.OutOfMemoryException"),
            GetCA2002CSharpResultAt(14, 27, "System.StackOverflowException"),
            GetCA2002CSharpResultAt(16, 27, "System.ExecutionEngineException"),
            GetCA2002CSharpResultAt(18, 27, "System.Threading.Thread"),
            GetCA2002CSharpResultAt(20, 27, "System.Type"),
            GetCA2002CSharpResultAt(23, 27, "System.Reflection.MemberInfo"),
            GetCA2002CSharpResultAt(26, 27, "System.Reflection.ConstructorInfo"),
            GetCA2002CSharpResultAt(29, 27, "System.Reflection.ParameterInfo"),
            GetCA2002CSharpResultAt(32, 27, "int[]"),
            GetCA2002CSharpResultAt(37, 27, "this"));

            VerifyBasic(@"
            Imports System
            Public Class foo
                Public Sub Test()
                    Dim s1 As String = """"
                    SyncLock s1
                    End SyncLock
                    SyncLock (""Hello"")
                    End SyncLock

                    Dim o1 = New OutOfMemoryException()
                    SyncLock o1
                    End SyncLock
                    Dim o2 = New StackOverflowException()
                    SyncLock o2
                    End SyncLock
                    Dim o3 = New ExecutionEngineException()
                    SyncLock o3
                    End SyncLock

                    SyncLock System.Threading.Thread.CurrentThread
                    End SyncLock

                    SyncLock GetType(foo)
                    End SyncLock

                    Dim mi As System.Reflection.MemberInfo = Nothing
                    SyncLock mi
                    End SyncLock

                    Dim ci As System.Reflection.ConstructorInfo = Nothing
                    SyncLock ci
                    End SyncLock

                    Dim pi As System.Reflection.ParameterInfo = Nothing
                    SyncLock pi
                    End SyncLock

                    Dim values As Integer() = { 1, 2, 3}
                    SyncLock values
                    End SyncLock

                    Dim values1 As System.Reflection.MemberInfo() = Nothing
                    SyncLock values1
                    End SyncLock

                    SyncLock Me
                    End SyncLock
                End Sub
            End Class",
            GetCA2002BasicResultAt(6, 30, "String"),
            GetCA2002BasicResultAt(8, 30, "String"),
            GetCA2002BasicResultAt(12, 30, "System.OutOfMemoryException"),
            GetCA2002BasicResultAt(15, 30, "System.StackOverflowException"),
            GetCA2002BasicResultAt(18, 30, "System.ExecutionEngineException"),
            GetCA2002BasicResultAt(21, 30, "System.Threading.Thread"),
            GetCA2002BasicResultAt(24, 30, "System.Type"),
            GetCA2002BasicResultAt(28, 30, "System.Reflection.MemberInfo"),
            GetCA2002BasicResultAt(32, 30, "System.Reflection.ConstructorInfo"),
            GetCA2002BasicResultAt(36, 30, "System.Reflection.ParameterInfo"),
            GetCA2002BasicResultAt(40, 30, "Integer()"),
            GetCA2002BasicResultAt(47, 30, "Me"));
        }

        [Fact]
        public void CA2002TestLockOnWeakIdentitiesWithScope()
        {
            VerifyCSharp(@"
            using System;
            public class foo
            {
                public void Test()
                {
                    string s1 = """";
                    lock (s1) { }
                    lock (""Hello"") { }

                    [|var o1 = new OutOfMemoryException();
                    lock (o1) { }
                    var o2 = new StackOverflowException();
                    lock (o2) { }
                    var o3 = new ExecutionEngineException();
                    lock (o3) { }|]

                    lock (System.Threading.Thread.CurrentThread) { }

                    lock (typeof(foo)) { }

                    System.Reflection.MemberInfo mi = null;
                    lock (mi) { }

                    System.Reflection.ConstructorInfo ci = null;
                    lock (ci) { }

                    System.Reflection.ParameterInfo pi = null;
                    lock (pi) { }

                    int[] values = { 1, 2, 3 };
                    lock (values) { }

                    System.Reflection.MemberInfo[] values1 = null;
                    lock (values1) { }
                }
            }
            ",
            GetCA2002CSharpResultAt(12, 27, "System.OutOfMemoryException"),
            GetCA2002CSharpResultAt(14, 27, "System.StackOverflowException"),
            GetCA2002CSharpResultAt(16, 27, "System.ExecutionEngineException"));

            VerifyBasic(@"
            Imports System
            Public Class foo
                Public Sub Test()
                    Dim s1 As String = """"
                    SyncLock s1
                    End SyncLock
                    SyncLock (""Hello"")
                    End SyncLock

                    [|Dim o1 = New OutOfMemoryException()
                    SyncLock o1
                    End SyncLock
                    Dim o2 = New StackOverflowException()
                    SyncLock o2
                    End SyncLock
                    Dim o3 = New ExecutionEngineException()
                    SyncLock o3
                    End SyncLock|]

                    SyncLock System.Threading.Thread.CurrentThread
                    End SyncLock

                    SyncLock GetType (foo)
                    End SyncLock

                    Dim mi As System.Reflection.MemberInfo = Nothing
                    SyncLock mi
                    End SyncLock

                    Dim ci As System.Reflection.ConstructorInfo = Nothing
                    SyncLock ci
                    End SyncLock

                    Dim pi As System.Reflection.ParameterInfo = Nothing
                    SyncLock pi
                    End SyncLock

                    Dim values As Integer() = { 1, 2, 3}
                    SyncLock values
                    End SyncLock

                    Dim values1 As System.Reflection.MemberInfo() = Nothing
                    SyncLock values1
                    End SyncLock
                End Sub
            End Class",
            GetCA2002BasicResultAt(12, 30, "System.OutOfMemoryException"),
            GetCA2002BasicResultAt(15, 30, "System.StackOverflowException"),
            GetCA2002BasicResultAt(18, 30, "System.ExecutionEngineException"));
        }

        [Fact]
        [WorkItem(2744, "https://github.com/dotnet/roslyn-analyzers/issues/2744")]
        public async Task CA2002_MonitorEnter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading;

public class C
{
    public void Foo()
    {
        Monitor.Enter(this);
        Monitor.Enter(""test1"");

        bool b = true;
        Monitor.Enter(this, ref b);
        Monitor.Enter(""test1"", ref b);
    }
}",
                GetCA2002CSharpResultAt(8, 23, "C"),
                GetCA2002CSharpResultAt(9, 23, "C"),
                GetCA2002CSharpResultAt(12, 23, "C"),
                GetCA2002CSharpResultAt(13, 23, "C"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading

Public Class C
    Public Sub Foo()
        Monitor.Enter(Me)
        Monitor.Enter(""test1"")

        Dim b As Boolean = True
        Monitor.Enter(Me, b)
        Monitor.Enter(""test1"", b)
    End Sub
End Class
",
                GetCA2002BasicResultAt(6, 23, "C"),
                GetCA2002BasicResultAt(7, 23, "C"),
                GetCA2002BasicResultAt(10, 23, "C"),
                GetCA2002BasicResultAt(11, 23, "C"));
        }

        [Fact]
        [WorkItem(2744, "https://github.com/dotnet/roslyn-analyzers/issues/2744")]
        public async Task CA2002_MonitorTryEnter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;

public class C
{
    public void Foo()
    {
        Monitor.TryEnter(this);
        Monitor.TryEnter(""test1"");

        Monitor.TryEnter(this, 42);
        Monitor.TryEnter(""test1"", 42);

        Monitor.TryEnter(this, TimeSpan.FromMilliseconds(42));
        Monitor.TryEnter(""test1"", TimeSpan.FromMilliseconds(42));

        bool b = true;
        Monitor.TryEnter(this, ref b);
        Monitor.TryEnter(""test1"", ref b);

        Monitor.TryEnter(this, 42, ref b);
        Monitor.TryEnter(""test1"", 42, ref b);
    }
}",
                GetCA2002CSharpResultAt(9, 26, "C"),
                GetCA2002CSharpResultAt(10, 26, "C"),
                GetCA2002CSharpResultAt(12, 26, "C"),
                GetCA2002CSharpResultAt(13, 26, "C"),
                GetCA2002CSharpResultAt(15, 26, "C"),
                GetCA2002CSharpResultAt(16, 26, "C"),
                GetCA2002CSharpResultAt(19, 26, "C"),
                GetCA2002CSharpResultAt(20, 26, "C"),
                GetCA2002CSharpResultAt(22, 26, "C"),
                GetCA2002CSharpResultAt(23, 26, "C"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading

Public Class C
    Public Sub Foo()
        Monitor.TryEnter(Me)
        Monitor.TryEnter(""test1"")

        Monitor.TryEnter(Me, 42)
        Monitor.TryEnter(""test1"", 42)

        Monitor.TryEnter(Me, TimeSpan.FromMilliseconds(42))
        Monitor.TryEnter(""test1"", TimeSpan.FromMilliseconds(42))

        Dim b As Boolean = True
        Monitor.TryEnter(Me, b)
        Monitor.TryEnter(""test1"", b)

        Monitor.TryEnter(Me, 42, b)
        Monitor.TryEnter(""test1"", 42, b)
    End Sub
End Class
",
                GetCA2002BasicResultAt(7, 26, "C"),
                GetCA2002BasicResultAt(8, 26, "C"),
                GetCA2002BasicResultAt(10, 26, "C"),
                GetCA2002BasicResultAt(11, 26, "C"),
                GetCA2002BasicResultAt(13, 26, "C"),
                GetCA2002BasicResultAt(14, 26, "C"),
                GetCA2002BasicResultAt(17, 26, "C"),
                GetCA2002BasicResultAt(18, 26, "C"),
                GetCA2002BasicResultAt(20, 26, "C"),
                GetCA2002BasicResultAt(21, 26, "C"));
        }

        private const string CA2002RuleName = "CA2002";

        private DiagnosticResult GetCA2002CSharpResultAt(int line, int column, string typeName)
        {
            return GetCSharpResultAt(line, column, CA2002RuleName, string.Format(CultureInfo.CurrentCulture, MicrosoftNetCoreAnalyzersResources.DoNotLockOnObjectsWithWeakIdentityMessage, typeName));
        }

        private DiagnosticResult GetCA2002BasicResultAt(int line, int column, string typeName)
        {
            return GetBasicResultAt(line, column, CA2002RuleName, string.Format(CultureInfo.CurrentCulture, MicrosoftNetCoreAnalyzersResources.DoNotLockOnObjectsWithWeakIdentityMessage, typeName));
        }
    }
}
