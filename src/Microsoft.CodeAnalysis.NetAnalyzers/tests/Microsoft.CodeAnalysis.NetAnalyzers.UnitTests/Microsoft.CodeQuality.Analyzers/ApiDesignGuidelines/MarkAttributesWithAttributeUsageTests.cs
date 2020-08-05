// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        [InlineData(AttributeTargets.All)]
        [InlineData(AttributeTargets.Assembly)]
        [InlineData(AttributeTargets.Class)]
        [InlineData(AttributeTargets.Constructor)]
        [InlineData(AttributeTargets.Delegate)]
        [InlineData(AttributeTargets.Enum)]
        [InlineData(AttributeTargets.Event)]
        [InlineData(AttributeTargets.Field)]
        [InlineData(AttributeTargets.GenericParameter)]
        [InlineData(AttributeTargets.Interface)]
        [InlineData(AttributeTargets.Method)]
        [InlineData(AttributeTargets.Module)]
        [InlineData(AttributeTargets.Parameter)]
        [InlineData(AttributeTargets.Property)]
        [InlineData(AttributeTargets.ReturnValue)]
        [InlineData(AttributeTargets.Struct)]
        public async Task TestSimpleAttributeClass(AttributeTargets attributeTarget)
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
        public async Task TestCSInheritedAttributeClass()
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
        public async Task TestCSAbstractAttributeClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

abstract class C : Attribute
{
}
");
        }

        [Fact, WorkItem(1732, "https://github.com/dotnet/roslyn-analyzers/issues/1732")]
        public async Task TestVBInheritedAttributeClass()
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
        public async Task TestVBAbstractAttributeClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

MustInherit Class C
    Inherits Attribute
End Class
");
        }

        private static DiagnosticResult GetCA1018CSharpResultAt(int line, int column, string objectName)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(objectName);

        private static DiagnosticResult GetCA1018BasicResultAt(int line, int column, string objectName)
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(objectName);
    }
}
