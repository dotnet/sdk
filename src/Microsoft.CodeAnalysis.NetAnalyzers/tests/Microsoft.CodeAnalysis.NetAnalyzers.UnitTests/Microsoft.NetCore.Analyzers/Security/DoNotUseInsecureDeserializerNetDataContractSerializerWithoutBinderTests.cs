// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerNetDataContractSerializerWithoutBinder,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerNetDataContractSerializerWithoutBinder,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PropertySetAnalysis)]
    public class DoNotUseInsecureDeserializerNetDataContractSerializerWithoutBinderTests
    {
        private static readonly DiagnosticDescriptor BinderNotSetRule = DoNotUseInsecureDeserializerNetDataContractSerializerWithoutBinder.RealBinderDefinitelyNotSetDescriptor;

        private static readonly DiagnosticDescriptor BinderMaybeNotSetRule = DoNotUseInsecureDeserializerNetDataContractSerializerWithoutBinder.RealBinderMaybeNotSetDescriptor;

        protected async Task VerifyCSharpAnalyzerWithMyBinderDefinedAsync(string source, params DiagnosticResult[] expected)
        {
            string myBinderCSharpSourceCode = @"
using System;
using System.Runtime.Serialization;

namespace Blah
{
    public class MyBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            throw new NotImplementedException();
        }
    }

    public class SomeOtherSerializer
    {
        public object Deserialize(byte[] bytes)
        {
            return null;
        }
    }
}
            ";

            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSerialization,
                TestState =
                {
                    Sources = { source, myBinderCSharpSourceCode },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task DocSample1_CSharp_Violation_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[DataContract]
public class BookRecord
{
    [DataMember]
    public string Title { get; set; }

    [DataMember]
    public AisleLocation Location { get; set; }
}

[DataContract]
public class AisleLocation
{
    [DataMember]
    public char Aisle { get; set; }

    [DataMember]
    public byte Shelf { get; set; }
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        NetDataContractSerializer serializer = new NetDataContractSerializer();
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) serializer.Deserialize(ms);    // CA2312 violation
        }
    }
}",
                GetCSharpResultAt(33, 33, BinderNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task DocSample1_VB_Violation_DiagnosticAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

<DataContract()>
Public Class BookRecord
    <DataMember()>
    Public Property Title As String

    <DataMember()>
    Public Property Location As AisleLocation
End Class

<DataContract()>
Public Class AisleLocation
    <DataMember()>
    Public Property Aisle As Char

    <DataMember()>
    Public Property Shelf As Byte
End Class

Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim serializer As NetDataContractSerializer = New NetDataContractSerializer()
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(serializer.Deserialize(ms), BookRecord)    ' CA2312 violation
        End Using
    End Function
End Class",
                GetBasicResultAt(28, 26, BinderNotSetRule, "Function NetDataContractSerializer.Deserialize(stream As Stream) As Object"));
        }

        [Fact]
        public async Task DocSample1_CSharp_Solution_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public class BookRecordSerializationBinder : SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        // One way to discover expected types is through testing deserialization
        // of **valid** data and logging the types used.

        ////Console.WriteLine($""BindToType('{assemblyName}', '{typeName}')"");

        if (typeName == ""BookRecord"" || typeName == ""AisleLocation"")
        {
            return null;
        }
        else
        {
            throw new ArgumentException(""Unexpected type"", nameof(typeName));
        }
    }
}

[DataContract]
public class BookRecord
{
    [DataMember]
    public string Title { get; set; }

    [DataMember]
    public AisleLocation Location { get; set; }
}

[DataContract]
public class AisleLocation
{
    [DataMember]
    public char Aisle { get; set; }

    [DataMember]
    public byte Shelf { get; set; }
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        NetDataContractSerializer serializer = new NetDataContractSerializer();
        serializer.Binder = new BookRecordSerializationBinder();
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) serializer.Deserialize(ms);
        }
    }
}");
        }

        [Fact]
        public async Task DocSample1_VB_Solution_NoDiagnosticAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Public Class BookRecordSerializationBinder
    Inherits SerializationBinder

    Public Overrides Function BindToType(assemblyName As String, typeName As String) As Type
        ' One way to discover expected types is through testing deserialization
        ' of **valid** data and logging the types used.

        'Console.WriteLine($""BindToType('{assemblyName}', '{typeName}')"")

        If typeName = ""BinaryFormatterVB.BookRecord"" Or typeName = ""BinaryFormatterVB.AisleLocation"" Then
            Return Nothing
        Else
            Throw New ArgumentException(""Unexpected type"", NameOf(typeName))
        End If
    End Function
End Class

