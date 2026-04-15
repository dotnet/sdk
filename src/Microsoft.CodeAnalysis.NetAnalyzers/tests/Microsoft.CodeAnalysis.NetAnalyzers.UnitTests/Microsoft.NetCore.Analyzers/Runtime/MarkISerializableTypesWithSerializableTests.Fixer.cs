// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.MarkTypesWithSerializableFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.MarkTypesWithSerializableFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class MarkISerializableTypesWithSerializableFixerTests
    {
        [Fact]
        public async Task CA2237SerializableMissingAttrFixAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.Serialization;
public class CA2237SerializableMissingAttr : ISerializable
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}",
                    },
                    ExpectedDiagnostics =
                    {
                        GetCA2237CSharpResultAt(4, 14, "CA2237SerializableMissingAttr"),
                    }
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.Serialization;

[Serializable]
public class CA2237SerializableMissingAttr : ISerializable
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}",
                    },
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
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
                    },
                    ExpectedDiagnostics =
                    {
                        GetCA2237BasicResultAt(4, 14, "CA2237SerializableMissingAttr"),
                    },
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Imports System
Imports System.Runtime.Serialization

<Serializable>
Public Class CA2237SerializableMissingAttr
    Implements ISerializable

    Protected Sub New(context As StreamingContext, info As SerializationInfo)
    End Sub

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class"
                    },
                },
            }.RunAsync();
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
