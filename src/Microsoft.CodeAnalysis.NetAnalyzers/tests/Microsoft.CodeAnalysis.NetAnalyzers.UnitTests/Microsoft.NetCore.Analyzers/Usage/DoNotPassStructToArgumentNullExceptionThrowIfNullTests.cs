// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.NetCore.CSharp.Analyzers.Usage;
using Microsoft.NetCore.VisualBasic.Analyzers.Usage;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Usage.UnitTests
{
    using VerifyCS = CSharpCodeFixVerifier<DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull, CSharpDoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer>;
    using VerifyVB = VisualBasicCodeFixVerifier<DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull, BasicDoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer>;

    public sealed class DoNotPassStructToArgumentNullExceptionThrowIfNullTests
    {
        #region C#

        #region Diagnostic

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("bool")]
        public Task NotNullable_PassedInConstructor_Diagnostic(string type)
        {
            var code = $@"
using System;

public class Test
{{
    public Test({type} x)
    {{
        {{|#0:ArgumentNullException.ThrowIfNull(x)|}};
        Console.WriteLine(x);
    }}
}}";
            var fixedCode = $@"
using System;

public class Test
{{
    public Test({type} x)
    {{
        Console.WriteLine(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("bool")]
        public Task Nullable_PassedInConstructor_Diagnostic(string type)
        {
            var code = $@"
using System;

public class Test
{{
    public Test({type}? x)
    {{
        {{|#0:ArgumentNullException.ThrowIfNull(x)|}};
        Console.WriteLine(x);
    }}
}}";
            var fixedCode = $@"
using System;

public class Test
{{
    public Test({type}? x)
    {{
        if (!x.HasValue)
        {{
            throw new ArgumentNullException(nameof(x));
        }}

        Console.WriteLine(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("bool")]
        public Task NotNullable_PassedAsLocalVariable_Diagnostic(string type)
        {
            var code = $@"
using System;

public class Test
{{
    public void Run()
    {{
        {type} x = default;
        {{|#0:ArgumentNullException.ThrowIfNull(x)|}};
        Console.WriteLine(x);
    }}
}}";
            var fixedCode = $@"
using System;

public class Test
{{
    public void Run()
    {{
        {type} x = default;
        Console.WriteLine(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("bool")]
        public Task Nullable_PassedAsLocalVariable_Diagnostic(string type)
        {
            var code = $@"
using System;

public class Test
{{
    public void Run()
    {{
        {type}? x = null;
        {{|#0:ArgumentNullException.ThrowIfNull(x)|}};
        Console.WriteLine(x);
    }}
}}";
            var fixedCode = $@"
using System;

public class Test
{{
    public void Run()
    {{
        {type}? x = null;
        if (!x.HasValue)
        {{
            throw new ArgumentNullException(nameof(x));
        }}

        Console.WriteLine(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task NotNullable_CustomStruct_Diagnostic()
        {
            const string code = @"
using System;

public class Test
{
    public Test(MyStruct x)
    {
        {|#0:ArgumentNullException.ThrowIfNull(x)|};
        Console.WriteLine(x);
    }
}

public struct MyStruct {}";
            const string fixedCode = @"
using System;

public class Test
{
    public Test(MyStruct x)
    {
        Console.WriteLine(x);
    }
}

public struct MyStruct {}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Nullable_CustomStruct_Diagnostic()
        {
            const string code = @"
using System;

public class Test
{
    public Test(MyStruct? x)
    {
        {|#0:ArgumentNullException.ThrowIfNull(x)|};
        Console.WriteLine(x);
    }
}

public struct MyStruct {}";
            const string fixedCode = @"
using System;

public class Test
{
    public Test(MyStruct? x)
    {
        if (!x.HasValue)
        {
            throw new ArgumentNullException(nameof(x));
        }

        Console.WriteLine(x);
    }
}

public struct MyStruct {}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [CombinatorialData]
        public Task NotNullable_FullyQualifiedExceptionName_Diagnostic([CombinatorialValues("int", "System.Guid", "bool")] string type,
            [CombinatorialValues("System.ArgumentNullException", "global::System.ArgumentNullException")] string exceptionType)
        {
            var code = $@"
public class Test
{{
    public Test({type} x)
    {{
        {{|#0:{exceptionType}.ThrowIfNull(x)|}};
        System.Console.WriteLine(x);
    }}
}}";
            var fixedCode = $@"
public class Test
{{
    public Test({type} x)
    {{
        System.Console.WriteLine(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [CombinatorialData]
        public Task Nullable_FullyQualifiedExceptionName_Diagnostic([CombinatorialValues("int", "Guid", "bool")] string type,
            [CombinatorialValues("System.ArgumentNullException", "global::System.ArgumentNullException")] string exceptionType)
        {
            var code = $@"
using System;

public class Test
{{
    public Test({type}? x)
    {{
        {{|#0:{exceptionType}.ThrowIfNull(x)|}};
        Console.WriteLine(x);
    }}
}}";
            var fixedCode = $@"
using System;

public class Test
{{
    public Test({type}? x)
    {{
        if (!x.HasValue)
        {{
            throw new ArgumentNullException(nameof(x));
        }}

        Console.WriteLine(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("bool")]
        public Task NotNullable_PropertyAccess_Diagnostic(string type)
        {
            var code = $@"
using System;

public class Test
{{
    public Test(MyRecord x)
    {{
        {{|#0:ArgumentNullException.ThrowIfNull(x.X)|}};
        Console.WriteLine(x);
    }}
}}

public record MyRecord({type} X);";
            var fixedCode = $@"
using System;

public class Test
{{
    public Test(MyRecord x)
    {{
        Console.WriteLine(x);
    }}
}}

public record MyRecord({type} X);";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                LanguageVersion = LanguageVersion.CSharp9,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("bool")]
        public Task Nullable_PropertyAccess_Diagnostic(string type)
        {
            var code = $@"
using System;

public class Test
{{
    public Test(MyRecord x)
    {{
        {{|#0:ArgumentNullException.ThrowIfNull(x.X)|}};
        Console.WriteLine(x);
    }}
}}

public record MyRecord({type}? X);";
            var fixedCode = $@"
using System;

public class Test
{{
    public Test(MyRecord x)
    {{
        if (!x.X.HasValue)
        {{
            throw new ArgumentNullException(nameof(x.X));
        }}

        Console.WriteLine(x);
    }}
}}

public record MyRecord({type}? X);";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                LanguageVersion = LanguageVersion.CSharp9,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("MyType")]
        public Task Instantiation_Diagnostic(string type)
        {
            var code = $@"
using System;

class Test
{{
    void Run()
    {{
        {{|#0:ArgumentNullException.ThrowIfNull(new {type}())|}};
    }}
}}

class MyType {{}}";
            const string fixedCode = @"
using System;

class Test
{
    void Run()
    {
    }
}

class MyType {}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task EmptyInitializer_Diagnostic()
        {
            const string code = @"
using System;
using System.Collections.Generic;

class Test
{
    void Run()
    {
        {|#0:ArgumentNullException.ThrowIfNull(new MyType {})|};
    }
}

class MyType {}";
            const string fixedCode = @"
using System;
using System.Collections.Generic;

class Test
{
    void Run()
    {
    }
}

class MyType {}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Initializer_Diagnostic()
        {
            const string code = @"
using System;

class Test
{
    void Run()
    {
        {|#0:ArgumentNullException.ThrowIfNull(new MyType { Name = ""Test"" })|};
    }
}

class MyType
{
    public string Name { get; set; }
}";
            const string fixedCode = @"
using System;

class Test
{
    void Run()
    {
    }
}

class MyType
{
    public string Name { get; set; }
}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task CollectionInitializer_Diagnostic()
        {
            const string code = @"
using System;
using System.Collections.Generic;

class Test
{
    void Run()
    {
        {|#0:ArgumentNullException.ThrowIfNull(new List<int> { 1, 2, 3 })|};
    }
}";
            const string fixedCode = @"
using System;
using System.Collections.Generic;

class Test
{
    void Run()
    {
    }
}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("int")]
        [InlineData("Guid")]
        [InlineData("MyType")]
        [InlineData("System.Net.Http.HttpClient")]
        public Task Nameof_Diagnostic(string type)
        {
            var code = $@"
using System;

class Test
{{
    void Run({type} x)
    {{
        {{|#0:ArgumentNullException.ThrowIfNull(nameof(x))|}};
    }}
}}

class MyType {{}}";
            var fixedCode = $@"
using System;

class Test
{{
    void Run({type} x)
    {{
    }}
}}

class MyType {{}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Generics_Diagnostic()
        {
            const string code = @"
using System;

class Test
{
    public void M<T>(T x) where T : struct
    {
        {|#0:ArgumentNullException.ThrowIfNull(x)|};
    }
}";
            const string fixedCode = @"
using System;

class Test
{
    public void M<T>(T x) where T : struct
    {
    }
}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task TriviaIsNotPreserved_Diagnostic()
        {
            const string code = @"
using System;

class Test
{
    public void M(int x)
    {
        // Throw if null
        {|#0:ArgumentNullException.ThrowIfNull(x)|};
        Console.WriteLine(x);
    }
}";
            const string fixedCode = @"
using System;

class Test
{
    public void M(int x)
    {
        Console.WriteLine(x);
    }
}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task TriviaIsPreserved_Diagnostic()
        {
            const string code = @"
using System;

class Test
{
    // This is a method.
    public void M(int x)
    {
        {|#0:ArgumentNullException.ThrowIfNull(x)|};
        // Print x
        Console.WriteLine(x);
    }
}";
            const string fixedCode = @"
using System;

class Test
{
    // This is a method.
    public void M(int x)
    {
        // Print x
        Console.WriteLine(x);
    }
}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task TwoArguments_Diagnostic()
        {
            const string code = @"
using System;

class Test
{
    public void M(int x)
    {
        {|#0:ArgumentNullException.ThrowIfNull(x, nameof(x))|};
        Console.WriteLine(x);
    }
}";
            const string fixedCode = @"
using System;

class Test
{
    public void M(int x)
    {
        Console.WriteLine(x);
    }
}";

            return new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        #endregion

        #region No diagnostic

        [Theory]
        [InlineData("int")]
        [InlineData("int?")]
        [InlineData("System.Guid")]
        [InlineData("System.Guid?")]
        [InlineData("bool")]
        [InlineData("bool?")]
        [InlineData("MyStruct")]
        [InlineData("MyStruct?")]
        public Task CustomThrowIfNull_NoDiagnostic(string type)
        {
            var code = $@"
#nullable enable
public class Test
{{
    public Test({type} x)
    {{
        ArgumentNullException.ThrowIfNull(x);
        System.Console.WriteLine(x);
    }}
}}

public class ArgumentNullException {{
    public static void ThrowIfNull(object? value) => throw null!;
}}

public struct MyStruct {{}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                LanguageVersion = LanguageVersion.CSharp8,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("string")]
        [InlineData("string?")]
        [InlineData("int[]")]
        [InlineData("int[]?")]
        [InlineData("Random")]
        [InlineData("Random?")]
        [InlineData("System.Net.Http.HttpClient")]
        [InlineData("System.Net.Http.HttpClient?")]
        public Task ReferenceTypes_NoDiagnostic(string type)
        {
            var code = $@"
using System;

#nullable enable
public class Test
{{
    public Test({type} x)
    {{
        ArgumentNullException.ThrowIfNull(x);
        Console.WriteLine(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                LanguageVersion = LanguageVersion.CSharp8,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Record_NoDiagnostic()
        {
            const string code = @"
using System;

public class Test
{
    public Test(MyRecord x)
    {
        global::System.ArgumentNullException.ThrowIfNull(x);
        Console.WriteLine(x);
    }
}

public record MyRecord;";

            return new VerifyCS.Test
            {
                TestCode = code,
                LanguageVersion = LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("")]
        [InlineData("where T : notnull")]
        [InlineData("where T : class")]
        public Task Generics_NoDiagnostic(string whereClause)
        {
            var code = $@"
using System;

class Test
{{
    public void M<T>(T x) {whereClause}
    {{
        ArgumentNullException.ThrowIfNull(x);
    }}
}}";

            return new VerifyCS.Test
            {
                TestCode = code,
                LanguageVersion = LanguageVersion.CSharp8,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        #endregion

        #endregion

        #region VB

        #region Diagnostic

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("Boolean")]
        public Task Vb_NotNullable_PassedInConstructor_Diagnostic(string type)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Test(x As {type})
        {{|#0:ArgumentNullException.ThrowIfNull(x)|}}
        Console.WriteLine(x)
    End Sub
End Class
";
            var fixedCode = $@"
Imports System

Public Class Test
    Public Sub Test(x As {type})
        Console.WriteLine(x)
    End Sub
End Class
";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("Boolean")]
        public Task Vb_Nullable_PassedInConstructor_Diagnostic(string type)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Test(x As {type}?)
       {{|#0:ArgumentNullException.ThrowIfNull(x)|}}
        Console.WriteLine(x)
    End Sub
End Class";
            var fixedCode = $@"
Imports System

Public Class Test
    Public Sub Test(x As {type}?)
        If Not x.HasValue Then
            Throw New ArgumentNullException(NameOf(x))
        End If

        Console.WriteLine(x)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("Boolean")]
        public Task Vb_NotNullable_PassedAsLocalVariable_Diagnostic(string type)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Run()
        Dim x As {type} = Nothing
        {{|#0:ArgumentNullException.ThrowIfNull(x)|}}
        Console.WriteLine(x)
    End Sub
End Class";
            var fixedCode = $@"
Imports System

Public Class Test
    Public Sub Run()
        Dim x As {type} = Nothing
        Console.WriteLine(x)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("Boolean")]
        public Task Vb_Nullable_PassedAsLocalVariable_Diagnostic(string type)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Run()
        Dim x As {type}? = Nothing
        {{|#0:ArgumentNullException.ThrowIfNull(x)|}}
        Console.WriteLine(x)
    End Sub
End Class";
            var fixedCode = $@"
Imports System

Public Class Test
    Public Sub Run()
        Dim x As {type}? = Nothing

        If Not x.HasValue Then
            Throw New ArgumentNullException(NameOf(x))
        End If

        Console.WriteLine(x)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Vb_NotNullable_CustomStruct_Diagnostic()
        {
            const string code = @"
Imports System

Public Class Test
    Public Sub Test(x As MyStruct)
        {|#0:ArgumentNullException.ThrowIfNull(x)|}
        Console.WriteLine(x)
    End Sub
End Class

Public Structure MyStruct
End Structure";
            const string fixedCode = @"
Imports System

Public Class Test
    Public Sub Test(x As MyStruct)
        Console.WriteLine(x)
    End Sub
End Class

Public Structure MyStruct
End Structure";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Vb_Nullable_CustomStruct_Diagnostic()
        {
            const string code = @"
Imports System

Public Class Test
    Public Sub Test(x As MyStruct?)
        {|#0:ArgumentNullException.ThrowIfNull(x)|}
        Console.WriteLine(x)
    End Sub
End Class

Public Structure MyStruct
End Structure";
            const string fixedCode = @"
Imports System

Public Class Test
    Public Sub Test(x As MyStruct?)
        If Not x.HasValue Then
            Throw New ArgumentNullException(NameOf(x))
        End If

        Console.WriteLine(x)
    End Sub
End Class

Public Structure MyStruct
End Structure";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [CombinatorialData]
        public Task Vb_NotNullable_FullyQualifiedExceptionName_Diagnostic([CombinatorialValues("System.Int32", "System.Guid", "System.Boolean")] string type,
            [CombinatorialValues("System.ArgumentNullException", "Global.System.ArgumentNullException")] string exceptionType)
        {
            var code = $@"
Public Class Test
    Public Sub Test(x As {type})
        {{|#0:{exceptionType}.ThrowIfNull(x)|}}
        System.Console.WriteLine(x)
    End Sub
End Class";
            var fixedCode = $@"
Public Class Test
    Public Sub Test(x As {type})
        System.Console.WriteLine(x)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [CombinatorialData]
        public Task Vb_Nullable_FullyQualifiedExceptionName_Diagnostic([CombinatorialValues("Int32", "Guid", "Boolean")] string type,
            [CombinatorialValues("System.ArgumentNullException", "Global.System.ArgumentNullException")] string exceptionType)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Test(x As {type}?)
       {{|#0:{exceptionType}.ThrowIfNull(x)|}}
        Console.WriteLine(x)
    End Sub
End Class";
            var fixedCode = $@"
Imports System

Public Class Test
    Public Sub Test(x As {type}?)
        If Not x.HasValue Then
            Throw New ArgumentNullException(NameOf(x))
        End If

        Console.WriteLine(x)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("Boolean")]
        public Task Vb_NotNullable_PropertyAccess_Diagnostic(string type)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Test(x As MyType)
       {{|#0:ArgumentNullException.ThrowIfNull(x.X)|}}
        Console.WriteLine(x)
    End Sub
End Class

Public Class MyType
    Public Dim X As {type}
End Class";
            var fixedCode = $@"
Imports System

Public Class Test
    Public Sub Test(x As MyType)
        Console.WriteLine(x)
    End Sub
End Class

Public Class MyType
    Public Dim X As {type}
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("Boolean")]
        public Task Vb_Nullable_PropertyAccess_Diagnostic(string type)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Test(x As MyType)
       {{|#0:ArgumentNullException.ThrowIfNull(x.X)|}}
        Console.WriteLine(x)
    End Sub
End Class

Public Class MyType
    Public Dim X As {type}?
End Class";
            var fixedCode = $@"
Imports System

Public Class Test
    Public Sub Test(x As MyType)
        If Not x.X.HasValue Then
            Throw New ArgumentNullException(NameOf(x.X))
        End If

        Console.WriteLine(x)
    End Sub
End Class

Public Class MyType
    Public Dim X As {type}?
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("MyType")]
        public Task Vb_Instantiation_Diagnostic(string type)
        {
            var code = $@"
Imports System

Class Test
    Sub Run()
        {{|#0:ArgumentNullException.ThrowIfNull(New {type}())|}}
    End Sub
End Class

Class MyType
End Class";
            const string fixedCode = @"
Imports System

Class Test
    Sub Run()
    End Sub
End Class

Class MyType
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("Int32")]
        [InlineData("Guid")]
        [InlineData("MyType")]
        [InlineData("System.Net.Http.HttpClient")]
        public Task Vb_Nameof_Diagnostic(string type)
        {
            var code = $@"
Imports System

Class Test
    Sub Run(x As {type})
        {{|#0:ArgumentNullException.ThrowIfNull(nameof(x))|}}
    End Sub
End Class

Class MyType
End Class";
            var fixedCode = $@"
Imports System

Class Test
    Sub Run(x As {type})
    End Sub
End Class

Class MyType
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Vb_Generics_Diagnostic()
        {
            const string code = @"
Imports System

Class Test
    Public Sub M(Of T As Structure)(x As T)
        {|#0:ArgumentNullException.ThrowIfNull(x)|}
    End Sub
End Class";
            const string fixedCode = @"
Imports System

Class Test
    Public Sub M(Of T As Structure)(x As T)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Vb_Initializer_Diagnostic()
        {
            const string code = @"
Imports System

Class Test
    Sub Run()
        {|#0:ArgumentNullException.ThrowIfNull(new MyType With { .Name = ""Test"" })|}
    End Sub
End Class

Class MyType
    Public Property Name As String
End Class";
            const string fixedCode = @"
Imports System

Class Test
    Sub Run()
    End Sub
End Class

Class MyType
    Public Property Name As String
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Fact]
        public Task Vb_CollectionInitializer_Diagnostic()
        {
            const string code = @"
Imports System
Imports System.Collections.Generic

Class Test
    Sub Run()
        {|#0:ArgumentNullException.ThrowIfNull(new List(Of Int32) From { 1, 2, 3 })|}
    End Sub
End Class";
            const string fixedCode = @"
Imports System
Imports System.Collections.Generic

Class Test
    Sub Run()
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { NonNullableDiagnosticResult },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        #endregion

        #region No diagnostic

        [Theory]
        [InlineData("System.Int32")]
        [InlineData("System.Int32?")]
        [InlineData("System.Guid")]
        [InlineData("System.Guid?")]
        [InlineData("System.Boolean")]
        [InlineData("System.Boolean?")]
        [InlineData("MyStruct")]
        [InlineData("MyStruct?")]
        public Task Vb_CustomThrowIfNull_NoDiagnostic(string type)
        {
            var code = $@"
Public Class Test
    Public Sub Test(x As {type})
        ArgumentNullException.ThrowIfNull(x)
        System.Console.WriteLine(x)
    End Sub
End Class

Public Class ArgumentNullException
    Public Shared Sub ThrowIfNull(value As Object)
        Throw New System.Exception()
    End Sub
End Class

Public Structure MyStruct
End Structure";

            return new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("String")]
        [InlineData("Int32()")]
        [InlineData("Random")]
        [InlineData("System.Net.Http.HttpClient")]
        public Task Vb_ReferenceTypes_NoDiagnostic(string type)
        {
            var code = $@"
Imports System

Public Class Test
    Public Sub Test(x As {type})
        ArgumentNullException.ThrowIfNull(x)
        Console.WriteLine(x)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        [Theory]
        [InlineData("")]
        [InlineData("As Class")]
        public Task Vb_Generics_NoDiagnostic(string whereClause)
        {
            var code = $@"
Imports System

Class Test
    Public Sub M(Of T {whereClause})(x As T)
        ArgumentNullException.ThrowIfNull(x)
    End Sub
End Class";

            return new VerifyVB.Test
            {
                TestCode = code,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            }.RunAsync();
        }

        #endregion

        #endregion

        private static readonly DiagnosticResult NonNullableDiagnosticResult = new DiagnosticResult(DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.DoNotPassNonNullableValueDiagnostic)
            .WithLocation(0);

        private static readonly DiagnosticResult NullableDiagnosticResult = new DiagnosticResult(DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.DoNotPassNullableStructDiagnostic)
            .WithLocation(0);
    }
}
