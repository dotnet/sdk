// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseSpanBasedStringConcat,
    Microsoft.NetCore.Analyzers.Runtime.UseSpanBasedStringConcatFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseSpanBasedStringConcat,
    Microsoft.NetCore.Analyzers.Runtime.UseSpanBasedStringConcatFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseSpanBasedStringConcatTests
    {
    }
}
