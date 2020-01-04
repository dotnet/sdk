// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        {
            return new DiagnosticResult(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseInsecureDtdProcessing).WithLocation(line, column).WithArguments(MicrosoftNetFrameworkAnalyzersResources.XmlDocumentWithNoSecureResolverMessage);
        }

        private static DiagnosticResult GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(int line, int column)
        {
            return new DiagnosticResult(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseInsecureDtdProcessing).WithLocation(line, column).WithArguments(MicrosoftNetFrameworkAnalyzersResources.XmlDocumentWithNoSecureResolverMessage);
        }


        [Fact]
        public async Task XmlDocumentSetResolverToNullShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentSetResolverToNullInInitializerShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentAsFieldSetResolverToNullInInitializerShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentAsFieldSetInsecureResolverInInitializerShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentAsFieldNoResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentUseSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentSetSecureResolverInInitializerShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentUseSecureResolverWithPermissionsShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentSetResolverToNullInTryClauseShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentNoResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentUseNonSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentUseNonSecureResolverInTryClauseShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentReassignmentSetResolverToNullInInitializerShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentReassignmentDefaultShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentSetResolversInDifferentBlock()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            {
                XmlDocument doc = new XmlDocument();
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentAsFieldSetResolverToInsecureResolverInOnlyMethodShouldGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentAsFieldSetResolverToInsecureResolverInSomeMethodShouldGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentAsFieldSetResolverToNullInSomeMethodShouldGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(8, 34)
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentCreatedAsTempNotSetResolverShouldGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeNotSetResolverShouldNotGenerateDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeWithNoSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
