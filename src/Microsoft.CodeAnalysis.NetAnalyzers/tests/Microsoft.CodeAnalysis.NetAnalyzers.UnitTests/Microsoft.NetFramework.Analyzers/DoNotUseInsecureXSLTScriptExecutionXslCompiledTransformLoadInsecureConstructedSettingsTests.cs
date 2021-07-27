// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureXSLTScriptExecutionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureXSLTScriptExecutionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureXSLTScriptExecutionAnalyzerTests
    {
        private static DiagnosticResult GetCA3076LoadCSharpResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureInputMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3076LoadBasicResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureInputMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task Issue2752()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml.Xsl

Public Module SomeClass
    Friend Sub method()
        Dim internalSettings As New XsltSettings(False, False)
    End Sub
End Module

Public Class MDIMain
    Public Sub New()
    End Sub
End Class");
        }

        [Fact]
        public async Task Issue2752_WorkAround()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml.Xsl

Public Module SomeClass
    Friend Sub method()
        Dim internalSettings As New XsltSettings()
        internalSettings.EnableDocumentFunction = False
        internalSettings.EnableScript = False
    End Sub
End Module

Public Class MDIMain
    Public Sub New()
    End Sub
End Class");
        }

        [Fact]
        public async Task Issue2752_WorkAround2()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml.Xsl

Public Module SomeClass
    Friend Sub method()
        Dim internalSettings = New XsltSettings(False, False)
    End Sub
End Module

Public Class MDIMain
    Public Sub New()
    End Sub
End Class");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload1ShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(IXPathNavigable stylesheet)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            xslCompiledTransform.Load(stylesheet);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl
Imports System.Xml.XPath

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheet As IXPathNavigable)
            Dim xslCompiledTransform As New XslCompiledTransform()
            xslCompiledTransform.Load(stylesheet)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload1InTryBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml.Xsl;
using System.Xml.XPath;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(IXPathNavigable stylesheet)
        {
            try
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(stylesheet);
            }
            catch { throw; }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml.Xsl
Imports System.Xml.XPath

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheet As IXPathNavigable)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(stylesheet)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload1InCatchBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml.Xsl;
using System.Xml.XPath;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(IXPathNavigable stylesheet)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(stylesheet);
            }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml.Xsl
