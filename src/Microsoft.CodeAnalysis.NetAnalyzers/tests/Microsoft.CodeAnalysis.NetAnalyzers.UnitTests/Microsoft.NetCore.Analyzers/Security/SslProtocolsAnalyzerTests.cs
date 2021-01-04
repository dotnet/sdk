// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.SslProtocolsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.SslProtocolsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class SslProtocolsAnalyzerTests
    {
        [Fact]
        public async Task DocSample1_CSharp_Violation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5397 violation for using Tls11
        SslProtocols protocols = SslProtocols.Tls11 | SslProtocols.Tls12;
    }
}",
            GetCSharpResultAt(10, 34, SslProtocolsAnalyzer.DeprecatedRule, "Tls11"),
            GetCSharpResultAt(10, 55, SslProtocolsAnalyzer.HardcodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample1_VB_Violation()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Security.Authentication

Public Class TestClass
    Public Sub ExampleMethod()
        ' CA5397 violation for using Tls11
        Dim sslProtocols As SslProtocols = SslProtocols.Tls11 Or SslProtocols.Tls12
    End Sub
End Class
",
            GetBasicResultAt(8, 44, SslProtocolsAnalyzer.DeprecatedRule, "Tls11"),
            GetBasicResultAt(8, 66, SslProtocolsAnalyzer.HardcodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample2_CSharp_Violation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5397 violation
        SslProtocols sslProtocols = (SslProtocols) 768;    // TLS 1.1
    }
}",
            GetCSharpResultAt(10, 37, SslProtocolsAnalyzer.DeprecatedRule, "768"));
        }

        [Fact]
        public async Task DocSample2_VB_Violation()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Security.Authentication

Public Class TestClass
    Public Sub ExampleMethod()
        ' CA5397 violation
        Dim sslProtocols As SslProtocols = CType(768, SslProtocols)   ' TLS 1.1
    End Sub
End Class
",
            GetBasicResultAt(8, 44, SslProtocolsAnalyzer.DeprecatedRule, "768"));
        }

        [Fact]
        public async Task DocSample1_CSharp_Solution()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

public class TestClass
{
    public void Method()
    {
        // Let the operating system decide what TLS protocol version to use.
        // See https://docs.microsoft.com/dotnet/framework/network-programming/tls
        SslProtocols sslProtocols = SslProtocols.None;
    }
}");
        }

        [Fact]
        public async Task DocSample1_VB_Solution()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Security.Authentication

Public Class TestClass
    Public Sub ExampleMethod()
        ' Let the operating system decide what TLS protocol version to use.
        ' See https://docs.microsoft.com/dotnet/framework/network-programming/tls
        Dim sslProtocols As SslProtocols = SslProtocols.None
    End Sub
End Class
");
        }

        [Fact]
        public async Task DocSample3_CSharp_Violation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

public class ExampleClass
{
    public void ExampleMethod()
    {
        // CA5398 violation
        SslProtocols sslProtocols = SslProtocols.Tls12;
    }
}",
            GetCSharpResultAt(10, 37, SslProtocolsAnalyzer.HardcodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample3_VB_Violation()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Security.Authentication

Public Class TestClass
    Public Function ExampleMethod() As SslProtocols
        ' CA5398 violation
        Return SslProtocols.Tls12
    End Function
End Class
",
            GetBasicResultAt(8, 16, SslProtocolsAnalyzer.HardcodedRule, "Tls12"));
        }

        [Fact]
        public async Task DocSample4_CSharp_Violation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

public class ExampleClass
{
    public SslProtocols ExampleMethod()
    {
        // CA5398 violation
        return (SslProtocols) 3072;    // TLS 1.2
    }
}",
            GetCSharpResultAt(10, 16, SslProtocolsAnalyzer.HardcodedRule, "3072"));
        }

        [Fact]
        public async Task DocSample4_VB_Violation()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Security.Authentication

Public Class TestClass
    Public Function ExampleMethod() As SslProtocols
        ' CA5398 violation
        Return CType(3072, SslProtocols)   ' TLS 1.2
    End Function
