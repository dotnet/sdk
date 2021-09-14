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
        private static DiagnosticResult GetCA3077NoConstructorCSharpResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassNoConstructorMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3077NoConstructorBasicResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(string.Format(CultureInfo.CurrentCulture, MicrosoftNetFrameworkAnalyzersResources.XmlDocumentDerivedClassNoConstructorMessage, name));
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task NonXmlDocumentDerivedTypeWithNoConstructorShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
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

            await VerifyVisualBasicAnalyzerAsync(@"
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
        public async Task NonXmlDocumentDerivedTypeWithConstructorShouldNotGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
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

            await VerifyVisualBasicAnalyzerAsync(@"
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
        public async Task XmlDocumentDerivedTypeWithNoConstructorShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.Xml;

namespace TestNamespace
{
    class TestClass : XmlDocument {}
}",
                GetCA3077NoConstructorCSharpResultAt(7, 11, "TestClass")
            );

            await VerifyVisualBasicAnalyzerAsync(@"
Imports System.Xml

Namespace TestNamespace
    Class TestClass
        Inherits XmlDocument
    End Class
End Namespace",
                GetCA3077NoConstructorBasicResultAt(5, 11, "TestClass")
            );
        }
    }
}
