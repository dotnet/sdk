// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

#pragma warning disable CA1305 // Specify IFormatProvider in string.Format

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamWriteAsyncMemoryOverloadsTest : PreferStreamAsyncMemoryOverloadsTestBase
    {
        #region C# - No diagnostic

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_WriteAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            s.Write(buffer, 0, buffer.Length);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_WriteAsync_ByteMemoryAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            Memory<byte> memory = new Memory<byte>(buffer);
            await s.WriteAsync(memory, new CancellationToken()).ConfigureAwait(false);
       }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_WriteAsync_AsMemoryAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            await s.WriteAsync(buffer.AsMemory(), new CancellationToken()).ConfigureAwait(false);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_NoAwait_SaveAsTaskAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            Task t = s.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_FileStream_NoAwait_ReturnMethodAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public Task M(FileStream s, byte[] buffer)
    {
        return s.WriteAsync(buffer, 0, buffer.Length);
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_Stream_NoAwait_VoidMethodAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public void M(Stream s, byte[] buffer)
    {
        s.WriteAsync(buffer, 0, 1);
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_Stream_NoAwait_VoidMethod_InvokeGetBufferMethodAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public byte[] GetBuffer()
    {
        return new byte[] { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
    }
    public void M(Stream s)
    {
        s.WriteAsync(GetBuffer(), 0, 1);
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_NoAwait_ExpressionBodyMethodAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public Task M(FileStream s, byte[] buffer) => s.WriteAsync(buffer, 0, buffer.Length);
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_ContinueWith_ConfigureAwaitAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            await s.WriteAsync(buffer, 0, buffer.Length).ContinueWith(c => {}).ConfigureAwait(false);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_ContinueWith_ContinueWith_ConfigureAwaitAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            await s.WriteAsync(buffer, 0, buffer.Length).ContinueWith(c => {}).ContinueWith(c => {}).ConfigureAwait(false);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_AutoCastedToReadOnlyMemoryAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            await s.WriteAsync(buffer);
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_AutoCastedToReadOnlyMemory_CancellationTokenAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            await s.WriteAsync(buffer, new CancellationToken());
        }
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_AwaitInvocationOutsideStreamInvocationAsync()
        {
            return CSharpVerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
class C
{
    public async void M()
    {
        using (FileStream s = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            await ProcessTask(s.WriteAsync(buffer, 0, buffer.Length, new CancellationToken())).ConfigureAwait(false);
        }
    }

    private async Task ProcessTask(Task writeAsyncTask)
    {
        await writeAsyncTask.ConfigureAwait(false);
    }
}
            ");
        }

        [Fact]
        public Task CS_Analyzer_NoDiagnostic_UnsupportedVersionAsync()
        {
            return CSharpVerifyAnalyzerForUnsupportedVersionAsync(@"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
        using (FileStream s = new FileStream(""path.txt"", FileMode.Create))
        {
            await s.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
            ");
        }

        #endregion

        #region VB - No diagnostic

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_WriteAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Private Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""path.txt"", FileMode.Create)
            s.Write(buffer, 0, buffer.Length)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_WriteAsync_AsMemoryAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Await s.WriteAsync(buffer.AsMemory(), New CancellationToken()).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_NoAwait_SaveAsTaskAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Dim t As Task = s.WriteAsync(buffer, 0, buffer.Length)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_FileStream_NoAwait_ReturnMethodAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Function M(ByVal s As FileStream, ByVal buffer As Byte()) As Task
        Return s.WriteAsync(buffer, 0, buffer.Length)
    End Function
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_Stream_NoAwait_VoidMethodAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Sub M(ByVal s As Stream, ByVal buffer As Byte())
        s.WriteAsync(buffer, 0, 1)
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_Stream_NoAwait_VoidMethod_InvokeGetBufferMethodAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Class C
    Public Function GetBuffer() As Byte()
        Return New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
    End Function
    Public Sub M(ByVal s As Stream)
        s.WriteAsync(GetBuffer(), 0, 1)
    End Sub
End Class
            ");
        }

        // The method VB_Analyzer_NoDiagnostic_NoAwait_ExpressionBodyMethod() is
        // skipped because VB does not support expression bodies for methods.

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_ContinueWith_ConfigureAwaitAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await s.WriteAsync(buffer, 0, buffer.Length).ContinueWith(Sub(c)
                                                                       End Sub).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_ContinueWith_ContinueWith_ConfigureAwaitAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""file.txt"", FileMode.Create)
            Await s.WriteAsync(buffer, 0, buffer.Length).ContinueWith(Sub(c)
                                                                       End Sub).ContinueWith(Sub(c)
                                                                                             End Sub).ConfigureAwait(False)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_AutoCastedToReadOnlyMemoryAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading

Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Await s.WriteAsync(buffer)
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_AutoCastedToReadOnlyMemory_CancellationTokenAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading

Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Await s.WriteAsync(buffer, New CancellationToken())
        End Using
    End Sub
End Class
            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_AwaitInvocationOutsideStreamInvocationAsync()
        {
            return VisualBasicVerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""file.txt"", FileMode.Open)
            Dim buffer As Byte() = { &HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await ProcessTask(s.WriteAsync(buffer, 0, buffer.Length, New CancellationToken())).ConfigureAwait(False)
        End Using
    End Sub

    Private Async Function ProcessTask(ByVal writeAsyncTask As Task) As Task
        Await writeAsyncTask.ConfigureAwait(False)
    End Function
End Class

            ");
        }

        [Fact]
        public Task VB_Analyzer_NoDiagnostic_UnsupportedVersionAsync()
        {
            return VisualBasicVerifyAnalyzerForUnsupportedVersionAsync(@"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
        Using s As FileStream = New FileStream(""path.txt"", FileMode.Create)
            Await s.WriteAsync(buffer, 0, buffer.Length)
        End Using
    End Sub
End Class
            ");
        }

        #endregion

        #region C# - Diagnostic

        private const string _sourceCSharp = @"
using System;
using System.IO;
using System.Threading;
class C
{{
    public async void M()
    {{
        using ({0} s = new FileStream(""path.txt"", FileMode.Create))
        {{
            {1}
            await s.WriteAsync({2}){3};
        }}
    }}
}}
            ";

        public static IEnumerable<object[]> CSharpInlinedByteArrayTestData()
        {
            yield return new object[] { "new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }, 0, 8",
                                        "(new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }).AsMemory(0, 8)" };
            yield return new object[] { "new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }, 0, 8, new CancellationToken()",
                                        "(new byte[]{ 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 }).AsMemory(0, 8), new CancellationToken()" };
        }

        [Fact]
        public Task CS_Fixer_Diagnostic_EnsureSystemNamespaceAutoAddedAsync()
        {
            string originalCode = @"
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream s = File.Open(""path.txt"", FileMode.Open))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            await s.WriteAsync(buffer, 1, buffer.Length);
        }
    }
}";
            string fixedCode = @"
