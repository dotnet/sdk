// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
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
        private static DiagnosticResult GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlTextReaderConstructedWithNoSecureResolution).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleXmlTextReaderConstructedWithNoSecureResolution).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        [WorkItem(998, "https://github.com/dotnet/roslyn-analyzers/issues/998")]
        [Fact]
        public async Task StaticPropertyAssignmentShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System;

namespace TestNamespace
{
    public static class SystemContext
    {
        public static Func<DateTime> UtcNow { get; set; }

        static SystemContext()
        {
            UtcNow = () => DateTime.UtcNow;
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System

Namespace TestNamespace
    Module SystemContext
        Public Property UtcNow As Func(Of DateTime)

        Sub New()
            UtcNow = Function() DateTime.UtcNow
        End Sub
    End Module
End Namespace
"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlTextReader reader = new XmlTextReader(path);
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(10, 36)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim reader As New XmlTextReader(path)
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(7, 27)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {
                XmlTextReader reader = new XmlTextReader(path);
            }
            catch { throw ; }
            finally {}
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(11, 40)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
                Dim reader As New XmlTextReader(path)
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(8, 31)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { 
                XmlTextReader reader = new XmlTextReader(path);
            }
            finally {}
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(12, 40)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Dim reader As New XmlTextReader(path)
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(9, 31)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { throw ; }
            finally {
                XmlTextReader reader = new XmlTextReader(path);
            }
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(13, 40)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Throw
            Finally
                Dim reader As New XmlTextReader(path)
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(11, 31)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderOnlySetResolverToSecureValueShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlTextReader reader = new XmlTextReader(path);
            reader.XmlResolver = null;
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(10, 36)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim reader As New XmlTextReader(path)
            reader.XmlResolver = Nothing
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(7, 27)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetResolverToSecureValueInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {
                XmlTextReader reader = new XmlTextReader(path);
                reader.XmlResolver = null;
            }
            catch { throw ; }
            finally {}
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(11, 40)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
                Dim reader As New XmlTextReader(path)
                reader.XmlResolver = Nothing
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(8, 31)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetResolverToSecureValueInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { 
                XmlTextReader reader = new XmlTextReader(path);
                reader.XmlResolver = null;
            }
            finally {}
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(12, 40)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Dim reader As New XmlTextReader(path)
                reader.XmlResolver = Nothing
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(9, 31)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetResolverToSecureValueInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { throw ; }
            finally {
                XmlTextReader reader = new XmlTextReader(path);
                reader.XmlResolver = null;
            }
        }
    }
}
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(13, 40)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Throw
            Finally
                Dim reader As New XmlTextReader(path)
                reader.XmlResolver = Nothing
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(11, 31)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderOnlySetDtdProcessingToSecureValueShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlTextReader reader = new XmlTextReader(path);
            reader.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim reader As New XmlTextReader(path)
            reader.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetDtdProcessingToSecureValueInTryBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {
                XmlTextReader reader = new XmlTextReader(path);
                reader.DtdProcessing = DtdProcessing.Prohibit;
            }
            catch { throw ; }
            finally {}
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
                Dim reader As New XmlTextReader(path)
                reader.DtdProcessing = DtdProcessing.Prohibit
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetDtdProcessingToSecureValueInCatchBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { 
                XmlTextReader reader = new XmlTextReader(path);
                reader.DtdProcessing = DtdProcessing.Prohibit;
            }
            finally {}
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Dim reader As New XmlTextReader(path)
                reader.DtdProcessing = DtdProcessing.Prohibit
            Finally
            End Try
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetDtdProcessingToSecureValueInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { throw ; }
            finally {
                XmlTextReader reader = new XmlTextReader(path);
                reader.DtdProcessing = DtdProcessing.Prohibit;
            }
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Throw
            Finally
                Dim reader As New XmlTextReader(path)
                reader.DtdProcessing = DtdProcessing.Prohibit
            End Try
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetResolverAndDtdProcessingToSecureValuesShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlTextReader reader = new XmlTextReader(path);
            reader.DtdProcessing = DtdProcessing.Prohibit;
            reader.XmlResolver = null;
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim reader As New XmlTextReader(path)
            reader.DtdProcessing = DtdProcessing.Prohibit
            reader.XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetSetResolverAndDtdProcessingToSecureValueInTryBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {
                XmlTextReader reader = new XmlTextReader(path);
                reader.DtdProcessing = DtdProcessing.Prohibit;
                reader.XmlResolver = null;
            }
            catch { throw ; }
            finally {}
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
                Dim reader As New XmlTextReader(path)
                reader.DtdProcessing = DtdProcessing.Prohibit
                reader.XmlResolver = Nothing
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetSetResolverAndDtdProcessingToSecureValueInCatchBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { 
                XmlTextReader reader = new XmlTextReader(path);
                reader.DtdProcessing = DtdProcessing.Prohibit;
                reader.XmlResolver = null;
            }
            finally {}
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Dim reader As New XmlTextReader(path)
                reader.DtdProcessing = DtdProcessing.Prohibit
                reader.XmlResolver = Nothing
            Finally
            End Try
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetSetResolverAndDtdProcessingToSecureValueInFinallyBlockShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch { throw ; }
            finally {
                XmlTextReader reader = new XmlTextReader(path);
                reader.DtdProcessing = DtdProcessing.Prohibit;
                reader.XmlResolver = null;
            }
        }
    }
}
");
            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch
                Throw
            Finally
                Dim reader As New XmlTextReader(path)
                reader.DtdProcessing = DtdProcessing.Prohibit
                reader.XmlResolver = Nothing
            End Try
        End Sub
    End Class
