// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.VisualBasic.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Microsoft.NetCore.VisualBasic.Analyzers.Runtime;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpMarkAllNonSerializableFieldsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SerializationRulesDiagnosticAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicMarkAllNonSerializableFieldsFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class MarkAllNonSerializableFieldsFixerTests
    {
        [Fact]
        public async Task CA2235WithNonSerializableFieldsWithFix()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
public class NonSerializableType { }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    internal NonSerializableType {|CA2235:s1|};
}",
@"
using System;
public class NonSerializableType { }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    [NonSerialized]
    internal NonSerializableType s1;
}");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Public Class NonSerializableType
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields 
    Friend {|CA2235:s1|} As NonSerializableType
End Class",
@"
Imports System
Public Class NonSerializableType
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields
    <NonSerialized>
    Friend s1 As NonSerializableType
End Class");
        }

        [Fact]
        public async Task CA2235WithNonSerializableFieldsWithFix1()
        {
            await new CSharpCodeFixTest<SerializationRulesDiagnosticAnalyzer, CSharpMarkAllNonSerializableFieldsFixer, XUnitVerifier>
            {
                TestCode = @"
using System;
public class NonSerializableType { }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    internal NonSerializableType {|CA2235:s1|};
}",
                FixedCode = @"
using System;

[Serializable]
public class NonSerializableType { }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    internal NonSerializableType s1;
}",
                CodeActionIndex = 1,
            }.RunAsync();

            await new VisualBasicCodeFixTest<SerializationRulesDiagnosticAnalyzer, BasicMarkAllNonSerializableFieldsFixer, XUnitVerifier>
            {
                TestCode = @"
Imports System
Public Class NonSerializableType
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields
    Friend {|CA2235:s1|} As NonSerializableType
End Class",
                FixedCode = @"
Imports System

<Serializable>
Public Class NonSerializableType
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields
    Friend s1 As NonSerializableType
End Class",
                CodeActionIndex = 1,
            }.RunAsync();
        }

        [Fact]
        public async Task CA2235WithNonSerializableFieldsWithFix2()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
public class NonSerializableType { }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    internal NonSerializableType {|CA2235:s1|}, {|CA2235:s2|} = new NonSerializableType(), {|CA2235:s3|};
}",
@"
using System;
public class NonSerializableType { }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    [NonSerialized]
    internal NonSerializableType s1, s2 = new NonSerializableType(), s3;
}");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Public Class NonSerializableType
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields 
    Friend {|CA2235:s1|}, {|CA2235:s2|}, {|CA2235:s3|} As NonSerializableType
End Class",
@"
Imports System
Public Class NonSerializableType
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields
    <NonSerialized>
    Friend s1, s2, s3 As NonSerializableType
End Class");
        }

        [Fact]
        public async Task CA2235WithNonSerializableFieldsWithFix3()
        {
            await new CSharpCodeFixTest<SerializationRulesDiagnosticAnalyzer, CSharpMarkAllNonSerializableFieldsFixer, XUnitVerifier>
            {
                TestCode = @"
using System;
public partial class NonSerializableType { }

public partial class NonSerializableType { public void method() { } }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    internal NonSerializableType {|CA2235:s1|};
}",
                FixedCode = @"
using System;

[Serializable]
public partial class NonSerializableType { }

public partial class NonSerializableType { public void method() { } }

[Serializable]
public class CA2235WithNonPublicNonSerializableFields
{
    internal NonSerializableType s1;
}",
                CodeActionIndex = 1,
            }.RunAsync();

            await new VisualBasicCodeFixTest<SerializationRulesDiagnosticAnalyzer, BasicMarkAllNonSerializableFieldsFixer, XUnitVerifier>
            {
                TestCode = @"
Imports System
Public Partial Class NonSerializableType
End Class

Public Class NonSerializableType
    Sub method()
    End Sub
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields
    Friend {|CA2235:s1|} As NonSerializableType
End Class",
                FixedCode = @"
Imports System

<Serializable>
Public Partial Class NonSerializableType
End Class

Public Class NonSerializableType
    Sub method()
    End Sub
End Class

<Serializable>
Public Class CA2235WithNonPublicNonSerializableFields
    Friend s1 As NonSerializableType
End Class",
                CodeActionIndex = 1,
            }.RunAsync();
        }
    }
}
