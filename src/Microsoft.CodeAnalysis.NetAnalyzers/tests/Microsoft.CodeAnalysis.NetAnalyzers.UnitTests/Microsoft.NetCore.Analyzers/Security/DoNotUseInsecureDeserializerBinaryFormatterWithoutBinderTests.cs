// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerBinaryFormatterWithoutBinder,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotUseInsecureDeserializerBinaryFormatterWithoutBinder,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    [Trait(Traits.DataflowAnalysis, Traits.Dataflow.PropertySetAnalysis)]
    public class DoNotUseInsecureDeserializerBinaryFormatterWithoutBinderTests
    {
        private static readonly DiagnosticDescriptor BinderNotSetRule = DoNotUseInsecureDeserializerBinaryFormatterWithoutBinder.RealBinderDefinitelyNotSetDescriptor;

        private static readonly DiagnosticDescriptor BinderMaybeNotSetRule = DoNotUseInsecureDeserializerBinaryFormatterWithoutBinder.RealBinderMaybeNotSetDescriptor;

        private async Task VerifyCSharpWithMyBinderDefinedAsync(string source, params DiagnosticResult[] expected)
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
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestState =
                {
                    Sources = { source, myBinderCSharpSourceCode }
                },
            };

            csharpTest.ExpectedDiagnostics.AddRange(expected);

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task DocSample1_CSharp_Violation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

[Serializable]
public class BookRecord
{
    public string Title { get; set; }
    public AisleLocation Location { get; set; }
}

[Serializable]
public class AisleLocation
{
    public char Aisle { get; set; }
    public byte Shelf { get; set; }
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) formatter.Deserialize(ms);   // CA2301 violation
        }
    }
}",
                GetCSharpResultAt(27, 33, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task DocSample1_VB_Violation_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization.Formatters.Binary

<Serializable()>
Public Class BookRecord
    Public Property Title As String
    Public Property Location As AisleLocation
End Class

<Serializable()>
Public Class AisleLocation
    Public Property Aisle As Char
    Public Property Shelf As Byte
End Class

Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim formatter As BinaryFormatter = New BinaryFormatter()
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(formatter.Deserialize(ms), BookRecord)    ' CA2301 violation
        End Using
    End Function
End Class",
                GetBasicResultAt(22, 26, BinderNotSetRule, "Function BinaryFormatter.Deserialize(serializationStream As Stream) As Object"));
        }

        [Fact]
        public async Task DocSample1_CSharp_Solution_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

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

[Serializable]
public class BookRecord
{
    public string Title { get; set; }
    public AisleLocation Location { get; set; }
}

[Serializable]
public class AisleLocation
{
    public char Aisle { get; set; }
    public byte Shelf { get; set; }
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        formatter.Binder = new BookRecordSerializationBinder();
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord)formatter.Deserialize(ms);
        }
    }
}");
        }

        [Fact]
        public async Task DocSample1_VB_Solution_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Formatters.Binary

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

<Serializable()>
Public Class BookRecord
    Public Property Title As String
    Public Property Location As AisleLocation
End Class

<Serializable()>
Public Class AisleLocation
    Public Property Aisle As Char
    Public Property Shelf As Byte
End Class

Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim formatter As BinaryFormatter = New BinaryFormatter()
        formatter.Binder = New BookRecordSerializationBinder()
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(formatter.Deserialize(ms), BookRecord)
        End Using
    End Function
End Class");
        }

        [Fact]
        public async Task DocSample2_CSharp_Violation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

[Serializable]
public class BookRecord
{
    public string Title { get; set; }
    public AisleLocation Location { get; set; }
}

[Serializable]
public class AisleLocation
{
    public char Aisle { get; set; }
    public byte Shelf { get; set; }
}

public class ExampleClass
{
    public BinaryFormatter Formatter { get; set; }

    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) this.Formatter.Deserialize(ms);    // CA2302 violation
        }
    }
}",
            GetCSharpResultAt(28, 33, BinderMaybeNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task DocSample2_VB_Violation_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization.Formatters.Binary

<Serializable()>
Public Class BookRecord
    Public Property Title As String
    Public Property Location As AisleLocation
End Class

<Serializable()>
Public Class AisleLocation
    Public Property Aisle As Char
    Public Property Shelf As Byte
End Class

Public Class ExampleClass
    Public Property Formatter As BinaryFormatter

    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(Me.Formatter.Deserialize(ms), BookRecord)    ' CA2302 violation
        End Using
    End Function
End Class",
                GetBasicResultAt(23, 26, BinderMaybeNotSetRule, "Function BinaryFormatter.Deserialize(serializationStream As Stream) As Object"));
        }

        [Fact]
        public async Task DocSample3_CSharp_Violation_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

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

