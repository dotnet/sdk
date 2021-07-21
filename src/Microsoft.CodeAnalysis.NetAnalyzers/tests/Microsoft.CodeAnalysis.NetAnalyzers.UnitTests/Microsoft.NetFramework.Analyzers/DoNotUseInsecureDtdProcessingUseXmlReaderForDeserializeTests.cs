// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        private static DiagnosticResult GetCA3075DeserializeCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("Deserialize");
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075DeserializeBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleDoNotUseDtdProcessingOverloads).WithLocation(line, column).WithArguments("Deserialize");
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseXmlSerializerDeserializeShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace TestNamespace
{
    public class UseXmlReaderForDeserialize
    {
        public void TestMethod(Stream stream)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
            serializer.Deserialize(stream);
        }
    }
}",
                GetCA3075DeserializeCSharpResultAt(13, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.IO
Imports System.Xml
Imports System.Xml.Serialization

Namespace TestNamespace
    Public Class UseXmlReaderForDeserialize
        Public Sub TestMethod(stream As Stream)
            Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
            serializer.Deserialize(stream)
        End Sub
    End Class
End Namespace",
                GetCA3075DeserializeBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeInGetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml.Serialization;

public class UseXmlReaderForDeserialize
{
    Stream stream;
    public XmlSerializer Test
    {
        get
        {
            XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
            serializer.Deserialize(stream);
            return serializer;
        }
    }
}",
                GetCA3075DeserializeCSharpResultAt(13, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.IO
Imports System.Xml.Serialization

Public Class UseXmlReaderForDeserialize
    Private stream As Stream
    Public ReadOnly Property Test() As XmlSerializer
        Get
            Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
            serializer.Deserialize(stream)
            Return serializer
        End Get
    End Property
End Class",
                GetCA3075DeserializeBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeInSetShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml.Serialization;

public class UseXmlReaderForDeserialize
{
    Stream stream;
    XmlSerializer privateDoc;
    public XmlSerializer SetDoc
    {
        set
        {
            if (value == null)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
                serializer.Deserialize(stream);
                privateDoc = serializer;
            }
            else
                privateDoc = value;
        }
    }
}",
                GetCA3075DeserializeCSharpResultAt(16, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.IO
Imports System.Xml.Serialization

Public Class UseXmlReaderForDeserialize
    Private stream As Stream
    Private privateDoc As XmlSerializer
    Public WriteOnly Property SetDoc() As XmlSerializer
        Set
            If value Is Nothing Then
                Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
                serializer.Deserialize(stream)
                privateDoc = serializer
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075DeserializeBasicResultAt(12, 17)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeInTryShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml.Serialization;
using System;

public class UseXmlReaderForDeserialize
{
    Stream stream;
    private void TestMethod()
    {
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
            serializer.Deserialize(stream);
        }
        catch (Exception) { throw; }
        finally { }
    }
}",
                GetCA3075DeserializeCSharpResultAt(14, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.IO
Imports System.Xml.Serialization

Public Class UseXmlReaderForDeserialize
    Private stream As Stream
    Private Sub TestMethod()
        Try
            Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
            serializer.Deserialize(stream)
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DeserializeBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeInCatchShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml.Serialization;
using System;

public class UseXmlReaderForDeserialize
{
    Stream stream;
    private void TestMethod()
    {
        try {        }
        catch (Exception) { 
            XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
            serializer.Deserialize(stream);
        }
        finally { }
    }
}",
                GetCA3075DeserializeCSharpResultAt(14, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.IO
Imports System.Xml.Serialization

Public Class UseXmlReaderForDeserialize
    Private stream As Stream
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
            serializer.Deserialize(stream)
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DeserializeBasicResultAt(12, 13)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeInFinallyShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml.Serialization;
using System;

public class UseXmlReaderForDeserialize
{
    Stream stream;
    private void TestMethod()
    {
        try {        }
        catch (Exception) { throw; }
        finally { 
            XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
            serializer.Deserialize(stream);
        }
    }
}",
                GetCA3075DeserializeCSharpResultAt(15, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.IO
Imports System.Xml.Serialization

Public Class UseXmlReaderForDeserialize
    Private stream As Stream
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
            serializer.Deserialize(stream)
        End Try
    End Sub
End Class",
                GetCA3075DeserializeBasicResultAt(14, 13)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeInDelegateShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml.Serialization;

public class UseXmlReaderForDeserialize
{    
    delegate void Del();

    Del d = delegate ()
    {
        Stream stream = null;
        XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
        serializer.Deserialize(stream);
    };

}",
                GetCA3075DeserializeCSharpResultAt(13, 9)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.IO
Imports System.Xml.Serialization

Public Class UseXmlReaderForDeserialize
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim stream As Stream = Nothing
    Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
    serializer.Deserialize(stream)

End Sub

End Class",
                GetCA3075DeserializeBasicResultAt(11, 5)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeInAsyncAwaitShouldGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

class UseXmlReaderForDeserialize
{
    private async Task TestMethod(Stream stream)
    {
        await Task.Run(() => {
            XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
            serializer.Deserialize(stream);
        });
    }

    private async void TestMethod2()
    {
        await TestMethod(null);
    }
}",
                GetCA3075DeserializeCSharpResultAt(12, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.IO
Imports System.Threading.Tasks
Imports System.Xml.Serialization

Class UseXmlReaderForDeserialize
    Private Async Function TestMethod(stream As Stream) As Task
        Await Task.Run(Function() 
        Dim serializer As New XmlSerializer(GetType(UseXmlReaderForDeserialize))
        serializer.Deserialize(stream)

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod(Nothing)
    End Sub
End Class",
                GetCA3075DeserializeBasicResultAt(10, 9)
            );
        }

        [Fact]
        public async Task UseXmlSerializerDeserializeWithXmlReaderShouldNoGenerateDiagnostic()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace TestNamespace
{
    public class UseXmlReaderForDeserialize
    {
        public void TestMethod(XmlTextReader reader)
        {
            System.Xml.Serialization.XmlSerializer serializer = new XmlSerializer(typeof(UseXmlReaderForDeserialize));
            serializer.Deserialize(reader);
        }
    }
}"
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.IO
Imports System.Xml
Imports System.Xml.Serialization

Namespace TestNamespace
    Public Class UseXmlReaderForDeserialize
        Public Sub TestMethod(reader As XmlTextReader)
            Dim serializer As System.Xml.Serialization.XmlSerializer = New XmlSerializer(GetType(UseXmlReaderForDeserialize))
            serializer.Deserialize(reader)
        End Sub
    End Class
End Namespace");
        }
    }
}