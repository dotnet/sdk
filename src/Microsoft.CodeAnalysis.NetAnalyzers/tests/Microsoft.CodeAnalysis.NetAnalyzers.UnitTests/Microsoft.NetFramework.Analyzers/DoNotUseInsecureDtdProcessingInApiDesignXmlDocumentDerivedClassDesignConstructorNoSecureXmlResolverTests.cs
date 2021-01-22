// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        private static DiagnosticResult GetCA3077ConstructorCSharpResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassConstructorNoSecureXmlResolverMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3077ConstructorBasicResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassConstructorNoSecureXmlResolverMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task XmlDocumentDerivedTypeWithEmptyConstructorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {
        public TestClass () {}
    }
}",
                GetCA3077ConstructorCSharpResultAt(9, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(7, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetResolverToNullInOnlyCtorShouldNotGenerateDiagnostic()
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
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetInsecureResolverInOnlyCtorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    { 
        public TestClass(XmlResolver resolver)
        {
            this.XmlResolver = resolver;
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(9, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New(resolver As XmlResolver)
            Me.XmlResolver = resolver
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(7, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetInsecureResolverInCtorShouldGenerateDiagnostic()
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

        public TestClass(XmlResolver resolver)
        {
            this.XmlResolver = resolver;
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(14, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub

        Public Sub New(resolver As XmlResolver)
            Me.XmlResolver = resolver
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(11, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetSecureResolverForVariableInCtorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    { 
        public TestClass(XmlDocument doc)
        {
            doc.XmlResolver = null;
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(9, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New(doc As XmlDocument)
            doc.XmlResolver = Nothing
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(7, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetSecureResolverWithOutThisInCtorShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    { 
        public TestClass(XmlDocument doc)
        {
            doc.XmlResolver = null;
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
        Public Sub New(doc As XmlDocument)
            doc.XmlResolver = Nothing
            XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetSecureResolverToAXmlDocumentFieldInCtorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    { 
        private XmlDocument doc = new XmlDocument();
        public TestClass(XmlDocument doc)
        {
            this.doc.XmlResolver = null;
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(10, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Private doc As New XmlDocument()
        Public Sub New(doc As XmlDocument)
            Me.doc.XmlResolver = Nothing
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(8, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetSecureResolverAtLeastOnceInCtorShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    { 
        public TestClass(bool flag)
        {
            if (flag)
            {
                XmlResolver = null;
            }
            else
            {
                XmlResolver = new XmlUrlResolver();
            }
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Public Sub New(flag As Boolean)
            If flag Then
                XmlResolver = Nothing
            Else
                XmlResolver = New XmlUrlResolver()
            End If
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetNullToHidingFieldInCtorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        XmlResolver XmlResolver;
        public TestClass()
        {
            this.XmlResolver = null;
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(10, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Private XmlResolver As XmlResolver
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(8, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetNullToBaseXmlResolverInCtorShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        XmlResolver XmlResolver;
        public TestClass()
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
        Private XmlResolver As XmlResolver
        Public Sub New()
            MyBase.XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetUrlResolverToBaseXmlResolverInCtorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        private XmlResolver XmlResolver;
        public TestClass()
        {
            base.XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(10, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Private XmlResolver As XmlResolver
        Public Sub New()
            MyBase.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(8, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetNullToHidingPropertyInCtorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        XmlResolver XmlResolver { set; get; }

        public TestClass()
        {
            this.XmlResolver = null;
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(11, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Private Property XmlResolver() As XmlResolver
            Get
                Return m_XmlResolver
            End Get
            Set
                m_XmlResolver = Value
            End Set
        End Property
        Private m_XmlResolver As XmlResolver

        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(17, 20, "TestClass")
            );
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetNullToBaseWithHidingPropertyInCtorShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        XmlResolver XmlResolver { set; get; }

        public TestClass()
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
        Private Property XmlResolver() As XmlResolver
            Get
                Return m_XmlResolver
            End Get
            Set
                m_XmlResolver = Value
            End Set
        End Property
        Private m_XmlResolver As XmlResolver

        Public Sub New()
            MyBase.XmlResolver = Nothing
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlDocumentDerivedTypeSetUrlResolverToBaseWithHidingPropertyInCtorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument 
    {    
        XmlResolver XmlResolver { set; get; }

        public TestClass()
        {
            base.XmlResolver = new XmlUrlResolver();
        }
    }
}",
                GetCA3077ConstructorCSharpResultAt(11, 16, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
        Private Property XmlResolver() As XmlResolver
            Get
                Return m_XmlResolver
            End Get
            Set
                m_XmlResolver = Value
            End Set
        End Property
        Private m_XmlResolver As XmlResolver

        Public Sub New()
            MyBase.XmlResolver = New XmlUrlResolver()
        End Sub
    End Class
End Namespace",
                GetCA3077ConstructorBasicResultAt(17, 20, "TestClass")
            );
        }

        private async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = source,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        private async Task VerifyVisualBasicAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = source,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }
    }
}
