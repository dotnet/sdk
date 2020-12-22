// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.ImplementSerializationConstructorsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.ImplementSerializationConstructorsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class ImplementSerializationConstructorsFixerTests
    {
        [Fact]
        public async Task CA2229NoConstructorFix()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Runtime.Serialization;
[Serializable]
public class CA2229NoConstructor : ISerializable
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}",
                GetCA2229DefaultCSharpResultAt(5, 14, "CA2229NoConstructor"),
@"
using System;
using System.Runtime.Serialization;
[Serializable]
public class CA2229NoConstructor : ISerializable
{
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }

    protected CA2229NoConstructor(SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
        throw new NotImplementedException();
    }
}");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Runtime.Serialization
<Serializable>
Public Class CA2229NoConstructor
    Implements ISerializable

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class",
                GetCA2229DefaultBasicResultAt(5, 14, "CA2229NoConstructor"),
@"
Imports System
Imports System.Runtime.Serialization
<Serializable>
Public Class CA2229NoConstructor
    Implements ISerializable

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub

    Protected Sub New(serializationInfo As SerializationInfo, streamingContext As StreamingContext)
        Throw New NotImplementedException()
    End Sub
End Class");
        }

        [Fact]
        public async Task CA2229HasConstructorWrongAccessibilityFix()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Runtime.Serialization;
[Serializable]
public class CA2229HasConstructorWrongAccessibility : ISerializable
{
    public CA2229HasConstructorWrongAccessibility(SerializationInfo info, StreamingContext context) { }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}",
                GetCA2229UnsealedCSharpResultAt(7, 12, "CA2229HasConstructorWrongAccessibility"),
@"
using System;
using System.Runtime.Serialization;
[Serializable]
public class CA2229HasConstructorWrongAccessibility : ISerializable
{
    protected CA2229HasConstructorWrongAccessibility(SerializationInfo info, StreamingContext context) { }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Runtime.Serialization
<Serializable>
Public Class CA2229HasConstructorWrongAccessibility
    Implements ISerializable

    Public Sub New(info As SerializationInfo, context As StreamingContext)
    End Sub

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class",
                GetCA2229UnsealedBasicResultAt(8, 16, "CA2229HasConstructorWrongAccessibility"),
@"
Imports System
Imports System.Runtime.Serialization
<Serializable>
Public Class CA2229HasConstructorWrongAccessibility
    Implements ISerializable

    Protected Sub New(info As SerializationInfo, context As StreamingContext)
    End Sub

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class");
        }

        [Fact]
        public async Task CA2229HasConstructorWrongAccessibility2Fix()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.Runtime.Serialization;
[Serializable]
public sealed class CA2229HasConstructorWrongAccessibility2 : ISerializable
{
    protected internal CA2229HasConstructorWrongAccessibility2(SerializationInfo info, StreamingContext context) { }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}",
                GetCA2229SealedCSharpResultAt(7, 24, "CA2229HasConstructorWrongAccessibility2"),
@"
using System;
using System.Runtime.Serialization;
[Serializable]
public sealed class CA2229HasConstructorWrongAccessibility2 : ISerializable
{
    private CA2229HasConstructorWrongAccessibility2(SerializationInfo info, StreamingContext context) { }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new NotImplementedException();
    }
}");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.Runtime.Serialization
<Serializable>
Public NotInheritable Class CA2229HasConstructorWrongAccessibility2
    Implements ISerializable

    Protected Friend Sub New(info As SerializationInfo, context As StreamingContext)
    End Sub

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class",
                GetCA2229SealedBasicResultAt(8, 26, "CA2229HasConstructorWrongAccessibility2"),
@"
Imports System
Imports System.Runtime.Serialization
<Serializable>
Public NotInheritable Class CA2229HasConstructorWrongAccessibility2
    Implements ISerializable

    Private Sub New(info As SerializationInfo, context As StreamingContext)
    End Sub

    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
        throw new NotImplementedException()
    End Sub
End Class");
        }

        private static DiagnosticResult GetCA2229DefaultCSharpResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2229Default)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA2229DefaultBasicResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2229Default)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA2229SealedCSharpResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2229Sealed)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA2229SealedBasicResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2229Sealed)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA2229UnsealedCSharpResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2229Unsealed)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA2229UnsealedBasicResultAt(int line, int column, string objectName) =>
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic(SerializationRulesDiagnosticAnalyzer.RuleCA2229Unsealed)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);
    }
}
