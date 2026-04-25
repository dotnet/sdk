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

            // Only apply fixer when the declared type is 'var' or exactly
            // System.Text.RegularExpressions.Match. If the user wrote a wider type
            // (e.g., Group, Capture, object), the pattern variable would change
            // the static type and could alter overload resolution.
            if (!declaration.Type.IsVar)
            {
                var typeInfo = model.GetTypeInfo(declaration.Type, context.CancellationToken);
                if (typeInfo.Type is null ||
                    typeInfo.Type.ToDisplayString() != "System.Text.RegularExpressions.Match")
                {
                    return;
                }
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

            // Check for name conflicts: a pattern variable from
            //   if (expr is { } m)
            // scopes to the entire enclosing block, not just the if body.
            // Bail if any else branch or subsequent sibling statement
            // declares a variable with the same name.
            if (ifStatement.Else is not null &&
                HasConflictingName(ifStatement.Else, variableName))
            {
                return;
            }

            if (HasConflictingNameInSubsequentSiblings(ifStatement, variableName))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: ct => ApplyFixAsync(doc, ifStatement, matchDeclarationStatement, variableName, ct),
                    equivalenceKey: title),
                diagnostic);
        }

        private static async Task<Document> ApplyFixAsync(
            Document document,
            IfStatementSyntax ifStatement,
            LocalDeclarationStatementSyntax matchDeclarationStatement,
            string variableName,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

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
                .WithTrailingTrivia(SyntaxFactory.TriviaList());

            // Replace the if condition
            editor.ReplaceNode(ifStatement.Condition, newCondition);

            // Remove the Match declaration statement from the if body
            editor.RemoveNode(matchDeclarationStatement);

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Checks whether the given syntax node (typically an else clause) contains any
        /// variable binding with the specified name — including variable declarators,
        /// pattern designations (is/switch patterns), out var, foreach, and catch.
        /// </summary>
        private static bool HasConflictingName(SyntaxNode node, string variableName)
        {
            foreach (var descendant in node.DescendantNodes())
            {
                if (descendant is VariableDeclaratorSyntax declarator &&
                    declarator.Identifier.ValueText == variableName)
                {
                    return true;
                }

                if (descendant is SingleVariableDesignationSyntax designation &&
                    designation.Identifier.ValueText == variableName)
                {
                    return true;
                }

                if (descendant is ForEachStatementSyntax forEach &&
                    forEach.Identifier.ValueText == variableName)
                {
                    return true;
                }

                // Deconstruction foreach: foreach (var (x, y) in ...)
                if (descendant is ForEachVariableStatementSyntax forEachVariable &&
                    forEachVariable.Variable
                        .DescendantNodesAndSelf()
                        .OfType<SingleVariableDesignationSyntax>()
                        .Any(d => d.Identifier.ValueText == variableName))
                {
                    return true;
                }

                if (descendant is CatchDeclarationSyntax catchDecl &&
                    catchDecl.Identifier.ValueText == variableName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether any statement after the given if statement in its parent block
        /// declares a variable with the specified name. Pattern variables from the if
        /// condition scope to the entire enclosing block, so later declarations conflict.
        /// For parent containers other than <see cref="BlockSyntax"/>, conservatively
        /// assume a conflict because this helper only scans block statements.
        /// </summary>
        private static bool HasConflictingNameInSubsequentSiblings(
            IfStatementSyntax ifStatement, string variableName)
        {
            // Walk up through else-if chains to find the outermost if statement.
            // Pattern variables scope to the enclosing block, so for an "else if" we
            // must check siblings after the outermost if in that chain.
            SyntaxNode current = ifStatement;
            while (current.Parent is ElseClauseSyntax elseClause &&
                   elseClause.Parent is IfStatementSyntax parentIf)
            {
                current = parentIf;
            }

            if (current.Parent is not BlockSyntax parentBlock)
            {
                return true;
            }

            bool foundIf = false;
            foreach (var statement in parentBlock.Statements)
            {
                if (statement == current)
                {
                    foundIf = true;
                    continue;
                }

                if (foundIf && HasConflictingName(statement, variableName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
