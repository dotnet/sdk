// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferHashDataOverComputeHashAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferHashDataOverComputeHashFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.PreferHashDataOverComputeHashAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicPreferHashDataOverComputeHashFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class PreferHashDataOverComputeHashTests
    {
        private const string HashTypeMD5 = "MD5";
        private const string HashTypeSHA1 = "SHA1";
        private const string HashTypeSHA256 = "SHA256";
        private const string HashTypeSHA384 = "SHA384";
        private const string HashTypeSHA512 = "SHA512";

        [Fact]
        public async Task CSharpBailOutNoFixCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod({hashType} hash)
    {{
        var buffer = new byte[1024];
        int aboveLine = 20;
        byte[] digest = hash.ComputeHash(buffer);
        int belowLine = 10;
    }}
}}
";
                await TestCSAsync(csInput);
            }
        }

        [Fact]
        public async Task BasicBailOutNoFixCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod(sha256 As {hashType})
        Dim buffer = New Byte(1023) {{}}
        Dim aboveLine = 20
        Dim digest As Byte() = sha256.ComputeHash(buffer)
        Dim belowLine = 10
    End Sub
End Class
";
                await TestVBAsync(vbInput);
            }
        }

        [Fact]
        public async Task CSharpCreateHelperUnknownMethodBailOutNoFixCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void UnknownMethod(HashAlgorithm hasher)
    {{
    }}
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        var hasher = {hashType}.Create();
        UnknownMethod(hasher);
        int aboveLine = 20;
        byte[] digest = hasher.ComputeHash(buffer);
        int belowLine = 10;
    }}
}}
";
                await TestCSAsync(csInput);
            }
        }

        [Fact]
        public async Task BasicCreateHelperUnknownMethodBailOutNoFixCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub UnknownMethod(hasher As HashAlgorithm)
    End Sub
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim hasher As {hashType} = {hashType}.Create()
        UnknownMethod(hasher)
        Dim aboveLine = 20
        Dim digest As Byte() = hasher.ComputeHash(buffer)
        Dim belowLine = 10
    End Sub
End Class
";
                await TestVBAsync(vbInput);
            }
        }

        [Fact]
        public async Task CSharpCreateHelperBailOutNoFixCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        var hasher = {hashType}.Create();
        int aboveLine = 20;
        int belowLine = 10;
    }}
}}
";
                await TestCSAsync(csInput);
            }
        }

        [Fact]
        public async Task BasicCreateHelperBailOutNoFixCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim sha256 As {hashType} = {hashType}.Create()
        Dim aboveLine = 20
        Dim belowLine = 10
    End Sub
