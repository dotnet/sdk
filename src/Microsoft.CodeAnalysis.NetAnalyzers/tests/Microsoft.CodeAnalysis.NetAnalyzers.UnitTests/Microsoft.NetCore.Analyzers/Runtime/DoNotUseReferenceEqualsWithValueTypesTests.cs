// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseReferenceEqualsWithValueTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DoNotUseReferenceEqualsWithValueTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class DoNotUseReferenceEqualsWithValueTypesTests
    {
        [Fact]
        public async Task ReferenceTypesAreOKAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod(string test)
        {
            return ReferenceEquals(test, string.Empty);
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(test as String)
            Return ReferenceEquals(string.Empty, test)
        End Function
    End Class
End Namespace");
        }

        [Fact]
        public async Task LeftArgumentFailsForValueTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod(string test)
        {
            return ReferenceEquals(IntPtr.Zero, test);
        }
    }
}",
                GetCSharpMethodResultAt(10, 36, "System.IntPtr"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(test as String)
            Return ReferenceEquals(IntPtr.Zero, test)
        End Function
    End Class
End Namespace",
                GetVisualBasicMethodResultAt(7, 36, "System.IntPtr"));
        }

        [Fact]
        public async Task RightArgumentFailsForValueTypeAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod(string test)
        {
            return object.ReferenceEquals(test, 4);
        }
    }
}",
                GetCSharpMethodResultAt(10, 49, "int"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(test as String)
            Return Object.ReferenceEquals(test, 4)
        End Function
    End Class
End Namespace",
                GetVisualBasicMethodResultAt(7, 49, "Integer"));
        }

        [Fact]
        public async Task NoErrorForUnconstrainedGenericAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<T>(T test, object other)
        {
            return ReferenceEquals(test, other);
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of T)(test as T, other as Object)
            Return ReferenceEquals(test, other)
        End Function
    End Class
End Namespace");
        }

        [Fact]
        public async Task NoErrorForInterfaceConstrainedGenericAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<T>(T test, object other)
            where T : IDisposable
        {
            return ReferenceEquals(test, other);
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of T As IDisposable)(test as T, other as Object)
            Return ReferenceEquals(test, other)
        End Function
    End Class
End Namespace");
        }

        [Fact]
        public async Task ErrorForValueTypeConstrainedGenericAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<T>(T test, object other)
            where T : struct
        {
            return ReferenceEquals(test, other);
        }
    }
}",
                GetCSharpMethodResultAt(11, 36, "T"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of T As Structure)(test as T, other as Object)
            Return ReferenceEquals(test, other)
        End Function
    End Class
End Namespace",
                GetVisualBasicMethodResultAt(7, 36, "T"));
        }

        [Fact]
        public async Task TwoValueTypesProducesTwoErrorsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<TLeft, TRight>(TLeft test, TRight other)
            where TLeft : struct
            where TRight : struct
        {
            return ReferenceEquals(
                test,
                other);
        }
    }
}",
                GetCSharpMethodResultAt(13, 17, "TLeft"),
                GetCSharpMethodResultAt(14, 17, "TRight"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of TLeft As Structure, TRight As Structure)(test as TLeft, other as TRight)
            Return ReferenceEquals(test, other)
        End Function
    End Class
End Namespace",
                GetVisualBasicMethodResultAt(7, 36, "TLeft"),
                GetVisualBasicMethodResultAt(7, 42, "TRight"));
        }

        [Fact]
        public async Task LeftArgumentFailsForValueTypeWhenRightIsNullAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod()
        {
            return ReferenceEquals(IntPtr.Zero, null);
        }
    }
}",
                GetCSharpMethodResultAt(10, 36, "System.IntPtr"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod()
            Return ReferenceEquals(IntPtr.Zero, Nothing)
        End Function
    End Class
End Namespace",
                GetVisualBasicMethodResultAt(7, 36, "System.IntPtr"));
        }

        [Fact]
        public async Task RightArgumentFailsForValueTypeWhenLeftIsNullAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod()
        {
            return object.ReferenceEquals(null, 4);
        }
    }
}",
                GetCSharpMethodResultAt(10, 49, "int"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod()
            Return Object.ReferenceEquals(Nothing, 4)
        End Function
    End Class
End Namespace",
                GetVisualBasicMethodResultAt(7, 52, "Integer"));
        }

        [Fact]
        public async Task DoNotWarnForUserDefinedConversionsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;