using System.IO;
using System.Threading;
using System;

class C
{
    public async void M()
    {
        using (FileStream s = File.Open(""path.txt"", FileMode.Open))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            await s.WriteAsync(buffer.AsMemory(1, buffer.Length));
        }
    }
}";
            return CSharpVerifyExpectedCodeFixDiagnosticsAsync(originalCode, fixedCode, GetCSharpResult(11, 19, 11, 57));
        }

        [Theory]
        [MemberData(nameof(CSharpUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenFullBufferTestData))]
        [MemberData(nameof(CSharpUnnamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenPartialBufferTestData))]
        public Task CS_Fixer_Diagnostic_ArgumentNamingAsync(string originalArgs, string fixedArgs) =>
            CSharpVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: true);

        [Theory]
        [MemberData(nameof(CSharpUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenFullBufferTestData))]
        [MemberData(nameof(CSharpUnnamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenPartialBufferTestData))]
        public Task CS_Fixer_Diagnostic_ArgumentNaming_WithConfigureAwaitAsync(string originalArgs, string fixedArgs) =>
            CSharpVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: false);

        [Theory]
        [MemberData(nameof(CSharpUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenFullBufferTestData))]
        [MemberData(nameof(CSharpUnnamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenPartialBufferTestData))]
        public Task CS_Fixer_Diagnostic_AsStreamAsync(string originalArgs, string fixedArgs) =>
            CSharpVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "Stream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: true);

        [Theory]
        [MemberData(nameof(CSharpUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenFullBufferTestData))]
        [MemberData(nameof(CSharpUnnamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(CSharpNamedArgumentsWithCancellationTokenPartialBufferTestData))]
        public Task CS_Fixer_Diagnostic_AsStream_WithConfigureAwaitAsync(string originalArgs, string fixedArgs) =>
            CSharpVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "Stream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: false);

        [Theory]
        [MemberData(nameof(CSharpInlinedByteArrayTestData))]
        public Task CS_Fixer_Diagnostic_InlineByteArrayAsync(string originalArgs, string fixedArgs)
            => CSharpVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: true, isEmptyConfigureAwait: true);

        [Theory]
        [MemberData(nameof(CSharpInlinedByteArrayTestData))]
        public Task CS_Fixer_Diagnostic_InlineByteArray_WithConfigureAwaitAsync(string originalArgs, string fixedArgs)
            => CSharpVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: true, isEmptyConfigureAwait: false);

        [Fact]
        public Task CS_Fixer_Diagnostic_WithTriviaAsync()
        {
            // Notes:
            // The invocation trivia is not part of the squiggle
            // If trivia is added before a method argument, it does not get saved as "leading trivia" for the argument, which is why this test only verifies argument trailing trivia
            // Trivia from await should remain untouched
            string originalSource = @"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream s = File.Open(""path.txt"", FileMode.Open))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            /* AwaitLeadingTrivia */ await /* InvocationLeadingTrivia */ s.WriteAsync(buffer /* BufferTrailingTrivia */, 1 /* OffsetTrailingTrivia */, buffer.Length /* CountTrailingTrivia */, new CancellationToken() /* CancellationTokenTrailingTrivia */) /* InvocationTrailingTrivia */; /* AwaitTrailingTrivia */
        }
    }
}
            ";

            string fixedSource = @"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream s = File.Open(""path.txt"", FileMode.Open))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            /* AwaitLeadingTrivia */ await /* InvocationLeadingTrivia */ s.WriteAsync(buffer.AsMemory(1 /* OffsetTrailingTrivia */, buffer.Length /* CountTrailingTrivia */) /* BufferTrailingTrivia */, new CancellationToken() /* CancellationTokenTrailingTrivia */) /* InvocationTrailingTrivia */; /* AwaitTrailingTrivia */
        }
    }
}
            ";

            return CSharpVerifyExpectedCodeFixDiagnosticsAsync(originalSource, fixedSource, GetCSharpResult(12, 74, 12, 255));
        }

        [Fact]
        public Task CS_Fixer_Diagnostic_WithTrivia_WithConfigureAwaitAsync()
        {
            // Notes:
            // The invocation trivia is not part of the squiggle
            // If trivia is added before a method argument, it does not get saved as "leading trivia" for the argument, which is why this test only verifies argument trailing trivia
            // Trivia from await and ConfigureAwait should remain untouched
            string originalSource = @"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream s = File.Open(""path.txt"", FileMode.Open))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            /* AwaitLeadingTrivia */ await /* InvocationLeadingTrivia */ s.WriteAsync(buffer /* BufferTrailingTrivia */, 1 /* OffsetTrailingTrivia */, buffer.Length /* CountTrailingTrivia */, new CancellationToken() /* CancellationTokenTrailingTrivia */).ConfigureAwait(/* ConfigureAwaitArgLeadingTrivia */ false /* ConfigureAwaitArgTrailngTrivia */) /* InvocationTrailingTrivia */; /* AwaitTrailingTrivia */
        }
    }
}
            ";

            string fixedSource = @"