<DataContract()>
Public Class BookRecord
    <DataMember()>
    Public Property Title As String

    <DataMember()>
    Public Property Location As AisleLocation
End Class

<DataContract()>
Public Class AisleLocation
    <DataMember()>
    Public Property Aisle As Char

    <DataMember()>
    Public Property Shelf As Byte
End Class

Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim serializer As NetDataContractSerializer = New NetDataContractSerializer()
        serializer.Binder = New BookRecordSerializationBinder()
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(serializer.Deserialize(ms), BookRecord)
        End Using
    End Function
End Class");
        }

        [Fact]
        public async Task DocSample2_CSharp_Violation_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[DataContract]
public class BookRecord
{
    [DataMember]
    public string Title { get; set; }

    [DataMember]
    public AisleLocation Location { get; set; }
}

[DataContract]
public class AisleLocation
{
    [DataMember]
    public char Aisle { get; set; }

    [DataMember]
    public byte Shelf { get; set; }
}

public class ExampleClass
{
    public NetDataContractSerializer Serializer { get; set; }

    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) this.Serializer.Deserialize(ms);
        }
    }
}",
                GetCSharpResultAt(34, 33, BinderMaybeNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task DocSample2_VB_Violation_DiagnosticAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

<DataContract()>
Public Class BookRecord
    <DataMember()>
    Public Property Title As String

    <DataMember()>
    Public Property Location As AisleLocation
End Class

<DataContract()>
Public Class AisleLocation
    <DataMember()>
    Public Property Aisle As Char

    <DataMember()>
    Public Property Shelf As Byte
End Class

Public Class ExampleClass
    Public Property Serializer As NetDataContractSerializer

    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(Me.Serializer.Deserialize(ms), BookRecord)
        End Using
    End Function
End Class",
                GetBasicResultAt(29, 26, BinderMaybeNotSetRule, "Function NetDataContractSerializer.Deserialize(stream As Stream) As Object"));
        }

        [Fact]
        public async Task DocSample3_CSharp_Violation_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public class BookRecordSerializationBinder : SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        // One way to discover expected types is through testing deserialization
        // of **valid** data and logging the types used.

        ////Console.WriteLine($""BindToType('{assemblyName}', '{typeName}')"");

        if (typeName == ""BookRecord"" || typeName == ""AisleLocation"")
        {
            return null;
        }
        else
        {
            throw new ArgumentException(""Unexpected type"", nameof(typeName));
        }
    }
}

[DataContract]
public class BookRecord
{
    [DataMember]
    public string Title { get; set; }

    [DataMember]
    public AisleLocation Location { get; set; }
}

[DataContract]
public class AisleLocation
{
    [DataMember]
    public char Aisle { get; set; }

    [DataMember]
    public byte Shelf { get; set; }
}

public class Binders
{
    public static SerializationBinder BookRecord = new BookRecordSerializationBinder();
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        NetDataContractSerializer serializer = new NetDataContractSerializer();
        serializer.Binder = Binders.BookRecord;
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) serializer.Deserialize(ms);
        }
    }
}",
                GetCSharpResultAt(59, 33, BinderMaybeNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task DocSample3_VB_Violation_DiagnosticAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Public Class BookRecordSerializationBinder
    Inherits SerializationBinder

    Public Overrides Function BindToType(assemblyName As String, typeName As String) As Type
        ' One way to discover expected types is through testing deserialization
        ' of **valid** data and logging the types used.

        'Console.WriteLine($""BindToType('{assemblyName}', '{typeName}')"")

        If typeName = ""BinaryFormatterVB.BookRecord"" Or typeName = ""BinaryFormatterVB.AisleLocation"" Then
            Return Nothing
        Else
            Throw New ArgumentException(""Unexpected type"", NameOf(typeName))
        End If
    End Function
End Class

<DataContract()>
Public Class BookRecord
    <DataMember()>
    Public Property Title As String

    <DataMember()>
    Public Property Location As AisleLocation
End Class

<DataContract()>
Public Class AisleLocation
    <DataMember()>
    Public Property Aisle As Char

    <DataMember()>
    Public Property Shelf As Byte
End Class

Public Class Binders
    Public Shared Property BookRecord As SerializationBinder = New BookRecordSerializationBinder()
End Class


Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim serializer As NetDataContractSerializer = New NetDataContractSerializer()
        serializer.Binder = Binders.BookRecord
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(serializer.Deserialize(ms), BookRecord)   ' CA2312 violation
        End Using
    End Function
End Class",
                GetBasicResultAt(51, 26, BinderMaybeNotSetRule, "Function NetDataContractSerializer.Deserialize(stream As Stream) As Object"));
        }

        [Fact]
        public async Task DocSample3_CSharp_Solution_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public class BookRecordSerializationBinder : SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        // One way to discover expected types is through testing deserialization
        // of **valid** data and logging the types used.

        ////Console.WriteLine($""BindToType('{assemblyName}', '{typeName}')"");

        if (typeName == ""BookRecord"" || typeName == ""AisleLocation"")
        {
            return null;
        }
        else
        {
            throw new ArgumentException(""Unexpected type"", nameof(typeName));
        }
    }
}

