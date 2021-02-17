// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.CSharp.Analyzers.Performance;
using Microsoft.NetCore.VisualBasic.Analyzers.Performance;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseCountProperlyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Performance.CSharpPreferIsEmptyOverCountFixer>;

using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Performance.UseCountProperlyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Performance.BasicPreferIsEmptyOverCountFixer>;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public class PreferIsEmptyOverCountTests
    {
        private const string Count = nameof(Count);
        private const string IsEmpty = nameof(IsEmpty);

        private const string csSnippet = @"
public class Test
{{
    private System.Collections.Concurrent.ConcurrentDictionary<string, string> _concurrent;
    public bool DummyProperty => {0};
}}
";

        private const string vbSnippet = @"
Public Class Test
    Private _concurrent As System.Collections.Concurrent.ConcurrentDictionary(Of string, string)
    Public ReadOnly Property DummyProperty As Boolean
        Get
            Return {0}
        End Get
    End Property
End Class
";

        [Fact]
        public async Task CSharpSimpleCase()
        {
            string csInput = @"
using System;
using System.Collections.Concurrent;

public class Test
{
    private ConcurrentDictionary<string, string> _myDictionary;
    public ConcurrentDictionary<string, string> MyDictionary 
    { 
        get => _myDictionary;
        set 
        {
            if (value == null || value.Count == 0)
            {
                throw new ArgumentException(nameof(value));
            }
            _myDictionary = value;
        }
    }    
}
";

            string csFix = @"
using System;
using System.Collections.Concurrent;

public class Test
{
    private ConcurrentDictionary<string, string> _myDictionary;
    public ConcurrentDictionary<string, string> MyDictionary 
    { 
        get => _myDictionary;
        set 
        {
            if (value == null || value.IsEmpty)
            {
                throw new ArgumentException(nameof(value));
            }
            _myDictionary = value;
        }
    }    
}
";
            await VerifyCS.VerifyCodeFixAsync(
                csInput,
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(13, 34, 13, 50),
                csFix);
        }

        [Fact]
        public async Task BasicSimpleCase()
        {
            string vbInput = @"
Imports System
Imports System.Collections.Concurrent

Public Class Test
    Private _myDictionary As ConcurrentDictionary(Of String, String)

    Public Property MyDictionary As ConcurrentDictionary(Of String, String)
        Get
            Return _myDictionary
        End Get
        Set(ByVal value As ConcurrentDictionary(Of String, String))

            If value Is Nothing OrElse value.Count = 0 Then
                Throw New ArgumentException(NameOf(value))
            End If

            _myDictionary = value
        End Set
    End Property
End Class
";

            string vbFix = @"
Imports System
Imports System.Collections.Concurrent

Public Class Test
    Private _myDictionary As ConcurrentDictionary(Of String, String)

    Public Property MyDictionary As ConcurrentDictionary(Of String, String)
        Get
            Return _myDictionary
        End Get
        Set(ByVal value As ConcurrentDictionary(Of String, String))

            If value Is Nothing OrElse value.IsEmpty Then
                Throw New ArgumentException(NameOf(value))
            End If

            _myDictionary = value
        End Set
    End Property
End Class
";
            await VerifyVB.VerifyCodeFixAsync(
                vbInput,
                VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(14, 40, 14, 55),
                vbFix);
        }

        [Theory]
        [InlineData("(_concurrent.Count) > 0", "!_concurrent.IsEmpty")]
        [InlineData("_concurrent.Count > (0)", "!_concurrent.IsEmpty")]
        [InlineData("(_concurrent.Count) > (0)", "!_concurrent.IsEmpty")]
        [InlineData("((_concurrent).Count) > (0)", "!(_concurrent).IsEmpty")]
        public Task CSharpTestFixOnParentheses(string condition, string expectedFix)
        {
            string input = string.Format(CultureInfo.InvariantCulture, csSnippet, condition);
            string fix = string.Format(CultureInfo.InvariantCulture, csSnippet, expectedFix);

            return VerifyCS.VerifyCodeFixAsync(
                 input,
                 VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(5, 34, 5, 34 + condition.Length),
                 fix);
        }

        [Theory]
        [InlineData("(_concurrent.Count) > 0", "Not _concurrent.IsEmpty")]
        [InlineData("_concurrent.Count > (0)", "Not _concurrent.IsEmpty")]
        [InlineData("(_concurrent.Count) > (0)", "Not _concurrent.IsEmpty")]
        // TODO: Reduce suggested fix to avoid special casing here.
        [InlineData("((_concurrent).Count) > (0)", "Not (_concurrent).IsEmpty")]
        public Task BasicTestFixOnParentheses(string condition, string expectedFix)
        {
            string input = string.Format(CultureInfo.InvariantCulture, vbSnippet, condition);
            string fix = string.Format(CultureInfo.InvariantCulture, vbSnippet, expectedFix);

            return VerifyVB.VerifyCodeFixAsync(
                 input,
                 VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(6, 20, 6, 20 + condition.Length),
                 fix);
        }

        [Theory]
        [InlineData("queue.Count > 0", true)]
        [InlineData("(queue.Count) > 0", true)]
        [InlineData("queue.Count > (0)", true)]
        [InlineData("queue.Count() == 0", false)]
        [InlineData("(queue.Count()) == 0", false)]
        [InlineData("queue.Count() == (0)", false)]
        [InlineData("queue.Count.Equals(0)", false)]
        [InlineData("0.Equals(queue.Count)", false)]
        [InlineData("queue.Count().Equals(0)", false)]
        [InlineData("0.Equals(queue.Count())", false)]
        public Task CSharpTestExpressionAsArgument(string expression, bool negate)
            => VerifyCS.VerifyCodeFixAsync(
    $@"using System;
using System.Linq;

public class Test
{{
    public static void TakeBool(bool isEmpty) {{ }}
    public static void M(System.Collections.Concurrent.ConcurrentQueue<int> queue) => TakeBool({expression});
}}",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithLocation(7, 96),
#pragma warning restore RS0030 // Do not used banned APIs
    $@"using System;
using System.Linq;

public class Test
{{
    public static void TakeBool(bool isEmpty) {{ }}
    public static void M(System.Collections.Concurrent.ConcurrentQueue<int> queue) => TakeBool({(negate ? "!" : "")}queue.IsEmpty);
}}");

        [Theory]
        [InlineData("(uint)_concurrent.Count > 0", true)]
        [InlineData("(uint)_concurrent.Count == 0", false)]
        [InlineData("((uint)_concurrent.Count).Equals(0)", false)]
        [InlineData("0.Equals((uint)_concurrent.Count)", false)]
        public Task CSharpTestCastExpression(string expression, bool negate)
            => VerifyCS.VerifyCodeFixAsync(
                string.Format(CultureInfo.InvariantCulture, csSnippet, expression),
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithLocation(5, 34),
#pragma warning restore RS0030 // Do not used banned APIs
                string.Format(CultureInfo.InvariantCulture, csSnippet, $"{(negate ? "!" : "")}_concurrent.IsEmpty"));

        [Theory]
        [InlineData("CType(_concurrent.Count, UInteger) > 0", true)]
        [InlineData("CType(_concurrent.Count, UInteger) = 0", false)]
        [InlineData("CType(_concurrent.Count, UInteger).Equals(0)", false)]
        [InlineData("0.Equals(CType(_concurrent.Count, UInteger))", false)]
        public Task BasicTestCastExpression(string expression, bool negate)
            => VerifyVB.VerifyCodeFixAsync(
                string.Format(CultureInfo.InvariantCulture, vbSnippet, expression),
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithLocation(6, 20),
#pragma warning restore RS0030 // Do not used banned APIs
                string.Format(CultureInfo.InvariantCulture, vbSnippet, $"{(negate ? "Not " : "")}_concurrent.IsEmpty"));

        [Theory]
        [InlineData("queue.Count > 0", true)]
        [InlineData("(queue.Count) > 0", true)]
        [InlineData("queue.Count > (0)", true)]
        [InlineData("queue.Count() = 0", false)]
        [InlineData("(queue.Count()) = 0", false)]
        [InlineData("queue.Count() = (0)", false)]
        [InlineData("queue.Count.Equals(0)", false)]
        [InlineData("0.Equals(queue.Count)", false)]
        [InlineData("queue.Count().Equals(0)", false)]
        [InlineData("0.Equals(queue.Count())", false)]
        public Task BasicTestExpressionAsArgument(string expression, bool negate)
            => VerifyVB.VerifyCodeFixAsync(
    $@"Imports System
Imports System.Linq

Public Class Test
    Public Shared Sub TakeBool(ByVal isEmpty As Boolean)
    End Sub

    Public Shared Sub M(ByVal queue As System.Collections.Concurrent.ConcurrentQueue(Of Integer))
        TakeBool({expression})
    End Sub
End Class",
#pragma warning disable RS0030 // Do not used banned APIs
                VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithLocation(9, 18),
#pragma warning restore RS0030 // Do not used banned APIs
    $@"Imports System
Imports System.Linq

Public Class Test
    Public Shared Sub TakeBool(ByVal isEmpty As Boolean)
    End Sub

    Public Shared Sub M(ByVal queue As System.Collections.Concurrent.ConcurrentQueue(Of Integer))
        TakeBool({(negate ? "Not " : "")}queue.IsEmpty)
    End Sub
End Class");

        [Theory(Skip = "Removed default support for all types but this scenario can be useful for .editorconfig")]
        [InlineData(false)]
        [InlineData(true)]
        public Task CSharpTestIsEmptyGetter_NoDiagnosis(bool useThis)
            => VerifyCS.VerifyAnalyzerAsync(
$@"class MyIntList
{{
    private System.Collections.Generic.List<int> _list;

    public bool IsEmpty {{
        get {{
            return {(useThis ? "this." : "")}Count == 0;
        }}
    }}
    public int Count => _list.Count;
}}");

        [Theory(Skip = "Removed default support for all types but this scenario can be useful for .editorconfig")]
        [InlineData(false)]
        [InlineData(true)]
        public Task BasicTestIsEmptyGetter_NoDiagnosis(bool useMe)
            => VerifyVB.VerifyAnalyzerAsync(
$@"Class MyIntList
    Private _list As System.Collections.Generic.List(Of Integer)
    Public ReadOnly Property IsEmpty As Boolean
        Get
            Return {(useMe ? "Me." : "")}Count = 0
        End Get
    End Property
    Public ReadOnly Property Count As Integer
        Get
            Return _list.Count
        End Get
    End Property
End Class");

        [Fact]
        public Task CSharpTestIsEmptyGetter_AsLambda_NoDiagnosis()
            => VerifyCS.VerifyAnalyzerAsync(
@"class MyIntList
{
    private System.Collections.Generic.List<int> _list;

    public bool IsEmpty => Count == 0;
    public int Count => _list.Count;
}");

        [Fact]
        public Task CSharpTestIsEmptyGetter_WithLinq_NoDiagnosis()
            => VerifyCS.VerifyAnalyzerAsync(
@"using System.Collections;
using System.Collections.Generic;
using System.Linq;

class MyIntList : IEnumerable<int>
{
    public bool IsEmpty => this.Count() == 0;

    public IEnumerator<int> GetEnumerator() => default;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}",
// Fallback on CA1827.
#pragma warning disable RS0030 // Do not used banned APIs
VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1827).WithLocation(7, 28).WithArguments("Count"));
#pragma warning restore RS0030 // Do not used banned APIs

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task BasicTestIsEmptyGetter_WithLinq_NoDiagnosis(bool useMe)
            => VerifyVB.VerifyAnalyzerAsync(
$@"Imports System.Collections
Imports System.Collections.Generic
Imports System.Linq

Class MyIntList
    Implements IEnumerable(Of Integer)
    Public ReadOnly Property IsEmpty As Boolean
        Get
            Return {(useMe ? "Me." : "")}Count() = 0
        End Get
    End Property

    Public Function GetEnumerator() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        Return Nothing
    End Function

    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return GetEnumerator()
    End Function
End Class",
// Fallback on CA1827.
#pragma warning disable RS0030 // Do not used banned APIs
VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1827).WithLocation(9, 20).WithArguments("Count"));
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public Task CSharpTestIsEmptyGetter_NoThis_Fixed()
            => VerifyCS.VerifyCodeFixAsync(
@"class MyStringIntDictionary
{
    private System.Collections.Concurrent.ConcurrentDictionary<string, int> _dictionary;

    public bool IsEmpty => _dictionary.Count == 0;
}",
#pragma warning disable RS0030 // Do not used banned APIs
VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithLocation(5, 28),
#pragma warning restore RS0030 // Do not used banned APIs
@"class MyStringIntDictionary
{
    private System.Collections.Concurrent.ConcurrentDictionary<string, int> _dictionary;

    public bool IsEmpty => _dictionary.IsEmpty;
}");

        [Fact]
        public Task BasicTestIsEmptyGetter_NoThis_Fixed()
            => VerifyVB.VerifyCodeFixAsync(
@"Class MyStringIntDictionary
    Private _dictionary As System.Collections.Concurrent.ConcurrentDictionary(Of String, Integer)
    Public ReadOnly Property IsEmpty As Boolean
        Get
            Return _dictionary.Count = 0
        End Get
    End Property
End Class",
#pragma warning disable RS0030 // Do not used banned APIs
VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithLocation(5, 20),
#pragma warning restore RS0030 // Do not used banned APIs
@"Class MyStringIntDictionary
    Private _dictionary As System.Collections.Concurrent.ConcurrentDictionary(Of String, Integer)
    Public ReadOnly Property IsEmpty As Boolean
        Get
            Return _dictionary.IsEmpty
        End Get
    End Property
End Class");

        [Fact]
        public Task CSharpTestWhitespaceTrivia()
            => VerifyCS.VerifyCodeFixAsync(
$@"class C
{{
    private System.Collections.Concurrent.ConcurrentDictionary<string, int> _dictionary;
    public int GetLength() => _dictionary.Count == 0 
        ? 0 :
        _dictionary.Count;
}}",
#pragma warning disable RS0030 // Do not used banned APIs
VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithLocation(4, 31),
#pragma warning restore RS0030 // Do not used banned APIs
@"class C
{
    private System.Collections.Concurrent.ConcurrentDictionary<string, int> _dictionary;
    public int GetLength() => _dictionary.IsEmpty
        ? 0 :
        _dictionary.Count;
}");

        [Theory]
        [InlineData("System.ReadOnlyMemory")]
        [InlineData("System.ReadOnlySpan")]
        [InlineData("System.Memory")]
        [InlineData("System.Span")]
        public Task CSharpTest_DisallowedTypesForCA1836_NoDiagnosis(string type)
            => VerifyCS.VerifyAnalyzerAsync(
$@"class C
{{
    private {type}<T> GetData_Generic<T>() => default;
    private {type}<char> GetData_NonGeneric() => default;

    private bool Test_Generic() => GetData_Generic<byte>().Length == 0;
    private bool Test_NonGeneric() => GetData_NonGeneric().Length == 0;
}}");
    }

    public abstract class PreferIsEmptyOverCountTestsBase
        : DoNotUseCountWhenAnyCanBeUsedTestsBase
    {
        protected PreferIsEmptyOverCountTestsBase(TestsSourceCodeProvider sourceProvider, VerifierBase verifier)
            : base(sourceProvider, verifier) { }

        [Theory]
        [ClassData(typeof(BinaryExpressionTestData))]
        public Task PropertyOnBinaryOperation(bool noDiagnosis, int literal, BinaryOperatorKind @operator, bool isRightSideExpression, bool shouldNegate)
        {
            string testSource = isRightSideExpression ?
                SourceProvider.GetTargetPropertyBinaryExpressionCode(literal, @operator, SourceProvider.MemberName) :
                SourceProvider.GetTargetPropertyBinaryExpressionCode(@operator, literal, SourceProvider.MemberName);

            testSource = SourceProvider.GetCodeWithExpression(testSource);

            if (noDiagnosis)
            {
                return VerifyAsync(testSource, extensionsSource: null);
            }
            else
            {
                string fixedSource = SourceProvider.GetCodeWithExpression(SourceProvider.GetFixedIsEmptyPropertyCode(shouldNegate));
                return VerifyAsync(methodName: null, testSource, fixedSource, extensionsSource: null);
            }
        }

        [Fact]
        public Task PropertyEqualsZero_Fixed()
            => VerifyAsync(
                methodName: null,
                testSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetEqualsTargetPropertyInvocationCode(0, SourceProvider.MemberName)),
                fixedSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetFixedIsEmptyPropertyCode(negate: false)),
                extensionsSource: null);

        [Fact]
        public Task ZeroEqualsProperty_Fixed()
            => VerifyAsync(
                methodName: null,
                testSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetTargetPropertyEqualsInvocationCode(0, SourceProvider.MemberName)),
                fixedSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetFixedIsEmptyPropertyCode(negate: false)),
                extensionsSource: null);
    }

    public abstract class PreferIsEmptyOverCountLinqTestsBase
        : DoNotUseCountWhenAnyCanBeUsedTestsBase
    {
        public static readonly IEnumerable<object[]> DiagnosisOnlyTestData = new BinaryExpressionTestData()
            .Where(x => (bool)x[0] == false)
            .Select(x => new object[] { x[1], x[2], x[3], x[4] });

        protected PreferIsEmptyOverCountLinqTestsBase(TestsSourceCodeProvider sourceProvider, VerifierBase verifier)
            : base(sourceProvider, verifier) { }

        /// <summary>
        /// Scenarios that are not diagnosed with CA1836 should fallback in CA1829 and those are covered in 
        /// <see cref="UsePropertyInsteadOfCountMethodWhenAvailableOverlapTests.PropertyOnBinaryOperation(int, BinaryOperatorKind, bool)"/>
        /// </summary>
        [Theory]
        [MemberData(nameof(DiagnosisOnlyTestData))]
        public Task LinqMethodOnBinaryOperation(int literal, BinaryOperatorKind @operator, bool isRightSideExpression, bool shouldNegate)
        {
            string testSource = SourceProvider.GetCodeWithExpression(
                isRightSideExpression ?
                SourceProvider.GetTargetExpressionBinaryExpressionCode(literal, @operator, withPredicate: false, "Count") :
                SourceProvider.GetTargetExpressionBinaryExpressionCode(@operator, literal, withPredicate: false, "Count"),
                additionalNamspaces: SourceProvider.ExtensionsNamespace);

            string fixedSource = SourceProvider.GetCodeWithExpression(
                SourceProvider.GetFixedIsEmptyPropertyCode(shouldNegate),
                additionalNamspaces: SourceProvider.ExtensionsNamespace);

            return VerifyAsync(methodName: null, testSource, fixedSource, extensionsSource: null);
        }

        [Fact]
        public Task LinqCountEqualsZero_Fixed()
            => VerifyAsync(
                methodName: null,
                testSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetEqualsTargetExpressionInvocationCode(0, withPredicate: false, "Count"),
                    additionalNamspaces: SourceProvider.ExtensionsNamespace),
                fixedSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetFixedIsEmptyPropertyCode(negate: false),
                    additionalNamspaces: SourceProvider.ExtensionsNamespace),
                extensionsSource: null);

        [Fact]
        public Task ZeroEqualsLinqCount_Fixed()
            => VerifyAsync(
                methodName: null,
                testSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetTargetExpressionEqualsInvocationCode(0, withPredicate: false, "Count"),
                    additionalNamspaces: SourceProvider.ExtensionsNamespace),
                fixedSource: SourceProvider.GetCodeWithExpression(
                    SourceProvider.GetFixedIsEmptyPropertyCode(negate: false),
                    additionalNamspaces: SourceProvider.ExtensionsNamespace),
                extensionsSource: null);

    }

    public class CSharpPreferIsEmptyOverCountTests_Concurrent
        : PreferIsEmptyOverCountTestsBase
    {
        public CSharpPreferIsEmptyOverCountTests_Concurrent()
            : base(
                  new CSharpTestsSourceCodeProvider(
                      "Count",
                      "global::System.Collections.Concurrent.ConcurrentBag<int>",
                      extensionsNamespace: null, extensionsClass: null, isAsync: false),
                  new CSharpVerifier<UseCountProperlyAnalyzer, CSharpPreferIsEmptyOverCountFixer>(UseCountProperlyAnalyzer.CA1836))
        { }
    }

    public class BasicPreferIsEmptyOverCountTests_Concurrent
        : PreferIsEmptyOverCountTestsBase
    {
        public BasicPreferIsEmptyOverCountTests_Concurrent()
            : base(
                  new BasicTestsSourceCodeProvider(
                      "Count",
                      "Global.System.Collections.Concurrent.ConcurrentBag(Of Integer)",
                      extensionsNamespace: null, extensionsClass: null, isAsync: false),
                  new BasicVerifier<UseCountProperlyAnalyzer, BasicPreferIsEmptyOverCountFixer>(UseCountProperlyAnalyzer.CA1836))
        { }
    }

    public class CSharpPreferIsEmptyOverCountLinqTests_Concurrent
        : PreferIsEmptyOverCountLinqTestsBase
    {
        public CSharpPreferIsEmptyOverCountLinqTests_Concurrent()
            : base(
                  new CSharpTestsSourceCodeProvider(
                      "Count",
                      "global::System.Collections.Concurrent.ConcurrentBag<int>",
                      extensionsNamespace: "System.Linq", extensionsClass: "Enumerable",
                      isAsync: false),
                  new CSharpVerifier<UseCountProperlyAnalyzer, CSharpPreferIsEmptyOverCountFixer>(UseCountProperlyAnalyzer.CA1836))
        { }
    }
}
