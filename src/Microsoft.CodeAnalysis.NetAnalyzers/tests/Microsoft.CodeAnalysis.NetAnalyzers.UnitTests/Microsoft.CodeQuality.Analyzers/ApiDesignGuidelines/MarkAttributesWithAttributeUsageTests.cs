// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.MarkAttributesWithAttributeUsageFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public partial class MarkAttributesWithAttributeUsageTests
    {
        [Theory]
        [InlineData(AttributeTargets.All, 0)]
        [InlineData(AttributeTargets.Assembly, 1)]
        [InlineData(AttributeTargets.Class, 2)]
        [InlineData(AttributeTargets.Constructor, 3)]
        [InlineData(AttributeTargets.Delegate, 4)]
        [InlineData(AttributeTargets.Enum, 5)]
        [InlineData(AttributeTargets.Event, 6)]
        [InlineData(AttributeTargets.Field, 7)]
        [InlineData(AttributeTargets.GenericParameter, 8)]
        [InlineData(AttributeTargets.Interface, 9)]
        [InlineData(AttributeTargets.Method, 10)]
        [InlineData(AttributeTargets.Module, 11)]
        [InlineData(AttributeTargets.Parameter, 12)]
        [InlineData(AttributeTargets.Property, 13)]
        [InlineData(AttributeTargets.ReturnValue, 14)]
        [InlineData(AttributeTargets.Struct, 15)]
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
            }.RunAsync();

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
            }.RunAsync();
        }

        [Fact, WorkItem(1732, "https://github.com/dotnet/roslyn-analyzers/issues/1732")]
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

        [Fact]
        public async Task TestCSAbstractAttributeClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

abstract class C : Attribute
{
}
");
        }

        [Fact, WorkItem(1732, "https://github.com/dotnet/roslyn-analyzers/issues/1732")]
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

        [Fact]
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
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);

        private static DiagnosticResult GetCA1018BasicResultAt(int line, int column, string objectName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName);
    }
}
