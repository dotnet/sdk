// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        public async Task NonXmlTextReaderDerivedTypeWithNoConstructorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlResolver
    {
        public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            throw new NotImplementedException();
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlResolver
        Public Overrides Function GetEntity(absoluteUri As Uri, role As String, ofObjectToReturn As Type) As Object
            Throw New NotImplementedException()
        End Function
    End Class
End Namespace");
        }

        [Fact]
        public async Task NonXmlTextReaderDerivedTypeWithConstructorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlResolver
    {
        public TestClass() {}

        public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            throw new NotImplementedException();
        }
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlResolver
        Public Sub New()
        End Sub

        Public Overrides Function GetEntity(absoluteUri As Uri, role As String, ofObjectToReturn As Type) As Object
            Throw New NotImplementedException()
        End Function
    End Class
End Namespace");
        }

        [Fact]
        public async Task TextReaderDerivedTypeWithNoConstructorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader {}
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
    End Class
End Namespace"
            );
        }

        [Fact]
        public async Task TextReaderDerivedTypeWithMethodAndNoConstructorShouldNotGenerateDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlTextReader 
    {
        public void TestMethod() {}
    }
}"
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlTextReader
        Public Sub TestMethod()
        End Sub
    End Class
End Namespace"
            );
        }
    }
}

