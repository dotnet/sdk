// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpUseNameofInPlaceOfStringAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.UseNameOfInPlaceOfStringFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicUseNameofInPlaceOfStringAnalyzer,
    Microsoft.CodeQuality.Analyzers.Maintainability.UseNameOfInPlaceOfStringFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class UseNameofInPlaceOfStringTests
    {
        #region Unit tests for no analyzer diagnostic

        [Fact]
        [WorkItem(3023, "https://github.com/dotnet/roslyn-analyzers/issues/3023")]
        public async Task NoDiagnostic_ArgList()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(__arglist)
    {
        M(__arglist());
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NoArguments()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException();
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NullLiteral()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(null);
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_StringIsAReservedWord()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(""static"");
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NoMatchingParametersInScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int y)
    {
        throw new ArgumentNullException(""x"");
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NameColonOtherParameterName()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int y)
    {
        Console.WriteLine(format:""x"");
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NotStringLiteral()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        string param = ""x"";
        throw new ArgumentNullException(param);
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NotValidIdentifier()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(""9x"");
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NoArgumentList()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException({|CS1002:|}{|CS1026:|}
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_NoMatchingParameter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new {|CS1729:ArgumentNullException|}(""test"", ""test2"", ""test3"");
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_MatchesParameterButNotCalledParamName()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        Console.WriteLine(""x"");
    }
}");
        }

        [Fact]
        public async Task NoDiagnostic_MatchesPropertyButNotCalledPropertyName()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
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
            Console.WriteLine(""PersonName"");
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

        [Fact]
        public async Task NoDiagnostic_PositionalArgumentOtherParameterName()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        Console.WriteLine(""x"");
    }
}");
        }

        [WorkItem(1426, "https://github.com/dotnet/roslyn-analyzers/issues/1426")]
        [Fact]
        public async Task NoDiagnostic_1426()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.CompilerServices;

public class C
{
    int M([CallerMemberName] string propertyName = """")
    {
        return 0;
    }

    public bool Property
    {
        set
        {
            M();
        }
    }
}");
        }

        [WorkItem(1524, "https://github.com/dotnet/roslyn-analyzers/issues/1524")]
        [Fact]
        public async Task NoDiagnostic_CSharp5()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(""x"");
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp5
            }.RunAsync();
        }

        [WorkItem(1524, "https://github.com/dotnet/roslyn-analyzers/issues/1524")]
        [Fact]
        public async Task Diagnostic_CSharp6()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(""x"");
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp6,
                ExpectedDiagnostics =
                {
                    GetCSharpNameofResultAt(7, 41, "x"),
                }
            }.RunAsync();
        }

        [WorkItem(1524, "https://github.com/dotnet/roslyn-analyzers/issues/1524")]
        [Fact]
        public async Task NoDiagnostic_VB12()
        {
            await new VerifyVB.Test
            {
                TestCode = @"
Imports System

Module Mod1
    Sub f(s As String)
        Throw New ArgumentNullException(""s"")
    End Sub
End Module",
                LanguageVersion = CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic12
            }.RunAsync();
        }

        [WorkItem(1524, "https://github.com/dotnet/roslyn-analyzers/issues/1524")]
        [Fact]
        public async Task Diagnostic_VB14()
        {
            await new VerifyVB.Test
            {
                TestCode = @"
Imports System

Module Mod1
    Sub f(s As String)
        Throw New ArgumentNullException(""s"")
    End Sub
End Module",
                LanguageVersion = CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic14,
                ExpectedDiagnostics =
                {
                    GetBasicNameofResultAt(6, 41, "s"),
                }
            }.RunAsync();
        }

        #endregion

        #region Unit tests for analyzer diagnostic(s)

        [Fact]
        public async Task Diagnostic_ArgumentMatchesAParameterInScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(""x"");
    }
}",
    GetCSharpNameofResultAt(7, 41, "x"));
        }

        [Fact]
        public async Task Diagnostic_VB_ArgumentMatchesAParameterInScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Module Mod1
    Sub f(s As String)
        Throw New ArgumentNullException(""s"")
    End Sub
End Module",
    GetBasicNameofResultAt(6, 41, "s"));
        }

        [Fact]
        public async Task Diagnostic_ArgumentMatchesAPropertyInScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            OnPropertyChanged(""PersonName"");
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
}",
    GetCSharpNameofResultAt(14, 31, "PersonName"));
        }

        [Fact]
        public async Task Diagnostic_ArgumentMatchesAPropertyInScope2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.ComponentModel;

public class Person : INotifyPropertyChanged
{
    private string name;
    public event PropertyChangedEventHandler PropertyChanged;

    public string PersonName 
    {
        get { return name; }
        set
        {
            name = value;
            OnPropertyChanged(""PersonName"");
        }
    }

    public string PersonName2
    {
        get { return name; }
        set
        {
            name = value;
            OnPropertyChanged(nameof(PersonName2));
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
}",
    GetCSharpNameofResultAt(15, 31, "PersonName"));
        }

        [Fact]
        public async Task Diagnostic_ArgumentNameColonParamName()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(paramName:""x"");
    }
}",
    GetCSharpNameofResultAt(7, 51, "x"));
        }

        [Fact]
        public async Task Diagnostic_ArgumentNameColonPropertyName()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
            OnPropertyChanged(propertyName:""PersonName"");
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
}",
    GetCSharpNameofResultAt(14, 44, "PersonName"));
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionMultiline1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) =>
        {
            throw new ArgumentException(""somemessage"", ""x"");
        };
    }
}",
    GetCSharpNameofResultAt(10, 56, "x"));
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionMultiLine2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) =>
        {
            throw new ArgumentException(""somemessage"", ""y"");
        };
    }
}",
    GetCSharpNameofResultAt(10, 56, "y"));
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionSingleLine1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) => throw new ArgumentException(""somemessage"", ""y"");
    }
}",
    GetCSharpNameofResultAt(8, 79, "y"));
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionSingleLine2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) => throw new ArgumentException(""somemessage"", ""x"");
    }
}",
    GetCSharpNameofResultAt(8, 79, "x"));
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionMultipleParameters()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int, int> a = (j, k) => throw new ArgumentException(""somemessage"", ""x"");
    }
}",
    GetCSharpNameofResultAt(8, 83, "x"));
        }

        [Fact]
        public async Task Diagnostic_LocalFunction1()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        void AnotherMethod(int y, int z)
            {
                throw new ArgumentException(""somemessage"", ""x"");
            }
    }
}",
    GetCSharpNameofResultAt(10, 60, "x"));
        }

        [Fact]
        public async Task Diagnostic_LocalFunction2()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        void AnotherMethod(int y, int z)
            {
                throw new ArgumentException(""somemessage"", ""y"");
            }
    }
}",
    GetCSharpNameofResultAt(10, 60, "y"));
        }

        [Fact]
        public async Task Diagnostic_Delegate()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace ConsoleApp14
{
    class Program
    {
         class test
        {
            Action<int> x2 = delegate (int xyz)
            {
                throw new ArgumentNullException(""xyz"");
            };
        }
    }
}",
    GetCSharpNameofResultAt(12, 49, "xyz"));
        }

        #endregion

        private static DiagnosticResult GetBasicNameofResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(name);

        private static DiagnosticResult GetCSharpNameofResultAt(int line, int column, string name)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(name);
    }
}
