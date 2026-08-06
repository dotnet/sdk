// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
        [Fact]
        public async Task TextReaderDerivedTypeWithEmptyConstructorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {
        public TestClass () {}
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeNullResolverAndProhibitInOnlyCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeUrlResolverAndProhibitInOnlyCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = new XmlUrlResolver();
            this.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = New XmlUrlResolver()
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSecureResolverAndParseInOnlyCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass(XmlSecureResolver resolver)
        {
            this.XmlResolver = resolver;
            this.DtdProcessing = DtdProcessing.Parse;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New(resolver As XmlSecureResolver)
            Me.XmlResolver = resolver
            Me.DtdProcessing = DtdProcessing.Parse
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeNullResolverInOnlyCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
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
        Inherits XmlTextReader
        Public Sub New()
            Me.XmlResolver = Nothing
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeIgnoreInOnlyCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass(XmlSecureResolver resolver)
        {
            this.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New(resolver As XmlSecureResolver)
            Me.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetInsecureResolverInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Ignore;
        }

        public TestClass(XmlResolver resolver)
        {
            this.XmlResolver = resolver;
            this.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
	Class TestClass
		Inherits XmlTextReader
		Public Sub New()
			Me.XmlResolver = Nothing
			Me.DtdProcessing = DtdProcessing.Ignore
		End Sub

		Public Sub New(resolver As XmlResolver)
			Me.XmlResolver = resolver
			Me.DtdProcessing = DtdProcessing.Ignore
		End Sub
	End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSecureSettingsForVariableInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    { 
        public TestClass(XmlTextReader reader)
        {
            reader.XmlResolver = null;
            reader.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
	Class TestClass
		Inherits XmlTextReader
		Public Sub New(reader As XmlTextReader)
			reader.XmlResolver = Nothing
			reader.DtdProcessing = DtdProcessing.Ignore
		End Sub
	End Class
End Namespace
"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSecureSettingsWithOutThisInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    { 
        public TestClass(XmlTextReader reader)
        {
            reader.XmlResolver = null;
            XmlResolver = null;
            DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New(reader As XmlTextReader)
            reader.XmlResolver = Nothing
            XmlResolver = Nothing
            DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetSecureSettingsToAXmlTextReaderFieldInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    { 
        private XmlTextReader reader = new XmlTextReader(""path"");
        public TestClass()
        {
            this.reader.XmlResolver = null;
            this.reader.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private reader As New XmlTextReader("""")
        Public Sub New()
            Me.reader.XmlResolver = Nothing
            Me.reader.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetSecureSettingsAtLeastOnceInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    { 
        public TestClass(bool flag)
        {
            if (flag)
            {
                XmlResolver = null;
                DtdProcessing = DtdProcessing.Ignore;
            }
            else
            {
                XmlResolver = new XmlUrlResolver();
                DtdProcessing = DtdProcessing.Parse;
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New(flag As Boolean)
            If flag Then
                XmlResolver = Nothing
                DtdProcessing = DtdProcessing.Ignore
            Else
                XmlResolver = New XmlUrlResolver()
                DtdProcessing = DtdProcessing.Parse
            End If
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetSecureSettingsAtLeastOnceInCtorShouldNotGenerateDiagnosticFalseNegAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    { 
        public TestClass(bool flag)
        {
            if (flag)
            {
                XmlResolver = null;
                DtdProcessing = DtdProcessing.Parse;
            }
            else
            {
                XmlResolver = new XmlUrlResolver();
                DtdProcessing = DtdProcessing.Ignore;
            }
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New(flag As Boolean)
            If flag Then
                XmlResolver = Nothing
                DtdProcessing = DtdProcessing.Parse
            Else
                XmlResolver = New XmlUrlResolver()
                DtdProcessing = DtdProcessing.Ignore
            End If
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetIgnoreToHidingFieldInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        DtdProcessing DtdProcessing;
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private DtdProcessing As DtdProcessing
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetNullToHidingFieldInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        XmlResolver XmlResolver;
        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private XmlResolver As XmlResolver
        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetNullToBaseXmlResolverInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        XmlResolver XmlResolver;
        public TestClass()
        {
            base.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private XmlResolver As XmlResolver
        Public Sub New()
            MyBase.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetProhibitToBaseInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        DtdProcessing DtdProcessing;
        public TestClass()
        {
            this.XmlResolver = null;
            base.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private DtdProcessing As DtdProcessing
        Public Sub New()
            Me.XmlResolver = Nothing
            MyBase.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetSecureSettingsToBaseWithHidingFieldsInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        DtdProcessing DtdProcessing;
        XmlResolver XmlResolver;
        public TestClass()
        {
            base.XmlResolver = null;
            base.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private DtdProcessing As DtdProcessing
        Private XmlResolver As XmlResolver
        Public Sub New()
            MyBase.XmlResolver = Nothing
            MyBase.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetSecureSettingsToBaseInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        public TestClass()
        {
            base.XmlResolver = null;
            base.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub New()
            MyBase.XmlResolver = Nothing
            MyBase.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetUrlResolverToBaseXmlResolverInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {                 
        DtdProcessing DtdProcessing;
        XmlResolver XmlResolver;
        public TestClass()
        {
            base.XmlResolver = new XmlUrlResolver();
            base.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private DtdProcessing As DtdProcessing
        Private XmlResolver As XmlResolver
        Public Sub New()
            MyBase.XmlResolver = New XmlUrlResolver()
            MyBase.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetNullToHidingPropertyInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        XmlResolver XmlResolver { set; get; }

        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
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
            Me.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetProhibitToHidingPropertyInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        DtdProcessing DtdProcessing { set; get; }

        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private Property DtdProcessing() As DtdProcessing
            Get
                Return m_DtdProcessing
            End Get
            Set
                m_DtdProcessing = Value
            End Set
        End Property
        Private m_DtdProcessing As DtdProcessing

        Public Sub New()
            Me.XmlResolver = Nothing
            Me.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetSecureSettingsToHidingPropertiesInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        DtdProcessing DtdProcessing { set; get; }   
        XmlResolver XmlResolver { set; get; }

        public TestClass()
        {
            this.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private Property DtdProcessing() As DtdProcessing
            Get
                Return m_DtdProcessing
            End Get
            Set
                m_DtdProcessing = Value
            End Set
        End Property
        Private m_DtdProcessing As DtdProcessing
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
            Me.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetNullToBaseWithHidingPropertyInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        XmlResolver XmlResolver { set; get; }

        public TestClass()
        {
            base.XmlResolver = null;
            this.DtdProcessing = DtdProcessing.Prohibit;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
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
            Me.DtdProcessing = DtdProcessing.Prohibit
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetIgnoreToBaseWithHidingPropertyInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        DtdProcessing DtdProcessing { set; get; }

        public TestClass()
        {
            this.XmlResolver = null;
            base.DtdProcessing = DtdProcessing.Ignore;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private Property DtdProcessing() As DtdProcessing
            Get
                Return m_DtdProcessing
            End Get
            Set
                m_DtdProcessing = Value
            End Set
        End Property
        Private m_DtdProcessing As DtdProcessing

        Public Sub New()
            Me.XmlResolver = Nothing
            MyBase.DtdProcessing = DtdProcessing.Ignore
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task XmlTextReaderDerivedTypeSetParseToBaseWithHidingPropertyInCtorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {    
        XmlResolver XmlResolver { set; get; }  
        DtdProcessing DtdProcessing { set; get; }

        public TestClass()
        {
            base.XmlResolver = null;
            base.DtdProcessing = DtdProcessing.Parse;
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Private Property XmlResolver() As XmlResolver
            Get
                Return m_XmlResolver
            End Get
            Set
                m_XmlResolver = Value
            End Set
        End Property
        Private m_XmlResolver As XmlResolver
        Private Property DtdProcessing() As DtdProcessing
            Get
                Return m_DtdProcessing
            End Get
            Set
                m_DtdProcessing = Value
            End Set
        End Property
        Private m_DtdProcessing As DtdProcessing

        Public Sub New()
            MyBase.XmlResolver = Nothing
            MyBase.DtdProcessing = DtdProcessing.Parse
        End Sub
    End Class
End Namespace"
            );
        }
    }
}
