// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamReadAsyncMemoryOverloadsTest : PreferStreamAsyncMemoryOverloadsTestBase
    {
        #region C# - No diagnostic - Analyzer

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_Read()
        {
            return AnalyzeCSAsync(@"
using System.IO;
class C
{
    public void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            fs.Read(buffer, 0, (int)fs.Length);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_ReadAsync_ByteMemory()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            byte[] buffer = new byte[fs.Length];
            Memory<byte> memory = new Memory<byte>(buffer);
            await fs.ReadAsync(memory, new CancellationToken()).ConfigureAwait(false);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_ReadAsync_AsMemory()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer.AsMemory(), new CancellationToken());
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_NoAwait_SaveAsTask()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            Task t = fs.ReadAsync(buffer, 0, (int)fs.Length);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_FileStream_NoAwait_ReturnMethod()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public Task M(FileStream fs, byte[] buffer)
    {
        return fs.ReadAsync(buffer, 0, (int)fs.Length);
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_Stream_NoAwait_ReturnMethod()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public void M(Stream s, byte[] buffer)
    {
        s.ReadAsync(buffer, 0, 1);
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_NoAwait_ExpressionBodyMethod()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public Task M(FileStream fs, byte[] buffer) => fs.ReadAsync(buffer, 0, (int)fs.Length);
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_ContinueWith_ConfigureAwait()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length).ContinueWith(c => {}).ConfigureAwait(false);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_ContinueWith_ContinueWith_ConfigureAwait()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length).ContinueWith(c => {}).ContinueWith(c => {}).ConfigureAwait(false);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_UnsupportedVersion()
        {
            return AnalyzeCSUnsupportedAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length);
        }
    }
}
            ");
        }

        #endregion

        #region VB - No diagnostic - Analyzer

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_Read()
        {
            return AnalyzeVBAsync(@"
Imports System.IO
Class C
    Public Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) { }
            fs.Read(buffer, 0, fs.Length)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_ReadAsync_ByteMemory()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = New FileStream("", path.txt, "", FileMode.Create)
            Dim buffer As Byte() = New Byte(fs.Length - 1) {}
            Dim memory As Memory(Of Byte) = New Memory(Of Byte)(buffer)
            Await fs.ReadAsync(memory, New CancellationToken()).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_ReadAsync_AsMemory()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) { }
            Await fs.ReadAsync(buffer.AsMemory(), New CancellationToken())
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_NoAwait_SaveAsTask()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) { }
            Dim t As Task = fs.ReadAsync(buffer, 0, CInt(fs.Length))
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_FileStream_NoAwait_ReturnMethod()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Friend Class C
    Public Function M(ByVal fs As FileStream, ByVal buffer As Byte()) As Task
        Return fs.ReadAsync(buffer, 0, fs.Length)
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_Stream_NoAwait_ReturnMethod()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Sub M(ByVal s As Stream, ByVal buffer As Byte())
        s.ReadAsync(buffer, 0, 1)
    End Sub
End Class
            ");
        }

        // The method VB_Analyzer_NoDiagnostic_NoAwait_ExpressionBodyMethod()
        // is skipped because VB does not support expression bodies for methods

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_ContinueWith_ConfigureAwait()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) {}
            Await fs.ReadAsync(buffer, 0, fs.Length).ContinueWith(Sub(c)
                                                                        End Sub).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_ContinueWith_ContinueWith_ConfigureAwait()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) {}
            Await fs.ReadAsync(buffer, 0, fs.Length).ContinueWith(Sub(c)
                                                                        End Sub).ContinueWith(Sub(c)
                                                                                              End Sub).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_UnsupportedVersion()
        {
            return AnalyzeVBUnsupportedAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) { }
            Await fs.ReadAsync(buffer, 0, fs.Length)
        End Using
    End Sub
End Class
            ");
        }

        #endregion

        #region C# - Diagnostic - Analyzer

        [Fact]
        public Task CS_Analyzer_Diagnostic_ByteArray()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length);
        }
    }
}
            ", GetCSResult(12, 19, 12, 58));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_Inline()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            await fs.ReadAsync(new byte[fs.Length], 0, (int)fs.Length);
        }
    }
}
            ", GetCSResult(11, 19, 11, 71));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_CancellationToken()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length, new CancellationToken());
        }
    }
}
            ", GetCSResult(12, 19, 12, 83));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_ConfigureAwait()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length).ConfigureAwait(false);
        }
    }
}
            ", GetCSResult(12, 19, 12, 58));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_CancellationToken_ConfigureAwait()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length, new CancellationToken()).ConfigureAwait(false);
        }
    }
}
            ", GetCSResult(12, 19, 12, 83));
        }

        #endregion

        #region VB - Diagnostic - Analyzer

        [Fact]
        public Task VB_Analyzer_Diagnostic_ByteArray()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) {}
            Await fs.ReadAsync(buffer, 0, fs.Length)
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 53));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_Inline()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Await fs.ReadAsync(New Byte(fs.Length - 1) {}, 0, fs.Length)
        End Using
    End Sub
End Class
            ", GetVBResult(8, 19, 8, 73));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_CancellationToken()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) {}
            Await fs.ReadAsync(buffer, 0, fs.Length, New CancellationToken())
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 78));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_ConfigureAwait()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) {}
            Await fs.ReadAsync(buffer, 0, fs.Length).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 53));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_CancellationToken_ConfigureAwait()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = File.Open("", file.txt, "", FileMode.Open)
            Dim buffer As Byte() = New Byte(fs.Length - 1) {}
            Await fs.ReadAsync(buffer, 0, CInt(fs.Length), New CancellationToken()).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 84));
        }

        #endregion

        #region Helpers

        protected static DiagnosticResult GetCSResult(int startLine, int startColumn, int endLine, int endColumn)
            => GetCSResultForRule(startLine, startColumn, endLine, endColumn,
                PreferStreamAsyncMemoryOverloads.PreferStreamReadAsyncMemoryOverloadsRule,
                "ReadAsync", "System.IO.Stream.ReadAsync(System.Memory<byte>, System.Threading.CancellationToken)");

        protected static DiagnosticResult GetVBResult(int startLine, int startColumn, int endLine, int endColumn)
            => GetVBResultForRule(startLine, startColumn, endLine, endColumn,
                PreferStreamAsyncMemoryOverloads.PreferStreamReadAsyncMemoryOverloadsRule,
                "ReadAsync", "System.IO.Stream.ReadAsync(System.Memory(Of Byte), System.Threading.CancellationToken)");

        #endregion
    }
}