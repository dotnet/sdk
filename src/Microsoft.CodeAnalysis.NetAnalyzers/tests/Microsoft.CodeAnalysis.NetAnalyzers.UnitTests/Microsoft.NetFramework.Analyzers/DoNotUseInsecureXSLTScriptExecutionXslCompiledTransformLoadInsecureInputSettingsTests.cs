// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
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
        private static DiagnosticResult GetCA3076LoadInsecureConstructedCSharpResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureConstructedMessage, name));
#pragma warning restore RS0030 // Do not use banned APIs

        private static DiagnosticResult GetCA3076LoadInsecureConstructedBasicResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XslCompiledTransformLoadInsecureConstructedMessage, name));
#pragma warning restore RS0030 // Do not use banned APIs

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsAndNonSecureResolverShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            var resolver = new XmlUrlResolver();
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
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
        Private Shared Sub TestMethod(settings As XsltSettings)
            Dim xslCompiledTransform As New XslCompiledTransform()
            Dim resolver = New XmlUrlResolver()
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadInsecureConstructedBasicResultAt(10, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsAndNonSecureResolverInTryBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings)
        {
            try
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                var resolver = new XmlUrlResolver();
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            catch { throw; }
            finally { }
        }
    }
}",
                GetCA3076LoadInsecureConstructedCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                Dim resolver = New XmlUrlResolver()
                xslCompiledTransform.Load("""", settings, resolver)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadInsecureConstructedBasicResultAt(11, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsAndNonSecureResolverInCatchBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                var resolver = new XmlUrlResolver();
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            finally { }
        }
    }
}",
                GetCA3076LoadInsecureConstructedCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                Dim resolver = New XmlUrlResolver()
                xslCompiledTransform.Load("""", settings, resolver)
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadInsecureConstructedBasicResultAt(12, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsAndNonSecureResolverInFinallyBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings)
        {
            try {   }
            catch { throw; }
            finally {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                var resolver = new XmlUrlResolver();
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
        }
    }
}",
                GetCA3076LoadInsecureConstructedCSharpResultAt(16, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                Dim resolver = New XmlUrlResolver()
                xslCompiledTransform.Load("""", settings, resolver)
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadInsecureConstructedBasicResultAt(14, 17, "TestMethod")
            );
        }
        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsAndNullResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
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
        Private Shared Sub TestMethod(settings As XsltSettings)
            Dim xslCompiledTransform As New XslCompiledTransform()
            xslCompiledTransform.Load("""", settings, Nothing)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsReconstructDefaultAndNonSecureResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            settings = XsltSettings.Default;
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
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Dim xslCompiledTransform As New XslCompiledTransform()
            settings = XsltSettings.[Default]
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsReconstructTrustedXsltAndNonSecureResolverShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            settings = XsltSettings.TrustedXslt;
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(13, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Dim xslCompiledTransform As New XslCompiledTransform()
            settings = XsltSettings.TrustedXslt
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(10, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsReconstructTrustedXsltAndNonSecureResolverInTryBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try
            {              
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings = XsltSettings.TrustedXslt;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            catch { throw; }
            finally { }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings = XsltSettings.TrustedXslt
                xslCompiledTransform.Load("""", settings, resolver)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(11, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsReconstructTrustedXsltAndNonSecureResolverInCatchBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings = XsltSettings.TrustedXslt;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            finally { }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings = XsltSettings.TrustedXslt
                xslCompiledTransform.Load("""", settings, resolver)
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(12, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsReconstructTrustedXsltAndNonSecureResolverInFinallyBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch { throw; }
            finally {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings = XsltSettings.TrustedXslt;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(16, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings = XsltSettings.TrustedXslt
                xslCompiledTransform.Load("""", settings, resolver)
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(14, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToFalseAndNonSecureResolverShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            settings.EnableScript = false;
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(13, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Dim xslCompiledTransform As New XslCompiledTransform()
            settings.EnableScript = False
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(10, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToFalseAndNonSecureResolverInTryBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try
            {              
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = false;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            catch { throw; }
            finally { }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = False
                xslCompiledTransform.Load("""", settings, resolver)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(11, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToFalseAndNonSecureResolverInCatchBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = false;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            finally { }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = False
                xslCompiledTransform.Load("""", settings, resolver)
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(12, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToFalseAndNonSecureResolverInFinallyBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch { throw; }
            finally {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = false;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(16, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = False
                xslCompiledTransform.Load("""", settings, resolver)
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(14, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndSecureResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlSecureResolver resolver)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            settings.EnableScript = true;
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
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlSecureResolver)
            Dim xslCompiledTransform As New XslCompiledTransform()
            settings.EnableScript = True
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndSecureResolverInTryBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlSecureResolver resolver)
        {
            try
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            catch { throw; }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlSecureResolver)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = True
                xslCompiledTransform.Load("""", settings, resolver)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndSecureResolverInCatchBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlSecureResolver resolver)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlSecureResolver)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = True
                xslCompiledTransform.Load("""", settings, resolver)
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndSecureResolverInFinallyBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlSecureResolver resolver)
        {
            try {   }
            catch { throw; }
            finally {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlSecureResolver)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = True
                xslCompiledTransform.Load("""", settings, resolver)
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndSecureResolverAsyncAwaitShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlSecureResolver resolver)
        {
            try {   }
            catch { throw; }
            finally {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlSecureResolver)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = True
                xslCompiledTransform.Load("""", settings, resolver)
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndNonSecureResolverShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            settings.EnableScript = true;
            xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(13, 13, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Dim xslCompiledTransform As New XslCompiledTransform()
            settings.EnableScript = True
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(10, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndNonSecureResolverInTryBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            catch { throw; }
            finally { }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = True
                xslCompiledTransform.Load("""", settings, resolver)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(11, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndNonSecureResolverInCatchBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch { 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            finally { }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(15, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = True
                xslCompiledTransform.Load("""", settings, resolver)
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(12, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndNonSecureResolverInFinallyBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch { throw; }
            finally {                 
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
        }
    }
}",
                GetCA3076LoadCSharpResultAt(16, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableScript = True
                xslCompiledTransform.Load("""", settings, resolver)
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(14, 17, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetOneToTrueAndNonSecureResolverAsyncAwaitShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            await Task.Run(() =>
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableScript = true;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            });
        }
        private async void TestMethod2()
        {
            await TestMethod(null, null);
        }
    }
}",
                GetCA3076LoadCSharpResultAt(16, 17, "TestMethod")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Async Function TestMethod(settings As XsltSettings, resolver As XmlResolver) As Task
            Await Task.Run(Function() 
            Dim xslCompiledTransform As New XslCompiledTransform()
            settings.EnableScript = True
            xslCompiledTransform.Load("""", settings, resolver)

End Function)
        End Function
        Private Async Sub TestMethod2()
            Await TestMethod(Nothing, Nothing)
        End Sub
    End Class
End Namespace",
                GetCA3076LoadBasicResultAt(12, 13, "TestMethod")
            );
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetBothToFalseAndNonSecureResolverShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
            settings.EnableDocumentFunction = false;
            settings.EnableScript = false;
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
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Dim xslCompiledTransform As New XslCompiledTransform()
            settings.EnableDocumentFunction = False
            settings.EnableScript = False
            xslCompiledTransform.Load("""", settings, resolver)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetBothToFalseAndNonSecureResolverInTryBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableDocumentFunction = false;
                settings.EnableScript = false;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            catch { throw; }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableDocumentFunction = False
                settings.EnableScript = False
                xslCompiledTransform.Load("""", settings, resolver)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetBothToFalseAndNonSecureResolverInCatchBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch 
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableDocumentFunction = false;
                settings.EnableScript = false;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
            finally { }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableDocumentFunction = False
                settings.EnableScript = False
                xslCompiledTransform.Load("""", settings, resolver)
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetBothToFalseAndNonSecureResolverInFinallyBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            try {   }
            catch { throw; }
            finally 
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableDocumentFunction = false;
                settings.EnableScript = false;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(settings As XsltSettings, resolver As XmlResolver)
            Try
            Catch
                Throw
            Finally
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableDocumentFunction = False
                settings.EnableScript = False
                xslCompiledTransform.Load("""", settings, resolver)
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task UseXslCompiledTransformLoadInputSettingsSetBothToFalseAndNonSecureResolverAsyncAwaitShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace TestNamespace
{
    class TestClass
    {
        private async Task TestMethod(XsltSettings settings, XmlResolver resolver)
        {
            await Task.Run(() =>
            {
                XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
                settings.EnableDocumentFunction = false;
                settings.EnableScript = false;
                xslCompiledTransform.Load(""testStylesheet"", settings, resolver);
            });
        }
        private async void TestMethod2()
        {
            await TestMethod(null, null);
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Threading.Tasks
Imports System.Xml
Imports System.Xml.Xsl

Namespace TestNamespace
    Class TestClass
        Private Async Function TestMethod(settings As XsltSettings, resolver As XmlResolver) As Task
            Await Task.Run(Function() 
                Dim xslCompiledTransform As New XslCompiledTransform()
                settings.EnableDocumentFunction = False
                settings.EnableScript = False
                xslCompiledTransform.Load("""", settings, resolver)
            End Function)
        End Function
        Private Async Sub TestMethod2()
            Await TestMethod(Nothing, Nothing)
        End Sub
    End Class
End Namespace");
        }
    }
}
