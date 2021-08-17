// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EventsShouldNotHaveBeforeOrAfterPrefix,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.EventsShouldNotHaveBeforeOrAfterPrefix,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class EventsShouldNotHaveBeforeOrAfterPrefixTests
    {
        [Fact]
        public async Task CA1713_EventNameStartsWithAfter_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class Class1
{
    public event EventHandler [|AfterClose|];
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class Class1
    Public Event [|AfterClose|] As EventHandler
End Class
");
        }

        [Fact]
        public async Task CA1713_EventNameStartsWithBefore_Diagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class Class1
{
    public event EventHandler [|BeforeClose|];
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class Class1
    Public Event [|BeforeClose|] As EventHandler
End Class
");
        }

        [Fact]
        public async Task CA1713_ValidEventName_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class Class1
{
    public event EventHandler Closing;
    public event EventHandler Closed;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class Class1
    Public Event Closing As EventHandler
    Public Event Closed As EventHandler
End Class
");
        }

        [Fact]
        public async Task CA1713_EventNameIsExactlyThePrefix_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class Class1
{
    public event EventHandler After;
    public event EventHandler Before;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class Class1
    Public Event After As EventHandler
    Public Event Before As EventHandler
End Class
");
        }

        [Fact]
        public async Task CA1713_CharAfterPrefixIsNotUpperCase_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class Class1
{
    public event EventHandler Aftermatch;
    public event EventHandler Beforehand;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class Class1
    Public Event Aftermatch As EventHandler
    Public Event Beforehand As EventHandler
End Class
");
        }

        [Fact]
        public async Task CA1713_PrefixIsNotFollowingRightCase_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
public class Class1
{
    public event EventHandler AFTERClose;
    public event EventHandler afterClose;
    public event EventHandler AfTeRClose;
    public event EventHandler BEFOREClose;
    public event EventHandler beforeClose;
    public event EventHandler BeFoReClose;
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Public Class Class1
    Public Event AFTERClose As EventHandler
    Public Event BEFOREClose As EventHandler
End Class

Public Class Class2
    Public Event afterClose As EventHandler
    Public Event beforeClose As EventHandler
End Class

Public Class Class3
    Public Event AfTeRClose As EventHandler
    Public Event BeFoReClose As EventHandler
End Class
");
        }
    }
}