using System;
using System.IO;
using System.Threading;
class C
{
    public async void M()
    {
        using (FileStream s = File.Open(""path.txt"", FileMode.Open))
        {
            byte[] buffer = { 0xBA, 0x5E, 0xBA, 0x11, 0xF0, 0x07, 0xBA, 0x11 };
            /* AwaitLeadingTrivia */ await /* InvocationLeadingTrivia */ s.WriteAsync(buffer.AsMemory(1 /* OffsetTrailingTrivia */, buffer.Length /* CountTrailingTrivia */) /* BufferTrailingTrivia */, new CancellationToken() /* CancellationTokenTrailingTrivia */).ConfigureAwait(/* ConfigureAwaitArgLeadingTrivia */ false /* ConfigureAwaitArgTrailngTrivia */) /* InvocationTrailingTrivia */; /* AwaitTrailingTrivia */
        }
    }
}
            ";

            return CSharpVerifyExpectedCodeFixDiagnosticsAsync(originalSource, fixedSource, GetCSharpResult(12, 74, 12, 255));
        }

        [Fact]
        public Task CS_Fixer_PreserveNullabilityAsync()
        {
            // The difference with the ReadAsync test is buffer?.Length
            string originalSource = @"
#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;

public class C
{
    async ValueTask M(Stream? stream, byte[]? buffer)
    {
        await stream!.WriteAsync(buffer!, offset: 0, count: buffer?.Length ?? 0).ConfigureAwait(false);
    }
}
";
            string fixedSource = @"
#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;

public class C
{
    async ValueTask M(Stream? stream, byte[]? buffer)
    {
        await (stream!).WriteAsync((buffer!).AsMemory(start: 0, length: buffer?.Length ?? 0)).ConfigureAwait(false);
    }
}
";

            return CSharpVerifyForVersionAsync(
                originalSource,
                fixedSource,
                ReferenceAssemblies.Net.Net50,
                CodeAnalysis.CSharp.LanguageVersion.CSharp8,
                GetCSharpResult(11, 15, 11, 81));
        }

        [Fact]
        public Task CS_Fixer_PreserveNullabilityWithCancellationTokenAsync()
        {
            // The difference with the ReadAsync test is buffer?.Length
            string originalSource = @"
#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    async ValueTask M(Stream? stream, byte[]? buffer, CancellationToken ct)
    {
        await stream!.WriteAsync(buffer!, offset: 0, count: buffer?.Length ?? 0, ct).ConfigureAwait(false);
    }
}
";
            string fixedSource = @"
