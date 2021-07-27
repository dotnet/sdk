// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentProcessId,
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentProcessIdFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentProcessId,
    Microsoft.NetCore.Analyzers.Runtime.UseEnvironmentProcessIdFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseEnvironmentProcessIdTests
    {
        [Fact]
        public async Task NoDiagnostics_NoEnvironmentProcessId_CSharp()
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
        public async Task NoDiagnostics_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics;

namespace System
{
    public static class Environment
    {
        public static int ProcessId => 0;
    }
}

class C
{
    void M()
    {
        int id = C.GetCurrentProcess().Id;

        string name = Process.GetCurrentProcess().ProcessName;

        using (var p = Process.GetCurrentProcess())
            _ = p.Id;
    }

    private static C GetCurrentProcess() => new C();
    public int Id => 0;
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(
@"
using System;
using System.Diagnostics;

namespace System
{
    public static class Environment
    {
        public static int ProcessId => 0;
    }
}

class C
{
    int M()
    {
        int pid = [|Process.GetCurrentProcess().Id|];
        pid = [|Process.GetCurrentProcess()/*willberemoved*/.Id|];
        Use([|Process.GetCurrentProcess().Id|]);
        Use(""test"",
            [|Process.GetCurrentProcess().Id|]);
        Use(""test"",
            [|Process.GetCurrentProcess().Id|] /* comment */,
            0.0);
        return [|Process.GetCurrentProcess().Id|];
    }

    void Use(int pid) {}
    void Use(string something, int pid) {}
    void Use(string something, int pid, double somethingElse) { }
}
",
@"
using System;
using System.Diagnostics;

namespace System
{
    public static class Environment
    {
        public static int ProcessId => 0;
    }
}

class C
{
    int M()
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

    void Use(int pid) {}
    void Use(string something, int pid) {}
    void Use(string something, int pid, double somethingElse) { }
}
");
        }

        [Fact]
        public async Task Diagnostics_FixApplies_VB()
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
        Dim pid As Integer = [|Process.GetCurrentProcess().Id|]
        pid = [|Process.GetCurrentProcess().Id|]
        Use([|Process.GetCurrentProcess().Id|])
        Use("", test, "", [|Process.GetCurrentProcess().Id|])
        Use("", test, "", [|Process.GetCurrentProcess().Id|], 0.0)
        Return [|Process.GetCurrentProcess().Id|]
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