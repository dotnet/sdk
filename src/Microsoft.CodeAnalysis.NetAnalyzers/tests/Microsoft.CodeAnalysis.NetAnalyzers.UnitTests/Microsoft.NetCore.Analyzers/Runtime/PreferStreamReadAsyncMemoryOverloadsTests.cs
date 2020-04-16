// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferStreamReadAsyncMemoryOverloadsTest : PreferStreamAsyncMemoryOverloadsTestBase
    {
        #region No diagnostic - Analyzer

        [Fact]
        public async Task NoDiagnosticAnalyzer_Read()
        {
            await AnalyzeAsync(@"
using System;
using System.IO;
using System.Threading;
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
        public async Task NoDiagnosticAnalyzer_ReadAsync_AsMemory()
        {
            await AnalyzeAsync(@"
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
        return fs.ReadAsync(buffer, 0, (int)fs.Length);
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
    public Task M(FileStream fs, byte[] buffer) => fs.ReadAsync(buffer, 0, (int)fs.Length);
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
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length);
        }
    }
}
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_Inline()
        {
            await AnalyzeAsync(@"
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
            ", GetCSharpResult(11, 19));
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
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length, new CancellationToken());
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
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length).ConfigureAwait(false);
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
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length).ContinueWith(c => {}).ConfigureAwait(false);
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
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length).ContinueWith(c => {}).ContinueWith(c => {}).ConfigureAwait(false);
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
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] buffer = new byte[fs.Length];
            await fs.ReadAsync(buffer, 0, (int)fs.Length, new CancellationToken()).ConfigureAwait(false);
        }
    }
}
            ", GetCSharpResult(12, 19));
        }

        #endregion

        #region Helpers

        protected static DiagnosticResult GetCSharpResult(int line, int column)
            => GetCSharpResultBase(line, column, PreferStreamAsyncMemoryOverloads.PreferStreamReadAsyncMemoryOverloadsRule);

        #endregion
    }
}
