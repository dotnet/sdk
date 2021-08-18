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
        [Fact]
        public async Task Issue2753_CS_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlResolver resolver)
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = resolver;
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(11, 13));
        }

        [Fact]
        public async Task Issue2753_VB_DiagnosticAsync()
        {
            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Module SomeClass
    Public Sub LoadXmlSafe(resolver As XmlResolver)
        Dim doc As New XmlDocument()
        doc.XmlResolver = resolver
    End Sub
End Module",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(7, 9));
        }

        [Fact]
        public async Task XmlDocumentNoCtorSetResolverToNullShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc)
        {
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
        Private Shared Sub TestMethod(doc As XmlDocument)
            doc.XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentNoCtorUseSecureResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc, XmlSecureResolver resolver)
        {
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
        Private Shared Sub TestMethod(doc As XmlDocument, resolver As XmlSecureResolver)
            doc.XmlResolver = resolver
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentNoCtorUseSecureResolverWithPermissionsShouldNotGenerateDiagnosticAsync()
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
        private static void TestMethod(XmlDocument doc)
        {
            PermissionSet myPermissions = new PermissionSet(PermissionState.None);
            WebPermission permission = new WebPermission(PermissionState.None);
            permission.AddPermission(NetworkAccess.Connect, ""http://www.contoso.com/"");
            permission.AddPermission(NetworkAccess.Connect, ""http://litwareinc.com/data/"");
            myPermissions.SetPermission(permission);
            XmlSecureResolver resolver = new XmlSecureResolver(new XmlUrlResolver(), myPermissions);

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
        Private Shared Sub TestMethod(doc As XmlDocument)
            Dim myPermissions As New PermissionSet(PermissionState.None)
            Dim permission As New WebPermission(PermissionState.None)
            permission.AddPermission(NetworkAccess.Connect, ""http://www.contoso.com/"")
            permission.AddPermission(NetworkAccess.Connect, ""http://litwareinc.com/data/"")
            myPermissions.SetPermission(permission)
            Dim resolver As New XmlSecureResolver(New XmlUrlResolver(), myPermissions)

            doc.XmlResolver = resolver
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentNoCtorSetResolverToNullInTryClauseShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc)
        {
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
        Private Shared Sub TestMethod(doc As XmlDocument)
            Try
                doc.XmlResolver = Nothing
            Catch
                Throw
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentNoCtorUseNonSecureResolverInCatchClauseShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc)
        { 
            try {   }
            catch { 
                doc.XmlResolver = new XmlUrlResolver();
            }
            finally {}
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(12, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(doc As XmlDocument)
            Try
            Catch
                doc.XmlResolver = New XmlUrlResolver()
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(9, 17)
            );
        }

        [Fact]
        public async Task XmlDocumentNoCtorUseNonSecureResolverInFinallyClauseShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc)
        { 
            try {   }
            catch { throw; }
            finally {
                doc.XmlResolver = new XmlUrlResolver();
            }
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
        Private Shared Sub TestMethod(doc As XmlDocument)
            Try
            Catch
                Throw
            Finally
                doc.XmlResolver = New XmlUrlResolver()
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task XmlDocumentNoCtorDoNotSetResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc, XmlReader reader)
        {
            doc.Load(reader);
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
        Private Shared Sub TestMethod(doc As XmlDocument, reader As XmlReader)
            doc.Load(reader)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentNoCtorUseNonSecureResolverShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc)
        {
            doc.XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(10, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(doc As XmlDocument)
            doc.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(7, 13)
            );
        }

        [Fact]
        public async Task XmlDocumentNoCtorUseNonSecureResolverInTryClauseShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlDocument doc)
        { 
            try
            {
                doc.XmlResolver = new XmlUrlResolver();
            }
            catch { throw; }
        }
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(12, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(doc As XmlDocument)
            Try
                doc.XmlResolver = New XmlUrlResolver()
            Catch
                Throw
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(8, 17)
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetInsecureResolverShouldGenerateDiagnosticAsync()
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
            var c = new DerivedType(){ XmlResolver = new XmlUrlResolver() };
        }
    }
    
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(13, 40)
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
            Dim c = New DerivedType() With { _
                .XmlResolver = New XmlUrlResolver() _
            }
        End Sub
    End Class

End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(12, 17)
            );
        }

        [Fact]
        public async Task XmlDocumentCreatedAsTempSetResolverToNullShouldNotGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public void Method1()
        {
            Method2(new XmlDocument(){ XmlResolver = null });
        }

        public void Method2(XmlDocument doc){}
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1()
            Method2(New XmlDocument() With { _
                .XmlResolver = Nothing _
            })
        End Sub

        Public Sub Method2(doc As XmlDocument)
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlDocumentCreatedAsTempSetInsecureResolverShouldGenerateDiagnosticsAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {

        public void Method1()
        {
            Method2(new XmlDocument(){XmlResolver = new XmlUrlResolver()});
        }

        public void Method2(XmlDocument doc){}
    }
}",
                GetCA3075XmlDocumentWithNoSecureResolverCSharpResultAt(11, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1()
            Method2(New XmlDocument() With { _
                .XmlResolver = New XmlUrlResolver() _
            })
        End Sub

        Public Sub Method2(doc As XmlDocument)
        End Sub
    End Class
End Namespace",
                GetCA3075XmlDocumentWithNoSecureResolverBasicResultAt(9, 17)
            );
        }
    }
}
