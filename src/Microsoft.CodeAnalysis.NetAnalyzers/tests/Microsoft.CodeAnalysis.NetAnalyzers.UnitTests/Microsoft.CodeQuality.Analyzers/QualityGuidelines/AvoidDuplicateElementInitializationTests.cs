﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidDuplicateElementInitialization,
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpAvoidDuplicateElementInitializationFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class AvoidDuplicateElementInitializationTests
    {
        [Fact]
        public async Task NoInitializerAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>();
    }
}
");
        }

        [Fact]
        public async Task LiteralIntIndexAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>
        {
            [|[1]|] = ""a"",
            [2] = ""b"",
            [1] = ""c""
        };
    }
}
", @"
class C
{
    void SomeMethod()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>
        {
            [2] = ""b"",
            [1] = ""c""
        };
    }
}
");
        }

        [Fact]
        public async Task CalculatedIntIndexAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>
        {
            [|[1]|] = ""a"",
            [2] = ""b"",
            [(10 + 3) * 2 - 25] = ""c""
        };
    }
}
", @"
class C
{
    void SomeMethod()
    {
        var x = new System.Collections.Generic.Dictionary<int, string>
        {
            [2] = ""b"",
            [(10 + 3) * 2 - 25] = ""c""
        };
    }
}
");
        }

        [Fact]
        public async Task LiteralStringIndexAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [|[""a""]|] = 1,
            [""b""] = 2,
            [""a""] = 3
        };
    }
}
", @"
class C
{
    void SomeMethod()
    {
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [""b""] = 2,
            [""a""] = 3
        };
    }
}
");
        }

        [Fact]
        public async Task ConcatenatedStringIndexAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new System.Collections.Generic.Dictionary<string, int>
        {
            [|[""ab""]|] = 1,
            [""bc""] = 2,
            [""a"" + ""b""] = 3
        };
    }
}
", @"
class C
{
    void SomeMethod()
    {
        var x = new System.Collections.Generic.Dictionary<string, int>
        {
            [""bc""] = 2,
            [""a"" + ""b""] = 3
        };
    }
}
");
        }

        [Fact]
        public async Task EnumIndexAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var dictionary = new System.Collections.Generic.Dictionary<System.DateTimeKind, int>
        {
            [|[System.DateTimeKind.Local]|] = 1,
            [System.DateTimeKind.Utc] = 2,
            [System.DateTimeKind.Local] = 3
        };
    }
}
", @"
class C
{
    void SomeMethod()
    {
        var dictionary = new System.Collections.Generic.Dictionary<System.DateTimeKind, int>
        {
            [System.DateTimeKind.Utc] = 2,
            [System.DateTimeKind.Local] = 3
        };
    }
}
");
        }

        [Fact]
        public async Task MultipleIndexerArgumentsAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
            [|[1, ""a""]|] = 1,
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
", @"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
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
");
        }

        [Fact]
        public async Task MultipleIndexerArgumentsNamedAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
            [|[""a"", ""b""]|] = 1,
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
", @"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
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
");
        }

        [Fact]
        public async Task MultipleIndexerArgumentsNamedWithAtPrefixAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
            [|[""a"", ""b""]|] = 1,
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
", @"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
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
");
        }

        [Fact]
        public async Task MultipleArgumentsWithOmittedDefaultAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
            [|[""a"", ""b""]|] = 1,
            [""b"", ""c""] = 2,
            [b: ""a"", a: ""b""] = 3,
            [|[""a""]|] = 4,
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
", @"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
            [""b"", ""c""] = 2,
            [b: ""a"", a: ""b""] = 3,
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
");
        }

        [Fact]
        public async Task MultipleArgumentsWithOmittedDefault_ConsecutiveDuplicatesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
            [|[""a"", ""b""]|] = 1,
            [|[""a""]|] = 2,
            [""b"", ""c""] = 3,
            [b: ""a"", a: ""b""] = 4,
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
", @"
class C
{
    void SomeMethod()
    {
        var x = new D
        {
            [""b"", ""c""] = 3,
            [b: ""a"", a: ""b""] = 4,
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
");
        }

        [Fact]
        public async Task NonConstantArgumentsAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
class C
{
    void SomeMethod()
    {
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [|[""a""]|] = 1,
            [typeof(C).ToString()] = 2,
            [""a""] = 3
        };
    }
}
", @"
class C
{
    void SomeMethod()
    {
        var dictionary = new System.Collections.Generic.Dictionary<string, int>
        {
            [typeof(C).ToString()] = 2,
            [""a""] = 3
        };
    }
}
");
        }

        [Fact]
        public async Task MatchingNonConstantArgumentsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    void SomeMethod()
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
        public async Task ConstantMatchingNonConstantArgumentAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    void SomeMethod()
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
        public async Task AllNonConstantArgumentsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
class C
{
    void SomeMethod(string a)
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
    }
}
