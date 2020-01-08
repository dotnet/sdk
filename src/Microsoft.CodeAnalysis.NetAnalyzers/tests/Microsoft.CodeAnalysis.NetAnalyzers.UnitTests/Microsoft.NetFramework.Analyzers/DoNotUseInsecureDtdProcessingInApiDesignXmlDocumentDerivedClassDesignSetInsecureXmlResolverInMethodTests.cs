// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.CSharp.Analyzers.CSharpDoNotUseInsecureDtdProcessingInApiDesignAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetFramework.VisualBasic.Analyzers.BasicDoNotUseInsecureDtdProcessingInApiDesignAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureDtdProcessingInApiDesignAnalyzerTests
    {
        private static DiagnosticResult GetCA3077InsecureMethodCSharpResultAt(int line, int column, string name)
        {
            return new DiagnosticResult(DoNotUseInsecureDtdProcessingInApiDesignAnalyzer.RuleDoNotUseInsecureDtdProcessingInApiDesign).WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassSetInsecureXmlResolverInMethodMessage, name));
        }

        private static DiagnosticResult GetCA3077InsecureMethodBasicResultAt(int line, int column, string name)
        {
            return new DiagnosticResult(DoNotUseInsecureDtdProcessingInApiDesignAnalyzer.RuleDoNotUseInsecureDtdProcessingInApiDesign).WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassSetInsecureXmlResolverInMethodMessage, name));
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeNoCtorSetUrlResolverToXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetUrlResolverToXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetNullToXmlResolverMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetUrlResolverToThisXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetNullToThisXmlResolverMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetUrlResolverToBaseXmlResolverMethodShouldGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetNullToBaseXmlResolverMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetUrlResolverToVariableMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeSetUrlResolverToHidingXmlResolverFieldInMethodShouldNotGenerateDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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

            await VerifyVB.VerifyAnalyzerAsync(@"
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
