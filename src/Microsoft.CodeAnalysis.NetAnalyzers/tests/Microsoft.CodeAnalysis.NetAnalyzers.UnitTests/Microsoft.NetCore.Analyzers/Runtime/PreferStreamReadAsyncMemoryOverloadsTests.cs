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
            await VerifyAnalyzerAsync50(@"
using System;
using System.IO;
using System.Threading;
class C
{
    void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] result = new byte[fs.Length];
            fs.Read(result, 0, (int)fs.Length);
        }
    }
}
            ");
        }

        [Fact]
        public async Task NoDiagnosticAnalyzer_ReadAsyncWithAsMemory()
        {
            await VerifyAnalyzerAsync50(@"
using System;
using System.IO;
using System.Threading;
class C
{
    async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] result = new byte[fs.Length];
            await fs.ReadAsync(result.AsMemory(), new CancellationToken());
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
            await VerifyAnalyzerAsync50(@"
using System;
using System.IO;
using System.Threading;
class C
{
    async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] result = new byte[fs.Length];
            await fs.ReadAsync(result, 0, (int)fs.Length);
        }
    }
}
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_Inline()
        {
            await VerifyAnalyzerAsync50(@"
using System;
using System.IO;
using System.Threading;
class C
{
    async void M()
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
            await VerifyAnalyzerAsync50(@"
using System;
using System.IO;
using System.Threading;
class C
{
    async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] result = new byte[fs.Length];
            await fs.ReadAsync(result, 0, (int)fs.Length, new CancellationToken());
        }
    }
}
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_ConfigureAwait()
        {
            await VerifyAnalyzerAsync50(@"
using System;
using System.IO;
using System.Threading;
class C
{
    async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] result = new byte[fs.Length];
            await fs.ReadAsync(result, 0, (int)fs.Length).ConfigureAwait(false);
        }
    }
}
            ", GetCSharpResult(12, 19));
        }

        [Fact]
        public async Task DiagnosticAnalyzer_CancellationTokenAndConfigureAwait()
        {
            await VerifyAnalyzerAsync50(@"
using System;
using System.IO;
using System.Threading;
class C
{
    async void M()
    {
        using (FileStream fs = File.Open(""file.txt"", FileMode.Open))
        {
            byte[] result = new byte[fs.Length];
            await fs.ReadAsync(result, 0, (int)fs.Length, new CancellationToken()).ConfigureAwait(false);
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
