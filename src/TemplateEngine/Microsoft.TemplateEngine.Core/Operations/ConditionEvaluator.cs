// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public delegate bool ConditionEvaluator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out bool faulted);
}
