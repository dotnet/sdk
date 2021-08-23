// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureDtdProcessingAnalyzerTests
    {
        private static DiagnosticResult GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlDocumentWithNoSecureResolver).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlDocumentWithNoSecureResolver).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task XmlDocumentSetResolverToNullShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument()
            doc.XmlResolver = Nothing
        End Sub
    End Class
End Namespace
"
            );
        }

        [Fact]
        public async Task XmlDocumentSetResolverToNullInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument()
            {
                XmlResolver = null
            };
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetResolverToNullInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument()
        {
            XmlResolver = null
        };
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument() With { _
            .XmlResolver = Nothing _
        }
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetInsecureResolverInInitializerShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument() { XmlResolver = new XmlUrlResolver() };
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(8, 54)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument() With { _
            .XmlResolver = New XmlUrlResolver() _
        }
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(7, 13)
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldNoResolverPre452ShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument();
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(8, 34)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument()
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(6, 37)
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldNoResolverPost452ShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument();
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument()
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentUseSecureResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlSecureResolver resolver)
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = resolver;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(resolver As XmlSecureResolver)
            Dim doc As New XmlDocument()
            doc.XmlResolver = resolver
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentSetSecureResolverInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlSecureResolver resolver)
        {
            XmlDocument doc = new XmlDocument()
            {
                XmlResolver = resolver
            };
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(resolver As XmlSecureResolver)
            Dim doc As New XmlDocument() With { _
                .XmlResolver = resolver _
            }
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentUseSecureResolverWithPermissionsShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Net;
using System.Security;
using System.Security.Permissions;
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            PermissionSet myPermissions = new PermissionSet(PermissionState.None);
            WebPermission permission = new WebPermission(PermissionState.None);
            permission.AddPermission(NetworkAccess.Connect, ""http://www.contoso.com/"");
            permission.AddPermission(NetworkAccess.Connect, ""http://litwareinc.com/data/"");
            myPermissions.SetPermission(permission);
            XmlSecureResolver resolver = new XmlSecureResolver(new XmlUrlResolver(), myPermissions);

            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = resolver;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Net
Imports System.Security
Imports System.Security.Permissions
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim myPermissions As New PermissionSet(PermissionState.None)
            Dim permission As New WebPermission(PermissionState.None)
            permission.AddPermission(NetworkAccess.Connect, ""http://www.contoso.com/"")
            permission.AddPermission(NetworkAccess.Connect, ""http://litwareinc.com/data/"")
            myPermissions.SetPermission(permission)
            Dim resolver As New XmlSecureResolver(New XmlUrlResolver(), myPermissions)

            Dim doc As New XmlDocument()
            doc.XmlResolver = resolver
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentSetResolverToNullInTryClauseShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.XmlResolver = null;
            }
            catch { throw; }
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument()
            Try
                doc.XmlResolver = Nothing
            Catch
                Throw
            End Try
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentNoResolverPre452ShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument();
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(10, 31)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument()
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(7, 24)
            );
        }

        [Fact]
        public async Task XmlDocumentNoResolverPost452ShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument();
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument()
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentUseNonSecureResolverShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = new XmlUrlResolver();     // warn
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument()
            doc.XmlResolver = New XmlUrlResolver()
            ' warn
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(8, 13)
            );
        }

        [Fact]
        public async Task XmlDocumentUseNonSecureResolverInTryClauseShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        { 
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.XmlResolver = new XmlUrlResolver();    // warn
            }
            catch { throw; }
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(13, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument()
            Try
                    ' warn
                doc.XmlResolver = New XmlUrlResolver()
            Catch
                Throw
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(10, 17)
            );
        }

        [Fact]
        public async Task XmlDocumentReassignmentSetResolverToNullInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument();
            doc = new XmlDocument()
            {
                XmlResolver = null
            };
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument()
            doc = New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentReassignmentDefaultTargetPre452ShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument()
            {
                XmlResolver = null
            };
            doc = new XmlDocument();    // warn
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(14, 19)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
            doc = New XmlDocument()
            ' warn
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(10, 19)
            );
        }

        [Fact]
        public async Task XmlDocumentReassignmentDefaultTargetPost452ShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XmlDocument doc = new XmlDocument()
            {
                XmlResolver = null
            };
            doc = new XmlDocument();    // ok
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim doc As New XmlDocument() With { _
                .XmlResolver = Nothing _
            }
            doc = New XmlDocument()
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentSetResolversInDifferentBlockPre452ShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            {
                XmlDocument doc = new XmlDocument(); //warn
            }
            {
                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null;
            }
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(11, 35)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            If True Then
                Dim doc As New XmlDocument()
            End If
            If True Then
                Dim doc As New XmlDocument()
                doc.XmlResolver = Nothing
            End If
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(8, 28)
            );
        }

        [Fact]
        public async Task XmlDocumentSetResolversInDifferentBlockPost452ShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            {
                XmlDocument doc = new XmlDocument(); //ok in 4.5.2
            }
            {
                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null;
            }
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            If True Then
                Dim doc As New XmlDocument()
            End If
            If True Then
                Dim doc As New XmlDocument()
                doc.XmlResolver = Nothing
            End If
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetResolverToInsecureResolverInOnlyMethodPre452ShouldGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument();

        public void Method1()
        {
            this.doc.XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(8, 34),
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument()
        ' warn
        Public Sub Method1()
            Me.doc.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(6, 37),
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetResolverToInsecureResolverInOnlyMethodPost452ShouldGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument(); // ok

        public void Method1()
        {
            this.doc.XmlResolver = new XmlUrlResolver(); // warn
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument()
        ' ok
        Public Sub Method1()
            Me.doc.XmlResolver = New XmlUrlResolver()
        ' warn
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetResolverToInsecureResolverInSomeMethodPre452ShouldGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument();     // warn

        public void Method1()
        {
            this.doc.XmlResolver = null;
        }

        public void Method2()
        {
            this.doc.XmlResolver = new XmlUrlResolver();    // warn
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(8, 34),
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(17, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument()
        ' warn
        Public Sub Method1()
            Me.doc.XmlResolver = Nothing
        End Sub

        Public Sub Method2()
            Me.doc.XmlResolver = New XmlUrlResolver()
            ' warn
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(6, 37),
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetResolverToNullInSomeMethodPre452ShouldGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument(); // warn

        public void Method1()
        {
            this.doc.XmlResolver = null;
        }

        public void Method2(XmlReader reader)
        {
            this.doc.Load(reader);
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(8, 34)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument()

        Public Sub Method1()
            Me.doc.XmlResolver = Nothing
        End Sub

        Public Sub Method2(reader As XmlReader)
            Me.doc.Load(reader)
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(6, 37)
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetResolverToInsecureResolverInSomeMethodPost452ShouldGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument();     // ok

        public void Method1()
        {
            this.doc.XmlResolver = null;
        }

        public void Method2()
        {
            this.doc.XmlResolver = new XmlUrlResolver();    // warn
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(17, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
            Imports System.Xml

            Namespace TestNamespace
                Class TestClass
                    Public doc As XmlDocument = New XmlDocument()
                    ' ok
                    Public Sub Method1()
                        Me.doc.XmlResolver = Nothing
                    End Sub

                    Public Sub Method2()
                        Me.doc.XmlResolver = New XmlUrlResolver()
                        ' warn
                    End Sub
                End Class
            End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(13, 25)
            );
        }

        [Fact]
        public async Task XmlDocumentAsFieldSetResolverToNullInSomeMethodPost452ShouldNotGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlDocument doc = new XmlDocument();

        public void Method1()
        {
            this.doc.XmlResolver = null;
        }

        public void Method2(XmlReader reader)
        {
            this.doc.Load(reader);
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public doc As XmlDocument = New XmlDocument()

        Public Sub Method1()
            Me.doc.XmlResolver = Nothing
        End Sub

        Public Sub Method2(reader As XmlReader)
            Me.doc.Load(reader)
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentCreatedAsTempNotSetResolverPre452ShouldGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {

        public void Method1()
        {
            Method2(new XmlDocument());
        }

        public void Method2(XmlDocument doc){}
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(11, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net451.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1()
            Method2(New XmlDocument())
        End Sub

        Public Sub Method2(doc As XmlDocument)
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(8, 21)
            );
        }

        [Fact]
        public async Task XmlDocumentCreatedAsTempNotSetResolverPost452ShouldNotGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {

        public void Method1()
        {
            Method2(new XmlDocument());
        }

        public void Method2(XmlDocument doc){}
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1()
            Method2(New XmlDocument())
        End Sub

        Public Sub Method2(doc As XmlDocument)
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeNotSetResolverShouldNotGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass1 : XmlDocument
    {
        public TestClass1()
        {
            XmlResolver = null;
        }
    }     

    class TestClass2
    {
        void TestMethod()
        {
            var c = new TestClass1();
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass1
        Inherits XmlDocument
        Public Sub New()
            XmlResolver = Nothing
        End Sub
    End Class

    Class TestClass2
        Private Sub TestMethod()
            Dim c = New TestClass1()
        End Sub
    End Class
End Namespace
"
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeWithNoSecureResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

namespace TestNamespace
{
    class DerivedType : XmlDocument {}   

    class TestClass
    {
        void TestMethod()
        {
            var c = new DerivedType();
        }
    }
    
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class DerivedType
        Inherits XmlDocument
    End Class

    Class TestClass
        Private Sub TestMethod()
            Dim c = New DerivedType()
        End Sub
    End Class

End Namespace"
            );
        }
    }
}