#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    async ValueTask M(Stream? stream, byte[]? buffer, CancellationToken ct)
    {
        await (stream!).WriteAsync((buffer!).AsMemory(start: 0, length: buffer?.Length ?? 0), ct).ConfigureAwait(false);
    }
}
";

            return CSharpVerifyForVersionAsync(
                originalSource,
                fixedSource,
                ReferenceAssemblies.Net.Net50,
                CodeAnalysis.CSharp.LanguageVersion.CSharp8,
                GetCSharpResult(12, 15, 12, 85));
        }

        #endregion

        #region VB - Diagnostic

        private const string _sourceVisualBasic = @"
Imports System
Imports System.IO
Imports System.Threading
Public Module C
    Public Async Sub M()
        Using s As {0} = New FileStream(""file.txt"", FileMode.Create)
            {1}
            Await s.WriteAsync({2}){3}
        End Using
    End Sub
End Module
            ";

        public static IEnumerable<object[]> VisualBasicInlinedByteArrayTestData()
        {
            yield return new object[] { @"New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}, 0, 8",
                                        @"(New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}).AsMemory(0, 8)" };
            yield return new object[] { @"New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}, 0, 8, New CancellationToken()",
                                        @"(New Byte() {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}).AsMemory(0, 8), New CancellationToken()" };
        }

        [Fact]
        public Task VB_Fixer_Diagnostic_EnsureSystemNamespaceAutoAddedAsync()
        {
            string originalCode = @"
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync(buffer, 1, buffer.Length)
        End Using
    End Sub
End Class
";
            string fixedCode = @"
Imports System.IO
Imports System.Threading
Imports System

Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync(buffer.AsMemory(1, buffer.Length))
        End Using
    End Sub
End Class
";
            return VisualBasicVerifyExpectedCodeFixDiagnosticsAsync(originalCode, fixedCode, GetVisualBasicResult(8, 19, 8, 57));
        }

        [Theory]
        [InlineData("System")]
        [InlineData("system")]
        [InlineData("SYSTEM")]
        [InlineData("sYSTEm")]
        public Task VB_Fixer_Diagnostic_EnsureSystemNamespaceNotAddedWhenAlreadyPresentAsync(string systemNamespace)
        {
            string originalCode = $@"
Imports System.IO
Imports System.Threading
Imports {systemNamespace}

Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {{&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}}
            Await s.WriteAsync(buffer, 1, buffer.Length)
        End Using
    End Sub
End Class
";
            string fixedCode = $@"
Imports System.IO
Imports System.Threading
Imports {systemNamespace}

Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {{&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}}
            Await s.WriteAsync(buffer.AsMemory(1, buffer.Length))
        End Using
    End Sub
End Class
";

            return VisualBasicVerifyExpectedCodeFixDiagnosticsAsync(originalCode, fixedCode, GetVisualBasicResult(10, 19, 10, 57));
        }

        [Theory]
        [MemberData(nameof(VisualBasicUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWithCancellationTokenFullBufferTestData))]
        [MemberData(nameof(VisualBasicUnnamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWithCancellationTokenPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWrongCaseTestData))]
        public Task VB_Fixer_Diagnostic_ArgumentNamingAsync(string originalArgs, string fixedArgs) =>
            VisualBasicVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: true);

        [Theory]
        [MemberData(nameof(VisualBasicUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWithCancellationTokenFullBufferTestData))]
        [MemberData(nameof(VisualBasicUnnamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWithCancellationTokenPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWrongCaseTestData))]
        public Task VB_Fixer_Diagnostic_ArgumentNaming_WithConfigureAwaitAsync(string originalArgs, string fixedArgs) =>
            VisualBasicVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: false);

        [Theory]
        [MemberData(nameof(VisualBasicUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWithCancellationTokenFullBufferTestData))]
        public Task VB_Fixer_Diagnostic_AsStreamAsync(string originalArgs, string fixedArgs) =>
            VisualBasicVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "Stream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: true);

        [Theory]
        [MemberData(nameof(VisualBasicUnnamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsFullBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWithCancellationTokenFullBufferTestData))]
        [MemberData(nameof(VisualBasicUnnamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWithCancellationTokenPartialBufferTestData))]
        [MemberData(nameof(VisualBasicNamedArgumentsWrongCaseTestData))]
        public Task VB_Fixer_Diagnostic_AsStream_WithConfigureAwaitAsync(string originalArgs, string fixedArgs) =>
            VisualBasicVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "Stream", isEmptyByteDeclaration: false, isEmptyConfigureAwait: false);

        [Theory]
        [MemberData(nameof(VisualBasicInlinedByteArrayTestData))]
        public Task VB_Fixer_Diagnostic_InlineByteArrayAsync(string originalArgs, string fixedArgs)
            => VisualBasicVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: true, isEmptyConfigureAwait: true);

        [Theory]
        [MemberData(nameof(VisualBasicInlinedByteArrayTestData))]
        public Task VB_Fixer_Diagnostic_InlineByteArray_WithConfigureAwaitAsync(string originalArgs, string fixedArgs)
            => VisualBasicVerifyCodeFixAsync(originalArgs, fixedArgs, streamTypeName: "FileStream", isEmptyByteDeclaration: true, isEmptyConfigureAwait: false);

        [Fact]
        public Task VB_Fixer_Diagnostic_WithTriviaAsync()
        {
            // Notes:
            // - Visual Basic does not allow inline comments like in C#: /**/, only at the end of the line
            // - Only the last comment in the arguments section remains ("CancellationTokenComment"), the rest get discarded
            string originalSource = @"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync( ' OpeningParenthesisComment
                buffer, ' BufferComment
                1, ' OffsetComment
                buffer.Length, ' CountComment
                New CancellationToken() ' CancellationTokenComment
                ) ' InvocationTrailingComment
            ' AwaitComment
        End Using
    End Sub