End Class
",
            GetBasicResultAt(8, 16, SslProtocolsAnalyzer.HardcodedRule, "3072"));
        }

        [Fact]
        public async Task Argument_Ssl2_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void Method(SslStream sslStream, string targetHost, X509CertificateCollection clientCertificates)
    {
        sslStream.AuthenticateAsClient(targetHost, clientCertificates, SslProtocols.Ssl2, false);
    }
}",
            GetCSharpResultAt(11, 72, SslProtocolsAnalyzer.DeprecatedRule, "Ssl2"));
        }

        [Fact]
        public async Task Argument_Tls12_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void Method(SslStream sslStream, string targetHost, X509CertificateCollection clientCertificates)
    {
        sslStream.AuthenticateAsClient(targetHost, clientCertificates, SslProtocols.Tls12, false);
    }
}",
            GetCSharpResultAt(11, 72, SslProtocolsAnalyzer.HardcodedRule, "Tls12"));
        }

        [Fact]
        public async Task Argument_None_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void Method(SslStream sslStream, string targetHost, X509CertificateCollection clientCertificates)
    {
        sslStream.AuthenticateAsClient(targetHost, clientCertificates, SslProtocols.None, false);
    }
}");
        }

        [Fact]
        public async Task UseSsl3_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        var a = SslProtocols.Ssl3;
    }
}",
            GetCSharpResultAt(9, 17, SslProtocolsAnalyzer.DeprecatedRule, "Ssl3"));
        }

        [Fact]
        public async Task UseTls_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        var a = SslProtocols.Tls;
    }
}",
            GetCSharpResultAt(9, 17, SslProtocolsAnalyzer.DeprecatedRule, "Tls"));
        }

        [Fact]
        public async Task UseTls11_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        SslProtocols protocols = SslProtocols.Tls11;
    }
}",
            GetCSharpResultAt(9, 34, SslProtocolsAnalyzer.DeprecatedRule, "Tls11"));
        }

        [Fact]
        public async Task UseSystemDefault_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        var a = SslProtocols.Default;
    }
}",
                GetCSharpResultAt(9, 17, SslProtocolsAnalyzer.DeprecatedRule, "Default"));
        }

        [Fact]
        public async Task UseTls12_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        SslProtocols protocols = SslProtocols.Tls12;
    }
}",
                GetCSharpResultAt(9, 34, SslProtocolsAnalyzer.HardcodedRule, "Tls12"));
        }

        [Fact]
        public async Task UseTls13_Diagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net48.Default,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        SslProtocols protocols = SslProtocols.Tls13;
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCSharpResultAt(9, 34, SslProtocolsAnalyzer.HardcodedRule, "Tls13"),
                    },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task UseTls12OrdTls11_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        SslProtocols protocols = SslProtocols.Tls12 | SslProtocols.Tls11;
    }
}",
                GetCSharpResultAt(9, 34, SslProtocolsAnalyzer.HardcodedRule, "Tls12"),
                GetCSharpResultAt(9, 55, SslProtocolsAnalyzer.DeprecatedRule, "Tls11"));
        }

        [Fact]
        public async Task Use192CompoundAssignment_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public SslProtocols SslProtocols { get; set; }

    public void Method()
    {
        this.SslProtocols |= (SslProtocols)192;
    }
}",
                GetCSharpResultAt(11, 30, SslProtocolsAnalyzer.DeprecatedRule, "192"));
        }

        [Fact]
        public async Task Use384SimpleAssignment_Diagnostic()
        {
            // 384 = SchProtocols.Tls11Server | SchProtocols.Tls10Client
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public SslProtocols SslProtocols { get; set; }

    public void Method()
    {
        this.SslProtocols = (SslProtocols)384;
    }
}",
                GetCSharpResultAt(11, 29, SslProtocolsAnalyzer.DeprecatedRule, "384"));
        }

        [Fact]
        public async Task Use768SimpleAssignmentOrExpression_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public SslProtocols SslProtocols { get; set; }

    public void Method(SslProtocols input)
    {
        this.SslProtocols = input | (SslProtocols)768;
    }
}",
                GetCSharpResultAt(11, 37, SslProtocolsAnalyzer.DeprecatedRule, "768"));
        }

        [Fact]
        public async Task Use12288SimpleAssignmentOrExpression_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public SslProtocols SslProtocols { get; set; }

    public void Method(SslProtocols input)
    {
        this.SslProtocols = input | (SslProtocols)12288;
    }
}",
                GetCSharpResultAt(11, 37, SslProtocolsAnalyzer.HardcodedRule, "12288"));
        }

        [Fact]
        public async Task UseTls12OrTls11Or192_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public SslProtocols SslProtocols { get; set; }

    public void Method()
    {
        this.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | (SslProtocols)192;
    }
}",
                GetCSharpResultAt(11, 29, SslProtocolsAnalyzer.HardcodedRule, "Tls12"),
                GetCSharpResultAt(11, 50, SslProtocolsAnalyzer.DeprecatedRule, "Tls11"));
        }

        [Fact]
        public async Task UseTls12Or192_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        SslProtocols protocols = SslProtocols.Tls12 | (SslProtocols)192;
    }
}",
                VerifyCS.Diagnostic(SslProtocolsAnalyzer.HardcodedRule).WithSpan(9, 34, 9, 52).WithArguments("Tls12"),
                VerifyCS.Diagnostic(SslProtocolsAnalyzer.DeprecatedRule).WithSpan(9, 34, 9, 72).WithArguments("3264"));
        }

        [Fact]
        public async Task Use768DeconstructionAssignment_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public SslProtocols SslProtocols { get; set; }

    public void Method()
    {
        int i;
        (this.SslProtocols, i) = ((SslProtocols)384, 384);
    }
}");
            // Ideally we'd handle the IDeconstructionAssignment, but this code pattern seems unlikely.
        }

        [Fact]
        public async Task Use24Plus24SimpleAssignment_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        SslProtocols sslProtocols = (SslProtocols)(24 + 24);
    }
}",
                GetCSharpResultAt(9, 37, SslProtocolsAnalyzer.DeprecatedRule, "48"));
        }

        [Fact]
        public async Task Use768NotSslProtocols_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Security.Authentication;

class TestClass
{
    public void Method()
    {
        int i = 384 | 768;
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}
