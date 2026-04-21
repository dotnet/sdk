// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.MarkTypesWithSerializableFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.MarkTypesWithSerializableFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class MarkISerializableTypesWithSerializableTests
    {
        [Fact]
        public async Task CA2237SerializableMissingAttrAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                public class CA2237SerializableMissingAttr : ISerializable
                {
                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }",
                GetCA2237CSharpResultAt(4, 30, "CA2237SerializableMissingAttr"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                Public Class CA2237SerializableMissingAttr
                    Implements ISerializable
                
                    Protected Sub New(context As StreamingContext, info As SerializationInfo)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class",
                GetCA2237BasicResultAt(4, 30, "CA2237SerializableMissingAttr"));
        }

        [Fact]
        public async Task CA2237SerializableInternalAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                class CA2237SerializableInternal : ISerializable
                {
                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                Friend Class CA2237SerializableInternal 
                    Implements ISerializable
                
                    Protected Sub New(context As StreamingContext, info As SerializationInfo)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class");
        }

        [Fact]
        public async Task CA2237SerializableWithBaseAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                public class CA2237SerializableWithBase : Base, ISerializable
                {
                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }
                public class Base { }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                Public Class CA2237SerializableWithBase
                    Inherits Base 
                    Implements ISerializable
                
                    Protected Sub New(context As StreamingContext, info As SerializationInfo)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class
                Public Class Base
                End Class");
        }

        [Fact]
        public async Task CA2237SerializableWithBaseAttrAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                public class CA2237SerializableWithBaseAttr : BaseAttr, ISerializable
                {
                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }
                [Serializable]
                public class BaseAttr { }",
                GetCA2237CSharpResultAt(4, 30, "CA2237SerializableWithBaseAttr"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                Public Class CA2237SerializableWithBaseAttr
                    Inherits BaseWithAttr 
                    Implements ISerializable
                
                    Protected Sub New(context As StreamingContext, info As SerializationInfo)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class
                <Serializable>
                Public Class BaseWithAttr
                End Class",
                GetCA2237BasicResultAt(4, 30, "CA2237SerializableWithBaseAttr"));
        }

        [Fact]
        public async Task CA2237_CA2229_NoDiagnosticForInterfaceAndDelegateAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

class A
{
    [Serializable]
    public delegate void B();
}

public interface I : ISerializable
{
    string Name { get; }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.Serialization

Class A
    <Serializable> _
    Public Delegate Sub B()
End Class

Public Interface I
    Inherits ISerializable
    ReadOnly Property Name() As String
End Interface");
        }

        [Fact, WorkItem(6916, "https://github.com/dotnet/roslyn-analyzers/issues/6916")]
        public async Task CA2237_NoDiagnosticForSerializableImplementedOnBaseClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

public class B : A { }

[Serializable]
public class A : ISerializable
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.Serialization

Class B
    Inherits A
End Class

<Serializable>
Public Class A
    Implements ISerializable

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class");
        }

        private static DiagnosticResult GetCA2237CSharpResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2237)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA2237BasicResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2237)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(objectName);
    }
}
