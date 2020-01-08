// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotHardCodeCertificateTests : TaintedDataAnalyzerTestBase
    {
        public DoNotHardCodeCertificateTests(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override DiagnosticDescriptor Rule => DoNotHardCodeCertificate.Rule;

        [Fact]
        public void Test_Source_ContantByteArray_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}",
            GetCSharpResultAt(11, 9, 9, 24, "X509Certificate.X509Certificate(string fileName)", "void TestClass.TestMethod(string path)", "byte[]", "void TestClass.TestMethod(string path)"));
        }

        [Fact]
        public void Test_Source_ConvertFromBase64String_WithConstantString_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path)
    {
        byte[] bytes = Convert.FromBase64String(""AAAAAaazaoensuth"");
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}",
            GetCSharpResultAt(12, 9, 10, 24, "X509Certificate.X509Certificate(string fileName)", "void TestClass.TestMethod(string path)", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod(string path)"));
        }

        [Fact]
        public void Test_Source_ASCIIEncodingGetBytes_WithConstantString_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Text;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path)
    {
        byte[] bytes = new ASCIIEncoding().GetBytes(""AAAAAaazaoensuth"");
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}",
            GetCSharpResultAt(12, 9, 10, 24, "X509Certificate.X509Certificate(string fileName)", "void TestClass.TestMethod(string path)", "byte[] Encoding.GetBytes(string s)", "void TestClass.TestMethod(string path)"));
        }

        [Fact]
        public void Test_Source_EncodingUTF8GetBytes_WithConstantString_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Text;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(""AAAAAaazaoensuth"");
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}",
            GetCSharpResultAt(12, 9, 10, 24, "X509Certificate.X509Certificate(string fileName)", "void TestClass.TestMethod(string path)", "byte[] Encoding.GetBytes(string s)", "void TestClass.TestMethod(string path)"));
        }

        [Fact]
        public void Test_Source_ASCIIEncodingGetBytes_WithStringAndInt32AndInt32AndByteArrayAndInt32Parameters_WithConstantString_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Text;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(byte[] bytes, string path)
    {
        new ASCIIEncoding().GetBytes(""AAAAAaazaoensuth"", 0, 3, bytes, 0);
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}",
            GetCSharpResultAt(12, 9, 10, 38, "X509Certificate.X509Certificate(string fileName)", "void TestClass.TestMethod(byte[] bytes, string path)", "string chars", "int ASCIIEncoding.GetBytes(string chars, int charIndex, int charCount, byte[] bytes, int byteIndex)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithStringAndSecureStringAndX509KeyStorageFlagsParameters_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, SecureString password, X509KeyStorageFlags keyStorageFlags)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path, password, keyStorageFlags);
    }
}",
            GetCSharpResultAt(12, 9, 10, 24, "X509Certificate.X509Certificate(string fileName, SecureString password, X509KeyStorageFlags keyStorageFlags)", "void TestClass.TestMethod(string path, SecureString password, X509KeyStorageFlags keyStorageFlags)", "byte[]", "void TestClass.TestMethod(string path, SecureString password, X509KeyStorageFlags keyStorageFlags)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithByteArrayAndStringAndX509KeyStorageFlagsParameters_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, string password, X509KeyStorageFlags keyStorageFlags)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(bytes, password, keyStorageFlags);
    }
}",
            GetCSharpResultAt(11, 9, 9, 24, "X509Certificate.X509Certificate(byte[] rawData, string password, X509KeyStorageFlags keyStorageFlags)", "void TestClass.TestMethod(string path, string password, X509KeyStorageFlags keyStorageFlags)", "byte[]", "void TestClass.TestMethod(string path, string password, X509KeyStorageFlags keyStorageFlags)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithStringAndStringParameters_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, string password)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(bytes, password);
    }
}",
            GetCSharpResultAt(11, 9, 9, 24, "X509Certificate.X509Certificate(byte[] rawData, string password)", "void TestClass.TestMethod(string path, string password)", "byte[]", "void TestClass.TestMethod(string path, string password)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithStringAndSecureStringParameters_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, SecureString password)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(bytes, password);
    }
}",
            GetCSharpResultAt(12, 9, 10, 24, "X509Certificate.X509Certificate(byte[] rawData, SecureString password)", "void TestClass.TestMethod(string path, SecureString password)", "byte[]", "void TestClass.TestMethod(string path, SecureString password)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithStringAndStringAndX509KeyStorageFlagsParameters_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, string password, X509KeyStorageFlags keyStorageFlags)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(bytes, password, keyStorageFlags);
    }
}",
            GetCSharpResultAt(11, 9, 9, 24, "X509Certificate.X509Certificate(byte[] rawData, string password, X509KeyStorageFlags keyStorageFlags)", "void TestClass.TestMethod(string path, string password, X509KeyStorageFlags keyStorageFlags)", "byte[]", "void TestClass.TestMethod(string path, string password, X509KeyStorageFlags keyStorageFlags)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithByteArrayAndSecureStringParameters_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, SecureString password)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(bytes, password);
    }
}",
            GetCSharpResultAt(12, 9, 10, 24, "X509Certificate.X509Certificate(byte[] rawData, SecureString password)", "void TestClass.TestMethod(string path, SecureString password)", "byte[]", "void TestClass.TestMethod(string path, SecureString password)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithByteArrayParameter_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, string password)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(bytes);
    }
}",
            GetCSharpResultAt(11, 9, 9, 24, "X509Certificate.X509Certificate(byte[] data)", "void TestClass.TestMethod(string path, string password)", "byte[]", "void TestClass.TestMethod(string path, string password)"));
        }

        [Fact]
        public void Test_Sink_X509Certificate_WithByteArrayAndStringParameters_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path, string password)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate(bytes, password);
    }
}",
            GetCSharpResultAt(11, 9, 9, 24, "X509Certificate.X509Certificate(byte[] rawData, string password)", "void TestClass.TestMethod(string path, string password)", "byte[]", "void TestClass.TestMethod(string path, string password)"));
        }

        [Fact]
        public void Test_X509Certificates2_Diagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string path)
    {
        byte[] bytes = new byte[] {1, 2, 3};
        File.WriteAllBytes(path, bytes);
        new X509Certificate2(path);
    }
}",
            GetCSharpResultAt(11, 9, 9, 24, "X509Certificate2.X509Certificate2(string fileName)", "void TestClass.TestMethod(string path)", "byte[]", "void TestClass.TestMethod(string path)"));
        }

        // For now, we didn't take serialization into consideration.
        [Fact]
        public void Test_Sink_X509Certificate_WithSerializationInfoAndStreamingContextParameters_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(SerializationInfo info, StreamingContext context)
    {
        new X509Certificate(info, context);
    }
}");
        }

        [Fact]
        public void Test_Source_NotContantByteArray_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(byte[] bytes, string path)
    {
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}");
        }

        [Fact]
        public void Test_Source_ConvertFromBase64String_WithNotConstantString_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(string s, string path)
    {
        byte[] bytes = Convert.FromBase64String(s);
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}");
        }

        [Fact]
        public void Test_X509Certificate2_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(byte[] bytes, string path)
    {
        File.WriteAllBytes(path, bytes);
        new X509Certificate2(path);
    }
}");
        }

        [Fact]
        public void Test_Source_ASCIIEncodingGetBytes_WithCharArrayAndInt32AndInt32AndByteArrayAndInt32Parameters_WithConstantCharArray_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.IO;