End Class
";
                await TestVBAsync(vbInput);
            }
        }

        [Fact]
        public async Task CSharpCreateHelperChainCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {{|#0:{hashType}.Create().ComputeHash(buffer)|}};
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        byte[] digest2 = {{|#1:{hashType}.Create().ComputeHash(buffer, 0, 10)|}};
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line3 = 10;
        byte[] digest3 = new byte[1024];
        int line4 = 10;
        if ({{|#2:{hashType}.Create().TryComputeHash(buffer, digest3, out var i)|}})
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";

                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line3 = 10;
        byte[] digest3 = new byte[1024];
        int line4 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetChainedCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperChainCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#0:{hashType}.Create().ComputeHash(buffer)|}}
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Dim digest As Byte() = {{|#1:{hashType}.Create().ComputeHash(buffer, 0, 10)|}}
        Dim line3 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line3 = 10
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        If {{|#2:{hashType}.Create().TryComputeHash(buffer, digest, i)|}} Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line3 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line3 = 10
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetChainedVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperChainNamedParameterCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {{|#0:{hashType}.Create().ComputeHash(buffer: buffer)|}};
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        byte[] digest2 = {{|#1:{hashType}.Create().ComputeHash(offset: 0, count: 10, buffer: buffer)|}};
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line3 = 10;
        byte[] digest3 = new byte[1024];
        int line4 = 10;
        if ({{|#2:{hashType}.Create().TryComputeHash(bytesWritten: out var i, source: buffer, destination: digest3)|}})
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";

                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(source: buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(source: buffer.AsSpan(start: 0, length: 10));
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line3 = 10;
        byte[] digest3 = new byte[1024];
        int line4 = 10;
        if ({hashType}.TryHashData(bytesWritten: out var i, source: buffer, destination: digest3))
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetChainedCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperChainNamedParameterCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#0:{hashType}.Create().ComputeHash(buffer:=buffer)|}}
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Dim digest As Byte() = {{|#1:{hashType}.Create().ComputeHash(OFFSET:=0, count:=10, BUFFER:=buffer)|}}
        Dim line3 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line3 = 10
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        If {{|#2:{hashType}.Create().TryComputeHash(bytesWritten:=i, source:=buffer, destination:=digest)|}} Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(source:=buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Dim digest As Byte() = {hashType}.HashData(source:=buffer.AsSpan(start:=0, length:=10))
        Dim line3 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line3 = 10
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        If {hashType}.TryHashData(bytesWritten:=i, source:=buffer, destination:=digest) Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetChainedVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperNoUsingStatement2Case()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        var {{|#2:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
        int line2 = 10;
        byte[] digest2 = {{|#1:hasher.ComputeHash(buffer)|}};
        int line3 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        var {{|#5:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#3:hasher.ComputeHash(buffer, 0, 10)|}};
        int line2 = 10;
        byte[] digest2 = {{|#4:hasher.ComputeHash(buffer, 0, 10)|}};
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        var {{|#8:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({{|#6:hasher.TryComputeHash(buffer, digest3, out var i)|}})
        {{
            int line3 = 10;
        }}
        int line4 = 10;
        if ({{|#7:hasher.TryComputeHash(buffer, digest3, out i)|}})
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer);
        int line3 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out i))
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationDoubleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperNoUsingBlock2Case()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#2:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
        Dim line2 = 10
        Dim digest2 As Byte() = {{|#1:hasher.ComputeHash(buffer)|}}
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#5:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#3:hasher.ComputeHash(buffer, 0, 10)|}}
        Dim line2 = 10
        Dim digest2 As Byte() = {{|#4:hasher.ComputeHash(buffer, 0, 10)|}}
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#8:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {{|#6:hasher.TryComputeHash(buffer, digest, i)|}} Then
            Dim line3 = 10
        End If
        Dim line4 = 10
        If {{|#7:hasher.TryComputeHash(buffer, digest, i)|}} Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
        Dim digest2 As Byte() = {hashType}.HashData(buffer)
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
        Dim digest2 As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationDoubleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperNoUsingStatementCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        var {{|#1:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        var {{|#3:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}};
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        var {{|#5:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({{|#4:hasher.TryComputeHash(buffer, digest3, out var i)|}})
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperNoUsingBlockCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#1:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#3:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}}
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#5:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {{|#4:hasher.TryComputeHash(buffer, digest, i)|}} Then
            Dim line3 = 10
        End If
        Dim line4 = 10
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationSingleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperManualDisposeCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        var {{|#1:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
        int line2 = 10;
        {{|#2:hasher.Dispose();|}}
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        var {{|#4:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#3:hasher.ComputeHash(buffer,0, 10)|}};
        int line2 = 10;
        {{|#5:hasher.Dispose();|}}
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        var {{|#7:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({{|#6:hasher.TryComputeHash(buffer, digest3, out var i)|}})
        {{
            int line3 = 10;
        }}
        int line4 = 10;
        {{|#8:hasher.Dispose();|}}
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeWithDisposeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperManualDisposeCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#1:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
        Dim line2 = 10
        {{|#2:hasher.Dispose()|}}
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#4:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#3:hasher.ComputeHash(buffer, 0, 10)|}}
        Dim line2 = 10
        {{|#5:hasher.Dispose()|}}
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim {{|#7:hasher As {hashType} = {hashType}.Create()|}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {{|#6:hasher.TryComputeHash(buffer, digest, i)|}} Then
            Dim line3 = 10
        End If
        Dim line4 = 10
        {{|#8:hasher.Dispose()|}}
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationSingleInvokeWithDisposeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperUsingStatement2Case()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using (var {{|#2:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int line2 = 10;
            byte[] digest2 = {{|#1:hasher.ComputeHash(buffer)|}};
            int line3 = 10;
        }}
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using (var {{|#5:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#3:hasher.ComputeHash(buffer, 0, 10)|}};
            int line2 = 10;
            byte[] digest2 = {{|#4:hasher.ComputeHash(buffer, 0, 10)|}};
            int line3 = 10;
        }}
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        using (var {{|#8:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest3 = new byte[1024];
            int line2 = 10;
            if ({{|#6:hasher.TryComputeHash(buffer, digest3, out var i)|}})
            {{
                int line3 = 10;
            }}
            int line4 = 10;
            if ({{|#7:hasher.TryComputeHash(buffer, digest3, out i)|}})
            {{
                int line5 = 10;
            }}
            int line6 = 10;
        }}
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer);
        int line3 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out i))
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationDoubleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperUsingBlock2Case()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#2:hasher As {hashType} = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim line2 = 10
            Dim digest2 As Byte() = {{|#1:hasher.ComputeHash(buffer)|}}
        End Using
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#5:hasher As {hashType} = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#3:hasher.ComputeHash(buffer, 0, 10)|}}
            Dim line2 = 10
            Dim digest2 As Byte() = {{|#4:hasher.ComputeHash(buffer, 0, 10)|}}
        End Using
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#8:hasher As {hashType} = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest = New Byte(1023) {{}}
            Dim i As Integer
            Dim line2 = 10
            If {{|#6:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line3 = 10
            End If
            Dim line4 = 10
            If {{|#7:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line5 = 10
            End If
            Dim line6 = 10
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
        Dim digest2 As Byte() = {hashType}.HashData(buffer)
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
        Dim digest2 As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationDoubleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperUsingDeclarationCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using var {{|#1:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using var {{|#3:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}};
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        using var {{|#5:hasher = {hashType}.Create()|}};
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({{|#4:hasher.TryComputeHash(buffer, digest3, out var i)|}})
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpFullyQualifiedCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        byte[] digest1 = {{|#0:new System.Security.Cryptography.{hashType}Managed().ComputeHash(buffer)|}};
        byte[] digest2 = {{|#1:System.Security.Cryptography.{hashType}.Create().ComputeHash(buffer)|}};
        using (var {{|#3:hasher = new System.Security.Cryptography.{hashType}Managed()|}})
        {{
            byte[] digest3 = {{|#2:hasher.ComputeHash(buffer)|}};
        }}
        using (var {{|#5:hasher = System.Security.Cryptography.{hashType}.Create()|}})
        {{
            byte[] digest4 = {{|#4:hasher.ComputeHash(buffer)|}};
        }}
    }}
}}
";
                string csFix = $@"
using System;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        byte[] digest1 = System.Security.Cryptography.{hashType}.HashData(buffer);
        byte[] digest2 = System.Security.Cryptography.{hashType}.HashData(buffer);
        byte[] digest3 = System.Security.Cryptography.{hashType}.HashData(buffer);
        byte[] digest4 = System.Security.Cryptography.{hashType}.HashData(buffer);
    }}
}}
";
                string hashFullType = $"System.Security.Cryptography.{hashType}";
                await TestCSAsync(
                    csInput,
                    csFix,
                new[] {
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(0),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(1),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(2)
                        .WithLocation(3),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(4)
                        .WithLocation(5)
                    });
            }
        }

        [Fact]
        public async Task BasicFullyQualifiedCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim digest1 As Byte() = {{|#0:New System.Security.Cryptography.{hashType}Managed().ComputeHash(buffer)|}}
        Dim digest2 As Byte() = {{|#1:System.Security.Cryptography.{hashType}.Create().ComputeHash(buffer)|}}
        Using {{|#3:hasher As System.Security.Cryptography.{hashType}Managed = New System.Security.Cryptography.{hashType}Managed()|}}
            Dim digest3 As Byte() = {{|#2:hasher.ComputeHash(buffer)|}}
        End Using
        Using {{|#5:hasher As System.Security.Cryptography.{hashType} = System.Security.Cryptography.{hashType}.Create()|}}
            Dim digest4 As Byte() = {{|#4:hasher.ComputeHash(buffer)|}}
        End Using
        Using {{|#7:hasher As New System.Security.Cryptography.{hashType}Managed()|}}
            Dim digest5 As Byte() = {{|#6:hasher.ComputeHash(buffer)|}}
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim digest1 As Byte() = System.Security.Cryptography.{hashType}.HashData(buffer)
        Dim digest2 As Byte() = System.Security.Cryptography.{hashType}.HashData(buffer)
        Dim digest3 As Byte() = System.Security.Cryptography.{hashType}.HashData(buffer)
        Dim digest4 As Byte() = System.Security.Cryptography.{hashType}.HashData(buffer)
        Dim digest5 As Byte() = System.Security.Cryptography.{hashType}.HashData(buffer)
    End Sub
End Class
";
                string hashFullType = $"System.Security.Cryptography.{hashType}";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                new[] {
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(0),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(1),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(2)
                        .WithLocation(3),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(4)
                        .WithLocation(5),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashFullType)
                        .WithLocation(6)
                        .WithLocation(7)
                    });
            }
        }

        [Fact]
        public async Task CSharpCreateHelperUsingStatementCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using (var {{|#1:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using (var {{|#3:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        using (var {{|#5:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest3 = new byte[1024];
            int line2 = 10;
            if ({{|#4:hasher.TryComputeHash(buffer, digest3, out var i)|}})
            {{
                int line3 = 10;
            }}
            int line4 = 10;
        }}
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperUsingBlockCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#1:hasher As {hashType} = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#3:hasher As {hashType} = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#5:hasher As {hashType} = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest = New Byte(1023) {{}}
            Dim i As Integer
            Dim line2 = 10
            If {{|#4:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line3 = 10
            End If
            Dim line4 = 10
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationSingleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperUsingStatementCastedCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using (HashAlgorithm {{|#1:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using (HashAlgorithm {{|#3:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        using (HashAlgorithm {{|#5:hasher = {hashType}.Create()|}})
        {{
            int line1 = 20;
            byte[] digest3 = new byte[1024];
            int line2 = 10;
            if ({{|#4:hasher.TryComputeHash(buffer, digest3, out var i)|}})
            {{
                int line3 = 10;
            }}
            int line4 = 10;
        }}
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicCreateHelperUsingBlockCastedCase()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#1:hasher As HashAlgorithm = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#3:hasher As HashAlgorithm = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#5:hasher As HashAlgorithm = {hashType}.Create()|}}
            Dim line1 = 20
            Dim digest = New Byte(1023) {{}}
            Dim i As Integer
            Dim line2 = 10
            If {{|#4:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line3 = 10
            End If
            Dim line4 = 10
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationSingleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpCreateHelperUsingStatements2Case()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using ({hashType} {{|#1:hasher = {hashType}.Create()|}}, {{|#4:hasher2 = {hashType}.Create()|}})
        {{
            int aboveLine = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int belowLine = 10;
            byte[] digest2 = {{|#2:hasher2.ComputeHash({{|#3:hasher2.ComputeHash(digest)|}})|}};
        }}
    }}
    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using ({hashType} {{|#6:hasher = {hashType}.Create()|}}, {{|#9:hasher2 = {hashType}.Create()|}})
        {{
            int aboveLine = 20;
            byte[] digest = {{|#5:hasher.ComputeHash(buffer, 0, 10)|}};
            int belowLine = 10;
            byte[] digest2 = {{|#7:hasher2.ComputeHash({{|#8:hasher2.ComputeHash(digest, 0, 10)|}}, 0, 10)|}};
        }}
    }}
}}
";

                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int aboveLine = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int belowLine = 10;
        byte[] digest2 = {hashType}.HashData({hashType}.HashData(digest));
    }}
    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int aboveLine = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int belowLine = 10;
        byte[] digest2 = {hashType}.HashData({hashType}.HashData(digest.AsSpan(0, 10)).AsSpan(0, 10));
    }}
}}
";
                var hashAlgorithmTypeName = $"System.Security.Cryptography.{hashType}";
                await TestCSAsync(
                    csInput,
                    csFix,
                    new[] {
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(1),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(2)
                        .WithLocation(4),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(3)
                        .WithLocation(4),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(5)
                        .WithLocation(6),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(7)
                        .WithLocation(9),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(8)
                        .WithLocation(9)
                    });
            }
        }

        [Fact]
        public async Task BasicCreateHelperUsingBlocks2Case()
        {
            await TestWithType(HashTypeMD5);
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#1:hasher As {hashType} = {hashType}.Create()|}}, {{|#4:hasher2 As {hashType} = {hashType}.Create()|}}
            Dim aboveLine = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim belowLine = 10
            Dim digest2 As Byte() = {{|#2:hasher2.ComputeHash({{|#3:hasher2.ComputeHash(digest)|}})|}}
        End Using
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#6:hasher As {hashType} = {hashType}.Create()|}}, {{|#9:hasher2 As {hashType} = {hashType}.Create()|}}
            Dim aboveLine = 20
            Dim digest As Byte() = {{|#5:hasher.ComputeHash(buffer, 0, 10)|}}
            Dim belowLine = 10
            Dim digest2 As Byte() = {{|#7:hasher2.ComputeHash({{|#8:hasher2.ComputeHash(digest, 0, 10)|}}, 0, 10)|}}
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim aboveLine = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim belowLine = 10
        Dim digest2 As Byte() = {hashType}.HashData({hashType}.HashData(digest))
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim aboveLine = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim belowLine = 10
        Dim digest2 As Byte() = {hashType}.HashData({hashType}.HashData(digest.AsSpan(0, 10)).AsSpan(0, 10))
    End Sub
End Class
";
                var hashAlgorithmTypeName = $"System.Security.Cryptography.{hashType}";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    new[] {
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(1),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(2)
                        .WithLocation(4),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(3)
                        .WithLocation(4),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(5)
                        .WithLocation(6),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(7)
                        .WithLocation(9),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(8)
                        .WithLocation(9)
                    });
            }
        }

        [Fact]
        public async Task CSharpObjectCreationBailOutNoFixCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        var sha256 = new {hashType}Managed();
        int aboveLine = 20;
        int belowLine = 10;
    }}
}}
";
                await TestCSAsync(csInput);
            }
        }

        [Fact]
        public async Task CSharpObjectCreationChainCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {{|#0:new {hashType}Managed().ComputeHash(buffer)|}};
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        byte[] digest2 = {{|#1:new {hashType}Managed().ComputeHash(buffer, 0, 10)|}};
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line3 = 10;
        byte[] digest3 = new byte[1024];
        int line4 = 10;
        if({{|#2:new {hashType}Managed().TryComputeHash(buffer, digest3, out var i)|}})
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";

                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line3 = 10;
        byte[] digest3 = new byte[1024];
        int line4 = 10;
        if({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetChainedCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicObjectCreationChainCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {{|#0:New {hashType}Managed().ComputeHash(buffer)|}}
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Dim digest As Byte() = {{|#1:New {hashType}Managed().ComputeHash(buffer, 0, 10)|}}
        Dim line3 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line3 = 10
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        If {{|#2:New {hashType}Managed().TryComputeHash(buffer, digest, i)|}} Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line3 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line3 = 10
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetChainedVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpObjectCreationChainInArgumentCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    private static void Test2(byte[] buffer)
    {{
    }}
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        Test2({{|#0:new {hashType}Managed().ComputeHash(buffer)|}});
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        Test2({{|#1:new {hashType}Managed().ComputeHash(buffer, 0, 10)|}});
        int line3 = 10;
    }}
}}
";

                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    private static void Test2(byte[] buffer)
    {{
    }}
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        Test2({hashType}.HashData(buffer));
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line2 = 10;
        Test2({hashType}.HashData(buffer.AsSpan(0, 10)));
        int line3 = 10;
    }}
}}
";
                var hashAlgorithmTypeName = $"System.Security.Cryptography.{hashType}";
                await TestCSAsync(
                    csInput,
                    csFix,
                    new[] {
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(1)
                        });
            }
        }

        [Fact]
        public async Task BasicObjectCreationChainInArgumentCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub Test2(buffer As Byte())
    End Sub
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Test2({{|#0:New {hashType}Managed().ComputeHash(buffer)|}})
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Test2({{|#1:New {hashType}Managed().ComputeHash(buffer, 0, 10)|}})
        Dim line3 = 10
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub Test2(buffer As Byte())
    End Sub
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Test2({hashType}.HashData(buffer))
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line2 = 10
        Test2({hashType}.HashData(buffer.AsSpan(0, 10)))
        Dim line3 = 10
    End Sub
End Class
";
                var hashAlgorithmTypeName = $"System.Security.Cryptography.{hashType}";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(1));
            }
        }

        [Fact]
        public async Task CSharpObjectCreationUsingStatement2Case()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using (var {{|#2:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int line2 = 10;
            byte[] digest2 = {{|#1:hasher.ComputeHash(buffer)|}};
            int line3 = 10;
        }}
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using (var {{|#5:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#3:hasher.ComputeHash(buffer, 0, 10)|}};
            int line2 = 10;
            byte[] digest2 = {{|#4:hasher.ComputeHash(buffer, 0, 10)|}};
            int line3 = 10;
        }}
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        using (var {{|#8:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest3 = new byte[1024];
            int line2 = 10;
            if ({{|#6:hasher.TryComputeHash(buffer, digest3, out var i)|}})
            {{
                int line3 = 10;
            }}
            int line4 = 10;
            if ({{|#7:hasher.TryComputeHash(buffer, digest3, out i)|}})
            {{
                int line5 = 10;
            }}
            int line6 = 10;
        }}
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer);
        int line3 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
        byte[] digest2 = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line3 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out i))
        {{
            int line5 = 10;
        }}
        int line6 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationDoubleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicObjectCreationUsingBlock2Case()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#2:hasher As New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim line2 = 10
            Dim digest2 As Byte() = {{|#1:hasher.ComputeHash(buffer)|}}
        End Using
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#5:hasher As New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#3:hasher.ComputeHash(buffer, 0, 10)|}}
            Dim line2 = 10
            Dim digest2 As Byte() = {{|#4:hasher.ComputeHash(buffer, 0, 10)|}}
        End Using
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#8:hasher As New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest = New Byte(1023) {{}}
            Dim i As Integer
            Dim line2 = 10
            If {{|#6:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line3 = 10
            End If
            Dim line4 = 10
            If {{|#7:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line5 = 10
            End If
            Dim line6 = 10
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
        Dim digest2 As Byte() = {hashType}.HashData(buffer)
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
        Dim digest2 As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line5 = 10
        End If
        Dim line6 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationDoubleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpObjectCreationUsingStatementCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using (var {{|#1:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using (var {{|#3:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        using (var {{|#5:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest3 = new byte[1024];
            int line2 = 10;
            if ({{|#4:hasher.TryComputeHash(buffer, digest3, out var i)|}})
            {{
                int line3 = 10;
            }}
            int line4 = 10;
        }}
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpObjectCreationUsingStatementCaseTopLevel()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

var buffer = new byte[1024];
using (var {{|#1:hasher = new {hashType}Managed()|}})
{{
    int line1 = 20;
    byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
    int line2 = 10;
}}

var buffer2 = new byte[1024];
using (var {{|#3:hasher2 = new {hashType}Managed()|}})
{{
    int line12 = 20;
    byte[] digest2 = {{|#2:hasher2.ComputeHash(buffer2, 0, 10)|}};
    int line22 = 10;
}}

var buffer3 = new byte[1024];
using (var {{|#5:hasher3 = new {hashType}Managed()|}})
{{
    int line13 = 20;
    byte[] digest3 = new byte[1024];
    int line23 = 10;
    if ({{|#4:hasher3.TryComputeHash(buffer3, digest3, out var i)|}})
    {{
        int line33 = 10;
    }}
    int line43 = 10;
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

var buffer = new byte[1024];
int line1 = 20;
byte[] digest = {hashType}.HashData(buffer);
int line2 = 10;

var buffer2 = new byte[1024];
int line12 = 20;
byte[] digest2 = {hashType}.HashData(buffer2.AsSpan(0, 10));
int line22 = 10;

var buffer3 = new byte[1024];
int line13 = 20;
byte[] digest3 = new byte[1024];
int line23 = 10;
if ({hashType}.TryHashData(buffer3, digest3, out var i))
{{
    int line33 = 10;
}}
int line43 = 10;
";
                await TestCSTopLevelAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicObjectCreationUsingBlockCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#1:hasher As New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#3:hasher As New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#5:hasher As New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest = New Byte(1023) {{}}
            Dim i As Integer
            Dim line2 = 10
            If {{|#4:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line3 = 10
            End If
            Dim line4 = 10
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationSingleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpObjectCreationUsingStatementCastedCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        using (HashAlgorithm {{|#1:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        using (HashAlgorithm {{|#3:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}};
            int line2 = 10;
        }}
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        using (HashAlgorithm {{|#5:hasher = new {hashType}Managed()|}})
        {{
            int line1 = 20;
            byte[] digest3 = new byte[1024];
            int line2 = 10;
            if ({{|#4:hasher.TryComputeHash(buffer, digest3, out var i)|}})
            {{
                int line3 = 10;
            }}
            int line4 = 10;
        }}
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
    }}

    public static void TestMethod2()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer.AsSpan(0, 10));
        int line2 = 10;
    }}

    public static void TestMethod3()
    {{
        var buffer = new byte[1024];
        int line1 = 20;
        byte[] digest3 = new byte[1024];
        int line2 = 10;
        if ({hashType}.TryHashData(buffer, digest3, out var i))
        {{
            int line3 = 10;
        }}
        int line4 = 10;
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                    GetCreationSingleInvokeCSDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task BasicObjectCreationUsingBlockCastedCase()
        {
            await TestWithType(HashTypeSHA1);
            await TestWithType(HashTypeSHA256);
            await TestWithType(HashTypeSHA384);
            await TestWithType(HashTypeSHA512);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#1:hasher As HashAlgorithm = New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#3:hasher As HashAlgorithm = New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest As Byte() = {{|#2:hasher.ComputeHash(buffer, 0, 10)|}}
            Dim line2 = 10
        End Using
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Using {{|#5:hasher As HashAlgorithm = New {hashType}Managed()|}}
            Dim line1 = 20
            Dim digest = New Byte(1023) {{}}
            Dim i As Integer
            Dim line2 = 10
            If {{|#4:hasher.TryComputeHash(buffer, digest, i)|}} Then
                Dim line3 = 10
            End If
            Dim line4 = 10
        End Using
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod2()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer.AsSpan(0, 10))
        Dim line2 = 10
    End Sub
    Public Shared Sub TestMethod3()
        Dim buffer = New Byte(1023) {{}}
        Dim line1 = 20
        Dim digest = New Byte(1023) {{}}
        Dim i As Integer
        Dim line2 = 10
        If {hashType}.TryHashData(buffer, digest, i) Then
            Dim line3 = 10
        End If
        Dim line4 = 10
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    GetCreationSingleInvokeVBDiagnostics($"System.Security.Cryptography.{hashType}"));
            }
        }

        [Fact]
        public async Task CSharpTriviaCase()
        {
            await TestWithType(HashTypeMD5);

            static async Task TestWithType(string hashType)
            {
                string csInput = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
#if !SOMETHING
        using (var {{|#1:hasher = {hashType}.Create()|}})  // test
        {{  // test2
            int line1 = 20;
            byte[] digest = {{|#0:hasher.ComputeHash(buffer)|}};
            int line2 = 10;
    /* test3
     a  */    }} 
#else
        byte[] digest = Array.Empty<byte>();
#endif
        //test4
        using var {{|#3:hasher2 = {hashType}.Create()|}}; //test5
        byte[] digest2 = {{|#2:hasher2.ComputeHash(buffer)|}};
        //test6
    }}
}}
";
                string csFix = $@"
using System;
using System.Security.Cryptography;

public class Test
{{
    public static void TestMethod()
    {{
        var buffer = new byte[1024];
#if !SOMETHING
        // test
        // test2
        int line1 = 20;
        byte[] digest = {hashType}.HashData(buffer);
        int line2 = 10;
        /* test3
         a  */
#else
        byte[] digest = Array.Empty<byte>();
#endif
        //test4
        //test5
        byte[] digest2 = {hashType}.HashData(buffer);
        //test6
    }}
}}
";
                await TestCSAsync(
                    csInput,
                    csFix,
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments($"System.Security.Cryptography.{hashType}")
                        .WithLocation(0)
                        .WithLocation(1),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments($"System.Security.Cryptography.{hashType}")
                        .WithLocation(2)
                        .WithLocation(3));
            }
        }

        [Fact]
        public async Task BasicTriviaCase()
        {
            await TestWithType(HashTypeMD5);

            static async Task TestWithType(string hashType)
            {
                string vbInput = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
#If Not SOMETHING
        Using {{|#1:hasher As {hashType} = {hashType}.Create()|}} 'test
            'test2
            Dim line1 = 20
            Dim digest As Byte() = {{|#0:hasher.ComputeHash(buffer)|}}
            Dim line2 = 10
        End Using 'test3
#Else
        Dim digest As Byte() = Array.Empty<byte>();
#End If
        'test4
        Dim {{|#3:hasher2 As {hashType} = {hashType}.Create()|}} 'test5
        Dim digest2 As Byte() = {{|#2:hasher2.ComputeHash(buffer)|}}
        'test6
    End Sub
End Class
";

                string vbFix = $@"
Imports System
Imports System.Security.Cryptography

Public Class Test
    Public Shared Sub TestMethod()
        Dim buffer = New Byte(1023) {{}}
#If Not SOMETHING
        'test
        'test2
        Dim line1 = 20
        Dim digest As Byte() = {hashType}.HashData(buffer)
        Dim line2 = 10
        'test3
#Else
        Dim digest As Byte() = Array.Empty<byte>();
#End If
        'test4
        'test5
        Dim digest2 As Byte() = {hashType}.HashData(buffer)
        'test6
    End Sub
End Class
";
                await TestVBAsync(
                    vbInput,
                    vbFix,
                    VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments($"System.Security.Cryptography.{hashType}")
                        .WithLocation(0)
                        .WithLocation(1),
                    VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments($"System.Security.Cryptography.{hashType}")
                        .WithLocation(2)
                        .WithLocation(3));
            }
        }

        private static VerifyCS.Test GetTestCS(string source, string corrected, ReferenceAssemblies referenceAssemblies)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = referenceAssemblies,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
                FixedCode = corrected,
            };
            return test;
        }

        private static VerifyCS.Test GetTestTopLevelCS(string source, string corrected, ReferenceAssemblies referenceAssemblies)
        {
            var test = new VerifyCS.Test
            {
                TestCode = source,
                TestState =
                {
                    OutputKind = OutputKind.ConsoleApplication,
                },
                ReferenceAssemblies = referenceAssemblies,
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.Preview,
                FixedCode = corrected,
            };
            return test;
        }

        private static async Task TestCSAsync(string source)
        {
            await GetTestCS(source, source, ReferenceAssemblies.Net.Net50).RunAsync();
            await GetTestCS(source, source, ReferenceAssemblies.NetCore.NetCoreApp31).RunAsync();
        }

        private static async Task TestCSAsync(string source, string corrected, params DiagnosticResult[] diagnosticResults)
        {
            var test = GetTestCS(source, corrected, ReferenceAssemblies.Net.Net50);

            for (int i = 0; i < diagnosticResults.Length; i++)
            {
                var expected = diagnosticResults[i];
                test.ExpectedDiagnostics.Add(expected);
            }

            await test.RunAsync();
            await GetTestCS(source, source, ReferenceAssemblies.NetCore.NetCoreApp31).RunAsync();
        }

        private static async Task TestCSTopLevelAsync(string source, string corrected, params DiagnosticResult[] diagnosticResults)
        {
            var test = GetTestTopLevelCS(source, corrected, ReferenceAssemblies.Net.Net80);

            for (int i = 0; i < diagnosticResults.Length; i++)
            {
                var expected = diagnosticResults[i];
                test.ExpectedDiagnostics.Add(expected);
            }

            await test.RunAsync();
        }

        private static VerifyVB.Test GetTestVB(string source, string corrected, ReferenceAssemblies referenceAssemblies)
        {
            var test = new VerifyVB.Test
            {
                TestCode = source,
                ReferenceAssemblies = referenceAssemblies,
                LanguageVersion = CodeAnalysis.VisualBasic.LanguageVersion.Latest,
                FixedCode = corrected,
            };
            return test;
        }

        private static async Task TestVBAsync(string source)
        {
            await GetTestVB(source, source, ReferenceAssemblies.Net.Net50).RunAsync();
            await GetTestVB(source, source, ReferenceAssemblies.NetCore.NetCoreApp31).RunAsync();
        }

        private static async Task TestVBAsync(string source, string corrected, params DiagnosticResult[] diagnosticResults)
        {
            var test = GetTestVB(source, corrected, ReferenceAssemblies.Net.Net50);

            for (int i = 0; i < diagnosticResults.Length; i++)
            {
                var expected = diagnosticResults[i];
                test.ExpectedDiagnostics.Add(expected);
            }

            await test.RunAsync();
            await GetTestVB(source, source, ReferenceAssemblies.NetCore.NetCoreApp31).RunAsync();
        }

        private static DiagnosticResult[] GetChainedCSDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(1),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(2)
                    };
        }

        private static DiagnosticResult[] GetChainedVBDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(1),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(2)
                    };
        }

        private static DiagnosticResult[] GetCreationDoubleInvokeCSDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(2),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(1)
                        .WithLocation(2),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(3)
                        .WithLocation(5),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(4)
                        .WithLocation(5),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(6)
                        .WithLocation(8),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(7)
                        .WithLocation(8)
                    };
        }

        private static DiagnosticResult[] GetCreationDoubleInvokeVBDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(2),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(1)
                        .WithLocation(2),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(3)
                        .WithLocation(5),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(4)
                        .WithLocation(5),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(6)
                        .WithLocation(8),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(7)
                        .WithLocation(8)
                    };
        }

        private static DiagnosticResult[] GetCreationSingleInvokeCSDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(1),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(2)
                        .WithLocation(3),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(4)
                        .WithLocation(5)
                    };
        }

        private static DiagnosticResult[] GetCreationSingleInvokeWithDisposeCSDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(3)
                        .WithLocation(4)
                        .WithLocation(5),
                        VerifyCS.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(6)
                        .WithLocation(7)
                        .WithLocation(8)
                    };
        }

        private static DiagnosticResult[] GetCreationSingleInvokeVBDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(1),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(2)
                        .WithLocation(3),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(4)
                        .WithLocation(5)
                    };
        }

        private static DiagnosticResult[] GetCreationSingleInvokeWithDisposeVBDiagnostics(string hashAlgorithmTypeName)
        {
            return new[] {
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(0)
                        .WithLocation(1)
                        .WithLocation(2),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(3)
                        .WithLocation(4)
                        .WithLocation(5),
                        VerifyVB.Diagnostic(PreferHashDataOverComputeHashAnalyzer.StringRule)
                        .WithArguments(hashAlgorithmTypeName)
                        .WithLocation(6)
                        .WithLocation(7)
                        .WithLocation(8)
                    };
        }
    }
}