[Serializable]
public class BookRecord
{
    public string Title { get; set; }
    public AisleLocation Location { get; set; }
}

[Serializable]
public class AisleLocation
{
    public char Aisle { get; set; }
    public byte Shelf { get; set; }
}

public class Binders
{
    public static SerializationBinder BookRecord =
        new BookRecordSerializationBinder();
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        formatter.Binder = Binders.BookRecord;
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord)formatter.Deserialize(ms);    // CA2302 violation
        }
    }
}",
                GetCSharpResultAt(55, 32, BinderMaybeNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task DocSample3_VB_Violation_Diagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Formatters.Binary

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

<Serializable()>
Public Class BookRecord
    Public Property Title As String
    Public Property Location As AisleLocation
End Class

<Serializable()>
Public Class AisleLocation
    Public Property Aisle As Char
    Public Property Shelf As Byte
End Class

Public Class Binders
    Public Shared Property BookRecord As SerializationBinder = New BookRecordSerializationBinder()
End Class

Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim formatter As BinaryFormatter = New BinaryFormatter()
        formatter.Binder = Binders.BookRecord
        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(formatter.Deserialize(ms), BookRecord)    ' CA2302 violation
        End Using
    End Function
End Class",
                GetBasicResultAt(45, 26, BinderMaybeNotSetRule, "Function BinaryFormatter.Deserialize(serializationStream As Stream) As Object"));
        }

        [Fact]
        public async Task DocSample3_CSharp_Solution_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

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

[Serializable]
public class BookRecord
{
    public string Title { get; set; }
    public AisleLocation Location { get; set; }
}

[Serializable]
public class AisleLocation
{
    public char Aisle { get; set; }
    public byte Shelf { get; set; }
}

public class Binders
{
    public static SerializationBinder BookRecord =
        new BookRecordSerializationBinder();
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        BinaryFormatter formatter = new BinaryFormatter();

        // Ensure that Binder is always non-null before deserializing
        formatter.Binder = Binders.BookRecord ?? throw new Exception(""Expected non-null"");

        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord)formatter.Deserialize(ms);
        }
    }
}");
        }

        [Fact]
        public async Task DocSample3_VB_Solution_NoDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Formatters.Binary

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

<Serializable()>
Public Class BookRecord
    Public Property Title As String
    Public Property Location As AisleLocation
End Class

<Serializable()>
Public Class AisleLocation
    Public Property Aisle As Char
    Public Property Shelf As Byte
End Class

Public Class Binders
    Public Shared Property BookRecord As SerializationBinder = New BookRecordSerializationBinder()
End Class

Public Class ExampleClass
    Public Function DeserializeBookRecord(bytes As Byte()) As BookRecord
        Dim formatter As BinaryFormatter = New BinaryFormatter()

        ' Ensure that Binder is always non-null before deserializing
        formatter.Binder = If(Binders.BookRecord, New Exception(""Expected non-null""))

        Using ms As MemoryStream = New MemoryStream(bytes)
            Return CType(formatter.Deserialize(ms), BookRecord)
        End Using
    End Function
