// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.NetFramework.CSharp.Analyzers;
using Microsoft.NetFramework.VisualBasic.Analyzers;
using Microsoft.CodeAnalysis.Diagnostics;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetFramework.CSharp.Analyzers.CSharpCallBaseClassMethodsOnISerializableTypesAnalyzer,
    Microsoft.NetFramework.CSharp.Analyzers.CSharpCallBaseClassMethodsOnISerializableTypesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetFramework.VisualBasic.Analyzers.BasicCallBaseClassMethodsOnISerializableTypesAnalyzer,
    Microsoft.NetFramework.VisualBasic.Analyzers.BasicCallBaseClassMethodsOnISerializableTypesFixer>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public class CallBaseClassMethodsOnISerializableTypesTests
    {
    }
}