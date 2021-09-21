// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpUseNameofInPlaceOfStringAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.UseNameOfInPlaceOfStringFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicUseNameofInPlaceOfStringAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.UseNameOfInPlaceOfStringFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class UseNameOfInPlaceOfStringFixerTests
    {
        [Fact]
        public async Task Fixer_CSharp_ArgumentMatchesAParameterInScopeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException([|""x""|]);
    }
}",
@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(nameof(x));
    }
}");
        }

        [Fact]
        public async Task Fixer_CSharp_ArgumentWithCommentsAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(/*Leading*/[|""x""|]/*Trailing*/);
    }
}",
@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(/*Leading*/nameof(x)/*Trailing*/);
    }
}");
        }

        [Fact]
        public async Task Fixer_CSharp_ArgumentWithComments2Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentException(""Somemessage"", /*Leading*/[|""x""|]/*Trailing*/);
    }
}",
@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentException(""Somemessage"", /*Leading*/nameof(x)/*Trailing*/);
    }
}");
        }

        [Fact]
        public async Task Fixer_VB_ArgumentMatchesAParameterInScopeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Module Mod1
    Sub f(s As String)
        Throw New ArgumentNullException([|""s""|])
    End Sub
End Module",
@"
Imports System

Module Mod1
    Sub f(s As String)
        Throw New ArgumentNullException(NameOf(s))
    End Sub
End Module");
        }

        [Fact]
        public async Task Fixer_CSharp_ArgumentMatchesPropertyInScopeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.ComponentModel;

public class Person : INotifyPropertyChanged
{
    private string name;
    public event PropertyChangedEventHandler PropertyChanged;

    public string PersonName {
        get { return name; }
        set
        {
            name = value;
            OnPropertyChanged([|""PersonName""|]);
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null)
        {
            handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}", @"
using System.ComponentModel;

public class Person : INotifyPropertyChanged
{
    private string name;
    public event PropertyChangedEventHandler PropertyChanged;

    public string PersonName {
        get { return name; }
        set
        {
            name = value;
            OnPropertyChanged(nameof(PersonName));
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null)
        {
            handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}");
        }
    }
}