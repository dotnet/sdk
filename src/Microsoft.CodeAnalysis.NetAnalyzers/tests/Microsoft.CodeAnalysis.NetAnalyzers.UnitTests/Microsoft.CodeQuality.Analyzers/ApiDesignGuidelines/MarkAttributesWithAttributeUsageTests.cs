// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public partial class MarkAttributesWithAttributeUsageTests
    {
        [TestMethod]
        [DataRow(AttributeTargets.All, 0)]
        [DataRow(AttributeTargets.Assembly, 1)]
        [DataRow(AttributeTargets.Class, 2)]
        [DataRow(AttributeTargets.Constructor, 3)]
        [DataRow(AttributeTargets.Delegate, 4)]
        [DataRow(AttributeTargets.Enum, 5)]
        [DataRow(AttributeTargets.Event, 6)]
        [DataRow(AttributeTargets.Field, 7)]
        [DataRow(AttributeTargets.GenericParameter, 8)]
        [DataRow(AttributeTargets.Interface, 9)]
        [DataRow(AttributeTargets.Method, 10)]
        [DataRow(AttributeTargets.Module, 11)]
        [DataRow(AttributeTargets.Parameter, 12)]
        [DataRow(AttributeTargets.Property, 13)]
        [DataRow(AttributeTargets.ReturnValue, 14)]
        [DataRow(AttributeTargets.Struct, 15)]
        public async Task TestSimpleAttributeClassAsync(AttributeTargets attributeTarget, int codeActionIndex)
        {
            var attributeTargetsValue = "AttributeTargets." + attributeTarget.ToString();

            await new VerifyCS.Test
            {
                TestCode = @"
using System;

class C : Attribute
{
}
",
                ExpectedDiagnostics = { GetCA1018CSharpResultAt(4, 7, "C"), },
                CodeActionIndex = codeActionIndex,
                CodeActionEquivalenceKey = attributeTargetsValue,
                FixedCode = @"
using System;

[AttributeUsage(" + attributeTargetsValue + @")]
class C : Attribute
{
}
",
            }.RunAsync(CancellationToken.None);

            await new VerifyVB.Test
            {
                TestCode = @"
Imports System

Class C
    Inherits Attribute
End Class
",
                ExpectedDiagnostics = { GetCA1018BasicResultAt(4, 7, "C"), },
                FixedCode = @"
Imports System

<AttributeUsage(AttributeTargets.All)>
Class C
    Inherits Attribute
End Class
",
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(1732, "https://github.com/dotnet/roslyn-analyzers/issues/1732")]
        public async Task TestCSInheritedAttributeClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

[AttributeUsage(AttributeTargets.Method)]
class C : Attribute
{
}
class D : C
{
}
");
        }

        [TestMethod]
        public async Task TestCSAbstractAttributeClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

abstract class C : Attribute
{
}
");
        }

        [TestMethod, WorkItem(1732, "https://github.com/dotnet/roslyn-analyzers/issues/1732")]
        public async Task TestVBInheritedAttributeClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

<AttributeUsage(AttributeTargets.Method)>
Class C
    Inherits Attribute
End Class
Class D
    Inherits C
End Class
");
        }

        [TestMethod]
        public async Task TestVBAbstractAttributeClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

MustInherit Class C
    Inherits Attribute
End Class
");
        }

        private static DiagnosticResult GetCA1018CSharpResultAt(int line, int column, string objectName)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA1018BasicResultAt(int line, int column, string objectName)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(objectName);
    }
}