End Namespace
"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderSetResolverAndDtdProcessingToSecureValuesInInitializerShouldNotGenerateDiagnostic()
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
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
        }
    }
}");
            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(path As String)
            Dim doc As New XmlTextReader(path) With { _
                .DtdProcessing = DtdProcessing.Prohibit, _
                .XmlResolver = Nothing _
            }
        End Sub
    End Class
End Namespace
");
        }

        [Fact]
        public async Task ConstructXmlTextReaderOnlySetResolverToSecureValueInInitializerShouldGenerateDiagnostic()
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
                XmlResolver = null
            };
        }
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(10, 33)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Private Shared Sub TestMethod(path As String)
            Dim doc As New XmlTextReader(path) With { _
                .XmlResolver = Nothing _
            }
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(7, 24)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderOnlySetDtdProcessingToSecureValueInInitializerShouldGenerateDiagnostic()
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
                DtdProcessing = DtdProcessing.Prohibit
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
        Private Shared Sub TestMethod(path As String)
            Dim doc As New XmlTextReader(path) With { _
                .DtdProcessing = DtdProcessing.Prohibit _
            }
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"");
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"")
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(6, 42)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldSetBothToSecureValuesInInitializerShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"")
        {
            DtdProcessing = DtdProcessing.Prohibit,
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
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"") With { _
            .DtdProcessing = DtdProcessing.Prohibit, _
            .XmlResolver = Nothing _
        }
        End Class
End Namespace");
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldOnlySetResolverToSecureValuesInInitializerShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"")
        {
            XmlResolver = null
        };
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"

Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"") With { _
           .XmlResolver = Nothing _
        }
        End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(7, 42)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldOnlySetDtdProcessingToSecureValuesInInitializerShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"")
        {
            DtdProcessing = DtdProcessing.Prohibit
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
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"") With { _
            .DtdProcessing = DtdProcessing.Prohibit _
        }
        End Class
End Namespace"
            );
        }

        [Fact]
        public async Task ConstructDefaultXmlTextReaderAsFieldSetBothToSecureValuesInMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"");

        public TestClass()
        {
            reader.XmlResolver = null;
            reader.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"")

        Public Sub New()
            reader.XmlResolver = Nothing
            reader.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace
",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(6, 42)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldOnlySetResolverToSecureValueInMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"");

        public TestClass()
        {
            reader.XmlResolver = null;
        }
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"")

        Public Sub New()
            reader.XmlResolver = Nothing
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(6, 42)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldOnlySetResolverToSecureValueInMethodInTryBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"");

        public void TestMethod()
        {
            try
            {
                reader.XmlResolver = null;
            }
            catch { throw; }
            finally { }
        }
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"")

        Public Sub TestMethod()
            Try
                reader.XmlResolver = Nothing
            Catch
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(6, 42)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldOnlySetResolverToSecureValueInMethodInCatchBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"");

        public void TestMethod()
        {
            try {  }
            catch { reader.XmlResolver = null; }
            finally { }
        }
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"")

        Public Sub TestMethod()
            Try
            Catch
                reader.XmlResolver = Nothing
            Finally
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(6, 42)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldOnlySetResolverToSecureValueInMethodInFinallyBlockShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"");

        public void TestMethod()
        {
            try {   }
            catch { throw; }
            finally { reader.XmlResolver = null; }
        }
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"")

        Public Sub TestMethod()
            Try
            Catch
                Throw
            Finally
                reader.XmlResolver = Nothing
            End Try
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(6, 42)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderAsFieldOnlySetDtdProcessingToSecureValueInMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Xml;

namespace TestNamespace
{
    class TestClass
    {
        public XmlTextReader reader = new XmlTextReader(""file.xml"");

        public TestClass()
        {
            reader.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(8, 39)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Public reader As XmlTextReader = New XmlTextReader(""file.xml"")

        Public Sub New()
            reader.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(6, 42)
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeWithNoSecureSettingsShouldNotGenerateDiagnostic()
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
        Inherits XmlTextReader
    End Class

    Class TestClass
        Private Sub TestMethod()
            Dim c = New DerivedType()
        End Sub
    End Class

End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderCreatedAsTempNoSettingsShouldGenerateDiagnostics()
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
            Method2(new XmlTextReader(path));
        }

        public void Method2(XmlTextReader reader){}
    }
}",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionCSharpResultAt(11, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Xml

Namespace TestNamespace
    Class TestClass

        Public Sub Method1(path As String)
            Method2(New XmlTextReader(path))
        End Sub

        Public Sub Method2(reader As XmlTextReader)
        End Sub
    End Class
End Namespace",
                GetCA3075XmlTextReaderConstructedWithNoSecureResolutionBasicResultAt(8, 21)
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderOnlySetDtdProcessingProhibitTargetFx451ShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Reflection;               
using System.Xml;   

[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute("".NETFramework,Version=v4.5.1"", FrameworkDisplayName = "".NET Framework 4.5.1"")]

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlTextReader reader = new XmlTextReader(path);
            reader.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}
"
            );
        }

        [Fact]
        public async Task ConstructXmlTextReaderOnlySetDtdProcessingProhibitTargetFx46ShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net46.Default,
                @"
using System;
using System.Reflection;               
using System.Xml;   

[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute("".NETFramework,Version=v4.6"", FrameworkDisplayName = "".NET Framework 4.6"")]

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlTextReader reader = new XmlTextReader(path);
            reader.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net46.Default,
                @"
Imports System.Reflection
Imports System.Xml

<Assembly: System.Runtime.Versioning.TargetFrameworkAttribute("".NETFramework, Version = v4.6"", FrameworkDisplayName := "".NET Framework 4.6"")>

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim reader As New XmlTextReader(path)
            reader.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task ConstructXmlTextReaderOnlySetDtdProcessingProhibitTargetFx452ShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
using System;
using System.Reflection;               
using System.Xml;   

[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute("".NETFramework,Version=v4.5.2"", FrameworkDisplayName = "".NET Framework 4.5.2"")]

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlTextReader reader = new XmlTextReader(path);
            reader.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}
"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net452.Default,
                @"
Imports System.Reflection
Imports System.Xml

<Assembly: System.Runtime.Versioning.TargetFrameworkAttribute("".NETFramework, Version = v4.5.2"", FrameworkDisplayName := "".NET Framework 4.5.2"")>

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim reader As New XmlTextReader(path)
            reader.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace"
            );
        }
    }
}