End Class
            ";

            string fixedSource = @"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync(buffer.AsMemory(1, buffer.Length), New CancellationToken() ' CancellationTokenComment
) ' InvocationTrailingComment
            ' AwaitComment
        End Using
    End Sub
End Class
            ";

            return VisualBasicVerifyExpectedCodeFixDiagnosticsAsync(originalSource, fixedSource, GetVisualBasicResult(9, 19, 14, 18));
        }

        [Fact]
        public Task VB_Fixer_Diagnostic_WithTrivia_WithConfigureAwait_PartialBufferAsync()
        {
            // Notes:
            // - Visual Basic does not allow inline comments like in C#: /**/, only at the end of the line
            // - Only the last comment in the arguments section remains ("CancellationTokenComment"), the rest get discarded
            string originalSource = @"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync( ' OpeningParenthesisComment
                buffer, ' BufferComment
                1, ' OffsetComment
                buffer.Length, ' CountComment
                New CancellationToken() ' CancellationTokenComment
                ).ConfigureAwait(False) ' InvocationTrailingComment
            ' AwaitComment
        End Using
    End Sub
End Class
            ";

            string fixedSource = @"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync(buffer.AsMemory(1, buffer.Length), New CancellationToken() ' CancellationTokenComment
).ConfigureAwait(False) ' InvocationTrailingComment
            ' AwaitComment
        End Using
    End Sub
