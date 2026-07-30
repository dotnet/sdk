// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2208: Instantiate argument exceptions correctly
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class InstantiateArgumentExceptionsCorrectlyFixer : CodeFixProvider
    {
        private const string AddNullMessageKey = nameof(MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyChangeToTwoArgumentCodeFixTitle);
        private const string SwapArgumentsKey = nameof(MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyFlipArgumentOrderCodeFixTitle);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(context => context.CodeActionEquivalenceKey, ApplyFixAsync);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Diagnostic diagnostic = context.Diagnostics[0];
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (!TryGetCreation(model, root, diagnostic, context.CancellationToken, out IObjectCreationOperation? creation, out _))
            {
                return;
            }

            (string title, string equivalenceKey) = creation.Arguments.Length == 1
                ? (MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyChangeToTwoArgumentCodeFixTitle, AddNullMessageKey)
                : (MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyFlipArgumentOrderCodeFixTitle, SwapArgumentsKey);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    ct => SyntaxEditorFixAllProvider.ApplyFixesAsync(context.Document, context.Diagnostics,
                        (document, diag, editor, token) => ApplyFixAsync(document, diag, editor, equivalenceKey, token), ct),
                    equivalenceKey),
                diagnostic);
        }

        private static async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (!TryGetCreation(model, editor.OriginalRoot, diagnostic, cancellationToken, out IObjectCreationOperation? creation, out int paramPosition))
            {
                return;
            }

            SyntaxGenerator generator = editor.Generator;
            int argumentCount = creation.Arguments.Length;

            if (argumentCount == 1)
            {
                if (equivalenceKey is not null && equivalenceKey != AddNullMessageKey)
                {
                    return;
                }

                // Add a null message ahead of the parameter name.
                FixArgument nullMessage = FixArgument.Generated(generator.Argument(generator.NullLiteralExpression()));
                ReplaceCreation(editor, creation, nullMessage, GetArgument(creation, generator, 0, nameOf: true));
                return;
            }

            if (equivalenceKey is not null && equivalenceKey != SwapArgumentsKey)
            {
                return;
            }

            // Swap the message and the parameter name.
            FixArgument parameter = GetArgument(creation, generator, paramPosition, nameOf: true);
            if (argumentCount == 2)
            {
                if (paramPosition == 0)
                {
                    ReplaceCreation(editor, creation, GetArgument(creation, generator, 1), parameter);
                }
                else
                {
                    ReplaceCreation(editor, creation, parameter, GetArgument(creation, generator, 0));
                }
            }
            else
            {
                Debug.Assert(argumentCount == 3);
                if (paramPosition == 0)
                {
                    ReplaceCreation(editor, creation, GetArgument(creation, generator, 1), parameter, GetArgument(creation, generator, 2));
                }
                else
                {
                    ReplaceCreation(editor, creation, parameter, GetArgument(creation, generator, 1), GetArgument(creation, generator, 0));
                }
            }
        }

        private static bool TryGetCreation(SemanticModel model, SyntaxNode root, Diagnostic diagnostic, CancellationToken cancellationToken,
            [NotNullWhen(true)] out IObjectCreationOperation? creation, out int paramPosition)
        {
            creation = null;
            paramPosition = 0;

            if (diagnostic.Properties.GetValueOrDefault(InstantiateArgumentExceptionsCorrectlyAnalyzer.MessagePosition) is not string paramPositionString ||
                !int.TryParse(paramPositionString, out paramPosition))
            {
                return false;
            }

            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not SyntaxNode node ||
                model.GetOperation(node, cancellationToken) is not IObjectCreationOperation objectCreation)
            {
                return false;
            }

            creation = objectCreation;
            return true;
        }

        /// <remarks>
        /// The rewritten argument list is positional, so the value is taken without the enclosing
        /// argument: carrying a named argument's syntax into a different position would either name
        /// the wrong parameter or fail to compile.
        /// </remarks>
        private static FixArgument GetArgument(IObjectCreationOperation creation, SyntaxGenerator generator, int parameterIndex, bool nameOf = false)
        {
            IOperation value = creation.Arguments.GetArgumentForParameterAtIndex(parameterIndex).Value;

            if (nameOf && value is ILiteralOperation literal && literal.ConstantValue.Value is object constant)
            {
                return FixArgument.Generated(generator.NameOfExpression(generator.IdentifierName(constant.ToString())));
            }

            return FixArgument.CarriedOver(value.Syntax);
        }

        private static void ReplaceCreation(SyntaxEditor editor, IObjectCreationOperation creation, params FixArgument[] arguments)
        {
            foreach (FixArgument argument in arguments)
            {
                if (argument.Original is SyntaxNode original)
                {
                    editor.TrackNode(original);
                }
            }

            // The carried-over arguments can themselves contain a diagnosed creation, so they are read
            // from the creation as the inner fixes left it rather than from the original tree.
            editor.ReplaceNode(creation.Syntax, (currentNode, generator) =>
            {
                SyntaxNode[] newArguments = new SyntaxNode[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                {
                    FixArgument argument = arguments[i];
                    newArguments[i] = argument.Original is SyntaxNode original
                        ? currentNode.GetCurrentNode(original) ?? original
                        : argument.Node;
                }

                return generator.ObjectCreationExpression(creation.Type, newArguments);
            });
        }

        /// <summary>
        /// An argument of the rewritten creation: either a node generated by the fix, or one carried
        /// over from the original creation and therefore tracked across the other fixes in the document.
        /// </summary>
        private readonly struct FixArgument
        {
            private FixArgument(SyntaxNode node, bool carriedOver)
            {
                Node = node;
                Original = carriedOver ? node : null;
            }

            public static FixArgument Generated(SyntaxNode node) => new FixArgument(node, carriedOver: false);

            public static FixArgument CarriedOver(SyntaxNode node) => new FixArgument(node, carriedOver: true);

            public SyntaxNode Node { get; }

            public SyntaxNode? Original { get; }
        }
    }
}