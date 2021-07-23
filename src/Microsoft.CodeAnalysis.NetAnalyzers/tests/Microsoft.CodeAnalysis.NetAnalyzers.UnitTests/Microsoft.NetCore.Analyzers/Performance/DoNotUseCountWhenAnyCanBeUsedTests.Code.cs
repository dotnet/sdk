// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public partial class DoNotUseCountWhenAnyCanBeUsedTestsBase
    {
        public abstract class TestsSourceCodeProvider
        {
            protected TestsSourceCodeProvider(
                string operationName,
                string targetType,
                string extensionsNamespace,
                string extensionsClass,
                bool isAsync,
                string asyncKeyword,
                string awaitKeyword,
                string commentPrefix)
            {
                TargetType = targetType;
                ExtensionsNamespace = extensionsNamespace;
                ExtensionsClass = extensionsClass;
                IsAsync = isAsync;
                AsyncKeyword = asyncKeyword;
                AwaitKeyword = awaitKeyword;
                CommentPrefix = commentPrefix;
                MethodSuffix = IsAsync ? "Async" : string.Empty;
                MemberName = operationName + MethodSuffix;
            }

            public string MemberName { get; }
            public string MethodSuffix { get; }
            public string AsyncKeyword { get; }
            public string AwaitKeyword { get; }
            public string CommentPrefix { get; }
            public string TargetType { get; }
            public string TestNamespace { get; } = "Test";
            public string TestExtensionsClass { get; } = "TestExtensions";
            public string ExtensionsNamespace { get; }
            public string ExtensionsClass { get; }
            public bool IsAsync { get; }

            public string GetTargetCode(string methodName)
                => $"{(IsAsync ? $"{AwaitKeyword} " : string.Empty)}GetData().{methodName}";

            public abstract string GetCodeWithExpression(string expression, params string[] additionalNamspaces);

            internal string WithDiagnostic(string code)
                => code;

            internal string GetFixedExpressionCode(bool withPredicate, bool negate)
                => $@"{GetLogicalNotText(negate)}{GetTargetExpressionCode(withPredicate, "Any" + this.MethodSuffix)}";

            internal string GetFixedIsEmptyPropertyCode(bool negate)
                => $@"{GetLogicalNotText(negate)}{GetTargetPropertyCode("IsEmpty")}";

            internal string GetTargetExpressionBinaryExpressionCode(int value, BinaryOperatorKind @operator, bool withPredicate, string methodName)
                => $@"{value} {GetOperatorCode(@operator)} {GetTargetExpressionCode(withPredicate, methodName)}";

            internal string GetTargetExpressionBinaryExpressionCode(BinaryOperatorKind @operator, int value, bool withPredicate, string methodName)
                => $@"{GetTargetExpressionCode(withPredicate, methodName)} {GetOperatorCode(@operator)} {value}";

            public string GetTargetExpressionEqualsInvocationCode(int value, bool withPredicate, string methodName)
                => $@"{(IsAsync ? "(" : string.Empty)}{GetTargetExpressionCode(withPredicate, methodName)}{(IsAsync ? ")" : string.Empty)}.Equals({value})";

            internal string GetEqualsTargetExpressionInvocationCode(int value, bool withPredicate, string methodName)
                => $@"{value}.Equals({GetTargetExpressionCode(withPredicate, methodName)})";

            public string GetTargetExpressionCode(bool withPredicate, string methodName)
                => $@"{GetTargetCode(methodName)}({(withPredicate ? GetPredicateCode() : string.Empty)})";

            internal string GetTargetPropertyBinaryExpressionCode(int value, BinaryOperatorKind @operator, string propertyName)
                => $@"{value} {GetOperatorCode(@operator)} {GetTargetPropertyCode(propertyName)}";

            internal string GetTargetPropertyBinaryExpressionCode(BinaryOperatorKind @operator, int value, string propertyName)
                => $@"{GetTargetPropertyCode(propertyName)} {GetOperatorCode(@operator)} {value}";

            public string GetTargetPropertyEqualsInvocationCode(int value, string propertyName)
                => $@"{GetTargetPropertyCode(propertyName)}.Equals({value})";

            internal string GetEqualsTargetPropertyInvocationCode(int value, string propertyName)
                => $@"{value}.Equals({GetTargetPropertyCode(propertyName)})";

            public string GetTargetPropertyCode(string propertyName)
                => $@"{GetTargetCode(propertyName)}";

            public abstract string GetSymbolInvocationCode(string methodName, params string[] arguments);
            public abstract string GetPredicateCode();
            public abstract string GetExtensionsCode(string namespaceName, string className);
            public abstract string GetOperatorCode(BinaryOperatorKind binaryOperatorKind);
            internal abstract object GetLogicalNotText(bool negate);
        }

        public sealed class CSharpTestsSourceCodeProvider : TestsSourceCodeProvider
        {
            public CSharpTestsSourceCodeProvider(
                string operationName,
                string targetType,
                string extensionsNamespace,
                string extensionsClass,
                bool isAsync)
                : base(
                    operationName,
                    targetType,
                    extensionsNamespace,
                    extensionsClass,
                    isAsync,
                    "async",
                    "await",
                    "//")
            {
            }

            public override string GetCodeWithExpression(string expression, params string[] additionalNamspaces)
            {
                var builder = new StringBuilder()
                    .AppendLine("using System;");

                foreach (var aditionalNamespace in additionalNamspaces)
                {
                    builder
                        .Append("using ")
                        .Append(aditionalNamespace)
                        .Append(';')
                        .AppendLine();
                }

                builder
                    .Append(@"namespace ")
                    .Append(TestNamespace)
                    .Append(@"
{
    class C
    {
        ")
                    .Append(TargetType)
                    .Append(@" GetData() => default;
        ");

                if (IsAsync)
                {
                    builder
                        .Append(AsyncKeyword)
                        .Append(' ');
                }

                return builder
                    .Append(@"void M()
        {
            var b = ")
                    .Append(expression)
                    .AppendLine(@";
        }
    }
}")
                    .ToString();
            }

            public override string GetExtensionsCode(string namespaceName, string className)
            {
                string targetType;
                string targetTypeOfSource;
                string predicate;
                string boolReturnType;
                string intReturnType;
                string boolReturnValue;
                string intReturnValue;

                if (this.IsAsync)
                {
                    targetType = "global::System.Linq.IQueryable";
                    targetTypeOfSource = "global::System.Linq.IQueryable<TSource>";
                    predicate = "global::System.Linq.Expressions.Expression<global::System.Func<TSource, bool>>";
                    boolReturnType = "global::System.Threading.Tasks.Task<bool>";
                    intReturnType = "global::System.Threading.Tasks.Task<int>";
                    boolReturnValue = "global::System.Threading.Tasks.Task.FromResult<bool>(default)";
                    intReturnValue = "global::System.Threading.Tasks.Task.FromResult<int>(default)";
                }
                else
                {
                    targetType = "global::System.Collections.IEnumerable";
                    targetTypeOfSource = "global::System.Collections.Generic.IEnumerable<TSource>";
                    predicate = "global::System.Func<TSource, bool>";
                    boolReturnType = "bool";
                    intReturnType = "int";
                    boolReturnValue = "default";
                    intReturnValue = "default";
                }

                return $@"namespace {namespaceName}
{{
    public static class {className}
    {{
        public static {boolReturnType} Any{this.MethodSuffix}(this {targetType} q) => {boolReturnValue};
        public static {boolReturnType} Any{this.MethodSuffix}<TSource>(this {targetTypeOfSource} q, {predicate} predicate) => {boolReturnValue};
        public static {intReturnType} {this.MemberName}(this {targetType} q) => {intReturnValue};
        public static {intReturnType} {this.MemberName}<TSource>(this {targetTypeOfSource} q, {predicate} predicate) => {intReturnValue};
        public static {intReturnType} Sum{this.MethodSuffix}(this {targetType} q) => {intReturnValue};
    }}
}}
";
            }

            public override string GetOperatorCode(BinaryOperatorKind binaryOperatorKind)
            {
                return binaryOperatorKind switch
                {
                    BinaryOperatorKind.Add => "+",
                    BinaryOperatorKind.Equals => "==",
                    BinaryOperatorKind.GreaterThan => ">",
                    BinaryOperatorKind.GreaterThanOrEqual => ">=",
                    BinaryOperatorKind.LessThan => "<",
                    BinaryOperatorKind.LessThanOrEqual => "<=",
                    BinaryOperatorKind.NotEquals => "!=",
                    _ => throw new ArgumentOutOfRangeException(nameof(binaryOperatorKind), binaryOperatorKind, $"Invalid value: {binaryOperatorKind}"),
                };
            }

            public override string GetPredicateCode() => "_ => true";

            public override string GetSymbolInvocationCode(string methodName, params string[] arguments)
            {
                throw new NotImplementedException();
            }

            internal override object GetLogicalNotText(bool negate) => negate ? "!" : string.Empty;
        }

        public sealed class BasicTestsSourceCodeProvider : TestsSourceCodeProvider
        {
            public BasicTestsSourceCodeProvider(
                string operationName,
                string targetType,
                string extensionsNamespace,
                string extensionsClass,
                bool isAsync)
                : base(
                    operationName,
                    targetType,
                    extensionsNamespace,
                    extensionsClass,
                    isAsync,
                    "Async",
                    "Await",
                    "'")
            {
            }

            public override string GetCodeWithExpression(string expression, params string[] additionalNamspaces)
            {
                var builder = new StringBuilder()
                    .AppendLine("Imports System");

                foreach (var aditionalNamespace in additionalNamspaces)
                {
                    builder
                        .Append("Imports ")
                        .Append(aditionalNamespace)
                        .AppendLine();
                }

                builder
                    .Append(@"Namespace Global.")
                    .Append(TestNamespace)
                    .Append(@"
    Class C
        Function GetData() As ")
                    .Append(TargetType)
                    .Append(@"
            Return Nothing
        End Function
        ");

                if (IsAsync)
                {
                    builder
                        .Append(AsyncKeyword)
                        .Append(' ');
                }

                return builder
                    .Append(@"Sub M()
            Dim b = ")
                    .Append(expression)
                    .AppendLine(@"
        End Sub
    End Class
End Namespace")
                    .ToString();
            }

            public override string GetExtensionsCode(string namespaceName, string className)
            {
                string targetType;
                string targetTypeOfSource;
                string predicate;
                string boolReturnType;
                string intReturnType;
                string boolReturnValue;
                string intReturnValue;

                if (this.IsAsync)
                {
                    targetType = "Global.System.Linq.IQueryable";
                    targetTypeOfSource = "Global.System.Linq.IQueryable(Of TSource)";
                    predicate = "Global.System.Linq.Expressions.Expression(Of Global.System.Func(Of TSource, Boolean))";
                    boolReturnType = "Global.System.Threading.Tasks.Task(Of Boolean)";
                    intReturnType = "Global.System.Threading.Tasks.Task(Of Integer)";
                    boolReturnValue = "Global.System.Threading.Tasks.Task.FromResult(Of Boolean)(Nothing)";
                    intReturnValue = "Global.System.Threading.Tasks.Task.FromResult(Of Integer)(Nothing)";
                }
                else
                {
                    targetType = "Global.System.Collections.IEnumerable";
                    targetTypeOfSource = "Global.System.Collections.Generic.IEnumerable(Of TSource)";
                    predicate = "Global.System.Func(Of TSource, Boolean)";
                    boolReturnType = "Boolean";
                    intReturnType = "Integer";
                    boolReturnValue = "Nothing";
                    intReturnValue = "Nothing";
                }

                return $@"Namespace Global.{namespaceName}
    <System.Runtime.CompilerServices.Extension>
    Public Module {className}
        <System.Runtime.CompilerServices.Extension>
        Public Function Any{this.MethodSuffix}(q As {targetType}) As {boolReturnType}
            Return {boolReturnValue}
        End Function
        <System.Runtime.CompilerServices.Extension>
        Public Function Any{this.MethodSuffix}(Of TSource)(q As {targetTypeOfSource}, predicate As {predicate}) As {boolReturnType}
            Return {boolReturnValue}
        End Function
        <System.Runtime.CompilerServices.Extension>
        Public Function {this.MemberName}(q As {targetType}) As {intReturnType}
            Return {intReturnValue}
        End Function
        <System.Runtime.CompilerServices.Extension>
        Public Function {this.MemberName}(Of TSource)(q As {targetTypeOfSource}, predicate As {predicate}) As {intReturnType}
            Return {intReturnValue}
        End Function
        <System.Runtime.CompilerServices.Extension>
        Public Function Sum{this.MethodSuffix}(q As {targetType}) As {intReturnType}
            Return {intReturnValue}
        End Function
    End Module
End Namespace
";
            }

            public override string GetOperatorCode(BinaryOperatorKind binaryOperatorKind)
            {
                return binaryOperatorKind switch
                {
                    BinaryOperatorKind.Add => "+",
                    BinaryOperatorKind.Equals => "=",
                    BinaryOperatorKind.GreaterThan => ">",
                    BinaryOperatorKind.GreaterThanOrEqual => ">=",
                    BinaryOperatorKind.LessThan => "<",
                    BinaryOperatorKind.LessThanOrEqual => "<=",
                    BinaryOperatorKind.NotEquals => "<>",
                    _ => throw new ArgumentOutOfRangeException(nameof(binaryOperatorKind), binaryOperatorKind, $"Invalid value: {binaryOperatorKind}"),
                };
            }

            public override string GetPredicateCode() => "Function(x) True";

            public override string GetSymbolInvocationCode(string methodName, params string[] arguments)
            {
                throw new NotImplementedException();
            }

            internal override object GetLogicalNotText(bool negate) => negate ? "Not " : string.Empty;
        }

        protected abstract class VerifierBase
        {
            protected VerifierBase(string diagnosticId)
            {
                DiagnosticId = diagnosticId;
            }

            public string DiagnosticId { get; }

            internal abstract Task VerifyAsync(string[] testSources);
            internal abstract Task VerifyAsync(string methodName, string[] testSources, string[] fixedSources, int line, int column);

            public static int GetNumberOfLines(string source)
            {
                var numberOfLines = 0;
                var index = -Environment.NewLine.Length;
                while ((index = source.IndexOf(Environment.NewLine, index + Environment.NewLine.Length, StringComparison.Ordinal)) >= 0) numberOfLines++;
                return numberOfLines;
            }
        }

        protected sealed class CSharpVerifier<TAnalyzer, TCodeFix>
            : VerifierBase
            where TAnalyzer : DiagnosticAnalyzer, new()
            where TCodeFix : CodeFixProvider, new()
        {
            public CSharpVerifier(string diagnosticId)
                : base(diagnosticId)
            {
            }

            internal override Task VerifyAsync(string[] testSources)
            {
                var test = new Test.Utilities.CSharpCodeFixVerifier<TAnalyzer, TCodeFix>.Test();

                foreach (var testSource in testSources)
                {
                    if (!string.IsNullOrEmpty(testSource))
                    {
                        test.TestState.Sources.Add(testSource);
                    }
                }

                return test.RunAsync();
            }

            internal override Task VerifyAsync(string methodName, string[] testSources, string[] fixedSources, int line, int column)
            {
                var test = new Test.Utilities.CSharpCodeFixVerifier<TAnalyzer, TCodeFix>.Test();

                foreach (var testSource in testSources)
                {
                    if (!string.IsNullOrEmpty(testSource))
                    {
                        test.TestState.Sources.Add(testSource);
                    }
                }

                test.TestState.ExpectedDiagnostics.Add(
#pragma warning disable RS0030 // Do not used banned APIs
                    Test.Utilities.CSharpCodeFixVerifier<TAnalyzer, TCodeFix>.Diagnostic(this.DiagnosticId)
                        .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                        .WithArguments(methodName));

                foreach (var fixedSource in fixedSources)
                {
                    if (!string.IsNullOrEmpty(fixedSource))
                    {
                        test.FixedState.Sources.Add(fixedSource);
                    }
                }

                return test.RunAsync();
            }
        }

        protected sealed class BasicVerifier<TAnalyzer, TCodeFix>
            : VerifierBase
            where TAnalyzer : DiagnosticAnalyzer, new()
            where TCodeFix : CodeFixProvider, new()
        {
            public BasicVerifier(string diagnosticId)
                : base(diagnosticId)
            {
            }

            internal override Task VerifyAsync(string[] testSources)
            {
                var test = new Test.Utilities.VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>.Test();

                foreach (var testSource in testSources)
                {
                    if (!string.IsNullOrEmpty(testSource))
                    {
                        test.TestState.Sources.Add(testSource);
                    }
                }

                return test.RunAsync();
            }

            internal override Task VerifyAsync(string methodName, string[] testSources, string[] fixedSources, int line, int column)
            {
                var test = new Test.Utilities.VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>.Test();

                foreach (var testSource in testSources)
                {
                    if (!string.IsNullOrEmpty(testSource))
                    {
                        test.TestState.Sources.Add(testSource);
                    }
                }

                test.TestState.ExpectedDiagnostics.Add(
#pragma warning disable RS0030 // Do not used banned APIs
                    Test.Utilities.VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>.Diagnostic(this.DiagnosticId)
                        .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                        .WithArguments(methodName));

                foreach (var fixedSource in fixedSources)
                {
                    if (!string.IsNullOrEmpty(fixedSource))
                    {
                        test.FixedState.Sources.Add(fixedSource);
                    }
                }

                return test.RunAsync();
            }
        }
    }
}
