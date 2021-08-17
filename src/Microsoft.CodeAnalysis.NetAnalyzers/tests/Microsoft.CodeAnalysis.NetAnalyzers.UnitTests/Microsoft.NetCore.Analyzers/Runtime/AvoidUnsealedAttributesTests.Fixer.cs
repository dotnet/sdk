// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class AvoidUnsealedAttributeFixerTests
    {
        #region CodeFix Tests

        [Fact]
        public async Task CA1813CSharpCodeFixProviderTestFiredAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class [|AttributeClass|] : Attribute
{
}", @"
using System;

public sealed class AttributeClass : Attribute
{
}");
        }

        [Fact]
        public async Task CA1813VisualBasicCodeFixProviderTestFiredAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Class [|AttributeClass|]
    Inherits Attribute
End Class", @"
Imports System

Public NotInheritable Class AttributeClass
    Inherits Attribute
End Class");
        }

        #endregion
    }
}
