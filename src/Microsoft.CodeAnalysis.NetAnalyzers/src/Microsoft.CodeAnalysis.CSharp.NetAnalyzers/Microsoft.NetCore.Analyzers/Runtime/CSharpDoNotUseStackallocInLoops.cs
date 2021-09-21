// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>CA2014: Do Not Use Stackalloc In Loops.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpDoNotUseStackallocInLoopsAnalyzer : DoNotUseStackallocInLoopsAnalyzer
    {
        private static readonly ImmutableArray<SyntaxKind> s_stackallocKinds = ImmutableArray.Create(SyntaxKind.StackAllocArrayCreationExpression);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(ctx =>
            {
                // We found a stackalloc.  Walk up from it to see if it's in a loop at any level.
                for (SyntaxNode node = ctx.Node; node != null; node = node.Parent)
                {
                    switch (node.Kind())
                    {
                        // Don't warn if a local function or lambda containing stackalloc is inside a loop.
                        case SyntaxKind.LocalFunctionStatement:
                        case SyntaxKind.ParenthesizedLambdaExpression:
                        case SyntaxKind.SimpleLambdaExpression:
                        case SyntaxKind.AnonymousMethodExpression:
                            return;

                        // Look for loops.  We don't bother with ad-hoc loops via gotos as we're
                        // too likely to incur false positives.
                        case SyntaxKind.ForStatement:
                        case SyntaxKind.ForEachStatement:
                        case SyntaxKind.WhileStatement:
                        case SyntaxKind.DoStatement:

                            // Determine if we should warn for the stackalloc we found in a loop.  In general
                            // if a stackalloc is in a loop, it should be moved out.  However, there's a pattern
                            // where a loop will search for something and then, once it finds it, it stackallocs
                            // as part of processing the thing it found, and exits the loop.  We want to avoid
                            // warning on such cases if possible.  So, as a heuristic, we walk up all the blocks
                            // we find until we reach the first containing loop, and for each block look at all
                            // operations it contains after the target one to see if any is a break or a return.
                            static bool ShouldWarn(IOperation? op, SyntaxNode node)
                            {
                                // Walk up the operation tree, looking at all blocks until we find a loop.
                                for (; op is not null and not ILoopOperation; op = op.Parent)
                                {
                                    // If we hit a block, iterate through all operations in the block
                                    // after the node's position, and see if it's something that will
                                    // execute the loop.
                                    if (op is IBlockOperation block)
                                    {
                                        foreach (IOperation child in block.Operations)
                                        {
                                            if (child.Syntax.SpanStart > node.SpanStart &&
                                                (child is IReturnOperation || (child is IBranchOperation branch && branch.BranchKind == BranchKind.Break)))
                                            {
                                                // Err on the side of false negatives / caution and say this stackalloc is ok.
                                                // Note, too, it's possible we're breaking out of a nested loop, and the outer loop
                                                // will still cause the stackalloc to be invoked an unbounded number of times,
                                                // but that's difficult to analyze well.
                                                return false;
                                            }
                                        }
                                    }
                                }

                                // Warn.
                                return true;
                            }

                            // Warn as needed.
                            if (ShouldWarn(ctx.SemanticModel.GetOperationWalkingUpParentChain(ctx.Node, default), ctx.Node))
                            {
                                ctx.ReportDiagnostic(ctx.Node.CreateDiagnostic(Rule));
                            }

                            return;
                    }
                }
            }, s_stackallocKinds);
        }
    }
}