End Class
            ";

            return VisualBasicVerifyExpectedCodeFixDiagnosticsAsync(originalSource, fixedSource, GetVisualBasicResult(9, 19, 14, 18));
        }

        [Fact]
        public Task VB_Fixer_Diagnostic_WithTrivia_WithConfigureAwait_FullBufferAsync()
        {
            // Notes:
            // - Visual Basic does not allow inline comments like in C#: /**/, only at the end of the line
            // - Only the last comment in the arguments section remains ("CancellationTokenComment"), the rest get discarded
            string originalSource = @"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync( ' OpeningParenthesisComment
                buffer, ' BufferComment
                0, ' OffsetComment
                buffer.Length, ' CountComment
                New CancellationToken() ' CancellationTokenComment
                ).ConfigureAwait(False) ' InvocationTrailingComment
            ' AwaitComment
        End Using
    End Sub
End Class
            ";

            string fixedSource = @"
Imports System
Imports System.IO
Imports System.Threading
Class C
    Public Async Sub M()
        Using s As FileStream = File.Open(""path.txt"", FileMode.Open)
            Dim buffer As Byte() = {&HBA, &H5E, &HBA, &H11, &HF0, &H07, &HBA, &H11}
            Await s.WriteAsync(buffer, New CancellationToken() ' CancellationTokenComment
).ConfigureAwait(False) ' InvocationTrailingComment
            ' AwaitComment
        End Using
    End Sub
End Class
            ";

            return VisualBasicVerifyExpectedCodeFixDiagnosticsAsync(originalSource, fixedSource, GetVisualBasicResult(9, 19, 14, 18));
        }

        #endregion

        #region Helpers

        private const int _columnBeforeStreamInvocation = 19;
        private readonly int _columnsBeforeArguments = _columnBeforeStreamInvocation + " s.WriteAsync(".Length;

        private Task CSharpVerifyCodeFixAsync(string originalArgs, string fixedArgs, string streamTypeName, bool isEmptyByteDeclaration, bool isEmptyConfigureAwait) =>
            CSharpVerifyExpectedCodeFixDiagnosticsAsync(
                string.Format(_sourceCSharp, streamTypeName, GetByteArrayWithDataCSharp(isEmptyByteDeclaration), originalArgs, GetConfigureAwaitCSharp(isEmptyConfigureAwait)),
                string.Format(_sourceCSharp, streamTypeName, GetByteArrayWithDataCSharp(isEmptyByteDeclaration), fixedArgs, GetConfigureAwaitCSharp(isEmptyConfigureAwait)),
                GetCSharpResult(12, _columnBeforeStreamInvocation, 12, _columnsBeforeArguments + originalArgs.Length));

        private Task VisualBasicVerifyCodeFixAsync(string originalArgs, string fixedArgs, string streamTypeName, bool isEmptyByteDeclaration, bool isEmptyConfigureAwait) =>
            VisualBasicVerifyExpectedCodeFixDiagnosticsAsync(
                string.Format(_sourceVisualBasic, streamTypeName, GetByteArrayWithDataVisualBasic(isEmptyByteDeclaration), originalArgs, GetConfigureAwaitVisualBasic(isEmptyConfigureAwait)),
                string.Format(_sourceVisualBasic, streamTypeName, GetByteArrayWithDataVisualBasic(isEmptyByteDeclaration), fixedArgs, GetConfigureAwaitVisualBasic(isEmptyConfigureAwait)),
                GetVisualBasicResult(9, _columnBeforeStreamInvocation, 9, _columnsBeforeArguments + originalArgs.Length));

        // Returns a C# diagnostic result using the specified rule, lines, columns and preferred method signature for the WriteAsync method.
        protected DiagnosticResult GetCSharpResult(int startLine, int startColumn, int endLine, int endColumn)
            => GetCSResultForRule(startLine, startColumn, endLine, endColumn,
                PreferStreamAsyncMemoryOverloads.PreferStreamWriteAsyncMemoryOverloadsRule,
                "WriteAsync", "Stream.WriteAsync(ReadOnlyMemory<byte>, CancellationToken)");

        // Returns a VB diagnostic result using the specified rule, lines, columns and preferred method signature for the WriteAsync method.
        protected DiagnosticResult GetVisualBasicResult(int startLine, int startColumn, int endLine, int endColumn)
            => GetVBResultForRule(startLine, startColumn, endLine, endColumn,
                PreferStreamAsyncMemoryOverloads.PreferStreamWriteAsyncMemoryOverloadsRule,
                "WriteAsync", "Stream.WriteAsync(ReadOnlyMemory(Of Byte), CancellationToken)");

        #endregion
    }
}