namespace TestNamespace
{
    class CacheKey
    {
        public static explicit operator CacheKey(int value)
        {
            return null;
        }
    }

    class TestClass
    {
        private static bool TestMethod()
        {
            return object.ReferenceEquals(null, (CacheKey)4);
        }
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System

Namespace TestNamespace
    Class CacheKey
        Public Shared Narrowing Operator CType(value as Integer) as CacheKey
            Return Nothing
        End Operator
    End Class
    Class TestClass
        Private Shared Function TestMethod()
            Return Object.ReferenceEquals(Nothing, CType(4, CacheKey))
        End Function
    End Class
End Namespace");
        }

        [Fact]
        public async Task Comparer_ReferenceTypesAreOKAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod(string test)
        {
            return ReferenceEqualityComparer.Instance.Equals(string.Empty, test);
        }
    }
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(test as String)
            Return ReferenceEqualityComparer.Instance.Equals(string.Empty, test)
        End Function
    End Class
End Namespace",
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_LeftArgumentFailsForValueTypeAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod(string test)
        {
            return ReferenceEqualityComparer.Instance.Equals(IntPtr.Zero, test);
        }
    }
}",
                ExpectedDiagnostics = { GetCSharpComparerResultAt(11, 62, "System.IntPtr") },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(test as String)
            Return ReferenceEqualityComparer.Instance.Equals(IntPtr.Zero, test)
        End Function
    End Class
End Namespace",
                ExpectedDiagnostics = { GetVisualBasicComparerResultAt(8, 62, "System.IntPtr") },
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_RightArgumentFailsForValueTypeAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod(string test)
        {
            return ReferenceEqualityComparer.Instance.Equals(test, 4);
        }
    }
}",
                ExpectedDiagnostics = { GetCSharpComparerResultAt(11, 68, "int") },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(test as String)
            Return ReferenceEqualityComparer.Instance.Equals(test, 4)
        End Function
    End Class
End Namespace",
                ExpectedDiagnostics = { GetVisualBasicComparerResultAt(8, 68, "Integer") },
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_NoErrorForUnconstrainedGenericAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<T>(T test, object other)
        {
            return ReferenceEqualityComparer.Instance.Equals(test, other);
        }
    }
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of T)(test as T, other as Object)
            Return ReferenceEqualityComparer.Instance.Equals(test, other)
        End Function
    End Class
End Namespace",
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_NoErrorForInterfaceConstrainedGenericAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<T>(T test, object other)
            where T : IDisposable
        {
            return ReferenceEqualityComparer.Instance.Equals(test, other);
        }
    }
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of T As IDisposable)(test as T, other as Object)
            Return ReferenceEqualityComparer.Instance.Equals(test, other)
        End Function
    End Class
End Namespace",
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_ErrorForValueTypeConstrainedGenericAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<T>(T test, object other)
            where T : struct
        {
            return ReferenceEqualityComparer.Instance.Equals(test, other);
        }
    }
}",
                ExpectedDiagnostics = { GetCSharpComparerResultAt(12, 62, "T") },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of T As Structure)(test as T, other as Object)
            Return ReferenceEqualityComparer.Instance.Equals(test, other)
        End Function
    End Class
End Namespace",
                ExpectedDiagnostics = { GetVisualBasicComparerResultAt(8, 62, "T") },
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_TwoValueTypesProducesTwoErrorsAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod<TLeft, TRight>(TLeft test, TRight other)
            where TLeft : struct
            where TRight : struct
        {
            return ReferenceEqualityComparer.Instance.Equals(
                test,
                other);
        }
    }
}",
                ExpectedDiagnostics =
                {
                    GetCSharpComparerResultAt(14, 17, "TLeft"),
                    GetCSharpComparerResultAt(15, 17, "TRight"),
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(Of TLeft As Structure, TRight As Structure)(test as TLeft, other as TRight)
            Return ReferenceEqualityComparer.Instance.Equals(test, other)
        End Function
    End Class
End Namespace",
                ExpectedDiagnostics =
                {
                    GetVisualBasicComparerResultAt(8, 62, "TLeft"),
                    GetVisualBasicComparerResultAt(8, 68, "TRight"),
                },
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_LeftArgumentFailsForValueTypeWhenRightIsNullAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod()
        {
            return ReferenceEqualityComparer.Instance.Equals(IntPtr.Zero, null);
        }
    }
}",
                ExpectedDiagnostics = { GetCSharpComparerResultAt(11, 62, "System.IntPtr") },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod()
            Return ReferenceEqualityComparer.Instance.Equals(IntPtr.Zero, Nothing)
        End Function
    End Class
