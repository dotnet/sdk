// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// C#-specific fixer for <see cref="AvoidRedundantRegexIsMatchBeforeMatch"/>.
    /// Transforms:
    /// <code>
    /// if (Regex.IsMatch(input, pattern))
    /// {
    ///     Match m = Regex.Match(input, pattern);
    ///     // use m
    /// }
    /// </code>
    /// Into:
    /// <code>
    /// if (Regex.Match(input, pattern) is { Success: true } m)
    /// {
    ///     // use m
    /// }
    /// </code>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpAvoidRedundantRegexIsMatchBeforeMatchFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(AvoidRedundantRegexIsMatchBeforeMatch.RuleId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic is null)
            {
                return;
            }

            Document doc = context.Document;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            // Require C# 8.0+ for property patterns (is { Success: true } m)
            if (root.SyntaxTree.Options is CSharpParseOptions parseOptions &&
                parseOptions.LanguageVersion < LanguageVersion.CSharp8)
            {
                return;
            }

            // Find the IsMatch invocation from the primary diagnostic location.
            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not SyntaxNode isMatchNode)
            {
                return;
            }

            // Find the Match invocation from the additional location.
            if (diagnostic.AdditionalLocations.Count < 1)
            {
                return;
            }

            var matchLocation = diagnostic.AdditionalLocations[0];
            if (root.FindNode(matchLocation.SourceSpan, getInnermostNodeForTie: true) is not SyntaxNode matchNode)
            {
                return;
            }

            // The Match call must be in a local declaration statement: Match m = Regex.Match(...);
            // We only handle single-variable declarations.
            var matchDeclarationStatement = matchNode.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
            if (matchDeclarationStatement is null)
            {
                return;
            }

            var declaration = matchDeclarationStatement.Declaration;
            if (declaration.Variables.Count != 1)
            {
                return;
            }

            var declarator = declaration.Variables[0];
            if (declarator.Initializer?.Value is null)
            {
                return;
            }

            // The IsMatch call must be the condition of an if statement.
            var ifStatement = isMatchNode.FirstAncestorOrSelf<IfStatementSyntax>();
            if (ifStatement is null)
            {
                return;
            }

            string variableName = declarator.Identifier.ValueText;

            // Only apply fixer when the Match declaration is the first executable statement
            // in the if body. If there are preceding statements, moving Match into the
            // condition would change their execution order relative to the Match call.
            if (ifStatement.Statement is BlockSyntax block)
            {
                var firstStatement = block.Statements.FirstOrDefault();
                if (firstStatement != matchDeclarationStatement)
                {
                    return;
                }
            }

            // Check for name conflicts: the pattern variable will be scoped to the
            // entire if statement, including any else branch. If the else branch
            // declares a variable with the same name, the fix would not compile.
            if (ifStatement.Else is not null &&
                HasConflictingDeclaration(ifStatement.Else, variableName))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: ct => ApplyFixAsync(doc, ifStatement, matchDeclarationStatement, matchNode, variableName, ct),
                    equivalenceKey: title),
                diagnostic);
        }

        private static async Task<Document> ApplyFixAsync(
            Document document,
            IfStatementSyntax ifStatement,
            LocalDeclarationStatementSyntax matchDeclarationStatement,
            SyntaxNode matchCallNode,
            string variableName,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Get the Match call expression (the initializer value of the local declaration).
            var matchCallExpression = matchDeclarationStatement.Declaration.Variables[0].Initializer!.Value;

            // Build: Regex.Match(input, pattern) is { Success: true } m
            var successPattern = SyntaxFactory.RecursivePattern()
                .WithPropertyPatternClause(
                    SyntaxFactory.PropertyPatternClause(
                        SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Subpattern(
                                SyntaxFactory.NameColon(SyntaxFactory.IdentifierName("Success")),
                                SyntaxFactory.ConstantPattern(
                                    SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)))
                        })))
                .WithDesignation(
                    SyntaxFactory.SingleVariableDesignation(
                        SyntaxFactory.Identifier(variableName)))
                .NormalizeWhitespace();

            var newCondition = SyntaxFactory.IsPatternExpression(
                matchCallExpression.WithoutTrivia(),
                successPattern)
                .WithLeadingTrivia(ifStatement.Condition.GetLeadingTrivia())
                .WithTrailingTrivia(ifStatement.Condition.GetTrailingTrivia());

            // Replace the if condition
            editor.ReplaceNode(ifStatement.Condition, newCondition);

            // Remove the Match declaration statement from the if body
            editor.RemoveNode(matchDeclarationStatement);

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Checks whether the given syntax node (typically an else clause) contains any
        /// variable declarator with the specified name, which would conflict with a
        /// pattern variable introduced in the if condition.
        /// </summary>
        private static bool HasConflictingDeclaration(SyntaxNode node, string variableName)
        {
            foreach (var declarator in node.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (declarator.Identifier.ValueText == variableName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
