// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamWriteAsyncMemoryOverloadsTest : PreferStreamAsyncMemoryOverloadsTestBase
    {
        #region No diagnostic - Analyzer

        [Fact]
        public async Task NoDiagnosticAnalyzer_Write()
        {
            await AnalyzeAsync(@"
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
        public async Task NoDiagnosticAnalyzer_WriteAsync_AsMemory()
        {
            await AnalyzeAsync(@"
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
        public async Task NoDiagnosticAnalyzer_NoAwait_SaveAsTask()
        {
            await AnalyzeAsync(@"
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
        public async Task NoDiagnosticAnalyzer_NoAwait_ReturnMethod()
        {
            await AnalyzeAsync(@"
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
        public async Task NoDiagnosticAnalyzer_NoAwait_ExpressionBodyMethod()
        {
            await AnalyzeAsync(@"
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
        public async Task NoDiagnosticAnalyzer_UnsupportedVersion()
        {
            await AnalyzeUnsupportedAsync(@"
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

        #region Diagnostic - Analyzer

        [Fact]
        public async Task DiagnosticAnalyzer_Basic()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_AsStream()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_CancellationToken()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_ConfigureAwait()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_ContinueWith_ConfigureAwait()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_ContinueWith_ContinueWith_ConfigureAwait()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_CancellationToken_ConfigureAwait()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_InlineBuffer()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(11, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_InlineBuffer_CancellationToken()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(11, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_InlineBuffer_ConfigureAwait()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(11, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_InlineBuffer_CancellationToken_ConfigureAwait()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(11, 19));
        }

        #endregion

        #region Helpers

        protected static DiagnosticResult GetCSharpResult(int line, int column)
            => GetCSharpResultBase(line, column, PreferStreamAsyncMemoryOverloads.PreferStreamWriteAsyncMemoryOverloadsRule);

        #endregion
    }
}
