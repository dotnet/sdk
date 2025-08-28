﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.Extensions
{
    internal static partial class IOperationExtensions
    {
        public static bool IsInsideCatchRegion([NotNullWhen(returnValue: true)] this IOperation? operation, ControlFlowGraph cfg)
        {
            if (operation == null)
            {
                return false;
            }

            foreach (var block in cfg.Blocks)
            {
                var isCatchRegionBlock = false;
                var currentRegion = block.EnclosingRegion;
                while (currentRegion != null)
                {
                    switch (currentRegion.Kind)
                    {
                        case ControlFlowRegionKind.Catch:
                            isCatchRegionBlock = true;
                            break;
                    }

                    currentRegion = currentRegion.EnclosingRegion;
                }

                if (isCatchRegionBlock)
                {
                    foreach (var descendant in block.DescendantOperations())
                    {
                        if (operation == descendant)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool IsLValueFlowCaptureReference(this IFlowCaptureReferenceOperation flowCaptureReference)
            => flowCaptureReference.Parent is IAssignmentOperation assignment &&
               assignment.Target == flowCaptureReference;
    }
}
