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
        private static DiagnosticResult GetCA3077InsecureMethodCSharpResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassSetInsecureXmlResolverInMethodMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3077InsecureMethodBasicResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassSetInsecureXmlResolverInMethodMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task XmlDocumentDerivedTypeNoCtorSetUrlResolverToXmlResolverMethodShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {
        public TestClass()
        {
            XmlResolver = null;
        }

        public void method()
        {
            XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3077InsecureMethodCSharpResultAt(16, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            XmlResolver = Nothing
        End Sub
        Public Sub method()
            XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077InsecureMethodBasicResultAt(11, 13, "method")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetUrlResolverToXmlResolverMethodShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
        }

        public void method()
        {
            XmlResolver = new XmlUrlResolver();    
        }
    }
}",
                GetCA3077InsecureMethodCSharpResultAt(16, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub method()
            XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077InsecureMethodBasicResultAt(12, 13, "method")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetNullToXmlResolverMethodShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
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
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub method()
            XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetUrlResolverToThisXmlResolverMethodShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
        }

        public void method()
        {
            this.XmlResolver = new XmlUrlResolver();    
        }
    }
}",
                GetCA3077InsecureMethodCSharpResultAt(16, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub method()
            Me.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077InsecureMethodBasicResultAt(12, 13, "method")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetNullToThisXmlResolverMethodShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
        }

        public void method()
        {
            this.XmlResolver = null;    
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub method()
            Me.XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetUrlResolverToBaseXmlResolverMethodShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
        }

        public void method()
        {
            base.XmlResolver = new XmlUrlResolver();    
        }
    }
}",
                GetCA3077InsecureMethodCSharpResultAt(16, 13, "method")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub method()
            MyBase.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077InsecureMethodBasicResultAt(12, 13, "method")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetNullToBaseXmlResolverMethodShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
        }

        public void method()
        {
            base.XmlResolver = null;    
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub method()
            MyBase.XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetUrlResolverToVariableMethodShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
        }

        public void method(XmlDocument doc)
        {
            doc.XmlResolver = new XmlUrlResolver();    
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub method(doc As XmlDocument)
            doc.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetUrlResolverToHidingXmlResolverFieldInMethodShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        XmlResolver XmlResolver;    //hide XmlDocument.XmlResolver roperty 

        public TestClass()
        {
            base.XmlResolver = null;
        }

        public void method()
        {
            this.XmlResolver = new XmlUrlResolver();    //ok   
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Private XmlResolver As XmlResolver
        'hide XmlDocument.XmlResolver roperty 
        Public Sub New()
            MyBase.XmlResolver = Nothing
        End Sub

        Public Sub method()
            Me.XmlResolver = New XmlUrlResolver()
            'ok   
        End Sub
    End Class
End Namespace");
        }
    }
}
