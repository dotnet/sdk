// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideStreamMemoryBasedAsyncOverrides,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.ProvideStreamMemoryBasedAsyncOverrides,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class ProvideStreamMemoryBasedAsyncOverridesTests
    {
        #region Reports Diagnostic
        [Fact]
        public async Task ReadAsyncArray_NoReadAsyncMemory_ReportsDiagnostic_CS()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:FooStream|}} : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncArray}
    }}
}}";

            var diagnostic = VerifyCS.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("FooStream", CSDisplayReadAsyncArray, CSDisplayReadAsyncMemory);
            await new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task ReadAsyncArray_NoReadAsyncMemory_ReportsDiagnostic_VB()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class {{|#0:FooStream|}} : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncArray}
    End Class
End Namespace";

            var diagnostic = VerifyVB.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("FooStream", VBDisplayReadAsyncArray, VBDisplayReadAsyncMemory);
            await new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task WriteAsyncArray_NoWriteAsyncMemory_ReportsDiagnostic_CS()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class {{|#0:BarStream|}} : Stream
    {{
        {CSAbstractMembers}
        {CSWriteAsyncArray}
    }}
}}";

            var diagnostic = VerifyCS.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("BarStream", CSDisplayWriteAsyncArray, CSDisplayWriteAsyncMemory);
            await new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }

        [Fact]
        public async Task WriteAsyncArray_NoWriteAsyncMemory_ReportsDiagnostic_VB()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class {{|#0:BarStream|}} : Inherits Stream
        {VBAbstractMembers}
        {VBWriteAsyncArray}
    End Class
End Namespace";

            var diagnostic = VerifyVB.Diagnostic(Rule)
                .WithLocation(0)
                .WithArguments("BarStream", VBDisplayWriteAsyncArray, VBDisplayWriteAsyncMemory);
            await new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { diagnostic }
            }.RunAsync();
        }
        #endregion

        #region No Diagnostic
        [Fact]
        public async Task ReadAsyncArray_WithReadAsyncMemory_NoDiagnostic_CS()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class BazStream : Stream
    {{
        {CSAbstractMembers}
        {CSReadAsyncArray}
        {CSReadAsyncMemory}
    }}
}}";

            await new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        [Fact]
        public async Task ReadAsyncArray_WithReadAsyncMemory_NoDiagnostic_VB()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class BazStream : Inherits Stream
        {VBAbstractMembers}
        {VBReadAsyncArray}
        {VBReadAsyncMemory}
    End Class
End Namespace";

            await new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        [Fact]
        public async Task WriteAsyncArray_WithWriteAsyncMemory_NoDiagnostic_CS()
        {
            string code = $@"
{CSUsings}
namespace Testopolis
{{
    public class WhippleStream : Stream
    {{
        {CSAbstractMembers}
        {CSWriteAsyncArray}
        {CSWriteAsyncMemory}
    }}
}}";

            await new VerifyCS.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }

        [Fact]
        public async Task WriteAsyncArray_WithWriteAsyncMemory_NoDiagnostic_VB()
        {
            string code = $@"
{VBUsings}
Namespace Testopolis
    Public Class WhippleStream : Inherits Stream
        {VBAbstractMembers}
        {VBWriteAsyncArray}
        {VBWriteAsyncMemory}
    End Class
End Namespace";

            await new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            }.RunAsync();
        }
        #endregion

        #region Helpers
        private const string CSUsings = @"using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;";
        private const string CSAbstractMembers = @"public override void Flush() => throw null;
        public override int Read(byte[] buffer, int offset, int count) => throw null;
        public override long Seek(long offset, SeekOrigin origin) => throw null;
        public override void SetLength(long value) => throw null;
        public override void Write(byte[] buffer, int offset, int count) => throw null;
        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }";
        private const string CSReadAsyncArray = @"public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => throw null;";
        private const string CSReadAsyncMemory = @"public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => throw null;";
        private const string CSWriteAsyncArray = @"public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => throw null;";
        private const string CSWriteAsyncMemory = @"public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => throw null;";

        private const string VBUsings = @"Imports System
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks";
        private const string VBAbstractMembers = @"Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotImplementedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Throw New NotImplementedException()
            End Get
            Set(value As Long)
                Throw New NotImplementedException()
            End Set
        End Property
        Public Overrides Sub Flush()
            Throw New NotImplementedException()
        End Sub
        Public Overrides Sub SetLength(value As Long)
            Throw New NotImplementedException()
        End Sub
        Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
            Throw New NotImplementedException()
        End Sub
        Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
            Throw New NotImplementedException()
        End Function
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotImplementedException()
        End Function";
        private const string VBReadAsyncArray = @"Public Overrides Function ReadAsync(buffer() As Byte, offset As Integer, count As Integer, ct As CancellationToken) As Task(Of Integer)
            Throw New NotImplementedException()
        End Function";
        private const string VBReadAsyncMemory = @"Public Overrides Function ReadAsync(buffer As Memory(Of Byte), Optional ct As CancellationToken = Nothing) As ValueTask(Of Integer)
            Throw New NotImplementedException()
        End Function";
        private const string VBWriteAsyncArray = @"Public Overrides Function WriteAsync(buffer() As Byte, offset As Integer, count As Integer, ct As CancellationToken) As Task
            Throw New NotImplementedException()
        End Function";
        private const string VBWriteAsyncMemory = @"Public Overrides Function WriteAsync(buffer As ReadOnlyMemory(Of Byte), Optional ct As CancellationToken = Nothing) As ValueTask
            Throw New NotImplementedException()
        End Function";

        private const string CSDisplayReadAsyncArray = @"Task<int> Stream.ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)";
        private const string CSDisplayReadAsyncMemory = @"ValueTask<int> Stream.ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))";
        private const string CSDisplayWriteAsyncArray = @"Task Stream.WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)";
        private const string CSDisplayWriteAsyncMemory = @"ValueTask Stream.WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))";

        private const string VBDisplayReadAsyncArray = @"Function Stream.ReadAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)";
        private const string VBDisplayReadAsyncMemory = @"Function Stream.ReadAsync(buffer As Memory(Of Byte), cancellationToken As CancellationToken = Nothing) As ValueTask(Of Integer)";
        private const string VBDisplayWriteAsyncArray = @"Function Stream.WriteAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task";
        private const string VBDisplayWriteAsyncMemory = @"Function Stream.WriteAsync(buffer As ReadOnlyMemory(Of Byte), cancellationToken As CancellationToken = Nothing) As ValueTask";

        private static DiagnosticDescriptor Rule => ProvideStreamMemoryBasedAsyncOverrides.Rule;
        #endregion
    }
}
