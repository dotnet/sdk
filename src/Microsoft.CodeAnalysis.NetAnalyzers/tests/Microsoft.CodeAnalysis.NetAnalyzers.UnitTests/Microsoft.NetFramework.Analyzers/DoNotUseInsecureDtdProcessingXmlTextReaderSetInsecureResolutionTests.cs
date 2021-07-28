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
        private static DiagnosticResult GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlTextReaderSetInsecureResolution).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlTextReaderSetInsecureResolution).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseXmlTextReaderNoCtorShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            var count = reader.AttributeCount;
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
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Dim count = reader.AttributeCount
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetResolverToNullShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            reader.XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(10, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            reader.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(7, 13)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetDtdProcessingToParseShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            reader.DtdProcessing = DtdProcessing.Parse;
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(10, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            reader.DtdProcessing = DtdProcessing.Parse
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(7, 13)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetBothToInsecureValuesShouldGenerateDiagnostics()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader, XmlUrlResolver resolver)
        {
            reader.XmlResolver = resolver;
            reader.DtdProcessing = DtdProcessing.Parse;
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(10, 13),
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader, resolver As XmlUrlResolver)
            reader.XmlResolver = resolver
            reader.DtdProcessing = DtdProcessing.Parse
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(7, 13),
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(8, 13)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetInSecureResolverInTryClauseShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            try
            {
                reader.XmlResolver = new XmlUrlResolver();
            }
            catch { throw; }
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(12, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Try
                reader.XmlResolver = New XmlUrlResolver()
            Catch
                Throw
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(8, 17)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetInSecureResolverInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            try {   }
            catch { reader.XmlResolver = new XmlUrlResolver(); }
            finally {}
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(11, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Try
            Catch
                reader.XmlResolver = New XmlUrlResolver()
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(9, 17)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetInSecureResolverInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            try {   }
            catch { throw; }
            finally { reader.XmlResolver = new XmlUrlResolver(); }
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(12, 23)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Try
            Catch
                Throw
            Finally
                reader.XmlResolver = New XmlUrlResolver()
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetDtdprocessingToParseInTryClauseShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            try
            {
                reader.DtdProcessing = DtdProcessing.Parse;
            }
            catch { throw; }
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(12, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Try
                reader.DtdProcessing = DtdProcessing.Parse
            Catch
                Throw
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(8, 17)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetDtdprocessingToParseInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            try {  }
            catch { reader.DtdProcessing = DtdProcessing.Parse; }
            finally {   }
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(11, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Try
            Catch
                reader.DtdProcessing = DtdProcessing.Parse
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(9, 17)
            );
        }

        [Fact]
        public async Task XmlTextReaderNoCtorSetDtdprocessingToParseInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(XmlTextReader reader)
        {
            try {  }
            catch { throw; }
            finally { reader.DtdProcessing = DtdProcessing.Parse; }
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(12, 23)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(reader As XmlTextReader)
            Try
            Catch
                Throw
            Finally
                reader.DtdProcessing = DtdProcessing.Parse
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetInsecureResolverInInitializerShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(string path)
        {
            XmlTextReader doc = new XmlTextReader(path)
            {
                XmlResolver = new XmlUrlResolver()
            };
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(10, 33)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(path As String)
            Dim doc As New XmlTextReader(path) With { _
                .XmlResolver = New XmlUrlResolver() _
            }
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(7, 24)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetDtdProcessingParseInInitializerShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(string path)
        {
            XmlTextReader doc = new XmlTextReader(path)
            {
                DtdProcessing = DtdProcessing.Parse
            };
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(10, 33)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(path As String)
            Dim doc As New XmlTextReader(path) With { _
                .DtdProcessing = DtdProcessing.Parse _
            }
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(7, 24)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetBothToInsecureValuesInInitializerShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        private static void TestMethod(string path)
        {
            XmlTextReader doc = new XmlTextReader(path)
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = new XmlUrlResolver()
            };
        }
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(10, 33)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(path As String)
            Dim doc As New XmlTextReader(path) With { _
                .DtdProcessing = DtdProcessing.Parse, _
                .XmlResolver = New XmlUrlResolver() _
            }
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(7, 24)
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetInsecureResolverShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

namespace TestNamespace
{
    class DerivedType : XmlTextReader {}   

    class TestClass
    {
        void TestMethod()
        {
            var c = new DerivedType(){ XmlResolver = new XmlUrlResolver() };
        }
    }
    
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(13, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class DerivedType
        Inherits XmlTextReader
    End Class

    Class TestClass
        Private Sub TestMethod()
            Dim c = New DerivedType() With { _
                .XmlResolver = New XmlUrlResolver() _
            }
        End Sub
    End Class

End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(11, 21)
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetDtdProcessingParseShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;
using System.Xml;

namespace TestNamespace
{
    class DerivedType : XmlTextReader {}   

    class TestClass
    {
        void TestMethod()
        {
            var c = new DerivedType(){ DtdProcessing = DtdProcessing.Parse };
        }
    }
    
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(13, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class DerivedType
        Inherits XmlTextReader
    End Class

    Class TestClass
        Private Sub TestMethod()
            Dim c = New DerivedType() With { _
                .DtdProcessing = DtdProcessing.Parse _
            }
        End Sub
    End Class

End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(11, 21)
            );
        }

        [Fact]
        public async Task XmlTextReaderCreatedAsTempSetSecureSettingsShouldNotGenerateDiagnostics()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {

        public void Method1(string path)
        {
            Method2(new XmlTextReader(path){ XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit });
        }

        public void Method2(XmlTextReader reader){}
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1(path As String)
            Method2(New XmlTextReader(path) With { _
                .XmlResolver = Nothing, _
                .DtdProcessing = DtdProcessing.Prohibit _
            })
        End Sub

        Public Sub Method2(reader As XmlTextReader)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderCreatedAsTempSetInsecureResolverShouldGenerateDiagnostics()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {

        public void Method1(string path)
        {
            Method2(new XmlTextReader(path){ XmlResolver = new XmlUrlResolver(), DtdProcessing = DtdProcessing.Prohibit });
        }

        public void Method2(XmlTextReader reader){}
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(11, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1(path As String)
            Method2(New XmlTextReader(path) With { _
                .XmlResolver = New XmlUrlResolver(), _
                .DtdProcessing = DtdProcessing.Prohibit _
            })
        End Sub

        Public Sub Method2(reader As XmlTextReader)
        End Sub
    End Class
End Namespace
",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(8, 21)
            );
        }

        [Fact]
        public async Task XmlTextReaderCreatedAsTempSetDtdProcessingParseShouldGenerateDiagnostics()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {

        public void Method1(string path)
        {
            Method2(new XmlTextReader(path){ XmlResolver = null, DtdProcessing = DtdProcessing.Parse });
        }

        public void Method2(XmlTextReader reader){}
    }
}",
                GetCA3075XmlTextReaderSetInsecureResolutionCSharpResultAt(11, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1(path As String)
            Method2(New XmlTextReader(path) With { _
                .XmlResolver = Nothing, _
                .DtdProcessing = DtdProcessing.Parse _
            })
        End Sub

        Public Sub Method2(reader As XmlTextReader)
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderSetInsecureResolutionBasicResultAt(8, 21)
            );
        }
    }
}