[DataContract]
public class BookRecord
{
    [DataMember]
    public string Title { get; set; }

    [DataMember]
    public AisleLocation Location { get; set; }
}

[DataContract]
public class AisleLocation
{
    [DataMember]
    public char Aisle { get; set; }

    [DataMember]
    public byte Shelf { get; set; }
}

public class Binders
{
    public static SerializationBinder BookRecord = new BookRecordSerializationBinder();
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        NetDataContractSerializer serializer = new NetDataContractSerializer();

        // Ensure that Binder is always non-null before deserializing
        serializer.Binder = Binders.BookRecord ?? throw new Exception(""Expected non-null"");

        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) serializer.Deserialize(ms);
        }
    }
}");
        }

        [Fact]
        public async Task DocSample3_VB_Solution_NoDiagnosticAsync()
        {
            await VerifyBasicAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Public Class BookRecordSerializationBinder
    Inherits SerializationBinder

    Public Overrides Function BindToType(assemblyName As String, typeName As String) As Type
        ' One way to discover expected types is through testing deserialization
        ' of **valid** data and logging the types used.

        'Console.WriteLine($""BindToType('{assemblyName}', '{typeName}')"")

        If typeName = ""BinaryFormatterVB.BookRecord"" Or typeName = ""BinaryFormatterVB.AisleLocation"" Then
            Return Nothing
        Else
            Throw New ArgumentException(""Unexpected type"", NameOf(typeName))
        End If
    End Function
End Class

<DataContract()>
Public Class BookRecord
    <DataMember()>
    Public Property Title As String

    <DataMember()>
    Public Property Location As AisleLocation
End Class

<DataContract()>
Public Class AisleLocation
    <DataMember()>
    Public Property Aisle As Char

    <DataMember()>
    Public Property Shelf As Byte
End Class

Public Class Binders
    Public Shared Property BookRecord As SerializationBinder = New BookRecordSerializationBinder()
End Class

Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim serializer As NetDataContractSerializer = New NetDataContractSerializer()

        ' Ensure that Binder is always non-null before deserializing
        serializer.Binder = If(Binders.BookRecord, New Exception(""Expected non-null""))

        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(serializer.Deserialize(ms), BookRecord)
        End Using
    End Function
End Class");
        }

        [Fact]
        public async Task Deserialize_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(12, 20, BinderNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        // Ideally, we'd detect that serializer.Binder is always null.
        [Fact]
        public async Task DeserializeWithInstanceField_Diagnostic_NotIdealAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        NetDataContractSerializer serializer = new NetDataContractSerializer();

        public object TestMethod(byte[] bytes)
        {
            return this.serializer.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(13, 20, BinderMaybeNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task Deserialize_BinderMaybeSet_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            if (Environment.MachineName.StartsWith(""a""))
            {
                serializer.Binder = new MyBinder();
            }

            return serializer.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(18, 20, BinderMaybeNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task Deserialize_BinderSet_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            serializer.Binder = new MyBinder();
            return serializer.Deserialize(new MemoryStream(bytes));
        }
    }
}");
        }

        [Fact]
        public async Task TwoDeserializersOneBinderOnFirst_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes1, byte[] bytes2)
        {
            if (Environment.GetEnvironmentVariable(""USEFIRST"") == ""1"")
            {
                NetDataContractSerializer bf = new NetDataContractSerializer();
                bf.Binder = new MyBinder();
                return bf.Deserialize(new MemoryStream(bytes1));
            }
            else
            {
                return new NetDataContractSerializer().Deserialize(new MemoryStream(bytes2));
            }
        }
    }
}",
                GetCSharpResultAt(20, 24, BinderNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task TwoDeserializersOneBinderOnSecond_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes1, byte[] bytes2)
        {
            if (Environment.GetEnvironmentVariable(""USEFIRST"") == ""1"")
            {
                return new NetDataContractSerializer().Deserialize(new MemoryStream(bytes1));
            }
            else
            {
                return (new NetDataContractSerializer() { Binder = new MyBinder() }).Deserialize(new MemoryStream(bytes2));
            }
        }
    }
}",
                GetCSharpResultAt(14, 24, BinderNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));

        }

        [Fact]
        public async Task TwoDeserializersNoBinder_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes1, byte[] bytes2)
        {
            if (Environment.GetEnvironmentVariable(""USEFIRST"") == ""1"")
            {
                return new NetDataContractSerializer().Deserialize(new MemoryStream(bytes1));
            }
            else
            {
                return new NetDataContractSerializer().Deserialize(new MemoryStream(bytes2));
            }
        }
    }
}",
                GetCSharpResultAt(14, 24, BinderNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"),
                GetCSharpResultAt(18, 24, BinderNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));

        }

        [Fact]
        public async Task BinderSetInline_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes)
        {
            return (new NetDataContractSerializer() { Binder = new MyBinder() }).Deserialize(new MemoryStream(bytes));
        }
    }
}");
        }

        [Fact]
        public async Task Serialize_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public byte[] S(object o)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            MemoryStream ms = new MemoryStream();
            serializer.Serialize(ms, o);
            return ms.ToArray();
        }
    }
}");
        }

        [Fact]
        public async Task Deserialize_InvokedAsDelegate_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        delegate object DeserializeDelegate(Stream s);

        public object DeserializeWithDelegate(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            DeserializeDelegate del = serializer.Deserialize;
            return del(new MemoryStream(bytes));
        }
    }
}",
                GetCSharpResultAt(15, 20, BinderNotSetRule, "object NetDataContractSerializer.Deserialize(Stream stream)"));
        }

        [Fact]
        public async Task ReadObject_Stream_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.ReadObject(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(12, 20, BinderNotSetRule, "object XmlObjectSerializer.ReadObject(Stream stream)"));
        }

        [Fact]
        public async Task ReadObject_Stream_BinderMaybeSet_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            if (Environment.MachineName.StartsWith(""a""))
            {
                serializer.Binder = new MyBinder();
            }

            return serializer.ReadObject(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(18, 20, BinderMaybeNotSetRule, "object XmlObjectSerializer.ReadObject(Stream stream)"));
        }

        [Fact]
        public async Task ReadObject_Stream_BinderSet_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        public object TestMethod(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            serializer.Binder = new MyBinder();
            return serializer.ReadObject(new MemoryStream(bytes));
        }
    }
}");
        }

        [Fact]
        public async Task ReadObject_Stream_InvokedAsDelegate_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;

