﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable warnings

using System.Threading;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions
{
    internal static class SemanticModelExtensions
    {
        public static IOperation? GetOperationWalkingUpParentChain(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
        {
            // Walk up the parent chain to fetch the first non-null operation.
            do
            {
                var operation = semanticModel.GetOperation(node, cancellationToken);
                if (operation != null)
                {
                    return operation;
                }

                node = node.Parent;
            }
            while (node != null);

            return null;
        }
    }
}
