// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.ApprovedCipherModeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.ApprovedCipherModeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class ApprovedCipherModeTests
    {
        [Fact]
        public async Task TestECBModeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Security.Cryptography;

class TestClass {
    private static void TestMethod () {
        RijndaelManaged rijn = new RijndaelManaged();
        rijn.Mode  = CipherMode.ECB;
    }
}",
            GetCSharpResultAt(9, 22, "ECB"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Public Module SecurityCenter
    Sub TestSub()
        Dim encripter As System.Security.Cryptography.Aes = System.Security.Cryptography.Aes.Create(""AES"")
        encripter.Mode = CipherMode.ECB
    End Sub
End Module",
            GetBasicResultAt(7, 26, "ECB"));
        }

        [Fact]
        public async Task TestOFBModeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Security.Cryptography;

class TestClass {
    private static void TestMethod () {
        RijndaelManaged rijn = new RijndaelManaged();
        rijn.Mode  = CipherMode.OFB;
    }
}",
            GetCSharpResultAt(9, 22, "OFB"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Public Module SecurityCenter
    Sub TestSub()
        Dim encripter As System.Security.Cryptography.Aes = System.Security.Cryptography.Aes.Create(""AES"")
        encripter.Mode = CipherMode.OFB
    End Sub
End Module",
            GetBasicResultAt(7, 26, "OFB"));
        }

        [Fact]
        public async Task TestCFBModeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Security.Cryptography;

class TestClass {
    private static void TestMethod () {
        RijndaelManaged rijn = new RijndaelManaged();
        rijn.Mode  = CipherMode.CFB;;
    }
}",
            GetCSharpResultAt(9, 22, "CFB"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Public Module SecurityCenter
    Sub TestSub()
        Dim encripter As System.Security.Cryptography.Aes = System.Security.Cryptography.Aes.Create(""AES"")
        encripter.Mode = CipherMode.CFB
    End Sub
End Module",
            GetBasicResultAt(7, 26, "CFB"));
        }

        [Fact]
        public async Task TestCBCModeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Security.Cryptography;

class TestClass {
    private static void TestMethod () {
        RijndaelManaged rijn = new RijndaelManaged();
        rijn.Mode  = CipherMode.CBC;
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Public Module SecurityCenter
    Sub TestSub()
        Dim encripter As System.Security.Cryptography.Aes = System.Security.Cryptography.Aes.Create(""AES"")
        encripter.Mode = CipherMode.CBC
    End Sub
End Module"
            );
        }

        [Fact]
        public async Task TestCTSModeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Security.Cryptography;

class TestClass {
    private static void TestMethod () {
        RijndaelManaged rijn = new RijndaelManaged();
        rijn.Mode  = CipherMode.CTS;
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Security.Cryptography

Public Module SecurityCenter
    Sub TestSub()
        Dim encripter As System.Security.Cryptography.Aes = System.Security.Cryptography.Aes.Create(""AES"")
        encripter.Mode = CipherMode.CTS
    End Sub
End Module"
            );
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}