End Namespace",
                ExpectedDiagnostics = { GetVisualBasicComparerResultAt(8, 62, "System.IntPtr") },
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_RightArgumentFailsForValueTypeWhenLeftIsNullAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod()
        {
            return ReferenceEqualityComparer.Instance.Equals(null, 4);
        }
    }
}",
                ExpectedDiagnostics = { GetCSharpComparerResultAt(11, 68, "int") },
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod()
            Return ReferenceEqualityComparer.Instance.Equals(Nothing, 4)
        End Function
    End Class
End Namespace",
                ExpectedDiagnostics = { GetVisualBasicComparerResultAt(8, 71, "Integer") },
            }.RunAsync();
        }

        [Fact]
        public async Task Comparer_DoNotWarnForUserDefinedConversionsAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    class CacheKey
    {
        public static explicit operator CacheKey(int value)
        {
            return null;
        }
    }

    class TestClass
    {
        private static bool TestMethod()
        {
            return ReferenceEqualityComparer.Instance.Equals(null, (CacheKey)4);
        }
    }
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections.Generic

Namespace TestNamespace
    Class CacheKey
        Public Shared Narrowing Operator CType(value as Integer) as CacheKey
            Return Nothing
        End Operator
    End Class
    Class TestClass
        Private Shared Function TestMethod()
            Return ReferenceEqualityComparer.Instance.Equals(Nothing, CType(4, CacheKey))
        End Function
    End Class
End Namespace",
            }.RunAsync();
        }

        [Fact]
        public async Task ComparerDoesNotTrackThroughInterfaceAsync()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;
using System.Collections;
using System.Collections.Generic;

namespace TestNamespace
{
    class TestClass
    {
        private static bool TestMethod(string test)
        {
            IEqualityComparer<object> generic = ReferenceEqualityComparer.Instance;
            IEqualityComparer nonGeneric = ReferenceEqualityComparer.Instance;
            
            return generic.Equals(4, 5) || nonGeneric.Equals(4, 5);
        }
    }
}",
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Namespace TestNamespace
    Class TestClass
        Private Shared Function TestMethod(test as String)
            Dim generic as IEqualityComparer(Of object) = ReferenceEqualityComparer.Instance
            Dim nonGeneric as IEqualityComparer = ReferenceEqualityComparer.Instance

            Return generic.Equals(4, 5) Or nonGeneric.Equals(4, 5)
        End Function
    End Class
End Namespace",
            }.RunAsync();
        }

        private DiagnosticResult GetCSharpMethodResultAt(int line, int column, string typeName)
            => GetCSharpResultAt(DoNotUseReferenceEqualsWithValueTypesAnalyzer.MethodRule, line, column, typeName);

        private DiagnosticResult GetVisualBasicMethodResultAt(int line, int column, string typeName)
            => GetVisualBasicResultAt(DoNotUseReferenceEqualsWithValueTypesAnalyzer.MethodRule, line, column, typeName);

        private DiagnosticResult GetCSharpComparerResultAt(int line, int column, string typeName)
            => GetCSharpResultAt(DoNotUseReferenceEqualsWithValueTypesAnalyzer.ComparerRule, line, column, typeName);

        private DiagnosticResult GetVisualBasicComparerResultAt(int line, int column, string typeName)
            => GetVisualBasicResultAt(DoNotUseReferenceEqualsWithValueTypesAnalyzer.ComparerRule, line, column, typeName);

        private DiagnosticResult GetCSharpResultAt(DiagnosticDescriptor rule, int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule).WithLocation(line, column).WithArguments(typeName);
#pragma warning restore RS0030 // Do not used banned APIs

        private DiagnosticResult GetVisualBasicResultAt(DiagnosticDescriptor rule, int line, int column, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule).WithLocation(line, column).WithArguments(typeName);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