namespace Blah
{
    public class Program
    {
        delegate object DeserializeDelegate(Stream s);

        public object DeserializeWithDelegate(byte[] bytes)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            DeserializeDelegate del = serializer.ReadObject;
            return del(new MemoryStream(bytes));
        }
    }
}",
                GetCSharpResultAt(15, 20, BinderNotSetRule, "object XmlObjectSerializer.ReadObject(Stream stream)"));
        }

        [Fact]
        public async Task ReadObject_XmlReader_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class Program
    {
        public object TestMethod(XmlReader xmlReader)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            return serializer.ReadObject(xmlReader);
        }
    }
}",
            GetCSharpResultAt(13, 20, BinderNotSetRule, "object NetDataContractSerializer.ReadObject(XmlReader reader)"));
        }

        [Fact]
        public async Task ReadObject_XmlReader_BinderMaybeSet_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class Program
    {
        public object TestMethod(XmlReader xmlReader)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            if (Environment.MachineName.StartsWith(""a""))
            {
                serializer.Binder = new MyBinder();
            }

            return serializer.ReadObject(xmlReader);
        }
    }
}",
            GetCSharpResultAt(19, 20, BinderMaybeNotSetRule, "object NetDataContractSerializer.ReadObject(XmlReader reader)"));
        }

        [Fact]
        public async Task ReadObject_XmlReader_BinderSet_NoDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class Program
    {
        public object TestMethod(XmlReader xmlReader)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            serializer.Binder = new MyBinder();
            return serializer.ReadObject(xmlReader);
        }
    }
}");
        }

        [Fact]
        public async Task ReadObject_XmlReader_InvokedAsDelegate_DiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Blah
{
    public class Program
    {
        delegate object DeserializeDelegate(XmlReader r);

        public object DeserializeWithDelegate(XmlReader xmlReader)
        {
            NetDataContractSerializer serializer = new NetDataContractSerializer();
            DeserializeDelegate del = serializer.ReadObject;
            return del(xmlReader);
        }
    }
}",
                GetCSharpResultAt(16, 20, BinderNotSetRule, "object NetDataContractSerializer.ReadObject(XmlReader reader)"));
        }

        private static async Task VerifyCSharpAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSerialization,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static async Task VerifyBasicAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var csharpTest = new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithSerialization,
                TestState =
                {
                    Sources = { source },
                }
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyCS.Diagnostic(rule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
           => VerifyVB.Diagnostic(rule)
               .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
               .WithArguments(arguments);
    }
}
