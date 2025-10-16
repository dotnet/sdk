// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentMembers,
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentMembersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentMembers,
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentMembersFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseEnvironmentMembersTests
    {
        [Fact]
        public async Task NoDiagnostics_NoEnvironmentProcessId_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

class C
{
    void M()
    {
        int id = Process.GetCurrentProcess().Id; // assumes Environment.ProcessId doesn't exist
    }
}
");
        }

        [Fact]
        public async Task NoDiagnostics_NoEnvironmentProcessPath_CSharpAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

class C
{
    void M()
    {
        string path = Process.GetCurrentProcess().MainModule.FileName; // assumes Environment.ProcessPath doesn't exist
    }
}
");
        }

        [Fact]
        public async Task NoDiagnostics_CSharpAsync()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode = @"
using System.Diagnostics;
using System.Threading;

namespace System
{
    public static class Environment
    {
        public static int ProcessId => 0;
        public static string ProcessPath => """";
        public static int CurrentManagedThreadId => 0;
    }
}

class C
{
    void M()
    {
        int id = C.GetCurrentProcess().Id;
        if (Process.GetCurrentProcess() is { Id: 42 })
        {
            id = 42;
        }

        string filename = C.GetCurrentProcess().MainModule.FileName;

        string name = Process.GetCurrentProcess().ProcessName;

        using (var p = Process.GetCurrentProcess())
            _ = p.Id;

        var module = Process.GetCurrentProcess().MainModule;
        filename = module.FileName;

        var thread = Thread.CurrentThread;
        id = thread.ManagedThreadId;
    }

    private static C GetCurrentProcess() => new C();
    public int Id => 0;
    public ProcessModule MainModule => null;
}
"
            }.RunAsync();
        }

        [Fact]
        public async Task Diagnostics_ProcessId_FixApplies_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"
using System;
using System.Diagnostics;
using System.Threading;

namespace System
{
    public static class Environment
    {
        public static int ProcessId => 0;
    }
}

class C
{
    int HandleProcessId()
    {
        int pid = {|CA1837:Process.GetCurrentProcess().Id|};
        pid = {|CA1837:Process.GetCurrentProcess()/*willberemoved*/.Id|};
        Use({|CA1837:Process.GetCurrentProcess().Id|});
        Use(""test"",
            {|CA1837:Process.GetCurrentProcess().Id|});
        Use(""test"",
            {|CA1837:Process.GetCurrentProcess().Id|} /* comment */,
            0.0);
        return {|CA1837:Process.GetCurrentProcess().Id|};
    }

    void Use(int id) {}
    void Use(string something, int id) {}
    void Use(string something, int id, double somethingElse) { }
}
",
@"
using System;
using System.Diagnostics;
using System.Threading;

namespace System
{
    public static class Environment
    {
        public static int ProcessId => 0;
    }
}

class C
{
    int HandleProcessId()
    {
        int pid = Environment.ProcessId;
        pid = Environment.ProcessId;
        Use(Environment.ProcessId);
        Use(""test"",
            Environment.ProcessId);
        Use(""test"",
            Environment.ProcessId /* comment */,
            0.0);
        return Environment.ProcessId;
    }

    void Use(int id) {}
    void Use(string something, int id) {}
    void Use(string something, int id, double somethingElse) { }
}
");
        }

        [Fact]
        public async Task Diagnostics_ProcessPath_FixApplies_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"
using System;
using System.Diagnostics;
using System.Threading;

namespace System
{
    public static class Environment
    {
        public static string ProcessPath => """";
    }
}

class C
{
    string HandleProcessPath()
    {
        string path = {|CA1839:Process.GetCurrentProcess().MainModule.FileName|};
        path = {|CA1839:Process.GetCurrentProcess().MainModule/*willberemoved*/.FileName|};
        Use({|CA1839:Process.GetCurrentProcess().MainModule.FileName|});
        Use(""test"",
            {|CA1839:Process.GetCurrentProcess().MainModule.FileName|});
        Use(""test"",
            {|CA1839:Process.GetCurrentProcess().MainModule.FileName|} /* comment */,
            0.0);
        return {|CA1839:Process.GetCurrentProcess().MainModule.FileName|};
    }

    void Use(string path) {}
    void Use(string something, string path) {}
    void Use(string something, string path, double somethingElse) {}
}
",
@"
using System;
using System.Diagnostics;
using System.Threading;

