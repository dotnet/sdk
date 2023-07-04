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
        protected abstract List<SyntaxNode> GetNewArguments(SyntaxGenerator generator, IInvocationOperation mainInvocationOperation,
            INamedTypeSymbol stringComparisonType, out SyntaxNode? mainInvocationInstance);

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

            if (model.GetOperation(node, ct) is not IInvocationOperation invocation)
            {
                return;
            }

            if (model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison)
                is not INamedTypeSymbol stringComparisonType)
            {
                return;
            }

            Task<Document> createChangedDocument(CancellationToken _) => FixInvocationAsync(doc, root,
                invocation, stringComparisonType, invocation.TargetMethod.Name);

            string title = string.Format(
                MicrosoftNetCoreAnalyzersResources.RecommendCaseInsensitiveStringComparerStringComparisonCodeFixTitle, invocation.TargetMethod.Name);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument,
                    equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.RecommendCaseInsensitiveStringComparerStringComparisonCodeFixTitle)),
                context.Diagnostics);
        }

        private Task<Document> FixInvocationAsync(Document doc, SyntaxNode root, IInvocationOperation mainInvocation,
            INamedTypeSymbol stringComparisonType, string diagnosableMethodName)
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

            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(doc);

            // Defensive check: Should not fix string.CompareTo
            Debug.Assert(diagnosableMethodName is
                RCISCAnalyzer.StringContainsMethodName or
                RCISCAnalyzer.StringIndexOfMethodName or
                RCISCAnalyzer.StringStartsWithMethodName);

            List<SyntaxNode> newArguments = GetNewArguments(generator, mainInvocation, stringComparisonType, out SyntaxNode? mainInvocationInstance);

            SyntaxNode stringMemberAccessExpression = generator.MemberAccessExpression(mainInvocationInstance, mainInvocation.TargetMethod.Name);

            SyntaxNode newInvocation = generator.InvocationExpression(stringMemberAccessExpression, newArguments).WithTriviaFrom(mainInvocation.Syntax);

            SyntaxNode newRoot = generator.ReplaceNode(root, mainInvocation.Syntax, newInvocation);
            return Task.FromResult(doc.WithSyntaxRoot(newRoot));
        }

        protected static SyntaxNode GetNewStringComparisonArgument(SyntaxGenerator generator,
            INamedTypeSymbol stringComparisonType, string caseChangingApproachName, bool isAnyArgumentNamed)
        {
            // Generate the enum access expression for "StringComparison.DesiredCultureDesiredCase"
            SyntaxNode stringComparisonEnumValueAccess = generator.MemberAccessExpression(
                generator.TypeExpressionForStaticMemberAccess(stringComparisonType),
                generator.IdentifierName(caseChangingApproachName));

            // Convert the above into an argument node, then append it to the argument list: "b, StringComparison.DesiredCultureDesiredCase"
            // If at least one of the pre-existing arguments is named, then the StringComparison enum value needs to be named too
            SyntaxNode stringComparisonArgument = isAnyArgumentNamed ?
                generator.Argument(name: RCISCAnalyzer.StringComparisonParameterName, RefKind.None, stringComparisonEnumValueAccess) :
                generator.Argument(stringComparisonEnumValueAccess);

            return stringComparisonArgument;
        }

        protected static string GetCaseChangingApproach(string methodName)
        {
            if (methodName is RCISCAnalyzer.StringToLowerMethodName or RCISCAnalyzer.StringToUpperMethodName)
            {
                return RCISCAnalyzer.StringComparisonCurrentCultureIgnoreCaseName;
            }

            Debug.Assert(methodName is RCISCAnalyzer.StringToLowerInvariantMethodName or RCISCAnalyzer.StringToUpperInvariantMethodName);
            return RCISCAnalyzer.StringComparisonInvariantCultureIgnoreCaseName;
        }
    }
}