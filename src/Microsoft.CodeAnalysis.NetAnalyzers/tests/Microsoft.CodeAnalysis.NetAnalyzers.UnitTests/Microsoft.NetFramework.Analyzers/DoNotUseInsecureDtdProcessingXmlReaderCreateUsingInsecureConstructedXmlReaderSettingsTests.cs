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
        public async Task DefaultXmlReaderSettingsInStaticFieldShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        private static readonly XmlReaderSettings Settings = new XmlReaderSettings();

        public void TestMethod(string path)
        {
            XmlReader reader = XmlReader.Create(path, Settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Private Shared ReadOnly Settings As New XmlReaderSettings()

        Public Sub TestMethod(path As String)
            Dim reader As XmlReader = XmlReader.Create(path, Settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task DefaultXmlReaderSettingsShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings()
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings(){ DtdProcessing = DtdProcessing.Parse };
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings() With { _
                .DtdProcessing = DtdProcessing.Parse _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseInInitializerTargetFx452ShouldNotGenerateDiagnosticAsync()
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
            XmlReaderSettings settings = new XmlReaderSettings(){ DtdProcessing = DtdProcessing.Parse };
            XmlReader reader = XmlReader.Create(path, settings);
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
            Dim settings As New XmlReaderSettings() With {
                .DtdProcessing = DtdProcessing.Parse _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsOnlySetMaxCharRoZeroInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings(){ MaxCharactersFromEntities = 0 };
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings() With { _
                .MaxCharactersFromEntities = 0 _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsSetSecureResolverInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path, XmlSecureResolver resolver)
        {
            XmlReaderSettings settings = new XmlReaderSettings(){ XmlResolver = resolver };
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String, resolver As XmlSecureResolver)
            Dim settings As New XmlReaderSettings() With { _
                .XmlResolver = resolver _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseAndMaxCharToNonZeroInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings()
                                        {
                                            DtdProcessing = DtdProcessing.Parse,
                                            MaxCharactersFromEntities = (long)1e7
                                        };
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings() With { _
                .DtdProcessing = DtdProcessing.Parse, _
                .MaxCharactersFromEntities = CLng(10000000.0) _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseAndSecureResolverInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path, XmlSecureResolver resolver)
        {
            XmlReaderSettings settings = new XmlReaderSettings()
                                        {
                                            DtdProcessing = DtdProcessing.Parse,
                                            XmlResolver = resolver
                                        };
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String, resolver As XmlSecureResolver)
            Dim settings As New XmlReaderSettings() With { _
                .DtdProcessing = DtdProcessing.Parse, _
                .XmlResolver = resolver _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseWithOtherValuesSecureInInitializerShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings()
                                        {
                                            DtdProcessing = DtdProcessing.Parse,
                                            MaxCharactersFromEntities = (long)1e7,
                                            XmlResolver = null
                                        };
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings() With { _
                .DtdProcessing = DtdProcessing.Parse, _
                .MaxCharactersFromEntities = CLng(10000000.0), _
                .XmlResolver = Nothing _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings()
            settings.DtdProcessing = DtdProcessing.Parse
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseInTryBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System;
namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            XmlReader reader = XmlReader.Create(path, settings);
            }
            catch (Exception) { throw; }
            finally { }
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml
Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
                Dim settings As New XmlReaderSettings()
                settings.DtdProcessing = DtdProcessing.Parse
                Dim reader As XmlReader = XmlReader.Create(path, settings)
            Catch generatedExceptionName As Exception
                Throw
            Finally
            End Try
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseInCatchBlockShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System;
namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try { }
            catch (Exception) { 
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            XmlReader reader = XmlReader.Create(path, settings);
            }
            finally { }
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml
Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch generatedExceptionName As Exception
                Dim settings As New XmlReaderSettings()
                settings.DtdProcessing = DtdProcessing.Parse
                Dim reader As XmlReader = XmlReader.Create(path, settings)
            Finally
            End Try
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseInFinallyBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;
using System;
namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            try {   }
            catch (Exception) { throw; }
            finally { 
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            XmlReader reader = XmlReader.Create(path, settings);
            }
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml
Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Try
            Catch generatedExceptionName As Exception
                Throw
            Finally
                Dim settings As New XmlReaderSettings()
                settings.DtdProcessing = DtdProcessing.Parse
                Dim reader As XmlReader = XmlReader.Create(path, settings)
            End Try
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseInUnusedOneShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings(){ DtdProcessing = DtdProcessing.Parse };   
            settings = new XmlReaderSettings();
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings() With { _
                .DtdProcessing = DtdProcessing.Parse _
            }
            settings = New XmlReaderSettings()
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlReaderSettingsSetDtdProcessingToParseInUsedOneShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Xml;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings = new XmlReaderSettings(){ DtdProcessing = DtdProcessing.Parse };
            XmlReader reader = XmlReader.Create(path, settings);
        }
    }
}
"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod(path As String)
            Dim settings As New XmlReaderSettings()
            settings = New XmlReaderSettings() With { _
                .DtdProcessing = DtdProcessing.Parse _
            }
            Dim reader As XmlReader = XmlReader.Create(path, settings)
        End Sub
    End Class
End Namespace"
            );
        }
    }
}
