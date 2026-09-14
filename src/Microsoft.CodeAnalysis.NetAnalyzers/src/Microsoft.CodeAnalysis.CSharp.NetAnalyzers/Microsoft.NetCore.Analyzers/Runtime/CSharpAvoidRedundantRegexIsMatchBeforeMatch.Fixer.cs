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
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;
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
    public sealed class CSharpAvoidRedundantRegexIsMatchBeforeMatchFixer : SyntaxEditorBasedCodeFixProvider
    {
        private const string EquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(AvoidRedundantRegexIsMatchBeforeMatch.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document doc = context.Document;
            SyntaxNode root = await doc.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SemanticModel model = await doc.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (TryGetFix(root, model, context.Diagnostics[0], context.CancellationToken, out _))
            {
                RegisterCodeFix(context, MicrosoftNetCoreAnalyzersResources.AvoidRedundantRegexIsMatchBeforeMatchFix, EquivalenceKey);
            }
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (!TryGetFix(editor.OriginalRoot, model, diagnostic, cancellationToken, out Fix fix))
            {
                return;
            }

            // A Match invocation cannot contain a guarded if statement, so a second diagnostic can never
            // nest inside this one and the nodes edited below are always disjoint from another fix's.
            editor.ReplaceNode(fix.IfStatement.Condition, BuildIsPatternCondition(fix.IfStatement, fix.MatchCallExpression, fix.VariableName));
            editor.RemoveNode(fix.StatementToRemove);

            if (fix.PreDeclaration is not null)
            {
                editor.RemoveNode(fix.PreDeclaration);
            }
        }

        private static bool TryGetFix(SyntaxNode root, SemanticModel model, Diagnostic diagnostic, CancellationToken cancellationToken, out Fix fix)
        {
            fix = default;

            // Require C# 8.0+ for property patterns (is { Success: true } m)
            if (root.SyntaxTree.Options is CSharpParseOptions parseOptions &&
                parseOptions.LanguageVersion < LanguageVersion.CSharp8)
            {
                return false;
            }

            // Find the IsMatch invocation from the primary diagnostic location.
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not SyntaxNode isMatchNode)
            {
                return false;
            }

            // Find the Match invocation from the additional location.
            if (diagnostic.AdditionalLocations.Count < 1)
            {
                return false;
            }

            var matchLocation = diagnostic.AdditionalLocations[0];
            if (root.FindNode(matchLocation.SourceSpan, getInnermostNodeForTie: true) is not SyntaxNode matchNode)
            {
                return false;
            }

            // The IsMatch call must be the condition of an if statement.
            var ifStatement = isMatchNode.FirstAncestorOrSelf<IfStatementSyntax>();
            if (ifStatement is null)
            {
                return false;
            }

            // Path 1: Match m = Regex.Match(...); — local declaration in if body
            var matchDeclarationStatement = matchNode.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
            if (matchDeclarationStatement is not null)
            {
                return TryGetDeclarationFix(model, ifStatement, matchDeclarationStatement, matchNode, cancellationToken, out fix);
            }

            // Path 2: m = Regex.Match(...); — assignment to pre-declared variable
            var assignmentExpression = matchNode.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
            if (assignmentExpression is not null)
            {
                return TryGetAssignmentFix(model, ifStatement, assignmentExpression, matchNode, cancellationToken, out fix);
            }

            return false;
        }

        /// <summary>
        /// Path 1: The Match result is assigned via a local declaration in the if body:
        /// <c>Match m = Regex.Match(...);</c>
        /// </summary>
        private static bool TryGetDeclarationFix(
            SemanticModel model,
            IfStatementSyntax ifStatement,
            LocalDeclarationStatementSyntax matchDeclarationStatement,
            SyntaxNode matchNode,
            CancellationToken cancellationToken,
            out Fix fix)
        {
            fix = default;

            var declaration = matchDeclarationStatement.Declaration;
            if (declaration.Variables.Count != 1)
            {
                return false;
            }

            var declarator = declaration.Variables[0];
            if (declarator.Initializer?.Value is null)
            {
                return false;
            }

            // Verify the initializer is exactly the Match invocation reported by the analyzer
            // (unwrapping any parentheses/casts). If the Match call is embedded inside a larger
            // expression (e.g., SomeMethod(Regex.Match(...))), the fix would change semantics.
            if (!IsMatchNode(declarator.Initializer.Value, matchNode))
            {
                return false;
            }

            // Only apply fixer when the declared type is 'var' or exactly
            // System.Text.RegularExpressions.Match. If the user wrote a wider type
            // (e.g., Group, Capture, object), the pattern variable would change
            // the static type and could alter overload resolution.
            if (!IsVarOrMatchType(declaration.Type, model, cancellationToken))
            {
                return false;
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
                    return false;
                }
            }

            if (!PassesNameConflictChecks(ifStatement, variableName))
            {
                return false;
            }

            fix = Fix.Declaration(ifStatement, matchDeclarationStatement, variableName);
            return true;
        }

        /// <summary>
        /// Path 2: The Match result is assigned to a pre-declared variable in the if body:
        /// <c>Match m; if (IsMatch(...)) { m = Regex.Match(...); }</c>
        /// Transforms to: <c>if (Regex.Match(...) is { Success: true } m) { }</c>
        /// Only applies when the pre-existing declaration is immediately before the if
        /// and the variable is not referenced after the if statement.
        /// </summary>
        private static bool TryGetAssignmentFix(
            SemanticModel model,
            IfStatementSyntax ifStatement,
            AssignmentExpressionSyntax assignmentExpression,
            SyntaxNode matchNode,
            CancellationToken cancellationToken,
            out Fix fix)
        {
            fix = default;

            // The left side must be a simple identifier (the variable being assigned).
            if (assignmentExpression.Left is not IdentifierNameSyntax identName)
            {
                return false;
            }

            // Verify the right side is exactly the Match invocation.
            if (!IsMatchNode(assignmentExpression.Right, matchNode))
            {
                return false;
            }

            // The assignment must be in an expression statement.
            var assignmentStatement = assignmentExpression.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (assignmentStatement is null)
            {
                return false;
            }

            // The assignment statement must be the first statement in a block body.
            if (ifStatement.Statement is not BlockSyntax block ||
                block.Statements.FirstOrDefault() != assignmentStatement)
            {
                return false;
            }

            // The if must be inside a block so we can find the preceding declaration.
            if (ifStatement.Parent is not BlockSyntax parentBlock)
            {
                return false;
            }

            string variableName = identName.Identifier.ValueText;

            // Find the pre-existing declaration of the variable immediately before the if.
            int ifIndex = parentBlock.Statements.IndexOf(ifStatement);
            if (ifIndex <= 0)
            {
                return false;
            }

            if (parentBlock.Statements[ifIndex - 1] is not LocalDeclarationStatementSyntax preDecl)
            {
                return false;
            }

            if (preDecl.Declaration.Variables.Count != 1)
            {
                return false;
            }

            var preVar = preDecl.Declaration.Variables[0];
            if (preVar.Identifier.ValueText != variableName)
            {
                return false;
            }

            // The pre-existing declaration must have no initializer, or be initialized
            // to a constant default expression (`null`, `default`, or `default(T)`) so
            // removing it doesn't lose meaningful computation.
            if (preVar.Initializer is not null)
            {
                var initValue = preVar.Initializer.Value;
                bool acceptable = initValue switch
                {
                    LiteralExpressionSyntax literal =>
                        literal.IsKind(SyntaxKind.NullLiteralExpression) ||
                        literal.IsKind(SyntaxKind.DefaultLiteralExpression),
                    DefaultExpressionSyntax => true,
                    _ => false,
                };

                if (!acceptable)
                {
                    return false;
                }
            }

            // Verify the declared type is 'var' or exactly Match.
            if (!IsVarOrMatchType(preDecl.Declaration.Type, model, cancellationToken))
            {
                return false;
            }

            // The variable must not be referenced in any statement after the if
            // or in the else branch, because the pattern variable won't be
            // definitely assigned there.
            if (IsVariableReferencedAfterIf(parentBlock, ifIndex, variableName) ||
                IsVariableReferencedInElse(ifStatement, variableName))
            {
                return false;
            }

            if (!PassesNameConflictChecks(ifStatement, variableName))
            {
                return false;
            }

            fix = Fix.Assignment(ifStatement, preDecl, assignmentStatement, variableName);
            return true;
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
            var matchType = WellKnownTypeProvider.GetOrCreate(model.Compilation).GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsMatch);
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
                if (ContainsIdentifierReference(parentBlock.Statements[i], variableName))
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

            return ContainsIdentifierReference(ifStatement.Else, variableName);
        }

        /// <summary>
        /// Returns true when <paramref name="node"/> contains an identifier that
        /// could plausibly bind to a local named <paramref name="variableName"/>.
        /// Excludes identifiers that are syntactically the name of a member access
        /// (e.g. <c>obj.m</c>) or a qualified name suffix, since those bind to a
        /// member rather than the local. Conservative — does not consult the
        /// semantic model, so it can still over-report (e.g. type names, nameof).
        /// </summary>
        private static bool ContainsIdentifierReference(SyntaxNode node, string variableName)
        {
            foreach (var id in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                if (id.Identifier.ValueText != variableName)
                {
                    continue;
                }

                // Skip `something.m` (right-hand side of a member access) — `m` here is
                // a member name, not a reference to the local.
                if (id.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == id)
                {
                    continue;
                }

                // Skip `Foo.m` where `m` is the right-hand side of a qualified name.
                if (id.Parent is QualifiedNameSyntax qualified && qualified.Right == id)
                {
                    continue;
                }

                // Skip `M(m: value)` — `m` here is a parameter name label, not a reference.
                if (id.Parent is NameColonSyntax nameColon && nameColon.Name == id)
                {
                    continue;
                }

                return true;
            }

            return false;
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
                .WithTrailingTrivia(SyntaxFactory.TriviaList())
                .WithAdditionalAnnotations(Formatter.Annotation);
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

                // LINQ query range variables. Pattern variables introduced by
                // `is { ... } m` scope to the enclosing block, so any subsequent
                // query that already binds the same name would start conflicting.
                if (descendant is FromClauseSyntax fromClause &&
                    fromClause.Identifier.ValueText == variableName)
                {
                    return true;
                }

                if (descendant is LetClauseSyntax letClause &&
                    letClause.Identifier.ValueText == variableName)
                {
                    return true;
                }

                if (descendant is JoinClauseSyntax joinClause &&
                    joinClause.Identifier.ValueText == variableName)
                {
                    return true;
                }

                if (descendant is JoinIntoClauseSyntax joinIntoClause &&
                    joinIntoClause.Identifier.ValueText == variableName)
                {
                    return true;
                }

                if (descendant is QueryContinuationSyntax queryCont &&
                    queryCont.Identifier.ValueText == variableName)
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

        /// <summary>
        /// The nodes a single fix rewrites, resolved before any edit is made so that the same code
        /// gates a single invocation and every diagnostic of a fix-all pass.
        /// </summary>
        private readonly struct Fix
        {
            private Fix(
                IfStatementSyntax ifStatement,
                ExpressionSyntax matchCallExpression,
                string variableName,
                StatementSyntax statementToRemove,
                LocalDeclarationStatementSyntax? preDeclaration)
            {
                IfStatement = ifStatement;
                MatchCallExpression = matchCallExpression;
                VariableName = variableName;
                StatementToRemove = statementToRemove;
                PreDeclaration = preDeclaration;
            }

            public IfStatementSyntax IfStatement { get; }

            public ExpressionSyntax MatchCallExpression { get; }

            public string VariableName { get; }

            public StatementSyntax StatementToRemove { get; }

            /// <summary>
            /// The pre-existing declaration of <see cref="VariableName"/>, which only Path 2 removes.
            /// </summary>
            public LocalDeclarationStatementSyntax? PreDeclaration { get; }

            public static Fix Declaration(IfStatementSyntax ifStatement, LocalDeclarationStatementSyntax matchDeclarationStatement, string variableName)
                => new(ifStatement, matchDeclarationStatement.Declaration.Variables[0].Initializer!.Value, variableName, matchDeclarationStatement, preDeclaration: null);

            public static Fix Assignment(IfStatementSyntax ifStatement, LocalDeclarationStatementSyntax preDeclaration, ExpressionStatementSyntax assignmentStatement, string variableName)
                => new(ifStatement, ((AssignmentExpressionSyntax)assignmentStatement.Expression).Right, variableName, assignmentStatement, preDeclaration);
        }
    }
}