using System.Text;

using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod(byte[] bytes, string path)
    {
        char[] chars = new char[] {'1', '2', '3'};
        new ASCIIEncoding().GetBytes(chars, 0, 3, bytes, 0);
        File.WriteAllBytes(path, bytes);
        new X509Certificate(path);
    }
}");
        }

        // Didn't find out what causes NRE.
        [Fact, WorkItem(3012, "https://github.com/dotnet/roslyn-analyzers/issues/3012")]
        public void Test_ExampleCodeFromTheIssue_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

class Constants
{
    public static Regex UnhashedNameIdRegex = new Regex(@""^[a-zA-Z0-9]\d{2}[a-zA-Z0-9](-\d{3}){2}[A-Za-z0-9]$"");
}

class TestClass
{
    public static string Calculate(string unhashedNameId)
    {
        if (string.IsNullOrWhiteSpace(unhashedNameId))
        {
            throw new ArgumentNullException(nameof(unhashedNameId), $""{ nameof(unhashedNameId)} must not be null, empty or whitespace."");
        }

        if (!Constants.UnhashedNameIdRegex.IsMatch(unhashedNameId))
        {
            throw new ArgumentException($""{ nameof(unhashedNameId)} does not match '{Constants.UnhashedNameIdRegex}'."", nameof(unhashedNameId));
        }

        using (var sha = new SHA256Managed())
        {
            byte[] textData = Encoding.UTF8.GetBytes(unhashedNameId);
            byte[] crypto = sha.ComputeHash(textData);

            var nameId = new StringBuilder();
            foreach (byte hash in crypto)
            {
                nameId.Append(hash.ToString(""x2"", CultureInfo.InvariantCulture));
            }

            return nameId.ToString();
        }
    }
}");
        }

        [Fact]
        public void Test_NullCfg_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;

public class TestClass
{
    public static string ContentName => ""Satya"";

    public static readonly byte[] ByteArray = Convert.FromBase64String(""Some strings."");
}");
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotHardCodeCertificate();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotHardCodeCertificate();
        }
    }
}
