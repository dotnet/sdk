// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using RCISCAnalyzer = RecommendCaseInsensitiveStringComparisonAnalyzer;

    /// <summary>
    /// CA1862: Prefer the StringComparison method overloads to perform case-insensitive string comparisons.
    /// </summary>
    public abstract class RecommendCaseInsensitiveStringComparisonFixer : CodeFixProvider
    {
        protected const string StringTypeName = "String";

        protected abstract IEnumerable<SyntaxNode> GetNewArgumentsForInvocation(SyntaxGenerator generator,
            string caseChangingApproachValue, IInvocationOperation mainInvocationOperation, INamedTypeSymbol stringComparisonType,
            string? leftOffendingMethod, string? rightOffendingMethod, out SyntaxNode? mainInvocationInstance);

        protected abstract IEnumerable<SyntaxNode> GetNewArgumentsForBinary(SyntaxGenerator generator, SyntaxNode rightNode, SyntaxNode typeMemberAccess);

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(RCISCAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            CancellationToken ct = context.CancellationToken;

            Document doc = context.Document;

            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(ct).ConfigureAwait(false);

            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not SyntaxNode node)
            {
                return;
            }

            SemanticModel model = await doc.GetRequiredSemanticModelAsync(ct).ConfigureAwait(false);

            if (model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison)
                is not INamedTypeSymbol stringComparisonType)
            {
                return;
            }

            IOperation? operation = model.GetOperation(node, ct);
            if (operation == null)
            {
                return;
            }

            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(doc);

            ImmutableDictionary<string, string?> dict = context.Diagnostics[0].Properties;

            // The dictionary should contain the keys for both left and right offending methods,
            // and at least one of them should not be null.
            if (!dict.TryGetValue(RCISCAnalyzer.LeftOffendingMethodName, out string? leftOffendingMethod) ||
                !dict.TryGetValue(RCISCAnalyzer.RightOffendingMethodName, out string? rightOffendingMethod) ||
                (leftOffendingMethod == null && rightOffendingMethod == null))
            {
                return;
            }

            bool leftIsToLowerOrToUpper = leftOffendingMethod is RCISCAnalyzer.StringToLowerMethodName or RCISCAnalyzer.StringToUpperMethodName;
            bool rightIsToLowerOrToUpper = rightOffendingMethod is RCISCAnalyzer.StringToLowerMethodName or RCISCAnalyzer.StringToUpperMethodName;

            bool leftIsToLowerInvariantOrToUpperInvariant = leftOffendingMethod is RCISCAnalyzer.StringToLowerInvariantMethodName or RCISCAnalyzer.StringToUpperInvariantMethodName;
            bool rightIsToLowerInvariantOrToUpperInvariant = rightOffendingMethod is RCISCAnalyzer.StringToLowerInvariantMethodName or RCISCAnalyzer.StringToUpperInvariantMethodName;

            // If the cultures of the two strings are incompatible, do not offer a fix
            if ((leftIsToLowerOrToUpper && rightIsToLowerInvariantOrToUpperInvariant) ||
                (rightIsToLowerOrToUpper && leftIsToLowerInvariantOrToUpperInvariant))
            {
                return;
            }

            string? caseChangingApproachValue = null;
            if (leftIsToLowerOrToUpper || rightIsToLowerOrToUpper)
            {
                caseChangingApproachValue = RCISCAnalyzer.StringComparisonCurrentCultureIgnoreCaseName;
            }
            else if (leftIsToLowerInvariantOrToUpperInvariant || rightIsToLowerInvariantOrToUpperInvariant)
            {
                caseChangingApproachValue = RCISCAnalyzer.StringComparisonInvariantCultureIgnoreCaseName;
            }

            Debug.Assert(caseChangingApproachValue != null, "Unexpected offending methods");

            if (operation is IInvocationOperation invocation)
            {
                if (invocation.TargetMethod.Name is RCISCAnalyzer.StringCompareToMethodName)
                {
                    // Never offer a fix for CompareTo
                    return;
                }

                Task<Document> createChangedDocument(CancellationToken _) => FixInvocationAsync(generator, doc, root,
                invocation, stringComparisonType, invocation.TargetMethod.Name,
                caseChangingApproachValue!, leftOffendingMethod, rightOffendingMethod);

                string title = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    MicrosoftNetCoreAnalyzersResources.RecommendCaseInsensitiveStringComparerStringComparisonCodeFixTitle, invocation.TargetMethod.Name);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        createChangedDocument,
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.RecommendCaseInsensitiveStringComparerStringComparisonCodeFixTitle)),
                    context.Diagnostics);
            }
            else if (operation is IBinaryOperation binaryOperation &&
                     binaryOperation.LeftOperand != null && binaryOperation.RightOperand != null)
            {
                Task<Document> createChangedDocument(CancellationToken _) => FixBinaryAsync(generator, doc, root, binaryOperation, stringComparisonType, caseChangingApproachValue!);

                string title = MicrosoftNetCoreAnalyzersResources.RecommendCaseInsensitiveStringEqualsCodeFixTitle;

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        createChangedDocument,
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.RecommendCaseInsensitiveStringEqualsCodeFixTitle)),
                    context.Diagnostics);
            }
        }

        private Task<Document> FixInvocationAsync(SyntaxGenerator generator, Document doc, SyntaxNode root, IInvocationOperation mainInvocation,
            INamedTypeSymbol stringComparisonType, string diagnosableMethodName,
            string caseChangingApproachValue, string? leftOffendingMethod, string? rightOffendingMethod)
        {
            // Defensive check: The max number of arguments is held by IndexOf
            Debug.Assert(mainInvocation.Arguments.Length <= 3);

            // For the Diagnosable methods Contains(string) and StartsWith(string)
            // If we have this code ('a' and 'b' are string instances):
            //     A) a.CaseChanging().Diagnosable(b);
            //     B) a.Diagnosable(b.CaseChanging());
            // We want to convert any of them to:
            //     a.Diagnosable(b, StringComparison.DesiredCultureDesiredCase);

            // For IndexOf we have 3 options:
            //    A.1) a.CaseChanging().IndexOf(b)
            //    A.2) a.IndexOf(b.CaseChanging())
            //    B.1) a.CaseChanging().IndexOf(b, startIndex: n)
            //    B.2) a.IndexOf(b.CaseChanging(), startIndex: n)
            //    C.1) a.CaseChanging().IndexOf(b, startIndex: n, count: m)
            //    C.2) a.IndexOf(b.CaseChanging(), startIndex: n, count: m)
            // We want to convert them to:
            //    A) a.IndexOf(b, StringComparison.Desired)
            //    B) a.IndexOf(b, startIndex: n, StringComparison.Desired)
            //    C) a.IndexOf(b, startIndex: n, count: m, StringComparison.Desired)

            // Defensive check: Should not fix string.CompareTo (or any other method)
            Debug.Assert(diagnosableMethodName is
                RCISCAnalyzer.StringContainsMethodName or
                RCISCAnalyzer.StringIndexOfMethodName or
                RCISCAnalyzer.StringStartsWithMethodName);

            IEnumerable<SyntaxNode> newArguments = GetNewArgumentsForInvocation(generator,
                caseChangingApproachValue, mainInvocation, stringComparisonType,
                leftOffendingMethod, rightOffendingMethod, out SyntaxNode? mainInvocationInstance);

            SyntaxNode stringMemberAccessExpression = generator.MemberAccessExpression(mainInvocationInstance, mainInvocation.TargetMethod.Name);

            SyntaxNode newInvocation = generator.InvocationExpression(stringMemberAccessExpression, newArguments).WithTriviaFrom(mainInvocation.Syntax);

            SyntaxNode newRoot = generator.ReplaceNode(root, mainInvocation.Syntax, newInvocation.WithTriviaFrom(mainInvocation.Syntax));
            return Task.FromResult(doc.WithSyntaxRoot(newRoot));
        }

        private Task<Document> FixBinaryAsync(SyntaxGenerator generator, Document doc, SyntaxNode root, IBinaryOperation binaryOperation,
            INamedTypeSymbol stringComparisonType, string caseChangingApproachValue)
        {
            SyntaxNode leftNode = binaryOperation.LeftOperand is IInvocationOperation leftInvocation ?
                leftInvocation.Instance!.Syntax :
                binaryOperation.LeftOperand.Syntax;

            SyntaxNode rightNode = binaryOperation.RightOperand is IInvocationOperation rightInvocation ?
                rightInvocation.Instance!.Syntax :
                binaryOperation.RightOperand.Syntax;

            SyntaxNode memberAccess = generator.MemberAccessExpression(leftNode, RCISCAnalyzer.StringEqualsMethodName).WithTriviaFrom(leftNode);

            SyntaxNode stringComparisonTypeExpression = generator.TypeExpressionForStaticMemberAccess(stringComparisonType);

            SyntaxNode typeMemberAccess = generator.MemberAccessExpression(stringComparisonTypeExpression, caseChangingApproachValue);

            IEnumerable<SyntaxNode> newArguments = GetNewArgumentsForBinary(generator, rightNode, typeMemberAccess);

            SyntaxNode equalsInvocation = generator.InvocationExpression(memberAccess, newArguments);

            // Determine if it should be a.Equals or !a.Equals
            var replacement = binaryOperation.OperatorKind == BinaryOperatorKind.NotEquals ?
                generator.LogicalNotExpression(equalsInvocation) :
                equalsInvocation;

            SyntaxNode newRoot = generator.ReplaceNode(root, binaryOperation.Syntax, replacement.WithTriviaFrom(binaryOperation.Syntax));
            return Task.FromResult(doc.WithSyntaxRoot(newRoot));
        }

        protected static SyntaxNode GetNewStringComparisonArgument(SyntaxGenerator generator,
            INamedTypeSymbol stringComparisonType, string caseChangingApproachValue, bool isAnyArgumentNamed)
        {
            // Generate the enum access expression for "StringComparison.DesiredCultureDesiredCase"
            SyntaxNode stringComparisonEnumValueAccess = generator.MemberAccessExpression(
                generator.TypeExpressionForStaticMemberAccess(stringComparisonType),
                generator.IdentifierName(caseChangingApproachValue));

            // Convert the above into an argument node, then append it to the argument list: "b, StringComparison.DesiredCultureDesiredCase"
            // If at least one of the pre-existing arguments is named, then the StringComparison enum value needs to be named too
            SyntaxNode stringComparisonArgument = isAnyArgumentNamed ?
                generator.Argument(name: RCISCAnalyzer.StringComparisonParameterName, RefKind.None, stringComparisonEnumValueAccess) :
                generator.Argument(stringComparisonEnumValueAccess);

            return stringComparisonArgument;
        }
    }
}