namespace System
{
    public static class Environment
    {
        public static string ProcessPath => """";
    }
}

class C
{
    string HandleProcessPath()
    {
        string path = Environment.ProcessPath;
        path = Environment.ProcessPath;
        Use(Environment.ProcessPath);
        Use(""test"",
            Environment.ProcessPath);
        Use(""test"",
            Environment.ProcessPath /* comment */,
            0.0);
        return Environment.ProcessPath;
    }

    void Use(string path) {}
    void Use(string something, string path) {}
    void Use(string something, string path, double somethingElse) {}
}
");
        }

        [Fact]
        public async Task Diagnostics_CurrentManagedThreadId_FixApplies_CSharpAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"
using System;
using System.Diagnostics;
using System.Threading;

namespace System
{
    public static class Environment
    {
        public static int CurrentManagedThreadId => 0;
    }
}

class C
{
    int HandleCurrentManagedThreadId()
    {
        int id = {|CA1840:Thread.CurrentThread.ManagedThreadId|};
        id = {|CA1840:Thread.CurrentThread/*willberemoved*/.ManagedThreadId|};
        Use({|CA1840:Thread.CurrentThread.ManagedThreadId|});
        Use(""test"",
            {|CA1840:Thread.CurrentThread.ManagedThreadId|});
        Use(""test"",
            {|CA1840:Thread.CurrentThread.ManagedThreadId|} /* comment */,
            0.0);
        return {|CA1840:Thread.CurrentThread.ManagedThreadId|};
    }

    void Use(int id) {}
    void Use(string something, int id) {}
    void Use(string something, int id, double somethingElse) { }
}
",
@"
using System;
using System.Diagnostics;
using System.Threading;

namespace System
{
    public static class Environment
    {
        public static int CurrentManagedThreadId => 0;
    }
}

class C
{
    int HandleCurrentManagedThreadId()
    {
        int id = Environment.CurrentManagedThreadId;
        id = Environment.CurrentManagedThreadId;
        Use(Environment.CurrentManagedThreadId);
        Use(""test"",
            Environment.CurrentManagedThreadId);
        Use(""test"",
            Environment.CurrentManagedThreadId /* comment */,
            0.0);
        return Environment.CurrentManagedThreadId;
    }

    void Use(int id) {}
    void Use(string something, int id) {}
    void Use(string something, int id, double somethingElse) { }
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_VBAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(
@"
Imports System
Imports System.Diagnostics

Namespace System
    Class Environment
        Public Shared ReadOnly Property ProcessId As Integer
            Get
                Return 0
            End Get
        End Property
    End Class
End Namespace

Class C
    Private Function M() As Integer
        Dim pid As Integer = {|CA1837:Process.GetCurrentProcess().Id|}
        pid = {|CA1837:Process.GetCurrentProcess().Id|}
        Use({|CA1837:Process.GetCurrentProcess().Id|})
        Use("", test, "", {|CA1837:Process.GetCurrentProcess().Id|})
        Use("", test, "", {|CA1837:Process.GetCurrentProcess().Id|}, 0.0)
        Return {|CA1837:Process.GetCurrentProcess().Id|}
    End Function

    Private Sub Use(ByVal pid As Integer)
    End Sub

    Private Sub Use(ByVal something As String, ByVal pid As Integer)
    End Sub

    Private Sub Use(ByVal something As String, ByVal pid As Integer, ByVal somethingElse As Double)
    End Sub
End Class
",
@"
Imports System
Imports System.Diagnostics

Namespace System
    Class Environment
        Public Shared ReadOnly Property ProcessId As Integer
            Get
                Return 0
            End Get
        End Property
    End Class
End Namespace

Class C
    Private Function M() As Integer
        Dim pid As Integer = Environment.ProcessId
        pid = Environment.ProcessId
        Use(Environment.ProcessId)
        Use("", test, "", Environment.ProcessId)
        Use("", test, "", Environment.ProcessId, 0.0)
        Return Environment.ProcessId
    End Function

    Private Sub Use(ByVal pid As Integer)
    End Sub

    Private Sub Use(ByVal something As String, ByVal pid As Integer)
    End Sub

    Private Sub Use(ByVal something As String, ByVal pid As Integer, ByVal somethingElse As Double)
    End Sub
End Class
");
        }
    }
}