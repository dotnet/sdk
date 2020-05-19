// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
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
using System;
using System.Linq;

public class Test
{{
    public int Count {{ get; }}
    public bool IsEmpty {{ get; }}

    public bool DummyProperty => {0};
}}
";

        private const string vbSnippet = @"
Imports System
Imports System.Collections.Concurrent

Public Class Test
    Public ReadOnly Property Count As Integer
    Public ReadOnly Property IsEmpty As Boolean

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

//        [Fact]
//        public Task QuickTest()
//        {
//            return VerifyCS.VerifyCodeFixAsync(
//@"using System;

//public class Test
//{
//    private int Count {get;}
//    public int Length {get;}
//    //public bool IsEmpty;
//    public bool Foo => Count > 0;
//}",
//                    VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836),
//@"using System;

//public class Test
//{
//    private static int Count = 0;
//    public bool Foo => !GetData().IsEmpty;
//}");
//        }

        [Theory]
        [InlineData("(Count) > 0")]
        [InlineData("Count > (0)")]
        [InlineData("(Count) > (0)")]
        [InlineData("(this.Count) > 0")]
        [InlineData("this.Count > (0)")]
        [InlineData("(this.Count) > (0)")]
        [InlineData("((this).Count) > (0)")]
        public Task CSharpTestFixOnParentheses(string condition)
        {
            string input = string.Format(CultureInfo.InvariantCulture, csSnippet, condition);
            string fix = string.Format(CultureInfo.InvariantCulture, csSnippet, $"!{IsEmpty}");

            return VerifyCS.VerifyCodeFixAsync(
                 input,
                 VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(10, 34, 10, 34 + condition.Length),
                 fix);
        }

        [Theory]
        [InlineData("(Count) > 0", "Not IsEmpty")]
        [InlineData("Count > (0)", "Not IsEmpty")]
        [InlineData("(Count) > (0)", "Not IsEmpty")]
        [InlineData("(Me.Count) > 0", "Not IsEmpty")]
        [InlineData("Me.Count > (0)", "Not IsEmpty")]
        [InlineData("(Me.Count) > (0)", "Not IsEmpty")]
        // TODO: Reduce suggested fix to avoid special casing here.
        [InlineData("((Me).Count) > (0)", "Not (Me).IsEmpty")]
        public Task BasicTestFixOnParentheses(string condition, string replacement)
        {
            string input = string.Format(CultureInfo.InvariantCulture, vbSnippet, condition);
            string fix = string.Format(CultureInfo.InvariantCulture, vbSnippet, replacement);

            return VerifyVB.VerifyCodeFixAsync(
                 input,
                 VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(11, 20, 11, 20 + condition.Length),
                 fix);
        }

        [Fact]
        public async Task TestOperations()
        {
            // The operators that we want to test.
            foreach (OperatorKind @operator in _operators)
            {
                // Whether Count {operator} {literal} OR {literal} {operator} Count.
                foreach (bool isRightSideExpression in new bool[] { false, true })
                {
                    // The literals or constants we want to test against.
                    foreach (int literal in new int[] { 0, 1 })
                    {
                        bool resultWhenExpressionEqualsZero = default;
                        bool noDiagnosis = false;
                        // Simulate a value that Count might have in order to determine what's the expected behavior in such case.
                        foreach (int expressionValue in new int[] { 0, 1, 2 })
                        {
                            int rightSide = isRightSideExpression ? expressionValue : literal;
                            int leftSide = isRightSideExpression ? literal : expressionValue;

                            if (expressionValue == 0)
                            {
                                resultWhenExpressionEqualsZero = @operator.Operation(leftSide, rightSide);
                            }
                            else
                            {
                                // If the evaluation for Count {operand} 0 when Count = 1 has the same result as the evaluation 
                                // when either Count = 1 or Count = 2 then the case does not apply and therefore no diagnosis should be given. 
                                if (resultWhenExpressionEqualsZero == @operator.Operation(leftSide, rightSide))
                                {
                                    noDiagnosis = true;
                                    break;
                                }
                            }
                        }

                        var conditions = GetConditionsAsString(literal, isRightSideExpression, @operator);

                        if (noDiagnosis)
                        {
                            // Test that we don't get a diagnosis from this combination.
                            await CSharpTestNoDiagnosis(conditions.Item1);
                            await BasicTestNoDiagnosis(conditions.Item2);
                        }
                        else if (resultWhenExpressionEqualsZero)
                        {
                            // Test replacement of the expression for IsEmpty.
                            await CSharpTestCodeFix(conditions.Item1, IsEmpty);
                            await BasicTestCodeFix(conditions.Item2, IsEmpty);

                        }
                        else
                        {
                            // Test replacement of the expression for !IsEmpty.
                            await CSharpTestCodeFix(conditions.Item1, $"!{IsEmpty}");
                            await BasicTestCodeFix(conditions.Item2, $"Not {IsEmpty}");
                        }
                    }
                }
            }
        }

        private Task CSharpTestNoDiagnosis(string condition)
        {
            string csInput = string.Format(CultureInfo.InvariantCulture, csSnippet, condition);
            return VerifyCS.VerifyAnalyzerAsync(csInput);
        }

        private Task BasicTestNoDiagnosis(string condition)
        {
            string vbInput = string.Format(CultureInfo.InvariantCulture, vbSnippet, condition);
            return VerifyVB.VerifyAnalyzerAsync(vbInput);
        }

        private Task CSharpTestCodeFix(string inputCondition, string fixCondition)
        {
            string csInput = string.Format(CultureInfo.InvariantCulture, csSnippet, inputCondition);
            string csFix = string.Format(CultureInfo.InvariantCulture, csSnippet, fixCondition);

            return VerifyCS.VerifyCodeFixAsync(
                csInput,
                VerifyCS.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(10, 34, 10, 34 + inputCondition.Length),
                csFix);
        }

        private Task BasicTestCodeFix(string inputCondition, string fixCondition)
        {
            string vbInput = string.Format(CultureInfo.InvariantCulture, vbSnippet, inputCondition);
            string vbFix = string.Format(CultureInfo.InvariantCulture, vbSnippet, fixCondition);

            return VerifyVB.VerifyCodeFixAsync(
                vbInput,
                VerifyVB.Diagnostic(UseCountProperlyAnalyzer.s_rule_CA1836).WithSpan(11, 20, 11, 20 + inputCondition.Length),
                vbFix);
        }

        private (string, string) GetConditionsAsString(int literal, bool isRightSideExpression, OperatorKind @operator)
        {
            string csharpCondition, basicCondition;

            if (isRightSideExpression)
            {
                csharpCondition = $"{literal} {@operator.CSharpOperator} {Count}";
                basicCondition = $"{literal} {@operator.BasicOperator} {Count}";
            }
            else
            {
                csharpCondition = $"{Count} {@operator.CSharpOperator} {literal}";
                basicCondition = $"{Count} {@operator.BasicOperator} {literal}";
            }

            return (csharpCondition, basicCondition);
        }

#pragma warning disable CA1815 // Override equals and operator equals on value types
        private struct OperatorKind
        {
            public string CSharpOperator { get; }
            public string BasicOperator { get; }

            public Func<int, int, bool> Operation { get; }

            public OperatorKind(string csharpOperator, string basicOperator, Func<int, int, bool> operation)
            {
                CSharpOperator = csharpOperator;
                BasicOperator = basicOperator;
                Operation = operation;
            }
        }

        private static readonly List<OperatorKind> _operators = new List<OperatorKind>
        {
            new OperatorKind("==",  "=",    (a,b) => a == b ),
            new OperatorKind("!=",  "<>",   (a,b) => a != b ),
            new OperatorKind(">",   ">",    (a,b) => a > b ),
            new OperatorKind(">=",  ">=",   (a,b) => a >= b ),
            new OperatorKind("<",   "<",    (a,b) => a < b ),
            new OperatorKind("<=",  "<=",   (a,b) => a <= b ),
        };
    }
}