Imports System.Xml.XPath

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheet As IXPathNavigable)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(stylesheet)
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload1InFinallyBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml.Xsl;
using System.Xml.XPath;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(IXPathNavigable stylesheet)
        {
            try {   }
            catch { throw; }
            finally {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(stylesheet);
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml.Xsl
Imports System.Xml.XPath

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheet As IXPathNavigable)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(stylesheet)
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload2ShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(String stylesheetUri)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            xslCompiledTransform.Load(stylesheetUri);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheetUri As [String])
            Dim xslCompiledTransform As New XslCompiledTransform()
            xslCompiledTransform.Load(stylesheetUri)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload2InTryBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(String stylesheetUri)
        {
            try
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(stylesheetUri);
            }
            catch { throw; }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheetUri As [String])
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(stylesheetUri)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload2InCatchBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(String stylesheetUri)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(stylesheetUri);            
            }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheetUri As [String])
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(stylesheetUri)
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload2InFinallyBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(String stylesheetUri)
        {
            try {   }
            catch { throw; }
            finally { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(stylesheetUri);    
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(stylesheetUri As [String])
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(stylesheetUri)
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload3ShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(Type compiledStylesheet)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            xslCompiledTransform.Load(compiledStylesheet);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(compiledStylesheet As Type)
            Dim xslCompiledTransform As New XslCompiledTransform()
            xslCompiledTransform.Load(compiledStylesheet)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload3InTryBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(Type compiledStylesheet)
        {
            try
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(compiledStylesheet);
            }
            catch { throw; }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(compiledStylesheet As Type)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(compiledStylesheet)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload3InCatchBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(Type compiledStylesheet)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(compiledStylesheet);            
            }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(compiledStylesheet As Type)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(compiledStylesheet)
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSecureOverload3InFinallyBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(Type compiledStylesheet)
        {
            try {   }
            catch { throw; }
            finally { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                xslCompiledTransform.Load(compiledStylesheet);    
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(compiledStylesheet As Type)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                xslCompiledTransform.Load(compiledStylesheet)
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadTrustedXsltAndNonSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings = System.Xml.Xsl.XsltSettings.TrustedXslt;
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(14, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = System.Xml.Xsl.XsltSettings.TrustedXslt
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(11, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadTrustedXsltAndNullResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings = System.Xml.Xsl.XsltSettings.TrustedXslt;
            xslCompiledTransform.Load(""testStylesheet"", settings, null);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = System.Xml.Xsl.XsltSettings.TrustedXslt
            xslCompiledTransform.Load("""", settings, Nothing)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadTrustedSourceAndSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        { 
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings = System.Xml.Xsl.XsltSettings.TrustedXslt;
            var resolver = new XmlSecureResolver(new XmlUrlResolver(), """");
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
	Class TestClass
		Private Shared Sub TestMethod()
			Dim xslCompiledTransform As New XslCompiledTransform()
			Dim settings = System.Xml.Xsl.XsltSettings.TrustedXslt
			Dim resolver = New XmlSecureResolver(New XmlUrlResolver(), """")
			xslCompiledTransform.Load(""testStylesheet"", settings, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadDefaultAndNonSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings();
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings()
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadDefaultAndSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings();
            xslCompiledTransform.Load(""testStylesheet"", settings, null);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings()
            xslCompiledTransform.Load("""", settings, Nothing)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadDefaultPropertyAndNonSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  XsltSettings.Default;
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = XsltSettings.[Default]
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadEnableScriptAndNonSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings() { EnableScript = true };
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(14, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings() With { _
                .EnableScript = True _
            }
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(13, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSetEnableScriptToTrueAndNonSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings();
            settings.EnableScript = true;
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings()
            settings.EnableScript = True
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(12, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadEnableDocumentFunctionAndNonSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings() { EnableDocumentFunction = true };
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(14, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings() With { _
                .EnableDocumentFunction = True _
            }
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(13, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSetEnableDocumentFunctionToTrueAndNonSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings();
            settings.EnableDocumentFunction = true;
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings()
            settings.EnableDocumentFunction = True
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(12, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSetEnableDocumentFunctionToTrueAndSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings() { EnableDocumentFunction = true };
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, null);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings() With { _
                .EnableDocumentFunction = True _
            }
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, Nothing)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadSetEnableScriptPropertyToTrueAndSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings();
            settings.EnableScript = true;
            xslCompiledTransform.Load(""testStylesheet"", settings, null);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings()
            settings.EnableScript = True
            xslCompiledTransform.Load("""", settings, Nothing)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadConstructSettingsWithTrueParamAndNonSecureResolverShouldGenerateDiagnostic1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings(true, false);
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(14, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings(True, False)
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(11, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadConstructSettingsWithTrueParamAndNonSecureResolverShouldGenerateDiagnostic2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings(false, true);
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(14, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings(False, True)
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(11, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadConstructSettingsWithFalseParamsAndNonSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var settings =  new XsltSettings(false, false);
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim settings = New XsltSettings(False, False)
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadNullSettingsAndNonSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", null, resolver);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", Nothing, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadDefaultAsArgumentAndNonSecureResolverShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", XsltSettings.Default, resolver);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", XsltSettings.[Default], resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadTrustedXsltAsArgumentAndNonSecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod()
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", XsltSettings.TrustedXslt, resolver);
        }
    }
}",
                GetCA3076LoadInsecureConstructedCSharpResultAt(13, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod()
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", XsltSettings.TrustedXslt, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadInsecureConstructedBasicResultAt(10, 13, "TestMethod")
            );
        }

        [Fact]
        [WorkItem(4750, "https://github.com/dotnet/roslyn-analyzers/issues/4750")]
        public async Task VariableDeclaratorWithoutInitializer_NoCrashAndNoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class TestClass
{
    private static void TestMethod()
    {
        string x;
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Class TestClass
    Private Shared Sub TestMethod()
        Dim x As String
    End Sub
End Class
");
        }
    }
}
