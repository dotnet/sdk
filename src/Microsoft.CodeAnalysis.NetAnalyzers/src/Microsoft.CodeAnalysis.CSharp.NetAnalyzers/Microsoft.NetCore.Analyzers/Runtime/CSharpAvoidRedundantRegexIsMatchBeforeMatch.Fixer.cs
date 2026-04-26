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
            var diagnostic = context.Diagnostics[0];

            Document doc = context.Document;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

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

            // The IsMatch call must be the condition of an if statement.
            var ifStatement = isMatchNode.FirstAncestorOrSelf<IfStatementSyntax>();
            if (ifStatement is null)
            {
                return;
            }

            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            // Path 1: Match m = Regex.Match(...); — local declaration in if body
            var matchDeclarationStatement = matchNode.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
            if (matchDeclarationStatement is not null)
            {
                TryRegisterDeclarationFix(context, diagnostic, doc, model, ifStatement, matchDeclarationStatement, matchNode);
                return;
            }

            // Path 2: m = Regex.Match(...); — assignment to pre-declared variable
            var assignmentExpression = matchNode.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
            if (assignmentExpression is not null)
            {
                TryRegisterAssignmentFix(context, diagnostic, doc, model, ifStatement, assignmentExpression, matchNode);
            }
        }

        /// <summary>
        /// Path 1: The Match result is assigned via a local declaration in the if body:
        /// <c>Match m = Regex.Match(...);</c>
        /// </summary>
        private static void TryRegisterDeclarationFix(
            CodeFixContext context,
            Diagnostic diagnostic,
            Document doc,
            SemanticModel model,
            IfStatementSyntax ifStatement,
            LocalDeclarationStatementSyntax matchDeclarationStatement,
            SyntaxNode matchNode)
        {
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

            // Verify the initializer is exactly the Match invocation reported by the analyzer
            // (unwrapping any parentheses/casts). If the Match call is embedded inside a larger
            // expression (e.g., SomeMethod(Regex.Match(...))), the fix would change semantics.
            if (!IsMatchNode(declarator.Initializer.Value, matchNode))
            {
                return;
            }

            // Only apply fixer when the declared type is 'var' or exactly
            // System.Text.RegularExpressions.Match. If the user wrote a wider type
            // (e.g., Group, Capture, object), the pattern variable would change
            // the static type and could alter overload resolution.
            if (!IsVarOrMatchType(declaration.Type, model, context.CancellationToken))
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

            if (!PassesNameConflictChecks(ifStatement, variableName))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: ct => ApplyDeclarationFixAsync(doc, ifStatement, matchDeclarationStatement, variableName, ct),
                    equivalenceKey: title),
                diagnostic);
        }

        /// <summary>
        /// Path 2: The Match result is assigned to a pre-declared variable in the if body:
        /// <c>Match m; if (IsMatch(...)) { m = Regex.Match(...); }</c>
        /// Transforms to: <c>if (Regex.Match(...) is { Success: true } m) { }</c>
        /// Only applies when the pre-existing declaration is immediately before the if
        /// and the variable is not referenced after the if statement.
        /// </summary>
        private static void TryRegisterAssignmentFix(
            CodeFixContext context,
            Diagnostic diagnostic,
            Document doc,
            SemanticModel model,
            IfStatementSyntax ifStatement,
            AssignmentExpressionSyntax assignmentExpression,
            SyntaxNode matchNode)
        {
            // The left side must be a simple identifier (the variable being assigned).
            if (assignmentExpression.Left is not IdentifierNameSyntax identName)
            {
                return;
            }

            // Verify the right side is exactly the Match invocation.
            if (!IsMatchNode(assignmentExpression.Right, matchNode))
            {
                return;
            }

            // The assignment must be in an expression statement.
            var assignmentStatement = assignmentExpression.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (assignmentStatement is null)
            {
                return;
            }

            // The assignment statement must be the first statement in a block body.
            if (ifStatement.Statement is not BlockSyntax block ||
                block.Statements.FirstOrDefault() != assignmentStatement)
            {
                return;
            }

            // The if must be inside a block so we can find the preceding declaration.
            if (ifStatement.Parent is not BlockSyntax parentBlock)
            {
                return;
            }

            string variableName = identName.Identifier.ValueText;

            // Find the pre-existing declaration of the variable immediately before the if.
            int ifIndex = parentBlock.Statements.IndexOf(ifStatement);
            if (ifIndex <= 0)
            {
                return;
            }

            if (parentBlock.Statements[ifIndex - 1] is not LocalDeclarationStatementSyntax preDecl)
            {
                return;
            }

            if (preDecl.Declaration.Variables.Count != 1)
            {
                return;
            }

            var preVar = preDecl.Declaration.Variables[0];
            if (preVar.Identifier.ValueText != variableName)
            {
                return;
            }

            // The pre-existing declaration must have no initializer, or be initialized to
            // null/default so removing it doesn't lose meaningful computation.
            if (preVar.Initializer is not null)
            {
                var initValue = preVar.Initializer.Value;
                if (initValue is not LiteralExpressionSyntax literal ||
                    (!literal.IsKind(SyntaxKind.NullLiteralExpression) &&
                     !literal.IsKind(SyntaxKind.DefaultLiteralExpression)))
                {
                    return;
                }
            }

            // Verify the declared type is 'var' or exactly Match.
            if (!IsVarOrMatchType(preDecl.Declaration.Type, model, context.CancellationToken))
            {
                return;
            }

            // The variable must not be referenced in any statement after the if
            // or in the else branch, because the pattern variable won't be
            // definitely assigned there.
            if (IsVariableReferencedAfterIf(parentBlock, ifIndex, variableName) ||
                IsVariableReferencedInElse(ifStatement, variableName))
            {
                return;
            }

            if (!PassesNameConflictChecks(ifStatement, variableName))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: ct => ApplyAssignmentFixAsync(doc, ifStatement, preDecl, assignmentStatement, variableName, ct),
                    equivalenceKey: title),
                diagnostic);
        }

        /// <summary>
        /// Checks whether <paramref name="expression"/> is exactly the Match invocation
        /// <paramref name="matchNode"/> after unwrapping parentheses and casts.
        /// </summary>
        private static bool IsMatchNode(ExpressionSyntax expression, SyntaxNode matchNode)
        {
            SyntaxNode core = expression;
            while (core is ParenthesizedExpressionSyntax parenExpr)
            {
                core = parenExpr.Expression;
            }

            while (core is CastExpressionSyntax castExpr)
            {
                core = castExpr.Expression;
            }

            return core.Span.Equals(matchNode.Span);
        }

        /// <summary>
        /// Returns true when the type syntax is <c>var</c> or exactly
        /// <c>System.Text.RegularExpressions.Match</c>.
        /// </summary>
        private static bool IsVarOrMatchType(
            TypeSyntax typeSyntax, SemanticModel model, CancellationToken cancellationToken)
        {
            if (typeSyntax.IsVar)
            {
                return true;
            }

            var typeInfo = model.GetTypeInfo(typeSyntax, cancellationToken);
            var matchType = model.Compilation.GetTypeByMetadataName("System.Text.RegularExpressions.Match");
            return typeInfo.Type is not null &&
                   matchType is not null &&
                   SymbolEqualityComparer.Default.Equals(typeInfo.Type, matchType);
        }

        /// <summary>
        /// Returns true when the variable name doesn't conflict with bindings in else
        /// branches or subsequent sibling statements.
        /// </summary>
        private static bool PassesNameConflictChecks(
            IfStatementSyntax ifStatement, string variableName)
        {
            if (ifStatement.Else is not null &&
                HasConflictingName(ifStatement.Else, variableName))
            {
                return false;
            }

            if (HasConflictingNameInSubsequentSiblings(ifStatement, variableName))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the variable is referenced in any statement after
        /// <paramref name="ifIndex"/> in the parent block.
        /// </summary>
        private static bool IsVariableReferencedAfterIf(
            BlockSyntax parentBlock, int ifIndex, string variableName)
        {
            for (int i = ifIndex + 1; i < parentBlock.Statements.Count; i++)
            {
                if (parentBlock.Statements[i]
                    .DescendantNodesAndSelf()
                    .OfType<IdentifierNameSyntax>()
                    .Any(id => id.Identifier.ValueText == variableName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVariableReferencedInElse(
            IfStatementSyntax ifStatement, string variableName)
        {
            if (ifStatement.Else is null)
            {
                return false;
            }

            return ifStatement.Else
                .DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(id => id.Identifier.ValueText == variableName);
        }

        /// <summary>
        /// Applies the fix for Path 1 (local declaration in if body).
        /// </summary>
        private static async Task<Document> ApplyDeclarationFixAsync(
            Document document,
            IfStatementSyntax ifStatement,
            LocalDeclarationStatementSyntax matchDeclarationStatement,
            string variableName,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var matchCallExpression = matchDeclarationStatement.Declaration.Variables[0].Initializer!.Value;

            editor.ReplaceNode(ifStatement.Condition, BuildIsPatternCondition(ifStatement, matchCallExpression, variableName));
            editor.RemoveNode(matchDeclarationStatement);

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Applies the fix for Path 2 (assignment to pre-declared variable).
        /// </summary>
        private static async Task<Document> ApplyAssignmentFixAsync(
            Document document,
            IfStatementSyntax ifStatement,
            LocalDeclarationStatementSyntax preDeclaration,
            ExpressionStatementSyntax assignmentStatement,
            string variableName,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var assignmentExpr = (AssignmentExpressionSyntax)assignmentStatement.Expression;
            var matchCallExpression = assignmentExpr.Right;

            editor.ReplaceNode(ifStatement.Condition, BuildIsPatternCondition(ifStatement, matchCallExpression, variableName));
            editor.RemoveNode(assignmentStatement);
            editor.RemoveNode(preDeclaration);

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Builds: <c>Regex.Match(input, pattern) is { Success: true } m</c>
        /// </summary>
        private static IsPatternExpressionSyntax BuildIsPatternCondition(
            IfStatementSyntax ifStatement,
            ExpressionSyntax matchCallExpression,
            string variableName)
        {
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

            return SyntaxFactory.IsPatternExpression(
                matchCallExpression.WithoutTrivia(),
                successPattern)
                .WithLeadingTrivia(ifStatement.Condition.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.TriviaList());
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

                // Lambda, anonymous method, and local function parameters
                if (descendant is ParameterSyntax parameter &&
                    parameter.Identifier.ValueText == variableName)
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