End Class");
        }

        [Fact]
        public async Task Deserialize_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfUnsafeDeserialize(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(12, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_OptionalParameters_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfDeserialize(byte[] bytes = null)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(12, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        // Ideally, we'd detect that formatter.Binder is always null.
        [Fact]
        public async Task DeserializeWithInstanceField_Diagnostic_NotIdeal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        BinaryFormatter formatter = new BinaryFormatter();

        public object BfUnsafeDeserialize(byte[] bytes)
        {
            return this.formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(13, 20, BinderMaybeNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/1851")]
        public async Task DeserializeWithInstanceField_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        BinaryFormatter formatter = new BinaryFormatter() { Binder = new MyBinder() };

        public object BfUnsafeDeserialize(byte[] bytes)
        {
            return this.formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}");

            // Ideally, we'd see that this.formatter.Binder is set and *not* generate a diagnostic.
        }

        [Fact]
        public async Task Deserialize_BinderMaybeSet_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfUnsafeDeserialize(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            if (Environment.MachineName.StartsWith(""a""))
            {
                formatter.Binder = new MyBinder();
            }

            return formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}",
            GetCSharpResultAt(18, 20, BinderMaybeNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_BinderSet_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfUnsafeDeserialize(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Binder = new MyBinder();
            return formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}");
        }

        [Fact]
        public async Task TwoDeserializersOneBinderOnFirst_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfUnsafeDeserialize(byte[] bytes1, byte[] bytes2)
        {
            if (Environment.GetEnvironmentVariable(""USEFIRST"") == ""1"")
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Binder = new MyBinder();
                return bf.Deserialize(new MemoryStream(bytes1));
            }
            else
            {
                return new BinaryFormatter().Deserialize(new MemoryStream(bytes2));
            }
        }
    }
}",
                GetCSharpResultAt(20, 24, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task TwoDeserializersOneBinderOnSecond_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfUnsafeDeserialize(byte[] bytes1, byte[] bytes2)
        {
            if (Environment.GetEnvironmentVariable(""USEFIRST"") == ""1"")
            {
                return new BinaryFormatter().Deserialize(new MemoryStream(bytes1));
            }
            else
            {
                return (new BinaryFormatter() { Binder = new MyBinder() }).Deserialize(new MemoryStream(bytes2));
            }
        }
    }
}",
                GetCSharpResultAt(14, 24, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));

        }

        [Fact]
        public async Task TwoDeserializersNoBinder_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfUnsafeDeserialize(byte[] bytes1, byte[] bytes2)
        {
            if (Environment.GetEnvironmentVariable(""USEFIRST"") == ""1"")
            {
                return new BinaryFormatter().Deserialize(new MemoryStream(bytes1));
            }
            else
            {
                return new BinaryFormatter().Deserialize(new MemoryStream(bytes2));
            }
        }
    }
}",
                GetCSharpResultAt(14, 24, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"),
                GetCSharpResultAt(18, 24, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));

        }

        [Fact]
        public async Task BinderSetInline_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object BfDeserialize(byte[] bytes)
        {
            return (new BinaryFormatter() { Binder = new MyBinder() }).Deserialize(new MemoryStream(bytes));
        }
    }
}");
        }

        [Fact]
        public async Task Serialize_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public byte[] S(object o)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, o);
            return ms.ToArray();
        }
    }
}");
        }

        [Fact]
        public async Task Deserialize_LoopBinderSetAfter_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public void D(byte[][] bytes, object[] objects)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            for (int i = 0; i < 2; i++)
            {
                objects[i] = formatter.Deserialize(new MemoryStream(bytes[i]));
                formatter.Binder = new MyBinder();
            }
        }
    }
}",
                GetCSharpResultAt(15, 30, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_LoopBinderSetBefore_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public void D(byte[][] bytes, object[] objects)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            for (int i = 0; i < 2; i++)
            {
                formatter.Binder = new MyBinder();
                objects[i] = formatter.Deserialize(new MemoryStream(bytes[i]));
            }
        }
    }
}");
        }

        [Fact]
        public async Task Deserialize_LoopBinderSetBeforeMaybe_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public void D(byte[][] bytes, object[] objects)
        {
            Random r = new Random();
            BinaryFormatter formatter = new BinaryFormatter();
            for (int i = 0; i < 2; i++)
            {
                if (r.Next() % 2 == 0)
                    formatter.Binder = new MyBinder();
                objects[i] = formatter.Deserialize(new MemoryStream(bytes[i]));
                
            }
        }
    }
}",
                GetCSharpResultAt(18, 30, BinderMaybeNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_InvokedAsDelegate_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        delegate object DeserializeDelegate(Stream s);

        public object DeserializeWithDelegate(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            DeserializeDelegate del = formatter.Deserialize;
            return del(new MemoryStream(bytes));
        }
    }
}",
                GetCSharpResultAt(15, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_BranchInvokedAsDelegate_Diagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
                TestCode = @"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        delegate object DeserializeDelegate(Stream s, HeaderHandler h);

        public object DeserializeWithDelegate(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            DeserializeDelegate del;
            if (Environment.GetEnvironmentVariable(""USEUNSAFE"") == ""1"")
                del = formatter.UnsafeDeserialize;
            else
                del = formatter.Deserialize;
            return del(new MemoryStream(bytes), null);
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpResultAt(21, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream, HeaderHandler handler)"),
                    GetCSharpResultAt(21, 20, BinderNotSetRule, "object BinaryFormatter.UnsafeDeserialize(Stream serializationStream, HeaderHandler handler)"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Deserialize_BranchInvokedAsDelegate_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        delegate object DeserializeDelegate(Stream s, HeaderHandler h);

        public object DeserializeWithDelegate(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Binder = new MyBinder();
            DeserializeDelegate del;
            if (Environment.GetEnvironmentVariable(""USEUNSAFE"") == ""1"")
                del = formatter.UnsafeDeserialize;
            else
                del = formatter.Deserialize;
            return del(new MemoryStream(bytes), null);
        }
    }
}");
        }

        [Fact]
        public async Task Deserialize_Property_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        private byte[] serialized;
        private string data;

        public byte[] Serialized
        {
            get { return this.serialized; }
            set
            {
                this.serialized = value;
                this.data = (string) new BinaryFormatter().Deserialize(new MemoryStream(value));
            }
        }
    }
}",
                GetCSharpResultAt(20, 38, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_Constructor_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        private string data;

        public Program(byte[] bytes)
        {
            this.data = (string) new BinaryFormatter().Deserialize(new MemoryStream(bytes));
        }
    }
}",
                GetCSharpResultAt(15, 34, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_FieldInitializer_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class SomeData
    {
        public static byte[] bytes = new byte[] { 0, 1, 2 };
        public string data = (string) new BinaryFormatter().Deserialize(new MemoryStream(bytes));
    }
}",
                GetCSharpResultAt(12, 39, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task Deserialize_InConstructorAndMethod_Diagnostics()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

abstract class Base
{
    object baseData;
    object otherData;

    protected Base(BinaryFormatter bf, byte[] bytes, object o)
    {
        baseData = bf.Deserialize(new MemoryStream(bytes));
        otherData = o;
    }
}

class Derived : Base
{
    object derivedData;

    public Derived(byte[] bytes1, byte[] bytes2, byte[] bytes3)
        : base(
            new BinaryFormatter(), 
            bytes1,
            new BinaryFormatter().Deserialize(new MemoryStream(bytes2)))
    {
        derivedData = new BinaryFormatter().Deserialize(new MemoryStream(bytes3));
    }

    public object Deserialize(byte[] bytes)
    {
        return new BinaryFormatter().Deserialize(new MemoryStream(bytes));
    }
}"
            ,
            GetCSharpResultAt(14, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"),
            GetCSharpResultAt(27, 13, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"),
            GetCSharpResultAt(29, 23, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"),
            GetCSharpResultAt(34, 16, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task BinderVariableSetInAllBranches_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            SerializationBinder firstBinder = new MyBinder();
            SerializationBinder binder;
            if (Environment.GetEnvironmentVariable(""RANDOMROLL"") == ""4"")
                binder = new MyBinder();
            else
                binder = firstBinder;
            return new BinaryFormatter() { Binder = binder }.Deserialize(new MemoryStream(bytes));
        }
    }
}");
        }

        [Fact]
        public async Task BinderParameter_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object Deserialize(SerializationBinder binder, byte[] bytes)
        {
            binder = binder ?? new MyBinder();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Binder = binder;
            return formatter.Deserialize(new MemoryStream(bytes));
        }
    }
}");
        }

        [Fact]
        public async Task BinderNotNullInsideIf_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object Deserialize(SerializationBinder binder, byte[] bytes)
        {
            if (binder != null)
            {
                return new BinaryFormatter() { Binder = binder }.Deserialize(new MemoryStream(bytes));
            }
            else
            {
                return null;
            }
        }
    }
}");
        }

        [Fact]
        public async Task SomeOtherSerializer_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter() { Binder = new MyBinder() };
            formatter.Deserialize(null);
            SomeOtherSerializer serializer = new SomeOtherSerializer();
            return serializer.Deserialize(bytes);
        }
    }
}");
        }

        [Fact]
        public async Task OtherMethodInstantiatesWithoutBinder_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            return GetBinaryFormatter().Deserialize(new MemoryStream(bytes));
        }

        private BinaryFormatter GetBinaryFormatter()
        {
            return new BinaryFormatter();
        }
    }
}",
            GetCSharpResultAt(14, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task OtherMethodInstantiatesWithBinderMaybe_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            return GetBinaryFormatter().Deserialize(new MemoryStream(bytes));
        }

        private BinaryFormatter GetBinaryFormatter()
        {
            SerializationBinder binder = null;
            if (Environment.GetEnvironmentVariable(""RANDOMROLL"") == ""4"")
                binder = new MyBinder();
            return new BinaryFormatter() { Binder = binder };
        }
    }
}",
            GetCSharpResultAt(14, 20, BinderMaybeNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Fact]
        public async Task OtherMethodInstantiatesWithBinder_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            return GetBinaryFormatter().Deserialize(new MemoryStream(bytes));
        }

        private BinaryFormatter GetBinaryFormatter()
        {
            return new BinaryFormatter() { Binder = new MyBinder() };
        }
    }
}");
        }

        [Fact]
        public async Task OtherMethodDeserializesWithoutBinder_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            return DoDeserialization(new BinaryFormatter(), new MemoryStream(bytes));
        }

        private object DoDeserialization(BinaryFormatter formatter, Stream stream)
        {
            return formatter.Deserialize(stream);
        }
    }
}",
            GetCSharpResultAt(19, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));

            // Ideally we'd see Binder is never set, rather than maybe not set.
        }

        [Fact]
        public async Task OtherMethodDeserializesWithoutBinderUsingDelegate_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        delegate object Des(Stream s);

        public object Deserialize(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            return DoDeserialization(formatter.Deserialize, new MemoryStream(bytes));
        }

        private object DoDeserialization(Des des, Stream stream)
        {
            return des(stream);
        }
    }
}");
            // Ideally we'd be able to detect this, but it's kinda a weird case.
        }

        [Fact]
        public async Task OtherMethodDeserializesWithoutBinderUsingBinaryFormatter_Diagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;

