// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotDisableCertificateValidation,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotDisableCertificateValidation,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotDisableCertificateValidationTests
    {
        [Fact]
        public async Task TestLambdaDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => { return true; };
    }
}",
            GetCSharpResultAt(8, 68));
        }

        [Fact]
        public async Task TestLambdaWithLiteralValueDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => true;
    }
}",
            GetCSharpResultAt(8, 68));
        }

        [Fact]
        public async Task TestAnonymousMethodDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback += delegate { return true; };
    }
}",
            GetCSharpResultAt(8, 68));
        }

        [Fact]
        public async Task TestDelegateCreationLocalFunctionDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);

        bool AcceptAllCertifications(
                  object sender,
                  X509Certificate certificate,
                  X509Chain chain,
                  SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}",
            GetCSharpResultAt(10, 67));
        }

        [Fact]
        public async Task TestDelegateCreationDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);
    }
}",
            GetCSharpResultAt(19, 67));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Net
Imports System.Net.Security
Imports System.Security.Cryptography.X509Certificates

Namespace TestNamespace
    Class TestClass
        Sub TestMethod()
            System.Net.ServicePointManager.ServerCertificateValidationCallback = New System.Net.Security.RemoteCertificateValidationCallback(AddressOf AcceptAllCertifications)
        End Sub

        Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
            Return True
        End Function
    End Class
End Namespace",
            GetBasicResultAt(9, 82));
        }

        [Fact]
        public async Task TestDelegateCreationNormalMethodWithLambdaDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors) => true;

    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);
    }
}",
            GetCSharpResultAt(16, 67));
        }

        [Fact]
        public async Task TestDelegatedMethodFromDifferentAssemblyNoDiagnostic()
        {
            string source1 = @"

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace AcceptAllCertificationsNamespace
{
    public class AcceptAllCertificationsClass
    {
        public static bool AcceptAllCertifications(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}";

            var source2 = @"
using System.Net;
using System.Net.Security;
using AcceptAllCertificationsNamespace;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertificationsClass.AcceptAllCertifications);
    }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source2 },
                    AdditionalProjects =
                    {
                        ["DependencyProject"] =
                        {
                            Sources = { ("Dependency.cs", source1) },
                        },
                    },
                    AdditionalProjectReferences = { "DependencyProject" },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestDelegatedMethodFromLocalFromDifferentAssemblyNoDiagnostic()
        {
            string source1 = @"

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace AcceptAllCertificationsNamespace
{
    public class AcceptAllCertificationsClass
    {
        public static bool AcceptAllCertifications2(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }

}";

            var source2 = @"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using AcceptAllCertificationsNamespace;

class TestClass
{
    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        return AcceptAllCertificationsClass.AcceptAllCertifications2(sender, certificate, chain, sslPolicyErrors);
    }

    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);
    }
}";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source2 },
                    AdditionalProjects =
                    {
                        ["DependencyProject"] =
                        {
                            Sources = { ("Dependency.cs", source1) },
                        },
                    },
                    AdditionalProjectReferences = { "DependencyProject" },
                },
            }.RunAsync();
        }

        [Fact]
        public async Task TestLambdaNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => { if(a != null) {return true;} return false;};
    }
}");
        }

        [Fact]
        public async Task TestLambdaWithLiteralValueNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => false;
    }
}");
        }

        [Fact]
        public async Task TestAnonymousMethodNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback += delegate { return false; };
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationLocalFunctionNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);

        bool AcceptAllCertifications(
                  object sender,
                  X509Certificate certificate,
                  X509Chain chain,
                  SslPolicyErrors sslPolicyErrors)
        {
            if(sender != null)
            {
                return true;
            }

            return false;
        }
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        if(sender != null)
        {
            return true;
        }
        return false;
    }

    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Net
Imports System.Net.Security
Imports System.Security.Cryptography.X509Certificates

Public Module TestModule
    Sub TestMethod()
        System.Net.ServicePointManager.ServerCertificateValidationCallback = New System.Net.Security.RemoteCertificateValidationCallback(AddressOf AcceptAllCertifications)
    End Sub

    Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        If sender IsNot Nothing
            Return True
        Else
            Return False
        End If
    End Function
End Module");
        }

        [Fact]
        public async Task TestDelegateCreationNoDiagnostic2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    public void TestMethod()
    {
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationNormalMethodWithLambdaNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors) => false;

    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);
    }
}");
        }

        [Fact]
        public async Task TestDelegateCreationFromLocalFromLocalNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public bool AcceptAllCertifications2(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        return AcceptAllCertifications2(
          sender,
          certificate,
          chain,
          sslPolicyErrors);
    }

    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Net
Imports System.Net.Security
Imports System.Security.Cryptography.X509Certificates

Public Module TestModule
    Sub TestMethod()
        System.Net.ServicePointManager.ServerCertificateValidationCallback = New System.Net.Security.RemoteCertificateValidationCallback(AddressOf AcceptAllCertifications)
    End Sub

    Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        Return AcceptAllCertifications2(sender, certification, chain, sslPolicyErrors)
    End Function

    Function AcceptAllCertifications2(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        Return True
    End Function
End Module");
        }

        [Fact]
        public async Task TestDelegateCreationFromLocalFromLocal2NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class TestClass
{
    public bool AcceptAllCertifications2(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    public bool AcceptAllCertifications(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        var a = 5;
        if(a > 1)
        {
            return true;
        }
        else
        {
            return AcceptAllCertifications2(
              sender,
              certificate,
              chain,
              sslPolicyErrors);
        }
    }

    public void TestMethod()
    {
        ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(AcceptAllCertifications);
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}