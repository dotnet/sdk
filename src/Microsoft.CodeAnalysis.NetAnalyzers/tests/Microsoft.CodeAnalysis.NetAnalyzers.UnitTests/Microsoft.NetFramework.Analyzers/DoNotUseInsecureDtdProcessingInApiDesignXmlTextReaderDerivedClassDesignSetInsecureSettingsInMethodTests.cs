// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingInApiDesignAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingInApiDesignAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureDtdProcessingInApiDesignAnalyzerTests
    {
        private static DiagnosticResult GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodCSharpResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlTextReaderDerivedClassSetInsecureSettingsInMethodMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodBasicResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlTextReaderDerivedClassSetInsecureSettingsInMethodMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task XmlTextReaderDerivedTypeNoCtorSetUrlResolverToXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {
        public void method()
        {
            XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodCSharpResultAt(11, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub method()
            XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodBasicResultAt(8, 13, "method")
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetUrlResolverToXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodCSharpResultAt(17, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodBasicResultAt(13, 13, "method")
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetDtdProcessingToParseMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            DtdProcessing = DtdProcessing.Parse;
        }
    }
}",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodCSharpResultAt(17, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            DtdProcessing = DtdProcessing.Parse
        End Sub
    End Class
End Namespace",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodBasicResultAt(13, 13, "method")
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetUrlResolverToThisXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            this.XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodCSharpResultAt(17, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            Me.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodBasicResultAt(13, 13, "method")
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetUrlResolverToBaseXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            base.XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodCSharpResultAt(17, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            MyBase.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodBasicResultAt(13, 13, "method")
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetXmlResolverToNullMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            XmlResolver = null;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetDtdProcessingToProhibitMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetDtdProcessingToTypoMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeParseAndNullResolverMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            DtdProcessing = DtdProcessing.Parse;
            XmlResolver = null;
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            DtdProcessing = DtdProcessing.Parse
            XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeIgnoreAndUrlResolverMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            DtdProcessing = DtdProcessing.Ignore;
            XmlResolver = new XmlUrlResolver();
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            DtdProcessing = DtdProcessing.Ignore
            XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeParseAndUrlResolverMethodShouldGenerateDiagnostic()
        {
#pragma warning disable RS0030 // Do not used banned APIs
            DiagnosticResult diagWith2Locations = GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodCSharpResultAt(17, 13, "method")
                .WithLocation(18, 13);
#pragma warning restore RS0030 // Do not used banned APIs

            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method()
        {
            DtdProcessing = DtdProcessing.Parse;
            XmlResolver = new XmlUrlResolver();
        }
    }
}",
                diagWith2Locations
            );

#pragma warning disable RS0030 // Do not used banned APIs
            diagWith2Locations = GetCA3077XmlTextReaderDerivedClassSetInsecureSettingsInMethodBasicResultAt(13, 13, "method")
                .WithLocation(14, 13);
#pragma warning restore RS0030 // Do not used banned APIs

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method()
            DtdProcessing = DtdProcessing.Parse
            XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                diagWith2Locations
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSecureResolverInOnePathMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method(bool flag)
        {
            DtdProcessing = DtdProcessing.Parse;
            if (flag)
            {
                XmlResolver = null;
            }
            else
            {  
                XmlResolver = new XmlUrlResolver();   // intended false negative, due to the lack of flow analysis
            }
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method(flag As Boolean)
            DtdProcessing = DtdProcessing.Parse
            If flag Then
                XmlResolver = Nothing
            Else
                    ' intended false negative, due to the lack of flow analysis
                XmlResolver = New XmlUrlResolver()
            End If
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetInsecureSettingsInSeperatePathsMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }

        public void method(bool flag)
        {
            if (flag)
            {
                // secure
                DtdProcessing = DtdProcessing.Ignore;
                XmlResolver = null;
            }
            else
            {  
                // insecure
                DtdProcessing = DtdProcessing.Parse;
                XmlResolver = new XmlUrlResolver();   // intended false negative, due to the lack of flow analysis
            }
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub

        Public Sub method(flag As Boolean)
            If flag Then
                ' secure
                DtdProcessing = DtdProcessing.Ignore
                XmlResolver = Nothing
            Else
                ' insecure
                DtdProcessing = DtdProcessing.Parse
                    ' intended false negative, due to the lack of flow analysis
                XmlResolver = New XmlUrlResolver()
            End If
        End Sub
    End Class
End Namespace");
        }
    }
}

