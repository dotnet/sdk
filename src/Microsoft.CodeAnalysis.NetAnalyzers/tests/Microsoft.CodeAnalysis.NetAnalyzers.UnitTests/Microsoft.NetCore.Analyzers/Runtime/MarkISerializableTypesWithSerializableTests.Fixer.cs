// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.MarkTypesWithSerializableFixer>;
using VerifyVB = Microsoft.CodeAnalysis.VisualBasic.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.MarkTypesWithSerializableFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class MarkISerializableTypesWithSerializableFixerTests
    {
        [Fact]
        public async Task CA2237SerializableMissingAttrFix()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Runtime.Serialization;
public class {|CA2237:CA2237SerializableMissingAttr|} : ISerializable
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}",
@"
using System;
using System.Runtime.Serialization;

[Serializable]
public class {|CA2229:CA2237SerializableMissingAttr|} : ISerializable
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Runtime.Serialization
Public Class {|CA2237:CA2237SerializableMissingAttr|}
    Implements ISerializable

    Protected Sub New(context As StreamingContext, info As SerializationInfo)
    End Sub

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class",
@"
Imports System
Imports System.Runtime.Serialization

<Serializable>
Public Class {|CA2229:CA2237SerializableMissingAttr|}
    Implements ISerializable

    Protected Sub New(context As StreamingContext, info As SerializationInfo)
    End Sub

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class");
        }
    }
}
