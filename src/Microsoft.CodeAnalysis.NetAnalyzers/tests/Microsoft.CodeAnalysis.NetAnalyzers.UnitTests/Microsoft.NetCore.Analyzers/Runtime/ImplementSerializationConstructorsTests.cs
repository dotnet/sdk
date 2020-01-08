// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.ImplementSerializationConstructorsFixer>;
using VerifyVB = Microsoft.CodeAnalysis.VisualBasic.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.ImplementSerializationConstructorsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class ImplementSerializationConstructorsTests
    {
        [Fact]
        public async Task CA2229NoConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                GetCA2229CSharpResultAt(5, 30, "CA2229NoConstructor", CA2229Message));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Public Class CA2229NoConstructor
                    Implements ISerializable
                
                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class",
                GetCA2229BasicResultAt(5, 30, "CA2229NoConstructor", CA2229Message));
        }

        [Fact]
        public async Task CA2229NoConstructorInternal()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                [Serializable]
                internal class CA2229NoConstructorInternal : ISerializable
                {
                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Friend Class CA2229NoConstructorInternal
                    Implements ISerializable
                
                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class");
        }

        [Fact]
        public async Task CA2229HasConstructor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                [Serializable]
                public class CA2229HasConstructor : ISerializable
                {
                    protected CA2229HasConstructor(SerializationInfo info, StreamingContext context) { }

                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Public Class CA2229HasConstructor
                    Implements ISerializable
                
                    Protected Sub New(info As SerializationInfo, context As StreamingContext)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class");
        }

        [Fact]
        public async Task CA2229HasConstructor1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                [Serializable]
                public sealed class CA2229HasConstructor1 : ISerializable
                {
                    private CA2229HasConstructor1(SerializationInfo info, StreamingContext context) { }

                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Public NotInheritable Class CA2229HasConstructor1
                    Implements ISerializable
                
                    Private Sub New(info As SerializationInfo, context As StreamingContext)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class");
        }

        [Fact]
        public async Task CA2229HasConstructorWrongAccessibility()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                GetCA2229CSharpResultAt(7, 28, "CA2229HasConstructorWrongAccessibility", CA2229MessageUnsealed));

            await VerifyVB.VerifyAnalyzerAsync(@"
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
                GetCA2229BasicResultAt(8, 32, "CA2229HasConstructorWrongAccessibility", CA2229MessageUnsealed));
        }

        [Fact]
        public async Task CA2229HasConstructorWrongAccessibilityWithScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;

                [Serializable]
                public sealed class CA2229HasConstructor1 : ISerializable
                {
                    private CA2229HasConstructor1(SerializationInfo info, StreamingContext context) { }

                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }

                [Serializable]
                public class CA2229HasConstructorWrongAccessibility : ISerializable
                {
                    public {|CA2229:CA2229HasConstructorWrongAccessibility|}(SerializationInfo info, StreamingContext context) { }

                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization

                <Serializable>
                Public NotInheritable Class CA2229HasConstructor1
                    Implements ISerializable
                
                    Private Sub New(info As SerializationInfo, context As StreamingContext)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class

                <Serializable>
                Public Class CA2229HasConstructorWrongAccessibility
                    Implements ISerializable
                
                    Public Sub {|CA2229:New|}(info As SerializationInfo, context As StreamingContext)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class");
        }

        [Fact]
        public async Task CA2229HasConstructorWrongAccessibility1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                [Serializable]
                public class CA2229HasConstructorWrongAccessibility1 : ISerializable
                {
                    internal CA2229HasConstructorWrongAccessibility1(SerializationInfo info, StreamingContext context) { }

                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }",
                GetCA2229CSharpResultAt(7, 30, "CA2229HasConstructorWrongAccessibility1", CA2229MessageUnsealed));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Public Class CA2229HasConstructorWrongAccessibility1
                    Implements ISerializable
                
                    Friend Sub New(info As SerializationInfo, context As StreamingContext)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class",
                GetCA2229BasicResultAt(8, 32, "CA2229HasConstructorWrongAccessibility1", CA2229MessageUnsealed));
        }

        [Fact]
        public async Task CA2229HasConstructorWrongAccessibility2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                GetCA2229CSharpResultAt(7, 40, "CA2229HasConstructorWrongAccessibility2", CA2229MessageSealed));

            await VerifyVB.VerifyAnalyzerAsync(@"
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
                GetCA2229BasicResultAt(8, 42, "CA2229HasConstructorWrongAccessibility2", CA2229MessageSealed));
        }

        [Fact]
        public async Task CA2229HasConstructorWrongAccessibility3()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                [Serializable]
                public class CA2229HasConstructorWrongAccessibility3 : ISerializable
                {
                    protected internal CA2229HasConstructorWrongAccessibility3(SerializationInfo info, StreamingContext context) { }

                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }",
                GetCA2229CSharpResultAt(7, 40, "CA2229HasConstructorWrongAccessibility3", CA2229MessageUnsealed));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Public Class CA2229HasConstructorWrongAccessibility3
                    Implements ISerializable
                
                    Protected Friend Sub New(info As SerializationInfo, context As StreamingContext)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class",
                GetCA2229BasicResultAt(8, 42, "CA2229HasConstructorWrongAccessibility3", CA2229MessageUnsealed));
        }

        [Fact]
        public async Task CA2229HasConstructorWrongOrder()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                [Serializable]
                public class CA2229HasConstructorWrongOrder : ISerializable
                {
                    protected CA2229HasConstructorWrongOrder(StreamingContext context, SerializationInfo info) { }

                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }",
                GetCA2229CSharpResultAt(5, 30, "CA2229HasConstructorWrongOrder", CA2229Message));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Public Class CA2229HasConstructorWrongOrder
                    Implements ISerializable
                
                    Protected Sub New(context As StreamingContext, info As SerializationInfo)
                    End Sub

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class",
                GetCA2229BasicResultAt(5, 30, "CA2229HasConstructorWrongOrder", CA2229Message));
        }

        [Fact]
        public async Task CA2229SerializableProper()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;
                using System.Runtime.Serialization;
                [Serializable]
                public class CA2229SerializableProper : ISerializable
                {
                    public void GetObjectData(SerializationInfo info, StreamingContext context)
                    {
                        throw new NotImplementedException();
                    }
                }",
                GetCA2229CSharpResultAt(5, 30, "CA2229SerializableProper", CA2229Message));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Imports System
                Imports System.Runtime.Serialization
                <Serializable>
                Public Class CA2229SerializableProper 
                    Implements ISerializable

                    Public Sub GetObjectData(info as SerializationInfo, context as StreamingContext) Implements ISerializable.GetObjectData
                        throw new NotImplementedException()
                    End Sub
                End Class",
                GetCA2229BasicResultAt(5, 30, "CA2229SerializableProper", CA2229Message));
        }

        internal static readonly string CA2229Message = MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsMessageCreateMagicConstructor;
        internal static readonly string CA2229MessageSealed = MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsMessageMakeSealedMagicConstructorPrivate;
        internal static readonly string CA2229MessageUnsealed = MicrosoftNetCoreAnalyzersResources.ImplementSerializationConstructorsMessageMakeUnsealedMagicConstructorFamily;

        private static DiagnosticResult GetCA2229CSharpResultAt(int line, int column, string objectName, string message)
        {
            return new DiagnosticResult(SerializationRulesDiagnosticAnalyzer.RuleCA2229).WithLocation(line, column).WithArguments(string.Format(message, objectName));
        }

        private static DiagnosticResult GetCA2229BasicResultAt(int line, int column, string objectName, string message)
        {
            return new DiagnosticResult(SerializationRulesDiagnosticAnalyzer.RuleCA2229).WithLocation(line, column).WithArguments(string.Format(message, objectName));
        }
    }
}
