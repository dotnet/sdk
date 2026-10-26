// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
        public async Task NoDiagnostic_ArgListAsync()
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
        public async Task NoDiagnostic_NoArgumentsAsync()
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
        public async Task NoDiagnostic_NullLiteralAsync()
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
        public async Task NoDiagnostic_StringIsAReservedWordAsync()
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
        public async Task NoDiagnostic_NoMatchingParametersInScopeAsync()
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
        public async Task NoDiagnostic_NameColonOtherParameterNameAsync()
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
        public async Task NoDiagnostic_NotStringLiteralAsync()
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
        public async Task NoDiagnostic_NotValidIdentifierAsync()
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
        public async Task NoDiagnostic_NoArgumentListAsync()
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
        public async Task NoDiagnostic_NoMatchingParameterAsync()
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
        public async Task NoDiagnostic_MatchesParameterButNotCalledParamNameAsync()
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
        public async Task NoDiagnostic_MatchesPropertyButNotCalledPropertyNameAsync()
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
        public async Task NoDiagnostic_PositionalArgumentOtherParameterNameAsync()
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
        public async Task NoDiagnostic_1426Async()
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
        public async Task NoDiagnostic_CSharp5Async()
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
        public async Task NoDiagnostic_VB12Async()
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

        #endregion

        #region Unit tests for analyzer diagnostic(s)
        [WorkItem(1524, "https://github.com/dotnet/roslyn-analyzers/issues/1524")]
        [Fact]
        public async Task Diagnostic_CSharp6Async()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException([|""x""|]);
    }
}",
                FixedCode = @"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(nameof(x));
    }
}",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp6,
            }.RunAsync();
        }

        [WorkItem(1524, "https://github.com/dotnet/roslyn-analyzers/issues/1524")]
        [Fact]
        public async Task Diagnostic_VB14Async()
        {
            await new VerifyVB.Test
            {
                TestCode = @"
Imports System

Module Mod1
    Sub f(s As String)
        Throw New ArgumentNullException([|""s""|])
    End Sub
End Module",
                FixedCode = @"
Imports System

Module Mod1
    Sub f(s As String)
        Throw New ArgumentNullException(NameOf(s))
    End Sub
End Module",
                LanguageVersion = CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic14,
            }.RunAsync();
        }

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
        public async Task Fixer_CSharp_ArgumentMatchesAPropertyInScopeAsync()
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

        [Fact]
        public async Task Diagnostic_ArgumentMatchesAPropertyInScope2Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
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
            OnPropertyChanged([|""PersonName""|]);
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
}", @"
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
            OnPropertyChanged(nameof(PersonName));
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
}");
        }

        [Fact]
        public async Task Diagnostic_ArgumentNameColonParamNameAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(paramName:[|""x""|]);
    }
}", @"
using System;
class C
{
    void M(int x)
    {
        throw new ArgumentNullException(paramName:nameof(x));
    }
}");
        }

        [Fact]
        public async Task Diagnostic_ArgumentNameColonPropertyNameAsync()
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
            OnPropertyChanged(propertyName:[|""PersonName""|]);
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
            OnPropertyChanged(propertyName:nameof(PersonName));
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
        public async Task Diagnostic_AnonymousFunctionMultiline1Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) =>
        {
            throw new ArgumentException(""somemessage"", [|""x""|]);
        };
    }
}", @"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) =>
        {
            throw new ArgumentException(""somemessage"", nameof(x));
        };
    }
}");
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionMultiLine2Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) =>
        {
            throw new ArgumentException(""somemessage"", [|""y""|]);
        };
    }
}", @"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) =>
        {
            throw new ArgumentException(""somemessage"", nameof(y));
        };
    }
}");
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionSingleLine1Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) => throw new ArgumentException(""somemessage"", [|""y""|]);
    }
}", @"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) => throw new ArgumentException(""somemessage"", nameof(y));
    }
}");
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionSingleLine2Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) => throw new ArgumentException(""somemessage"", [|""x""|]);
    }
}", @"
using System;

class Test
{
    void Method(int x)
    {
        Action<int> a = (int y) => throw new ArgumentException(""somemessage"", nameof(x));
    }
}");
        }

        [Fact]
        public async Task Diagnostic_AnonymousFunctionMultipleParametersAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        Action<int, int> a = (j, k) => throw new ArgumentException(""somemessage"", [|""x""|]);
    }
}", @"
using System;

class Test
{
    void Method(int x)
    {
        Action<int, int> a = (j, k) => throw new ArgumentException(""somemessage"", nameof(x));
    }
}");
        }

        [Fact]
        public async Task Diagnostic_LocalFunction1Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        void AnotherMethod(int y, int z)
            {
                throw new ArgumentException(""somemessage"", [|""x""|]);
            }
    }
}", @"
using System;

class Test
{
    void Method(int x)
    {
        void AnotherMethod(int y, int z)
            {
                throw new ArgumentException(""somemessage"", nameof(x));
            }
    }
}");
        }

        [Fact]
        public async Task Diagnostic_LocalFunction2Async()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

class Test
{
    void Method(int x)
    {
        void AnotherMethod(int y, int z)
            {
                throw new ArgumentException(""somemessage"", [|""y""|]);
            }
    }
}", @"
using System;

class Test
{
    void Method(int x)
    {
        void AnotherMethod(int y, int z)
            {
                throw new ArgumentException(""somemessage"", nameof(y));
            }
    }
}");
        }

        [Fact]
        public async Task Diagnostic_DelegateAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace ConsoleApp14
{
    class Program
    {
         class test
        {
            Action<int> x2 = delegate (int xyz)
            {
                throw new ArgumentNullException([|""xyz""|]);
            };
        }
    }
}", @"
using System;

namespace ConsoleApp14
{
    class Program
    {
         class test
        {
            Action<int> x2 = delegate (int xyz)
            {
                throw new ArgumentNullException(nameof(xyz));
            };
        }
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

        #endregion
    }
}
