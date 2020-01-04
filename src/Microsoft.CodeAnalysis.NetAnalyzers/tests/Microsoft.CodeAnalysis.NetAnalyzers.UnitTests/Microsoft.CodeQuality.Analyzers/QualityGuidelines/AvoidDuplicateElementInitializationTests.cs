// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public partial class AvoidDuplicateElementInitializationTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return null;
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AvoidDuplicateElementInitialization();
        }

        [Fact]
        public void NoInitializer()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>();
    }
}
");
        }

        [Fact]
        public void LiteralIntIndex()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>
        {
            [1] = ""a"",
            [2] = ""b"",
            [1] = ""c""
        };
    }
}
",
                GetCSharpResultAt(8, 13, "1"));
        }

        [Fact]
        public void CalculatedIntIndex()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>
        {
            [1] = ""a"",
            [2] = ""b"",
            [(10 + 3) * 2 - 25] = ""c""
        };
    }
}
",
                GetCSharpResultAt(8, 13, "1"));
        }

        [Fact]
        public void LiteralStringIndex()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [""a""] = 1,
            [""b""] = 2,
            [""a""] = 3
        };
    }
}
",
                GetCSharpResultAt(8, 13, "a"));
        }

        [Fact]
        public void ConcatenatedStringIndex()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new System.Collections.Generic.Dictionary<string, int>
        {
            [""ab""] = 1,
            [""bc""] = 2,
            [""a"" + ""b""] = 3
        };
    }
}
",
                GetCSharpResultAt(8, 13, "ab"));
        }

        [Fact]
        public void EnumIndex()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var dictionary = new System.Collections.Generic.Dictionary<System.DateTimeKind, int>
        {
            [System.DateTimeKind.Local] = 1,
            [System.DateTimeKind.Utc] = 2,
            [System.DateTimeKind.Local] = 3
        };
    }
}
",
                // TODO: See if there's a way to use 'DateTimeKind.Local' here.
                GetCSharpResultAt(8, 13, "2"));
        }

        [Fact]
        public void MultipleIndexerArguments()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new D
        {
            [1, ""a""] = 1,
            [1, ""b""] = 2,
            [2, ""a""] = 3,
            [1, ""a""] = 4
        };
    }
}

class D
{
    public int this[int a, string b]
    {
        get { return 0; }
        set { }
    }
}
",
                GetCSharpResultAt(8, 13, "1, a"));
        }

        [Fact]
        public void MultipleIndexerArgumentsNamed()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new D
        {
            [""a"", ""b""] = 1,
            [""b"", ""c""] = 2,
            [b: ""a"", a: ""b""] = 3,
            [b: ""b"", a: ""a""] = 4
        };
    }
}

class D
{
    public int this[string a, string b]
    {
        get { return 0; }
        set { }
    }
}
",
                GetCSharpResultAt(8, 13, "a, b"));
        }

        [Fact]
        public void MultipleIndexerArgumentsNamedWithAtPrefix()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new D
        {
            [""a"", ""b""] = 1,
            [""b"", ""c""] = 2,
            [@class: ""a"", a: ""b""] = 3,
            [@class: ""b"", @a: ""a""] = 4
        };
    }
}

class D
{
    public int this[string a, string @class]
    {
        get { return 0; }
        set { }
    }
}
",
                GetCSharpResultAt(8, 13, "a, b"));
        }

        [Fact]
        public void MultipleArgumentsWithOmittedDefault()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var x = new D
        {
            [""a"", ""b""] = 1,
            [""b"", ""c""] = 2,
            [b: ""a"", a: ""b""] = 3,
            [""a""] = 4,
            [b: ""b""] = 5
        };
    }
}

class D
{
    public int this[string a = ""a"", string b = ""b""]
    {
        get { return 0; }
        set { }
    }
}
",
                GetCSharpResultAt(8, 13, "a, b"),
                GetCSharpResultAt(11, 13, "a, b"));
        }

        [Fact]
        public void NonConstantArguments()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [""a""] = 1,
            [typeof(C).ToString()] = 2,
            [""a""] = 3
        };
    }
}
",
                GetCSharpResultAt(8, 13, "a"));
        }

        [Fact]
        public void MatchingNonConstantArguments()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var a1 = ""a"";
        var a2 = ""a"";
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [a1] = 1,
            [a2] = 2
        };
    }
}
");
        }

        [Fact]
        public void ConstantMatchingNonConstantArgument()
        {
            VerifyCSharp(@"
class C
{
    void Foo()
    {
        var a = ""a"";
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [a] = 1,
            [""a""] = 2
        };
    }
}
");
        }

        [Fact]
        public void AllNonConstantArguments()
        {
            VerifyCSharp(@"
class C
{
    void Foo(string a)
    {
        var b = ""b"";
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [a] = 1,
            [b] = 2,
            [typeof(C).ToString()] = 3
        };
    }
}
");
        }

        private DiagnosticResult GetCSharpResultAt(int line, int column, string symbolName)
        {
            return GetCSharpResultAt(line, column, AvoidDuplicateElementInitialization.Rule, symbolName);
        }
    }
}
