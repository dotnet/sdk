﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.FlowAnalysis
{
    internal static class ControlFlowBranchExtensions
    {
        public static bool IsBackEdge(this ControlFlowBranch controlFlowBranch)
            => controlFlowBranch?.Source != null &&
               controlFlowBranch.Destination != null &&
               controlFlowBranch.Source.Ordinal >= controlFlowBranch.Destination.Ordinal;
    }
}