namespace Blah
{
    public class Program
    {
        public object Deserialize(byte[] bytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Binder = null;
            return DoDeserialization(formatter, new MemoryStream(bytes));
        }

        private object DoDeserialization(BinaryFormatter formatter, Stream stream)
        {
            return formatter.Deserialize(stream);
        }
    }
}",
            GetCSharpResultAt(21, 20, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = DeserializeBookRecord")]
        [InlineData(@"dotnet_code_quality.CA2301.excluded_symbol_names = DeserializeBookRecord
                      dotnet_code_quality.CA2302.excluded_symbol_names = DeserializeBookRecord")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = DeserializeBookRecord")]
        [InlineData(@"dotnet_code_quality.CA2301.excluded_symbol_names = DeserializeBook*
                      dotnet_code_quality.CA2302.excluded_symbol_names = DeserializeBook*")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var csharpTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

[Serializable]
public class BookRecord
{
    public string Title { get; set; }
    public string Author { get; set; }
    public int PageCount { get; set; }
    public AisleLocation Location { get; set; }
}

[Serializable]
public class AisleLocation
{
    public char Aisle { get; set; }
    public byte Shelf { get; set; }
}

public class ExampleClass
{
    public BookRecord DeserializeBookRecord(byte[] bytes)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            return (BookRecord) formatter.Deserialize(ms);
        }
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };

            if (editorConfigText.Length == 0)
            {
                csharpTest.ExpectedDiagnostics.Add(GetCSharpResultAt(29, 33, BinderNotSetRule, "object BinaryFormatter.Deserialize(Stream serializationStream)"));
            }

            await csharpTest.RunAsync();
        }

        [Fact]
        public async Task Deserialize_SharedBinderInstance_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public static SerializationBinder B { get; set; }

        private object DoDeserialization(Stream stream)
        {
            BinaryFormatter f = new BinaryFormatter();
            f.Binder = B ?? throw new Exception(""Expected a non-null SerializationBinder"");
            return f.Deserialize(stream);
        }
    }
}");
        }

        [Fact]
        public async Task Deserialize_SharedBinderInstanceIntermediate_NoDiagnostic()
        {
            await VerifyCSharpWithMyBinderDefinedAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Blah
{
    public class Program
    {
        public static SerializationBinder B { get; set; }

        private object DoDeserialization(Stream stream)
        {
            BinaryFormatter f = new BinaryFormatter();
            SerializationBinder b = B ?? throw new Exception(""Expected a non-null SerializationBinder"");
            f.Binder = b;
            return f.Deserialize(stream);
        }
    }
}");
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
