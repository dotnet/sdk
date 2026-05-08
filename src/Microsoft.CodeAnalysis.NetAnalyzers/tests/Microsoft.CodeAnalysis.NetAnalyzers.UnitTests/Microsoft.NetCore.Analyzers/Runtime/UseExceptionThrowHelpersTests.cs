// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseExceptionThrowHelpers,
    Microsoft.NetCore.Analyzers.Runtime.UseExceptionThrowHelpersFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseExceptionThrowHelpers,
    Microsoft.NetCore.Analyzers.Runtime.UseExceptionThrowHelpersFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseExceptionThrowHelpersTests
    {
        [Fact]
        public async Task ArgumentNullExceptionThrowIfNull_DoesntExist_NoDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace System
{
    public class ArgumentNullException : Exception
    {
        public ArgumentNullException(string paramName) { }
        public ArgumentNullException(string paramName, string message) { }
    }
}

class C
{
    void M(string arg)
    {
        if (arg is null)
            throw new ArgumentNullException(nameof(arg));

        if (arg == null)
            throw new ArgumentNullException(nameof(arg));

        if (null == arg)
            throw new ArgumentNullException(nameof(arg));

        if (arg is null)
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (arg == null)
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (null == arg)
        {
            throw new ArgumentNullException(nameof(arg));
        }
    }

    string this[string name]
    {
        get
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));
            return name;
        }
    }
}
");
        }

        [Fact]
        public async Task ArgumentNullExceptionThrowIfNull_FixAppliesAsAppropriate()
        {
            await new VerifyCS.Test()
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode =
@"
using System;

namespace System
{
    public class ArgumentNullException : Exception
    {
        public ArgumentNullException() { }
        public ArgumentNullException(string paramName) { }
        public ArgumentNullException(string paramName, string message) { }
        public ArgumentNullException(string paramName, Exception innerException) { }
        public static void ThrowIfNull(object argument, string name = null) { }           
    }
}

class C
{
    void M(string arg)
    {
        {|CA1510:if (arg is null)
            throw new ArgumentNullException(nameof(arg));|}
        {|CA1510:if (arg == null)
            throw new ArgumentNullException(""arg"", (string)null);|}
        {|CA1510:if (null == arg)
            throw new ArgumentNullException(nameof(arg));|}
        {|CA1510:if (arg is null)
        {
            throw new ArgumentNullException(""arg"");
        }|}
        {|CA1510:if (arg == null)
        {
            throw new ArgumentNullException(nameof(arg), """");
        }|}
        {|CA1510:if (null == arg)
        {
            throw new ArgumentNullException(nameof(arg));
        }|}
        {|CA1510:if (null == arg)
        {
            throw new ArgumentNullException(""something else"");
        }|}

        if (arg == ""test"")
        {
            Console.WriteLine(arg);
        }
        else {|CA1510:if (arg is null)
        {
            throw new ArgumentNullException(nameof(arg));
        }|}

        if (arg is null)
            throw new ArgumentNullException(nameof(arg), ""possibly meaningful message"");

        if (arg is null)
            throw new ArgumentNullException(nameof(arg), new Exception());

        if (arg is null)
        {
            Console.WriteLine(); // another operation in the block
            throw new ArgumentNullException(nameof(arg));
        }

        if (arg is not null) // inverted condition
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (arg != null) // inverted condition
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (null != arg) // inverted condition
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (arg is null)
        {
            throw new ArgumentNullException(nameof(arg));
        }
        else if (arg == ""test"")
        {
            Console.WriteLine(arg);
        }

        if (arg is null)
            throw new ArgumentNullException(ComputeName(nameof(arg)));

        if (arg is null)
            throw new ArgumentNullException(innerException: null, paramName: ComputeName(nameof(arg)));

        if (arg is null)
            throw new ArgumentNullException(IntPtr.Size == 8 ? ""arg"" : ""arg"");

        throw new ArgumentNullException(nameof(arg)); // no guard
    }

    static string ComputeName(string arg) => arg;

    string this[string name]
    {
        get
        {
            {|CA1510:if (name is null)
                throw new ArgumentNullException(nameof(name));|}
            return name;
        }
    }

    void NullableArg(int? arg)
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }

    void GenericMethod<T>(T arg)
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }

    void GenericMethodWithClassConstraint<T>(T arg) where T : class
    {
        {|CA1510:if (arg is null) throw new ArgumentNullException(nameof(arg));|}
    }

    void GenericMethodWithTypeConstraint<T>(T arg) where T : C
    {
        {|CA1510:if (arg is null) throw new ArgumentNullException(nameof(arg));|}
    }

    void GenericMethodWithInterfaceConstraint<T>(T arg) where T : IDisposable
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }
}

class GenericType<T>
{
    void M(T arg)
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }
}
",
                FixedCode =
@"
using System;

namespace System
{
    public class ArgumentNullException : Exception
    {
        public ArgumentNullException() { }
        public ArgumentNullException(string paramName) { }
        public ArgumentNullException(string paramName, string message) { }
        public ArgumentNullException(string paramName, Exception innerException) { }
        public static void ThrowIfNull(object argument, string name = null) { }           
    }
}

class C
{
    void M(string arg)
    {
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(arg);
        ArgumentNullException.ThrowIfNull(arg);

        if (arg == ""test"")
        {
            Console.WriteLine(arg);
        }
        else ArgumentNullException.ThrowIfNull(arg);

        if (arg is null)
            throw new ArgumentNullException(nameof(arg), ""possibly meaningful message"");

        if (arg is null)
            throw new ArgumentNullException(nameof(arg), new Exception());

        if (arg is null)
        {
            Console.WriteLine(); // another operation in the block
            throw new ArgumentNullException(nameof(arg));
        }

        if (arg is not null) // inverted condition
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (arg != null) // inverted condition
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (null != arg) // inverted condition
        {
            throw new ArgumentNullException(nameof(arg));
        }

        if (arg is null)
        {
            throw new ArgumentNullException(nameof(arg));
        }
        else if (arg == ""test"")
        {
            Console.WriteLine(arg);
        }

        if (arg is null)
            throw new ArgumentNullException(ComputeName(nameof(arg)));

        if (arg is null)
            throw new ArgumentNullException(innerException: null, paramName: ComputeName(nameof(arg)));

        if (arg is null)
            throw new ArgumentNullException(IntPtr.Size == 8 ? ""arg"" : ""arg"");

        throw new ArgumentNullException(nameof(arg)); // no guard
    }

    static string ComputeName(string arg) => arg;

    string this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            return name;
        }
    }

    void NullableArg(int? arg)
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }

    void GenericMethod<T>(T arg)
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }

    void GenericMethodWithClassConstraint<T>(T arg) where T : class
    {
        ArgumentNullException.ThrowIfNull(arg);
    }

    void GenericMethodWithTypeConstraint<T>(T arg) where T : C
    {
        ArgumentNullException.ThrowIfNull(arg);
    }

    void GenericMethodWithInterfaceConstraint<T>(T arg) where T : IDisposable
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }
}

class GenericType<T>
{
    void M(T arg)
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }
}
"
            }.RunAsync();
        }

        [Fact]
        public async Task ArgumentNullExceptionThrowIfNull_EnsureSystemIsUsed()
        {
            await new VerifyCS.Test()
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                TestCode =
@"
class C
{
    void M(string arg)
    {
        {|CA1510:if (arg is null) throw new System.ArgumentNullException(nameof(arg));|}
    }
}
",
                FixedCode =
@"
class C
{
    void M(string arg)
    {
        System.ArgumentNullException.ThrowIfNull(arg);
    }
}
"
            }.RunAsync();
        }

        [Fact]
        public async Task ArgumentExceptionThrowIfNullOrEmpty_DoesntExist_NoDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace System
{
    public class ArgumentException : Exception
    {
        public ArgumentException() { }
        public ArgumentException(string message) { }
        public ArgumentException(string message, string paramName) { }
        public ArgumentException(string message, string paramName, Exception innerException) { }
    }
}

class C
{
    void M(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            throw new ArgumentException("""", ""arg"");

        if (arg is null || arg.Length == 0)
            throw new ArgumentException("""", ""arg"");

        if (arg == null || 0 == arg.Length)
            throw new ArgumentException("""", ""arg"");

        if (arg is null || arg == string.Empty)
        {
            throw new ArgumentException("""", ""arg"");
        }
    }
}
");
        }

        [Fact]
        public async Task ArgumentExceptionThrowIfNullOrEmpty_FixAppliesAsAppropriate()
        {
            await new VerifyCS.Test()
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode =
@"
using System;

namespace System
{
    public class ArgumentException : Exception
    {
        public ArgumentException() { }
        public ArgumentException(string message) { }
        public ArgumentException(string message, string paramName) { }
        public ArgumentException(string message, string paramName, Exception innerException) { }
        public static void ThrowIfNullOrEmpty(string arg) { }
    }
}

class C
{
    void M0(string arg)
    {
        {|CA1511:if (string.IsNullOrEmpty(arg))
            throw new ArgumentException("""", ""arg"");|}

        {|CA1511:if (arg is null || arg.Length == 0)
            throw new ArgumentException("""", ""arg"");|}

        {|CA1511:if (arg == null || 0 == arg.Length)
            throw new ArgumentException("""", ""arg"");|}

        {|CA1511:if (arg == null || arg == string.Empty)
            throw new ArgumentException("""", ""arg"");|}

        {|CA1511:if (string.IsNullOrEmpty(arg))
        {
            throw new ArgumentException();
        }|}

        {|CA1511:if (arg is null || arg.Length == 0)
        {
            throw new ArgumentException("""");
        }|}

        {|CA1511:if (arg == null || 0 == arg.Length)
        {
            throw new ArgumentException("""", ""arg"");
        }|}

        {|CA1511:if (arg == null || arg == string.Empty)
        {
            throw new ArgumentException();
        }|}

        if (arg is null)
            throw new ArgumentException("""", ""arg"");
    }

    void M1(string arg1, string arg2)
    {
        if (!string.IsNullOrEmpty(arg1))
            throw new ArgumentException("""", ""arg1"");

        if (string.IsNullOrEmpty(arg1))
            throw new ArgumentException(""something"", ""arg1"");

        if (string.IsNullOrEmpty(arg1))
            throw new ArgumentException("""", ""arg1"", new Exception());

        if (arg1 is not null && arg1.Length != 0)
            throw new ArgumentException("""", ""arg1"");

        if (arg1 is null)
            throw new ArgumentException("""", ""arg1"");

        if (arg1.Length == 0) // this case combined with the previous one could be handled in the future
            throw new ArgumentException("""", ""arg1"");

        if (arg1 == string.Empty) // here as well
        {
            throw new ArgumentException("""", ""arg1"");
        }

        if (string.IsNullOrEmpty(arg1))
        {
            Console.WriteLine();
            throw new ArgumentException();
        }

        if (arg1 is null || arg2.Length == 0)
            throw new ArgumentException();

        if (arg2 == null || arg1 == string.Empty)
            throw new ArgumentException();

        string nonArg = ""test"";

        if (string.IsNullOrEmpty(nonArg))
        {
            Console.WriteLine();
            throw new ArgumentException();
        }

        if (nonArg is null || nonArg.Length == 0)
        {
            Console.WriteLine();
            throw new ArgumentException();
        }
    }
}
",
                FixedCode =
@"
using System;

namespace System
{
    public class ArgumentException : Exception
    {
        public ArgumentException() { }
        public ArgumentException(string message) { }
        public ArgumentException(string message, string paramName) { }
        public ArgumentException(string message, string paramName, Exception innerException) { }
        public static void ThrowIfNullOrEmpty(string arg) { }
    }
}

class C
{
    void M0(string arg)
    {
        ArgumentException.ThrowIfNullOrEmpty(arg);

        ArgumentException.ThrowIfNullOrEmpty(arg);

        ArgumentException.ThrowIfNullOrEmpty(arg);

        ArgumentException.ThrowIfNullOrEmpty(arg);

        ArgumentException.ThrowIfNullOrEmpty(arg);

        ArgumentException.ThrowIfNullOrEmpty(arg);

        ArgumentException.ThrowIfNullOrEmpty(arg);

        ArgumentException.ThrowIfNullOrEmpty(arg);

        if (arg is null)
            throw new ArgumentException("""", ""arg"");
    }

    void M1(string arg1, string arg2)
    {
        if (!string.IsNullOrEmpty(arg1))
            throw new ArgumentException("""", ""arg1"");

        if (string.IsNullOrEmpty(arg1))
            throw new ArgumentException(""something"", ""arg1"");

        if (string.IsNullOrEmpty(arg1))
            throw new ArgumentException("""", ""arg1"", new Exception());

        if (arg1 is not null && arg1.Length != 0)
            throw new ArgumentException("""", ""arg1"");

        if (arg1 is null)
            throw new ArgumentException("""", ""arg1"");

        if (arg1.Length == 0) // this case combined with the previous one could be handled in the future
            throw new ArgumentException("""", ""arg1"");

        if (arg1 == string.Empty) // here as well
        {
            throw new ArgumentException("""", ""arg1"");
        }

        if (string.IsNullOrEmpty(arg1))
        {
            Console.WriteLine();
            throw new ArgumentException();
        }

        if (arg1 is null || arg2.Length == 0)
            throw new ArgumentException();

        if (arg2 == null || arg1 == string.Empty)
            throw new ArgumentException();

        string nonArg = ""test"";

        if (string.IsNullOrEmpty(nonArg))
        {
            Console.WriteLine();
            throw new ArgumentException();
        }

        if (nonArg is null || nonArg.Length == 0)
        {
            Console.WriteLine();
            throw new ArgumentException();
        }
    }
}
"
            }.RunAsync();
        }

        [Fact]
        public async Task ArgumentOutOfRangeExceptionThrowIf_DoesntExist_NoDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace System
{
    public class ArgumentOutOfRangeException : Exception
    {
        public ArgumentOutOfRangeException(string paramName) { }
        public ArgumentOutOfRangeException(string paramName, string message) { }
    }
}

class C
{
    void M(int arg)
    {
        if (arg is 0)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg == 0)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg < 0)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg <= 0)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg <= 42)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg < 42)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg > 42)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }

        if (arg >= 42)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }

        if (arg > TimeSpan.FromSeconds(42).TotalSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }
    }
}
");
        }

        [Fact]
        public async Task ArgumentOutOfRangeExceptionThrowIf_FixAppliesAsAppropriate()
        {
            await new VerifyCS.Test()
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode =
@"
using System;

namespace System
{
    public class ArgumentOutOfRangeException : Exception
    {
        public ArgumentOutOfRangeException(string paramName) { }
        public ArgumentOutOfRangeException(string paramName, string message) { }
        public static void ThrowIfZero<T>(T arg) { }
        public static void ThrowIfNegative<T>(T arg) { }
        public static void ThrowIfNegativeOrZero<T>(T arg) { }
        public static void ThrowIfGreaterThan<T>(T arg, T other) { }
        public static void ThrowIfGreaterThanOrEqual<T>(T arg, T other) { }
        public static void ThrowIfLessThan<T>(T arg, T other) { }
        public static void ThrowIfLessThanOrEqual<T>(T arg, T other) { }
        public static void ThrowIfEqual<T>(T arg, T other) { }
        public static void ThrowIfNotEqual<T>(T arg, T other) { }
    }
}

class C
{
    void M(int arg)
    {
        {|CA1512:if (arg is 0)
            throw new ArgumentOutOfRangeException(nameof(arg));|}
        {|CA1512:if (arg == 0)
            throw new ArgumentOutOfRangeException(nameof(arg));|}
        {|CA1512:if (0 == arg)
            throw new ArgumentOutOfRangeException(nameof(arg));|}

        {|CA1512:if (arg < 0)
            throw new ArgumentOutOfRangeException(nameof(arg));|}
        {|CA1512:if (0 > arg)
            throw new ArgumentOutOfRangeException(nameof(arg));|}

        {|CA1512:if (arg <= 0)
            throw new ArgumentOutOfRangeException(nameof(arg));|}
        {|CA1512:if (0 >= arg)
            throw new ArgumentOutOfRangeException(nameof(arg));|}

        {|CA1512:if (arg <= 42)
            throw new ArgumentOutOfRangeException(nameof(arg));|}
        {|CA1512:if (42 >= arg)
            throw new ArgumentOutOfRangeException(nameof(arg));|}

        {|CA1512:if (arg < 42)
            throw new ArgumentOutOfRangeException(nameof(arg));|}
        {|CA1512:if (42 > arg)
            throw new ArgumentOutOfRangeException(nameof(arg));|}

        {|CA1512:if (arg > 42)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}
        {|CA1512:if (42 < arg)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}

        {|CA1512:if (arg >= 42)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}
        {|CA1512:if (42 <= arg)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}

        {|CA1512:if (arg == 42)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}
        {|CA1512:if (42 == arg)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}
        {|CA1512:if (arg != 42)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}
        {|CA1512:if (42 != arg)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}

        {|CA1512:if (arg > (int)TimeSpan.FromSeconds(42).TotalSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}
        {|CA1512:if ((int)TimeSpan.FromSeconds(42).TotalSeconds < arg)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }|}

        if (arg is 42)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg is 0)
        {
            Console.WriteLine();
            throw new ArgumentOutOfRangeException(nameof(arg));
        }

        if (arg is < 0) // we could augment the analyzer in the future to support this
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }

        if (arg > 42 && arg < 84) // we could augment the analyzer in the future to support this
            throw new ArgumentOutOfRangeException(nameof(arg));
    }

    void Enums(DayOfWeek dow)
    {
        if (dow > DayOfWeek.Sunday)
            throw new ArgumentOutOfRangeException(nameof(dow));
    }

    void Nullables(int? arg)
    {
        if (arg < 0)
            throw new ArgumentOutOfRangeException(nameof(arg));
    }
}
",
                FixedCode =
@"
using System;

namespace System
{
    public class ArgumentOutOfRangeException : Exception
    {
        public ArgumentOutOfRangeException(string paramName) { }
        public ArgumentOutOfRangeException(string paramName, string message) { }
        public static void ThrowIfZero<T>(T arg) { }
        public static void ThrowIfNegative<T>(T arg) { }
        public static void ThrowIfNegativeOrZero<T>(T arg) { }
        public static void ThrowIfGreaterThan<T>(T arg, T other) { }
        public static void ThrowIfGreaterThanOrEqual<T>(T arg, T other) { }
        public static void ThrowIfLessThan<T>(T arg, T other) { }
        public static void ThrowIfLessThanOrEqual<T>(T arg, T other) { }
        public static void ThrowIfEqual<T>(T arg, T other) { }
        public static void ThrowIfNotEqual<T>(T arg, T other) { }
    }
}

class C
{
    void M(int arg)
    {
        ArgumentOutOfRangeException.ThrowIfZero(arg);
        ArgumentOutOfRangeException.ThrowIfZero(arg);
        ArgumentOutOfRangeException.ThrowIfZero(arg);

        ArgumentOutOfRangeException.ThrowIfNegative(arg);
        ArgumentOutOfRangeException.ThrowIfNegative(arg);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arg);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arg);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(arg, 42);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(arg, 42);

        ArgumentOutOfRangeException.ThrowIfLessThan(arg, 42);
        ArgumentOutOfRangeException.ThrowIfLessThan(arg, 42);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(arg, 42);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(arg, 42);

        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arg, 42);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arg, 42);

        ArgumentOutOfRangeException.ThrowIfEqual(arg, 42);
        ArgumentOutOfRangeException.ThrowIfEqual(arg, 42);
        ArgumentOutOfRangeException.ThrowIfNotEqual(arg, 42);
        ArgumentOutOfRangeException.ThrowIfNotEqual(arg, 42);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(arg, (int)TimeSpan.FromSeconds(42).TotalSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(arg, (int)TimeSpan.FromSeconds(42).TotalSeconds);

        if (arg is 42)
            throw new ArgumentOutOfRangeException(nameof(arg));

        if (arg is 0)
        {
            Console.WriteLine();
            throw new ArgumentOutOfRangeException(nameof(arg));
        }

        if (arg is < 0) // we could augment the analyzer in the future to support this
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }

        if (arg > 42 && arg < 84) // we could augment the analyzer in the future to support this
            throw new ArgumentOutOfRangeException(nameof(arg));
    }

    void Enums(DayOfWeek dow)
    {
        if (dow > DayOfWeek.Sunday)
            throw new ArgumentOutOfRangeException(nameof(dow));
    }

    void Nullables(int? arg)
    {
        if (arg < 0)
            throw new ArgumentOutOfRangeException(nameof(arg));
    }
}
"
            }.RunAsync();
        }

        [Fact]
        public async Task ArgumentOutOfRangeExceptionThrowIf_NoDiagnosticForMissingHelper()
        {
            await new VerifyCS.Test()
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestCode =
@"
using System;

namespace System
{
    public class ArgumentOutOfRangeException : Exception
    {
        public ArgumentOutOfRangeException(string paramName) { }
        public ArgumentOutOfRangeException(string paramName, string message) { }
        public static void ThrowIfZero<T>(T arg) { }
        public static void ThrowIfNegative<T>(T arg) { }
        public static void ThrowIfNegativeOrZero<T>(T arg) { }
        public static void ThrowIfGreaterThan<T>(T arg, T other) { }
        public static void ThrowIfGreaterThanOrEqual<T>(T arg, T other) { }
        public static void ThrowIfLessThan<T>(T arg, T other) { }
        public static void ThrowIfLessThanOrEqual<T>(T arg, T other) { }
    }
}

class C
{
    void M(int arg)
    {
        if (arg != 42)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }
        if (42 != arg)
        {
            throw new ArgumentOutOfRangeException(nameof(arg));
        }
    }
}
"
            }.RunAsync();
        }

        [Fact]
        public async Task ObjectDisposedExceptionThrowIf_DoesntExist_NoDiagnostics()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace System
{
    public class ObjectDisposedException : Exception
    {
        public ObjectDisposedException(string type) { }
        public ObjectDisposedException(string type, string message) { }
    }
}

class C
{
    private bool IsDisposed { get; set; }

    void M()
    {
        if (IsDisposed) throw new ObjectDisposedException(null);

        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);

        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (DateTime.UtcNow.Hour == 0)
        {
            throw new ObjectDisposedException(nameof(DateTime));
        }
    }

    string Prop
    {
        get
        {
            if (IsDisposed) throw new ObjectDisposedException(null);
            return ""test"";
        }
    }
}
");
        }

        [Fact]
        public async Task ObjectDisposedExceptionThrowIf_FixesAppliedAsAppropriate()
        {
            var test = new VerifyCS.Test()
            {
                TestCode = @"
using System;

namespace System
{
    public class ObjectDisposedException : Exception
    {
        public ObjectDisposedException(string type) { }
        public ObjectDisposedException(string type, string message) { }
        public static void ThrowIf(bool condition, object instance) { }
        public static void ThrowIf(bool condition, Type type) { }
    }
}

class C
{
    private bool IsDisposed { get; set; }

    private C _state;

    void M(object something)
    {
        {|CA1513:if (IsDisposed) throw new ObjectDisposedException(null);|}

        {|CA1513:if (IsDisposed)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }|}

        {|CA1513:if (IsDisposed)
            throw new ObjectDisposedException(something.GetType().Name);|}

        {|CA1513:if (_state.IsDisposed)
            throw new ObjectDisposedException(_state.GetType().Name);|}

        {|CA1513:if (DateTime.UtcNow.Hour == 0)
        {
            throw new ObjectDisposedException(nameof(DateTime));
        }|}

        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name, ""something"");

        throw new ObjectDisposedException(null);
    }

    string Prop
    {
        get
        {
            {|CA1513:if (IsDisposed)
                throw new ObjectDisposedException(null);|}
            return ""test"";
        }
    }
}

struct S
{
    private bool IsDisposed { get; set; }

    void M()
    {
        if (IsDisposed) throw new ObjectDisposedException(null);
        if (IsDisposed) throw new ObjectDisposedException(this.GetType().FullName);
    }
}
",
                FixedCode =
@"
using System;

namespace System
{
    public class ObjectDisposedException : Exception
    {
        public ObjectDisposedException(string type) { }
        public ObjectDisposedException(string type, string message) { }
        public static void ThrowIf(bool condition, object instance) { }
        public static void ThrowIf(bool condition, Type type) { }
    }
}

class C
{
    private bool IsDisposed { get; set; }

    private C _state;

    void M(object something)
    {
        {|CA1513:if (IsDisposed) throw new ObjectDisposedException(null);|}

        ObjectDisposedException.ThrowIf(IsDisposed, this);

        ObjectDisposedException.ThrowIf(IsDisposed, something);

        ObjectDisposedException.ThrowIf(_state.IsDisposed, _state);

        {|CA1513:if (DateTime.UtcNow.Hour == 0)
        {
            throw new ObjectDisposedException(nameof(DateTime));
        }|}

        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name, ""something"");

        throw new ObjectDisposedException(null);
    }

    string Prop
    {
        get
        {
            {|CA1513:if (IsDisposed)
                throw new ObjectDisposedException(null);|}
            return ""test"";
        }
    }
}

struct S
{
    private bool IsDisposed { get; set; }

    void M()
    {
        if (IsDisposed) throw new ObjectDisposedException(null);
        if (IsDisposed) throw new ObjectDisposedException(this.GetType().FullName);
    }
}
"
            };
            test.FixedState.MarkupHandling = CodeAnalysis.Testing.MarkupMode.Allow;
            await test.RunAsync();
        }

        [Fact]
        public async Task VisualBasic_ValidateAllThrowHelpers()
        {
            await VerifyVB.VerifyCodeFixAsync(
@"
Imports System
 
Class C
    Public  Sub M(ByVal arg As String, ByVal value As Integer)
        {|CA1510:If arg Is Nothing Then
        	 Throw New ArgumentNullException(nameof(arg))
        End If|}

        {|CA1511:If String.IsNullOrEmpty(arg) Then
        	 Throw New ArgumentException("""", nameof(arg))
        End If|}

        {|CA1513:If arg Is Nothing Then
        	 Throw New ObjectDisposedException(Me.GetType().Name)
        End If|}

        {|CA1512:If value < 42 Then
        	 Throw New ArgumentOutOfRangeException(nameof(value))
        End If|}

        {|CA1512:If value = 42 Then
        	 Throw New ArgumentOutOfRangeException(nameof(value))
        End If|}
    End Sub
End Class

Namespace System
    Public Class ArgumentNullException
        Inherits Exception

        Public Sub New()
        End Sub

        Public Sub New(ByVal paramName As String)
        End Sub

        Public Shared Sub ThrowIfNull(ByVal argument As Object, ByVal Optional name As String = Nothing)
        End Sub
    End Class

    Public Class ArgumentException
        Inherits Exception

        Public Sub New()
        End Sub

        Public Sub New(ByVal message As String)
        End Sub

        Public Sub New(ByVal message As String, ByVal paramName As String)
        End Sub

        Public Shared Sub ThrowIfNullOrEmpty(ByVal argument As String, ByVal Optional name As String = Nothing)
        End Sub
    End Class

    Public Class ArgumentOutOfRangeException
        Inherits Exception

        Public Sub New(ByVal paramName As String)
        End Sub

        Public Shared Sub ThrowIfZero(Of T)(ByVal arg As T)
        End Sub

        Public Shared Sub ThrowIfNegative(Of T)(ByVal arg As T)
        End Sub

        Public Shared Sub ThrowIfNegativeOrZero(Of T)(ByVal arg As T)
        End Sub

        Public Shared Sub ThrowIfGreaterThan(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfGreaterThanOrEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfLessThan(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfLessThanOrEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfNotEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub
    End Class

    Public Class ObjectDisposedException
        Inherits Exception

        Public Sub New(ByVal type As String)
        End Sub

        Public Sub New(ByVal type As String, ByVal message As String)
        End Sub

        Public Shared Sub ThrowIf(ByVal condition As Boolean, ByVal instance As Object)
        End Sub

        Public Shared Sub ThrowIf(ByVal condition As Boolean, ByVal type As Type)
        End Sub
    End Class
End Namespace
",
@"
Imports System
 
Class C
    Public  Sub M(ByVal arg As String, ByVal value As Integer)
        ArgumentNullException.ThrowIfNull(arg)

        ArgumentException.ThrowIfNullOrEmpty(arg)

        ObjectDisposedException.ThrowIf(arg Is Nothing, Me)

        ArgumentOutOfRangeException.ThrowIfLessThan(value, 42)

        ArgumentOutOfRangeException.ThrowIfEqual(value, 42)
    End Sub
End Class

Namespace System
    Public Class ArgumentNullException
        Inherits Exception

        Public Sub New()
        End Sub

        Public Sub New(ByVal paramName As String)
        End Sub

        Public Shared Sub ThrowIfNull(ByVal argument As Object, ByVal Optional name As String = Nothing)
        End Sub
    End Class

    Public Class ArgumentException
        Inherits Exception

        Public Sub New()
        End Sub

        Public Sub New(ByVal message As String)
        End Sub

        Public Sub New(ByVal message As String, ByVal paramName As String)
        End Sub

        Public Shared Sub ThrowIfNullOrEmpty(ByVal argument As String, ByVal Optional name As String = Nothing)
        End Sub
    End Class

    Public Class ArgumentOutOfRangeException
        Inherits Exception

        Public Sub New(ByVal paramName As String)
        End Sub

        Public Shared Sub ThrowIfZero(Of T)(ByVal arg As T)
        End Sub

        Public Shared Sub ThrowIfNegative(Of T)(ByVal arg As T)
        End Sub

        Public Shared Sub ThrowIfNegativeOrZero(Of T)(ByVal arg As T)
        End Sub

        Public Shared Sub ThrowIfGreaterThan(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfGreaterThanOrEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfLessThan(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfLessThanOrEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub

        Public Shared Sub ThrowIfNotEqual(Of T)(ByVal arg As T, ByVal other As T)
        End Sub
    End Class

    Public Class ObjectDisposedException
        Inherits Exception

        Public Sub New(ByVal type As String)
        End Sub

        Public Sub New(ByVal type As String, ByVal message As String)
        End Sub

        Public Shared Sub ThrowIf(ByVal condition As Boolean, ByVal instance As Object)
        End Sub

        Public Shared Sub ThrowIf(ByVal condition As Boolean, ByVal type As Type)
        End Sub
    End Class
End Namespace
");
        }
    }
}