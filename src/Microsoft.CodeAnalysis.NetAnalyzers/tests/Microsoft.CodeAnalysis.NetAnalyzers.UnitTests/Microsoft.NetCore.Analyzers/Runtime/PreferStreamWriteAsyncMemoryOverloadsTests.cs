// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamWriteAsyncMemoryOverloadsTest : PreferStreamAsyncMemoryOverloadsTestBase
    {
        #region C# - No diagnostic - Analyzer

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_Write()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            fs.Write(buffer, 0, buffer.Length);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_WriteAsync_ByteMemory()
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
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            Memory<byte> memory = new Memory<byte>(buffer);
            await fs.WriteAsync(memory, new CancellationToken()).ConfigureAwait(false);
       }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_WriteAsync_AsMemory()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer.AsMemory(), new CancellationToken()).ConfigureAwait(false);
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            Task t = fs.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_NoAwait_ReturnMethod()
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
        return fs.WriteAsync(buffer, 0, buffer.Length);
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
    public Task M(FileStream fs, byte[] buffer) => fs.WriteAsync(buffer, 0, buffer.Length);
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer, 0, buffer.Length).ContinueWith(c => {}).ConfigureAwait(false);
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer, 0, buffer.Length).ContinueWith(c => {}).ContinueWith(c => {}).ConfigureAwait(false);
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
            ");
        }

        #endregion

        #region VB - No diagnostic - Analyzer

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_Write()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Private Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""path.txt"", FileMode.Create)
            fs.Write(buffer, 0, buffer.Length)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_WriteAsync_AsMemory()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer.AsMemory(), New CancellationToken()).ConfigureAwait(False)
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
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Dim t As Task = fs.WriteAsync(buffer, 0, buffer.Length)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_NoAwait_ReturnMethod()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Function M(ByVal fs As FileStream, ByVal buffer As Byte()) As Task
        Return fs.WriteAsync(buffer, 0, buffer.Length)
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_NoAwait_ExpressionBodyMethod()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Function M(ByVal fs As FileStream, ByVal buffer As Byte()) As Task
        Return fs.WriteAsync(buffer, 0, buffer.Length)
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_ContinueWith_ConfigureAwait()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer, 0, buffer.Length).ContinueWith(Sub(c)
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
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer, 0, buffer.Length).ContinueWith(Sub(c)
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
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer, 0, buffer.Length)
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
            ", GetCSResult(12, 19, 12, 58));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_AsStream()
        {
            return AnalyzeCSAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (Stream s = new FileStream(""path.txt"", FileMode.Create))
        {
            await s.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
            ", GetCSResult(12, 19, 12, 57));
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer, 0, buffer.Length, new CancellationToken());
        }
    }
}
            ", GetCSResult(12, 19, 12, 83));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_InlineBuffer()
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
            await fs.WriteAsync(new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }, 0, 8);
        }
    }
}
            ", GetCSResult(11, 19, 11, 100));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_InlineBuffer_CancellationToken()
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
            await fs.WriteAsync(new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }, 0, 8, new CancellationToken());
        }
    }
}
            ", GetCSResult(11, 19, 11, 125));
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
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
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream fs = new FileStream(""path.txt"", FileMode.Create))
        {
            await fs.WriteAsync(buffer, 0, buffer.Length, new CancellationToken()).ConfigureAwait(false);
        }
    }
}
            ", GetCSResult(12, 19, 12, 83));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_InlineBuffer_ConfigureAwait()
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
            await fs.WriteAsync(new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }, 0, 8).ConfigureAwait(false);
        }
    }
}
            ", GetCSResult(11, 19, 11, 100));
        }

        [Fact]
        public Task CS_Analyzer_Diagnostic_InlineBuffer_CancellationToken_ConfigureAwait()
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
            await fs.WriteAsync(new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }, 0, 8, new CancellationToken()).ConfigureAwait(false);
        }
    }
}
            ", GetCSResult(11, 19, 11, 125));
        }

        #endregion

        #region VB - Diagnostic - Analyzer

        [Fact]
        public Task VB_Analyzer_Diagnostic_Basic()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer, 0, buffer.Length)
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 58));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_AsStream()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As Stream = New FileStream(""file.txt"", FileMode.Create)
            Await s.WriteAsync(buffer, 0, buffer.Length)
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 57));
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
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer, 0, buffer.Length, New CancellationToken())
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 83));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_InlineBuffer()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}, 0, 8)
        End Using
    End Sub
End Class
            ", GetVBResult(8, 19, 8, 99));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_InlineBuffer_CancellationToken()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}, 0, 8, New CancellationToken())
        End Using
    End Sub
End Class
            ", GetVBResult(8, 19, 8, 124));
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
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 58));
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
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(buffer, 0, buffer.Length, New CancellationToken()).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ", GetVBResult(9, 19, 9, 83));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_InlineBuffer_ConfigureAwait()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}, 0, 8).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ", GetVBResult(8, 19, 8, 99));
        }

        [Fact]
        public Task VB_Analyzer_Diagnostic_InlineBuffer_CancellationToken_ConfigureAwait()
        {
            return AnalyzeVBAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using fs As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await fs.WriteAsync(New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}, 0, 8, New CancellationToken()).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ", GetVBResult(8, 19, 8, 124));
        }

        #endregion

        #region Helpers

        protected static DiagnosticResult GetCSResult(int startLine, int startColumn, int endLine, int endColumn)
            => GetCSResultForRule(startLine, startColumn, endLine, endColumn, PreferStreamAsyncMemoryOverloads.PreferStreamWriteAsyncMemoryOverloadsRule);

        protected static DiagnosticResult GetVBResult(int startLine, int startColumn, int endLine, int endColumn)
            => GetVBResultForRule(startLine, startColumn, endLine, endColumn, PreferStreamAsyncMemoryOverloads.PreferStreamWriteAsyncMemoryOverloadsRule);

        #endregion
    }
}