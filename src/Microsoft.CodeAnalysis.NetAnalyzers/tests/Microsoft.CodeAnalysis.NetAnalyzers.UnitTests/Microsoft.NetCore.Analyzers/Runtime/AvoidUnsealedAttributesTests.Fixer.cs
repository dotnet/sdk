// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.AvoidUnsealedAttributesFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    [TestClass]
    public class AvoidUnsealedAttributeFixerTests
    {
        #region CodeFix Tests

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public async Task CA1813CSharpCodeFixAllAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class [|FirstAttribute|] : Attribute
{
}

public class [|SecondAttribute|] : Attribute
{
}", @"
using System;

public sealed class FirstAttribute : Attribute
{
}

public sealed class SecondAttribute : Attribute
{
}");
        }

        [TestMethod]
        public async Task CA1813VisualBasicCodeFixAllAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Public Class [|FirstAttribute|]
    Inherits Attribute
End Class

Public Class [|SecondAttribute|]
    Inherits Attribute
End Class", @"
Imports System

Public NotInheritable Class FirstAttribute
    Inherits Attribute
End Class

Public NotInheritable Class SecondAttribute
    Inherits Attribute
End Class");
        }

        #endregion
    }
}
